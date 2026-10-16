import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { ConfirmarDialogo } from '../../shared/ui/ConfirmarDialogo'
import { useMapaStore } from '../mapa/mapaStore'
import { TokenIcone } from '../mapa/TokenIcone'
import { chaveDaMesa, obterMesa } from '../mesas/mesasApi'
import { CampoDeIniciativa } from './CampoDeIniciativa'
import {
  definirIniciativaDoGrupo,
  definirIniciativaDoJogador,
  encerrarCombate,
  iniciarCombate,
  solicitarIniciativa,
  type Iniciativa,
} from './iniciativaApi'
import { useIniciativaStore } from './iniciativaStore'
import { gruposNoMapa } from './ordemDeIniciativa'

interface PainelDeIniciativaProps {
  mesaId: string
  aoAvisar: (mensagem: string) => void
}

type Confirmacao = 'solicitar' | 'encerrar'

/**
 * Aba do Mestre: pede a iniciativa, inicia e encerra o combate, e define (ou corrige) os valores dos
 * jogadores e dos grupos de inimigos e NPCs do mapa ativo. Quem já jogou na rodada fica travado até a próxima.
 */
export function PainelDeIniciativa({ mesaId, aoAvisar }: PainelDeIniciativaProps) {
  const iniciativa = useIniciativaStore((estado) => estado.iniciativa)
  const definir = useIniciativaStore((estado) => estado.definir)
  const { data: mesa } = useQuery({ queryKey: chaveDaMesa(mesaId), queryFn: () => obterMesa(mesaId) })
  const noMapaAtivo = useMapaStore((estado) => estado.mapa !== null && estado.mapa.id === estado.mapaAtivoId)
  const tokens = useMapaStore((estado) => estado.tokens)
  const ids = useMapaStore((estado) => estado.ordem)
  const [ocupado, setOcupado] = useState(false)
  const [confirmacao, setConfirmacao] = useState<Confirmacao | null>(null)

  const doMapa = useMemo(() => ids.map((id) => tokens[id]).filter((token) => token !== undefined), [ids, tokens])
  const grupos = useMemo(() => gruposNoMapa(doMapa), [doMapa])
  const jogadores = mesa?.membros.filter((membro) => membro.papel === 'Jogador') ?? []

  const emCombate = iniciativa.estado === 'EmCombate'
  const jogaram = new Set(iniciativa.jogaramNaRodada)
  // Só trava em combate: o token da vez e quem já jogou nesta rodada.
  const travado = (tokenIds: string[]) =>
    emCombate && tokenIds.some((id) => id === iniciativa.vezDoTokenId || jogaram.has(id))

  const executar = async (acao: () => Promise<Iniciativa>, falha: string) => {
    setOcupado(true)
    try {
      definir(await acao())
    } catch (erro) {
      aoAvisar(erro instanceof Error && erro.message ? erro.message : falha)
    } finally {
      setOcupado(false)
    }
  }

  const solicitar = () => executar(() => solicitarIniciativa(mesaId), 'Não foi possível pedir a iniciativa.')

  return (
    <section className="painel-mestre-secao">
      <div className="painel-mestre-topo">
        <h2>Iniciativa</h2>
        <div className="painel-mestre-topo-acoes">
          {iniciativa.estado === 'Inativa' ? (
            <button className="botao botao-primario" type="button" disabled={ocupado} onClick={() => void solicitar()}>
              Solicitar iniciativa
            </button>
          ) : (
            <>
              {iniciativa.estado === 'Coletando' && (
                <button
                  className="botao botao-primario"
                  type="button"
                  disabled={ocupado}
                  onClick={() => void executar(() => iniciarCombate(mesaId), 'Não foi possível iniciar o combate.')}
                >
                  Iniciar combate
                </button>
              )}
              <button className="botao botao-discreto" type="button" disabled={ocupado} onClick={() => setConfirmacao('solicitar')}>
                Pedir de novo
              </button>
              <button
                className="botao botao-discreto botao-perigo"
                type="button"
                disabled={ocupado}
                onClick={() => setConfirmacao('encerrar')}
              >
                Encerrar
              </button>
            </>
          )}
        </div>
      </div>

      <p className="painel-mestre-vazio">
        {iniciativa.estado === 'Inativa'
          ? 'Peça a iniciativa para os jogadores informarem o valor; você define o dos inimigos e NPCs abaixo.'
          : iniciativa.estado === 'Coletando'
            ? 'Aguardando os valores. Inicie o combate quando quiser: quem informar depois entra na ordem.'
            : `Rodada ${iniciativa.rodada}. Só o token da vez anda por blocos; você ainda arrasta qualquer token livremente.`}
        {mesa?.movimentacao === 'Livre' && ' Com a movimentação livre, a ordem não é aplicada.'}
      </p>

      {iniciativa.estado !== 'Inativa' && (
        <>
          <h3 className="painel-iniciativa-titulo">Jogadores</h3>
          {jogadores.length === 0 ? (
            <p className="painel-mestre-vazio">Nenhum jogador na mesa.</p>
          ) : (
            <ul className="painel-lista">
              {jogadores.map((membro) => {
                const valor = iniciativa.valores.find((v) => v.usuarioId === membro.usuarioId)?.valor ?? null
                const tokenDoJogador = doMapa.filter((token) => token.donoUsuarioId === membro.usuarioId)
                const bloqueado = travado(tokenDoJogador.map((token) => token.id))
                return (
                  <li key={membro.usuarioId} className="painel-item painel-iniciativa-item">
                    <div className="painel-item-dados">
                      <p className="painel-item-nome">
                        <strong>{membro.personagemAtivo?.nome ?? membro.nomeExibicao}</strong>
                      </p>
                      <p className="painel-item-detalhe">
                        {membro.nomeExibicao}
                        {valor === null ? ' · aguardando' : bloqueado ? ' · já jogou nesta rodada' : ''}
                      </p>
                    </div>
                    <CampoDeIniciativa
                      rotulo={`Iniciativa de ${membro.nomeExibicao}`}
                      valor={valor}
                      desabilitado={bloqueado || ocupado}
                      aoEnviar={(novo) =>
                        executar(
                          () => definirIniciativaDoJogador(mesaId, membro.usuarioId, novo),
                          'Não foi possível alterar a iniciativa.',
                        )
                      }
                    />
                  </li>
                )
              })}
            </ul>
          )}

          <h3 className="painel-iniciativa-titulo">Inimigos e NPCs</h3>
          {!noMapaAtivo ? (
            <p className="painel-mestre-vazio">Abra o mapa ativo para definir a iniciativa dos grupos dele.</p>
          ) : grupos.length === 0 ? (
            <p className="painel-mestre-vazio">Nenhum inimigo ou NPC no mapa ativo.</p>
          ) : (
            <ul className="painel-lista">
              {grupos.map((grupo) => {
                const valor = iniciativa.valores.find((v) => v.grupo === grupo.chave)?.valor ?? null
                const bloqueado = travado(grupo.tokens.map((token) => token.id))
                const ocultos = grupo.tokens.every((token) => !token.visivel)
                const [primeiro] = grupo.tokens
                return (
                  <li key={grupo.chave} className="painel-item painel-iniciativa-item">
                    <TokenIcone tipo={primeiro.tipo} icone={primeiro.icone} corBorda={primeiro.corBorda} tamanho={28} />
                    <div className="painel-item-dados">
                      <p className="painel-item-nome">
                        <strong>{grupo.nome}</strong>
                      </p>
                      <p className="painel-item-detalhe">
                        {grupo.tokens.length === 1 ? '1 token' : `${grupo.tokens.length} tokens`}
                        {ocultos ? ' · ocultos, fora da ordem' : bloqueado ? ' · já jogou nesta rodada' : ''}
                      </p>
                    </div>
                    <CampoDeIniciativa
                      rotulo={`Iniciativa de ${grupo.nome}`}
                      valor={valor}
                      desabilitado={bloqueado || ocupado}
                      aoEnviar={(novo) =>
                        executar(() => definirIniciativaDoGrupo(mesaId, grupo.nome, novo), 'Não foi possível alterar a iniciativa.')
                      }
                    />
                  </li>
                )
              })}
            </ul>
          )}
        </>
      )}

      {confirmacao && (
        <ConfirmarDialogo
          titulo={confirmacao === 'solicitar' ? 'Pedir a iniciativa de novo?' : 'Encerrar a iniciativa?'}
          rotuloConfirmar={confirmacao === 'solicitar' ? 'Pedir de novo' : 'Encerrar'}
          rotuloEmAndamento={confirmacao === 'solicitar' ? 'Pedindo…' : 'Encerrando…'}
          emAndamento={ocupado}
          aoFechar={() => setConfirmacao(null)}
          aoConfirmar={() =>
            void (confirmacao === 'solicitar'
              ? solicitar()
              : executar(() => encerrarCombate(mesaId), 'Não foi possível encerrar a iniciativa.')
            ).then(() => setConfirmacao(null))
          }
        >
          <p>
            {confirmacao === 'solicitar'
              ? 'Os valores atuais são apagados e cada jogador informa a iniciativa outra vez. Um combate em andamento para.'
              : 'A ordem deixa de valer e os valores são apagados; os tokens voltam a se mover sem ordem.'}
          </p>
        </ConfirmarDialogo>
      )}
    </section>
  )
}
