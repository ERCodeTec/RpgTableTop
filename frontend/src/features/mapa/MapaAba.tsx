import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { lazy, Suspense, useEffect, useState } from 'react'
import { AjudaDeAtalhos } from './AjudaDeAtalhos'
import { definirAvisoDoEditor, reiniciarEditor } from './acoesDoEditor'
import { AtalhosDoMapa } from './AtalhosDoMapa'
import { BarraDaNevoa } from './BarraDaNevoa'
import { BarraDoEditor } from './BarraDoEditor'
import { BarraDoFundo } from './BarraDoFundo'
import { BotaoNovaRodada } from './BotaoNovaRodada'
import { ControlesDeVisao } from './ControlesDeVisao'
import { useEditorStore } from './editorStore'
import { ativarMapa, chaveDosMapas, gerarMapaProcedural, listarMapas } from './mapaApi'
import { MapaFormDialogo } from './MapaFormDialogo'
import { useMapaStore } from './mapaStore'
import { BarraDeMovimento } from './BarraDeMovimento'
import { GerarMapa } from './GerarMapa'
import { FotoDoToken } from './FotoDoToken'
import { PainelDoMestre } from './PainelDoMestre'
import { SeletorDeMovimentacao } from './SeletorDeMovimentacao'
import { useNevoa } from './useNevoa'
import { useTelaCheia, useTelaEstreita } from './useTelaCheia'
import type { AcoesDeMovimento } from './useMapaTempoReal'
import type { ModoDeMovimentacao } from '../mesas/mesasApi'

// O three.js só é baixado quando alguém abre um mapa.
const CenaDoMapa = lazy(() => import('./CenaDoMapa'))

interface MapaAbaProps {
  mesaId: string
  meuUsuarioId: string
  souMestre: boolean
  /** Livre: os jogadores também arrastam o próprio token, sem movimento por blocos. */
  movimentacao: ModoDeMovimentacao
  /** Arraste livre do Mestre, e do jogador com a movimentação livre. */
  aoMover: (tokenId: string, x: number, y: number) => void
  movimento: AcoesDeMovimento
  /** Informa ao servidor o que os tokens do jogador veem, com a névoa ligada. */
  aoExplorar: (mapaId: string, celulas: string) => void
  /** Mostra um aviso curto (porta que não abriu, ação do editor recusada). */
  aoAvisar: (mensagem: string) => void
}

export function MapaAba({ mesaId, meuUsuarioId, souMestre, movimentacao, aoMover, movimento, aoExplorar, aoAvisar }: MapaAbaProps) {
  const mapa = useMapaStore((estado) => estado.mapa)
  const enquadrar = useMapaStore((estado) => estado.enquadrar)
  const modo = useEditorStore((estado) => estado.modo)
  const definirModo = useEditorStore((estado) => estado.definirModo)
  const [criandoMapa, setCriandoMapa] = useState(false)
  const [ajudaAberta, setAjudaAberta] = useState(false)
  const telaCheia = useTelaCheia()
  const telaEstreita = useTelaEstreita()
  // Em telas estreitas e na tela cheia, o painel do Mestre é uma gaveta lateral, fechada por padrão.
  const [painelAberto, setPainelAberto] = useState(false)
  const comGaveta = souMestre && (telaEstreita || telaCheia.ativa)
  const mapaId = mapa?.id
  const queryClient = useQueryClient()
  useNevoa({ meuUsuarioId, souMestre, aoExplorar })
  const movimentoLivre = movimentacao === 'Livre'

  // Ao liberar a movimentação, some o alcance do token que estava em movimento por blocos.
  useEffect(() => {
    if (movimentoLivre) useMapaStore.getState().mover(null)
  }, [movimentoLivre])

  // Cada clique gera um mapa novo (semente sorteada no servidor) no tema escolhido, que já abre para o Mestre preparar.
  const gerarMapa = useMutation({
    mutationFn: (tema: string) => gerarMapaProcedural(mesaId, { tema }),
    onSuccess: async (estado) => {
      useMapaStore.getState().exibir(estado)
      await queryClient.invalidateQueries({ queryKey: chaveDosMapas(mesaId) })
    },
    onError: (erro) => aoAvisar(erro.message || 'Não foi possível gerar o mapa.'),
  })

  useEffect(() => {
    definirAvisoDoEditor(aoAvisar)
  }, [aoAvisar])

  // Seleção, rascunhos e histórico do editor são do mapa que estava na tela.
  useEffect(() => reiniciarEditor, [mapaId])

  // Sem mapa, a barra (com o botão de sair) some: a tela cheia não pode ficar presa.
  const { ativa: emTelaCheia, sair: sairDaTelaCheia } = telaCheia
  useEffect(() => {
    if (!mapaId && emTelaCheia) sairDaTelaCheia()
  }, [mapaId, emTelaCheia, sairDaTelaCheia])

  function ajustarFundo() {
    if (!mapa?.imagemFundoId) return
    useEditorStore.getState().definirFundo({
      imagemId: mapa.imagemFundoId,
      escala: mapa.fundoEscala,
      offsetX: mapa.fundoOffsetX,
      offsetY: mapa.fundoOffsetY,
    })
    definirModo('fundo')
  }

  const botaoDoPainel = comGaveta && (
    <button className="botao" type="button" aria-expanded={painelAberto} onClick={() => setPainelAberto(true)}>
      Painel do Mestre
    </button>
  )

  const classes = ['mapa-aba', souMestre && 'mapa-aba-mestre', comGaveta && 'mapa-aba-com-gaveta', telaCheia.ativa && 'mapa-aba-tela-cheia']

  return (
    <div className={classes.filter(Boolean).join(' ')}>
      <section className="mapa-palco" aria-label="Mapa">
        {!mapa && botaoDoPainel}
        {mapa ? (
          <>
            <div className="mapa-barra">
              <h2>{mapa.nome}</h2>
              <span className={`selo ${mapa.ativo ? 'selo-ativo' : ''}`}>{mapa.ativo ? 'Mapa ativo' : 'Em preparação'}</span>
              <span className="mapa-dimensoes">
                {mapa.largura} × {mapa.altura} células
              </span>
              <div className="mapa-barra-acoes">
                {souMestre && (
                  <div className="alternancia" role="group" aria-label="Modo do mapa">
                    <button type="button" aria-pressed={modo === 'jogar'} onClick={() => definirModo('jogar')}>
                      Jogar
                    </button>
                    <button type="button" aria-pressed={modo === 'editar'} onClick={() => definirModo('editar')}>
                      Editar mapa
                    </button>
                  </div>
                )}
                {souMestre && mapa.imagemFundoId && modo !== 'fundo' && (
                  <button className="botao botao-discreto" type="button" onClick={ajustarFundo}>
                    Ajustar fundo
                  </button>
                )}
                <button className="botao botao-discreto" type="button" onClick={enquadrar}>
                  Enquadrar
                </button>
                <button
                  className="botao botao-discreto"
                  type="button"
                  aria-pressed={telaCheia.ativa}
                  title="Atalho: F"
                  onClick={telaCheia.alternar}
                >
                  {telaCheia.ativa ? 'Sair da tela cheia' : 'Tela cheia'}
                </button>
                <button
                  className="botao botao-discreto"
                  type="button"
                  aria-label="Atalhos de teclado"
                  title="Atalhos de teclado (?)"
                  onClick={() => setAjudaAberta(true)}
                >
                  ?
                </button>
                {botaoDoPainel}
              </div>
            </div>
            {!mapa.ativo && <p className="aviso">Os jogadores não veem este mapa até você ativá-lo.</p>}
            {souMestre && modo === 'editar' && <BarraDoEditor />}
            {souMestre && modo === 'fundo' && <BarraDoFundo mapaId={mapa.id} />}
            {souMestre && modo === 'nevoa' && <BarraDaNevoa />}
            <div className="mapa-moldura">
              <Suspense fallback={<p className="mapa-carregando">Carregando o mapa…</p>}>
                <CenaDoMapa
                  meuUsuarioId={meuUsuarioId}
                  souMestre={souMestre}
                  movimentoLivre={movimentoLivre}
                  aoMover={aoMover}
                  aoMoverPorBlocos={movimento.moverPorBlocos}
                  aoAvisar={aoAvisar}
                />
              </Suspense>
              {!movimentoLivre && <BarraDeMovimento meuUsuarioId={meuUsuarioId} souMestre={souMestre} movimento={movimento} />}
              <FotoDoToken />
            </div>
            <div className="mapa-rodape">
              <ControlesDeVisao />
              {souMestre && modo === 'jogar' && (
                <div className="mapa-rodada">
                  <SeletorDeMovimentacao mesaId={mesaId} movimentacao={movimentacao} aoAvisar={aoAvisar} />
                  {!movimentoLivre && <BotaoNovaRodada mapaId={mapa.id} movimento={movimento} />}
                </div>
              )}
            </div>
            {modo === 'jogar' && (
              <p className="mapa-dica">
                {souMestre
                  ? movimentoLivre
                    ? 'Arraste qualquer token para movê-lo; os jogadores também arrastam os próprios tokens, sem custo de movimento. Clique em uma porta para abrir ou fechar.'
                    : 'Arraste qualquer token para movê-lo livremente, ou clique nele para mover por blocos com o custo de movimento; clique em uma porta para abrir ou fechar.'
                  : movimentoLivre
                    ? 'Arraste o seu token para qualquer célula livre do mapa para movê-lo.'
                    : 'Clique no seu token para ver as células que ele alcança e clique em uma delas para mover.'}{' '}
                Arraste o fundo (ou use dois dedos) para deslocar o mapa; role ou faça a pinça para aproximar. Arraste com
                o botão direito para girar e inclinar a visão; Q e E giram 90°.
              </p>
            )}
            <AtalhosDoMapa
              souMestre={souMestre}
              aoAbrirAjuda={() => setAjudaAberta(true)}
              aoAlternarTelaCheia={telaCheia.alternar}
              aoAvisar={aoAvisar}
            />
          </>
        ) : souMestre ? (
          <SemMapaParaOMestre
            mesaId={mesaId}
            aoCriarMapa={() => setCriandoMapa(true)}
            aoGerarMapa={(tema) => gerarMapa.mutate(tema)}
            gerandoMapa={gerarMapa.isPending}
          />
        ) : (
          <div className="estado">
            <p>
              <strong>Aguardando o Mestre.</strong>
            </p>
            <p>O mapa aparece aqui assim que o Mestre ativar um.</p>
          </div>
        )}
      </section>

      {souMestre && (
        <div className={`gaveta ${painelAberto ? 'gaveta-aberta' : ''}`}>
          <button className="gaveta-fundo" type="button" aria-label="Fechar o painel" tabIndex={-1} onClick={() => setPainelAberto(false)} />
          <div className="gaveta-conteudo">
            {comGaveta && (
              <button className="botao gaveta-fechar" type="button" onClick={() => setPainelAberto(false)}>
                Fechar painel
              </button>
            )}
            <PainelDoMestre
              mesaId={mesaId}
              movimento={movimento}
              aoCriarMapa={() => setCriandoMapa(true)}
              aoGerarMapa={(tema) => gerarMapa.mutate(tema)}
              gerandoMapa={gerarMapa.isPending}
            />
          </div>
        </div>
      )}

      {ajudaAberta && <AjudaDeAtalhos souMestre={souMestre} movimentoLivre={movimentoLivre} aoFechar={() => setAjudaAberta(false)} />}

      {criandoMapa && (
        <MapaFormDialogo
          mesaId={mesaId}
          aoFechar={() => setCriandoMapa(false)}
          // O mapa novo nasce inativo e já abre para o Mestre preparar.
          aoSalvar={(novo) => useMapaStore.getState().exibir({ mapa: novo, tokens: [], estruturas: [], memorias: [] })}
        />
      )}
    </div>
  )
}

interface SemMapaParaOMestreProps {
  mesaId: string
  aoCriarMapa: () => void
  aoGerarMapa: (tema: string) => void
  gerandoMapa: boolean
}

function SemMapaParaOMestre({ mesaId, aoCriarMapa, aoGerarMapa, gerandoMapa }: SemMapaParaOMestreProps) {
  const mapas = useQuery({ queryKey: chaveDosMapas(mesaId), queryFn: () => listarMapas(mesaId) })
  const ativar = useMutation({
    mutationFn: (mapaId: string) => ativarMapa(mapaId),
    onSuccess: (estado) => {
      useMapaStore.getState().definirMapaAtivo(estado.mapa.id)
      useMapaStore.getState().exibir(estado)
    },
  })
  const existentes = mapas.data ?? []

  return (
    <div className="estado">
      <p>
        <strong>Nenhum mapa ativo.</strong>
      </p>
      {existentes.length === 0 ? (
        <p>Crie o primeiro mapa da mesa. Ele só aparece para os jogadores depois de ativado.</p>
      ) : (
        <p>Ative um mapa para os jogadores entrarem nele, ou abra um no painel para prepará-lo antes.</p>
      )}
      {ativar.isError && (
        <p className="aviso-erro" role="alert">
          {ativar.error.message || 'Não foi possível ativar o mapa.'}
        </p>
      )}
      <div className="estado-acoes">
        {existentes.map((mapa) => (
          <button
            key={mapa.id}
            className="botao botao-primario"
            type="button"
            disabled={ativar.isPending}
            onClick={() => ativar.mutate(mapa.id)}
          >
            Ativar "{mapa.nome}"
          </button>
        ))}
        <button className={`botao ${existentes.length === 0 ? 'botao-primario' : ''}`} type="button" onClick={aoCriarMapa}>
          Criar mapa
        </button>
        <GerarMapa aoGerar={aoGerarMapa} gerando={gerandoMapa} destaque />
      </div>
    </div>
  )
}
