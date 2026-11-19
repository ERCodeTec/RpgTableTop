import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { memo, useEffect, useRef, useState, type CSSProperties } from 'react'
import { ConfirmarDialogo } from '../../shared/ui/ConfirmarDialogo'
import { PainelDeIniciativa } from '../iniciativa/PainelDeIniciativa'
import { ColocarDaBibliotecaDialogo } from './ColocarDaBibliotecaDialogo'
import { ControlesDaInteracao } from './ControlesDaInteracao'
import { ControlesDoToken } from './ControlesDoToken'
import { useEditorStore } from './editorStore'
import { GaleriaDeImagens } from './GaleriaDeImagens'
import {
  ativarMapa,
  chaveDasPastas,
  chaveDosMapas,
  definirExibicao,
  definirPastaDoMapa,
  duplicarToken,
  excluirMapa,
  excluirPasta,
  listarMapas,
  listarPastas,
  obterEstadoDoMapa,
  removerToken,
  type Mapa,
  type PastaDeMapas,
  type Token,
} from './mapaApi'
import { MapaFormDialogo } from './MapaFormDialogo'
import { PastaDeMapasDialogo } from './PastaDeMapasDialogo'
import { agruparPorPasta, gravarRecolhidas, lerRecolhidas, type GrupoDeMapas } from './pastas'
import { useMapaStore } from './mapaStore'
import { PainelDaNevoa } from './PainelDaNevoa'
import { PainelDeEstruturas } from './PainelDeEstruturas'
import { TokenFormDialogo } from './TokenFormDialogo'
import { TokenIcone } from './TokenIcone'
import { GerarMapa } from './GerarMapa'
import type { AcoesDeMovimento } from './useMapaTempoReal'

type DialogoAberto =
  | { tipo: 'editar-mapa'; mapa: Mapa }
  | { tipo: 'excluir-mapa'; mapa: Mapa }
  | { tipo: 'nova-pasta' }
  | { tipo: 'renomear-pasta'; pasta: PastaDeMapas }
  | { tipo: 'excluir-pasta'; pasta: PastaDeMapas }
  | { tipo: 'novo-token' }
  | { tipo: 'da-biblioteca' }
  | { tipo: 'editar-token'; token: Token }
  | { tipo: 'remover-token'; token: Token }

type Aba = 'mapas' | 'tokens' | 'iniciativa' | 'estruturas' | 'nevoa' | 'imagens'

const ABAS: { id: Aba; nome: string }[] = [
  { id: 'mapas', nome: 'Mapas' },
  { id: 'tokens', nome: 'Tokens' },
  { id: 'iniciativa', nome: 'Iniciativa' },
  { id: 'estruturas', nome: 'Estruturas' },
  { id: 'nevoa', nome: 'Névoa' },
  { id: 'imagens', nome: 'Imagens' },
]

const chaveDoGrupo = (grupo: GrupoDeMapas) => grupo.pasta?.id ?? 'sem-pasta'

interface PainelDoMestreProps {
  mesaId: string
  movimento: AcoesDeMovimento
  aoCriarMapa: () => void
  aoGerarMapa: (tema: string) => void
  gerandoMapa: boolean
  aoAvisar: (mensagem: string) => void
}

/**
 * Gestão de mapas, tokens, estruturas, névoa e imagens, em abas. As ações passam pela API REST; a tela é atualizada pelos eventos que
 * o servidor envia (ao Mestre, também os dos mapas em preparação) e, para não esperar por eles,
 * pela própria resposta da requisição.
 */
export function PainelDoMestre({ mesaId, movimento, aoCriarMapa, aoGerarMapa, gerandoMapa, aoAvisar }: PainelDoMestreProps) {
  const queryClient = useQueryClient()
  const mapas = useQuery({ queryKey: chaveDosMapas(mesaId), queryFn: () => listarMapas(mesaId) })
  const pastas = useQuery({ queryKey: chaveDasPastas(mesaId), queryFn: () => listarPastas(mesaId) })
  const mapaNaTela = useMapaStore((estado) => estado.mapa)
  const ordem = useMapaStore((estado) => estado.ordem)
  const [dialogo, setDialogo] = useState<DialogoAberto | null>(null)
  const fechar = () => setDialogo(null)
  const [aba, setAba] = useState<Aba>('mapas')
  /** Pastas recolhidas na aba Mapas (o id, ou 'sem-pasta'). Lembradas neste navegador, por mesa; não vão ao servidor. */
  const [recolhidasDaMesa, setRecolhidasDaMesa] = useState(() => ({ mesaId, recolhidas: lerRecolhidas(mesaId) }))
  // Trocou de mesa sem desmontar o painel: carrega as da mesa nova.
  if (recolhidasDaMesa.mesaId !== mesaId) setRecolhidasDaMesa({ mesaId, recolhidas: lerRecolhidas(mesaId) })
  const recolhidas = recolhidasDaMesa.recolhidas
  const setRecolhidas = (mudar: Set<string> | ((atual: Set<string>) => Set<string>)) =>
    setRecolhidasDaMesa((estado) => {
      const recolhidas = typeof mudar === 'function' ? mudar(estado.recolhidas) : mudar
      return recolhidas === estado.recolhidas ? estado : { mesaId: estado.mesaId, recolhidas }
    })
  useEffect(() => gravarRecolhidas(recolhidasDaMesa.mesaId, recolhidasDaMesa.recolhidas), [recolhidasDaMesa])
  const editando = useEditorStore((estado) => estado.modo === 'editar')
  const painelRef = useRef<HTMLElement>(null)

  // A aba nova começa do topo: sem isso, quem trocava de aba no meio de uma lista longa caía no meio da outra.
  useEffect(() => {
    const painel = painelRef.current
    if (!painel) return
    painel.scrollTop = 0
    const gaveta = painel.closest('.gaveta-conteudo')
    if (gaveta) gaveta.scrollTop = 0
  }, [aba])

  // Ao entrar no modo de edição, o painel mostra as estruturas.
  useEffect(() => {
    if (editando) setAba('estruturas')
  }, [editando])

  // Token selecionado: os controles de HP e exibição aparecem no topo da aba Tokens.
  const selecionado = useMapaStore((estado) => (estado.selecionadoId ? estado.tokens[estado.selecionadoId] : undefined))

  // Remoção pedida pelo teclado (Delete no token selecionado): abre a mesma confirmação do botão.
  const pedidoDeRemocaoId = useMapaStore((estado) => estado.pedidoDeRemocaoId)
  useEffect(() => {
    if (!pedidoDeRemocaoId) return
    const { tokens, pedirRemocao } = useMapaStore.getState()
    const token = tokens[pedidoDeRemocaoId]
    pedirRemocao(null)
    if (token) {
      setAba('tokens')
      setDialogo({ tipo: 'remover-token', token })
    }
  }, [pedidoDeRemocaoId])

  const recarregarLista = () => queryClient.invalidateQueries({ queryKey: chaveDosMapas(mesaId) })

  const abrir = useMutation({
    mutationFn: (mapaId: string) => obterEstadoDoMapa(mapaId),
    onSuccess: (estado) => useMapaStore.getState().exibir(estado),
  })

  const ativar = useMutation({
    mutationFn: (mapaId: string) => ativarMapa(mapaId),
    onSuccess: async (estado) => {
      useMapaStore.getState().definirMapaAtivo(estado.mapa.id)
      useMapaStore.getState().exibir(estado)
      await recarregarLista()
    },
  })

  const excluir = useMutation({
    mutationFn: (mapa: Mapa) => excluirMapa(mapa.id),
    onSuccess: async (_, mapa) => {
      const store = useMapaStore.getState()
      if (mapa.ativo) store.definirMapaAtivo(null)
      if (store.mapa?.id === mapa.id) {
        store.exibir(null)
        // Excluiu o mapa que estava preparando: volta para o ativo, se houver.
        const ativoId = useMapaStore.getState().mapaAtivoId
        if (ativoId) abrir.mutate(ativoId)
      }
      await recarregarLista()
      fechar()
    },
  })

  const duplicar = useMutation({
    mutationFn: (tokenId: string) => duplicarToken(tokenId),
    onSuccess: (copia) => {
      useMapaStore.getState().aplicarToken(copia)
      useMapaStore.getState().selecionar(copia.id)
    },
  })

  const remover = useMutation({
    mutationFn: (token: Token) => removerToken(token.id),
    onSuccess: (_, token) => {
      useMapaStore.getState().tokenRemovido(token.mapaId, token.id)
      fechar()
    },
  })

  const alternarVisibilidade = useMutation({
    mutationFn: (token: Token) => definirExibicao(token.id, { visivel: !token.visivel }),
    onSuccess: (token) => useMapaStore.getState().aplicarToken(token),
  })

  const recarregarPastas = () =>
    Promise.all([recarregarLista(), queryClient.invalidateQueries({ queryKey: chaveDasPastas(mesaId) })])

  const mover = useMutation({
    mutationFn: ({ mapa, pastaId }: { mapa: Mapa; pastaId: string | null }) => definirPastaDoMapa(mapa.id, pastaId),
    onSuccess: async (salvo) => {
      useMapaStore.getState().mapaAtualizado(salvo)
      await recarregarPastas()
    },
  })

  const removerPasta = useMutation({
    mutationFn: (pasta: PastaDeMapas) => excluirPasta(pasta.id),
    onSuccess: async () => {
      await recarregarPastas()
      fechar()
    },
  })

  const listaDePastas = pastas.data ?? []
  const grupos = mapas.data && listaDePastas.length > 0 ? agruparPorPasta(mapas.data, listaDePastas) : []
  const todasRecolhidas = grupos.length > 0 && grupos.every((grupo) => recolhidas.has(chaveDoGrupo(grupo)))

  // Sincroniza com o clique direto no título da pasta; só muda o estado se for diferente.
  const alternarPasta = (chave: string, recolhida: boolean) =>
    setRecolhidas((atual) => {
      if (atual.has(chave) === recolhida) return atual
      const novas = new Set(atual)
      if (recolhida) novas.add(chave)
      else novas.delete(chave)
      return novas
    })

  const linhaDoMapa = (mapa: Mapa) => {
    const naTela = mapa.id === mapaNaTela?.id
    return (
      <li key={mapa.id} className={`painel-item ${naTela ? 'painel-item-atual' : ''}`}>
        <div className="painel-item-dados">
          <p className="painel-item-nome">
            <strong>{mapa.nome}</strong>
            {mapa.ativo && <span className="selo selo-ativo">Ativo</span>}
          </p>
          <p className="painel-item-detalhe">
            {mapa.largura} × {mapa.altura} células{naTela ? ' · na tela' : ''}
          </p>
        </div>
        <div className="painel-item-acoes">
          {!naTela && (
            <button className="botao botao-discreto" type="button" disabled={abrir.isPending} onClick={() => abrir.mutate(mapa.id)}>
              Abrir
            </button>
          )}
          {!mapa.ativo && (
            <button className="botao botao-discreto" type="button" disabled={ativar.isPending} onClick={() => ativar.mutate(mapa.id)}>
              Ativar
            </button>
          )}
          <button className="botao botao-discreto" type="button" onClick={() => setDialogo({ tipo: 'editar-mapa', mapa })}>
            Editar
          </button>
          <button className="botao botao-discreto botao-perigo" type="button" onClick={() => setDialogo({ tipo: 'excluir-mapa', mapa })}>
            Excluir
          </button>
        </div>
        {listaDePastas.length > 0 && (
          <div className="campo">
            <select
              aria-label={`Pasta de ${mapa.nome}`}
              value={mapa.pastaId ?? ''}
              disabled={mover.isPending}
              onChange={(evento) => mover.mutate({ mapa, pastaId: evento.target.value || null })}
            >
              <option value="">Sem pasta</option>
              {listaDePastas.map((pasta) => (
                <option key={pasta.id} value={pasta.id}>
                  {pasta.nome}
                </option>
              ))}
            </select>
          </div>
        )}
      </li>
    )
  }

  const erroDeAcao = abrir.error ?? ativar.error ?? duplicar.error ?? alternarVisibilidade.error ?? mover.error ?? pastas.error

  return (
    <aside ref={painelRef} className="painel-mestre" aria-label="Painel do Mestre">
      {erroDeAcao && (
        <p className="aviso-erro" role="alert">
          {erroDeAcao.message || 'Não foi possível concluir a ação.'}
        </p>
      )}

      <div className="abas abas-painel" role="tablist" aria-label="Seções do painel">
        {ABAS.map((opcao) => (
          <button
            key={opcao.id}
            className="aba"
            type="button"
            role="tab"
            aria-selected={aba === opcao.id}
            onClick={() => setAba(opcao.id)}
          >
            {opcao.nome}
          </button>
        ))}
      </div>

      {aba === 'iniciativa' && <PainelDeIniciativa mesaId={mesaId} aoAvisar={aoAvisar} />}

      {aba === 'mapas' && (
        <section className="painel-mestre-secao">
          <div className="painel-mestre-topo">
            <h2>Mapas</h2>
            <div className="painel-mestre-topo-acoes">
              <GerarMapa aoGerar={aoGerarMapa} gerando={gerandoMapa} />
              <button className="botao botao-discreto" type="button" onClick={aoCriarMapa}>
                Novo mapa
              </button>
              <button className="botao botao-discreto" type="button" onClick={() => setDialogo({ tipo: 'nova-pasta' })}>
                Nova pasta
              </button>
            </div>
          </div>
          {mapas.isPending ? (
            <p className="painel-mestre-vazio">Carregando mapas…</p>
          ) : mapas.isError ? (
            <p className="aviso-erro" role="alert">
              {mapas.error.message}
            </p>
          ) : mapas.data.length === 0 ? (
            <p className="painel-mestre-vazio">Nenhum mapa ainda.</p>
          ) : listaDePastas.length === 0 ? (
            <ul className="painel-lista">{mapas.data.map(linhaDoMapa)}</ul>
          ) : (
            <div className="painel-pastas">
              <div className="painel-pastas-barra">
                <button
                  className="botao botao-discreto"
                  type="button"
                  onClick={() => setRecolhidas(todasRecolhidas ? new Set() : new Set(grupos.map(chaveDoGrupo)))}
                >
                  {todasRecolhidas ? 'Expandir todas' : 'Recolher todas'}
                </button>
              </div>
              {grupos.map(({ pasta, mapas: doGrupo }) => (
                <details
                  key={pasta?.id ?? 'sem-pasta'}
                  className={`painel-pasta ${pasta ? '' : 'painel-pasta-sem-pasta'}`}
                  style={pasta ? ({ '--cor-da-pasta': pasta.cor } as CSSProperties) : undefined}
                  open={!recolhidas.has(pasta?.id ?? 'sem-pasta')}
                  onToggle={(evento) => alternarPasta(pasta?.id ?? 'sem-pasta', !evento.currentTarget.open)}
                >
                  <summary className="painel-pasta-titulo">
                    <strong>{pasta?.nome ?? 'Sem pasta'}</strong>
                    <span className="painel-item-detalhe">
                      {doGrupo.length} {doGrupo.length === 1 ? 'mapa' : 'mapas'}
                    </span>
                  </summary>
                  {pasta && (
                    <div className="painel-item-acoes">
                      <button
                        className="botao botao-discreto"
                        type="button"
                        onClick={() => setDialogo({ tipo: 'renomear-pasta', pasta })}
                      >
                        Editar pasta
                      </button>
                      <button
                        className="botao botao-discreto botao-perigo"
                        type="button"
                        onClick={() => setDialogo({ tipo: 'excluir-pasta', pasta })}
                      >
                        Excluir pasta
                      </button>
                    </div>
                  )}
                  {doGrupo.length === 0 ? (
                    <p className="painel-mestre-vazio">Nenhum mapa nesta pasta.</p>
                  ) : (
                    <ul className="painel-lista">{doGrupo.map(linhaDoMapa)}</ul>
                  )}
                </details>
              ))}
            </div>
          )}
        </section>
      )}

      {aba !== 'mapas' && aba !== 'imagens' && !mapaNaTela && (
        <p className="painel-mestre-vazio">Abra ou crie um mapa na aba Mapas.</p>
      )}

      {aba === 'estruturas' && mapaNaTela && <PainelDeEstruturas mesaId={mesaId} />}

      {aba === 'nevoa' && mapaNaTela && <PainelDaNevoa />}

      {aba === 'imagens' && <GaleriaDeImagens mesaId={mesaId} />}

      {aba === 'tokens' && mapaNaTela && (
        <section className="painel-mestre-secao">
          <div className="painel-mestre-topo">
            <h2>Tokens</h2>
            <button className="botao botao-discreto" type="button" onClick={() => setDialogo({ tipo: 'da-biblioteca' })}>
              Da biblioteca
            </button>
            <button className="botao botao-discreto" type="button" onClick={() => setDialogo({ tipo: 'novo-token' })}>
              Novo token
            </button>
          </div>
          {selecionado?.tipo === 'Interacao' ? (
            <ControlesDaInteracao key={selecionado.id} mesaId={mesaId} token={selecionado} />
          ) : selecionado ? (
            <ControlesDoToken key={selecionado.id} token={selecionado} movimento={movimento} />
          ) : (
            ordem.length > 0 && <p className="painel-mestre-vazio">Selecione um token para alterar o HP e a exibição.</p>
          )}
          {ordem.length === 0 ? (
            <p className="painel-mestre-vazio">Nenhum token em {mapaNaTela.nome}.</p>
          ) : (
            <ul className="painel-lista">
              {ordem.map((id) => (
                <LinhaDoToken
                  key={id}
                  id={id}
                  duplicando={duplicar.isPending}
                  aoEditar={(token) => setDialogo({ tipo: 'editar-token', token })}
                  aoDuplicar={(token) => duplicar.mutate(token.id)}
                  aoAlternarVisibilidade={(token) => alternarVisibilidade.mutate(token)}
                  aoRemover={(token) => setDialogo({ tipo: 'remover-token', token })}
                />
              ))}
            </ul>
          )}
        </section>
      )}

      {dialogo?.tipo === 'editar-mapa' && <MapaFormDialogo mesaId={mesaId} mapa={dialogo.mapa} aoFechar={fechar} />}

      {dialogo?.tipo === 'excluir-mapa' && (
        <ConfirmarDialogo
          titulo={`Excluir o mapa "${dialogo.mapa.nome}"?`}
          rotuloConfirmar="Excluir mapa"
          rotuloEmAndamento="Excluindo…"
          emAndamento={excluir.isPending}
          erro={excluir.error?.message}
          aoConfirmar={() => excluir.mutate(dialogo.mapa)}
          aoFechar={fechar}
        >
          <p>
            Os tokens e as estruturas deste mapa são excluídos junto.
            {dialogo.mapa.ativo && ' Como ele é o mapa ativo, os jogadores ficam sem mapa até você ativar outro.'} Não dá
            para desfazer.
          </p>
        </ConfirmarDialogo>
      )}

      {dialogo?.tipo === 'nova-pasta' && <PastaDeMapasDialogo mesaId={mesaId} aoFechar={fechar} />}
      {dialogo?.tipo === 'renomear-pasta' && <PastaDeMapasDialogo mesaId={mesaId} pasta={dialogo.pasta} aoFechar={fechar} />}

      {dialogo?.tipo === 'excluir-pasta' && (
        <ConfirmarDialogo
          titulo={`Excluir a pasta "${dialogo.pasta.nome}"?`}
          rotuloConfirmar="Excluir pasta"
          rotuloEmAndamento="Excluindo…"
          emAndamento={removerPasta.isPending}
          erro={removerPasta.error?.message}
          aoConfirmar={() => removerPasta.mutate(dialogo.pasta)}
          aoFechar={fechar}
        >
          <p>Os mapas desta pasta não são excluídos: eles vão para "Sem pasta".</p>
        </ConfirmarDialogo>
      )}

      {dialogo?.tipo === 'novo-token' && mapaNaTela && (
        <TokenFormDialogo mesaId={mesaId} mapaId={mapaNaTela.id} aoFechar={fechar} />
      )}

      {dialogo?.tipo === 'da-biblioteca' && mapaNaTela && (
        <ColocarDaBibliotecaDialogo mesaId={mesaId} mapa={mapaNaTela} aoFechar={fechar} />
      )}

      {dialogo?.tipo === 'editar-token' && (
        <TokenFormDialogo mesaId={mesaId} mapaId={dialogo.token.mapaId} token={dialogo.token} aoFechar={fechar} />
      )}

      {dialogo?.tipo === 'remover-token' && (
        <ConfirmarDialogo
          titulo={`Remover ${dialogo.token.nome} do mapa?`}
          rotuloConfirmar="Remover token"
          rotuloEmAndamento="Removendo…"
          emAndamento={remover.isPending}
          erro={remover.error?.message}
          aoConfirmar={() => remover.mutate(dialogo.token)}
          aoFechar={fechar}
        >
          <p>O token some do mapa para todos. Não dá para desfazer.</p>
        </ConfirmarDialogo>
      )}
    </aside>
  )
}

interface LinhaDoTokenProps {
  id: string
  duplicando: boolean
  aoEditar: (token: Token) => void
  aoDuplicar: (token: Token) => void
  aoAlternarVisibilidade: (token: Token) => void
  aoRemover: (token: Token) => void
}

const LinhaDoToken = memo(function LinhaDoToken({
  id,
  duplicando,
  aoEditar,
  aoDuplicar,
  aoAlternarVisibilidade,
  aoRemover,
}: LinhaDoTokenProps) {
  const token = useMapaStore((estado) => estado.tokens[id])
  const selecionado = useMapaStore((estado) => estado.selecionadoId === id)
  const selecionar = useMapaStore((estado) => estado.selecionar)
  if (!token) return null

  const deJogador = token.donoUsuarioId !== null
  return (
    <li className={`painel-item ${selecionado ? 'painel-item-atual' : ''}`}>
      <button
        className="painel-token"
        type="button"
        aria-pressed={selecionado}
        onClick={() => selecionar(selecionado ? null : token.id)}
      >
        <TokenIcone tipo={token.tipo} icone={token.icone} corBorda={token.corBorda} tamanho={40} />
        <span className="painel-item-dados">
          <span className="painel-item-nome">
            <strong>{token.nome}</strong>
            {deJogador && <span className="selo">Jogador</span>}
            {token.caido && <span className="selo selo-perigo">Caído</span>}
            {!token.visivel && <span className="selo">Oculto</span>}
          </span>
          <span className="painel-item-detalhe">
            {token.tipo === 'Interacao'
              ? token.revelado
                ? 'Interação · conteúdo revelado'
                : 'Interação'
              : `HP ${token.hpAtual}/${token.hpMaximo}`}
          </span>
        </span>
      </button>
      <div className="painel-item-acoes">
        <button
          className="botao botao-discreto"
          type="button"
          aria-pressed={!token.visivel}
          onClick={() => aoAlternarVisibilidade(token)}
        >
          {token.visivel ? 'Ocultar' : 'Revelar'}
        </button>
        <button className="botao botao-discreto" type="button" onClick={() => aoEditar(token)}>
          {deJogador ? 'Imagem' : 'Editar'}
        </button>
        {!deJogador && (
          <>
            <button className="botao botao-discreto" type="button" disabled={duplicando} onClick={() => aoDuplicar(token)}>
              Duplicar
            </button>
            <button className="botao botao-discreto botao-perigo" type="button" onClick={() => aoRemover(token)}>
              Remover
            </button>
          </>
        )}
      </div>
    </li>
  )
})
