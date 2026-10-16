import { useMemo, useState } from 'react'
import { useMapaStore } from '../mapa/mapaStore'
import { TokenIcone } from '../mapa/TokenIcone'
import { CampoDeIniciativa } from './CampoDeIniciativa'
import { encerrarVez, informarMinhaIniciativa } from './iniciativaApi'
import { foraDaVez, useIniciativaStore } from './iniciativaStore'
import { ordemDeIniciativa } from './ordemDeIniciativa'

interface TrilhaDeIniciativaProps {
  mesaId: string
  meuUsuarioId: string
  souMestre: boolean
  movimentoLivre: boolean
  aoAvisar: (mensagem: string) => void
}

const CHAVE = 'vtt.trilha-de-iniciativa'

interface Preferencias {
  vertical: boolean
  minimizada: boolean
}

/** Como cada pessoa prefere ver a trilha; fica neste navegador e volta ao padrão sem localStorage. */
function lerPreferencias(): Preferencias {
  try {
    const salvo = JSON.parse(localStorage.getItem(CHAVE) ?? '{}') as Partial<Preferencias>
    return { vertical: salvo.vertical === true, minimizada: salvo.minimizada === true }
  } catch {
    return { vertical: false, minimizada: false }
  }
}

function guardarPreferencias(preferencias: Preferencias) {
  try {
    localStorage.setItem(CHAVE, JSON.stringify(preferencias))
  } catch {
    // sem localStorage, a preferência vale só até recarregar
  }
}

/**
 * Sobre o mapa, para todos: a ordem da iniciativa, a rodada e de quem é a vez. Os tokens ocultos não
 * entram (os jogadores nem os recebem). O jogador que ainda não informou a iniciativa a envia daqui,
 * e quem está na vez (o dono, ou o Mestre por qualquer um) a encerra. A trilha fica na horizontal ou
 * na vertical e pode ser minimizada; clicar em um token da trilha o seleciona no mapa, como o clique nele.
 */
export function TrilhaDeIniciativa({ mesaId, meuUsuarioId, souMestre, movimentoLivre, aoAvisar }: TrilhaDeIniciativaProps) {
  const iniciativa = useIniciativaStore((estado) => estado.iniciativa)
  const definir = useIniciativaStore((estado) => estado.definir)
  const noMapaAtivo = useMapaStore((estado) => estado.mapa !== null && estado.mapa.id === estado.mapaAtivoId)
  const tokens = useMapaStore((estado) => estado.tokens)
  const ids = useMapaStore((estado) => estado.ordem)
  const selecionadoId = useMapaStore((estado) => estado.selecionadoId)
  const [encerrando, setEncerrando] = useState(false)
  const [preferencias, setPreferencias] = useState(lerPreferencias)

  const ordem = useMemo(
    () => ordemDeIniciativa(iniciativa.valores, ids.map((id) => tokens[id]).filter((token) => token !== undefined)),
    [iniciativa.valores, ids, tokens],
  )

  // Com a movimentação livre a iniciativa não vale; fora do mapa ativo, a ordem é de outro mapa.
  if (iniciativa.estado === 'Inativa' || movimentoLivre || !noMapaAtivo) return null

  const alterarPreferencias = (parcial: Partial<Preferencias>) => {
    const novas = { ...preferencias, ...parcial }
    setPreferencias(novas)
    guardarPreferencias(novas)
  }

  const emCombate = iniciativa.estado === 'EmCombate'
  const jogaram = new Set(iniciativa.jogaramNaRodada)
  const daVez = emCombate && iniciativa.vezDoTokenId ? tokens[iniciativa.vezDoTokenId] : undefined
  const minhaVez = daVez?.donoUsuarioId === meuUsuarioId
  const meuValor = iniciativa.valores.find((valor) => valor.usuarioId === meuUsuarioId)?.valor ?? null
  const possoEncerrar = daVez !== undefined && (souMestre || minhaVez)

  const enviarMinha = async (valor: number) => {
    try {
      definir(await informarMinhaIniciativa(mesaId, valor))
    } catch (erro) {
      aoAvisar(erro instanceof Error && erro.message ? erro.message : 'Não foi possível enviar a iniciativa.')
    }
  }

  const passarAVez = async () => {
    if (!daVez) return
    setEncerrando(true)
    try {
      definir(await encerrarVez(mesaId, daVez.id))
    } catch (erro) {
      aoAvisar(erro instanceof Error && erro.message ? erro.message : 'Não foi possível encerrar a vez.')
    } finally {
      setEncerrando(false)
    }
  }

  /**
   * O mesmo que clicar no token no mapa (modoDeJogo.ts): seleciona qualquer um e, se é meu (ou sou o
   * Mestre), não está caído e pode andar por blocos agora, mostra o alcance.
   */
  const selecionarNoMapa = (tokenId: string) => {
    const store = useMapaStore.getState()
    const token = store.tokens[tokenId]
    store.selecionar(tokenId)
    const possoMover = souMestre || (token?.donoUsuarioId === meuUsuarioId && !token.caido)
    if (!movimentoLivre && possoMover && !foraDaVez(tokenId, movimentoLivre)) store.mover(tokenId)
  }

  const classes = [
    'trilha-de-iniciativa',
    preferencias.vertical && 'trilha-de-iniciativa-vertical',
    preferencias.minimizada && 'trilha-de-iniciativa-minimizada',
  ]

  return (
    <section className={classes.filter(Boolean).join(' ')} aria-label="Iniciativa">
      <div className="trilha-de-iniciativa-topo">
        <strong>{emCombate ? `Rodada ${iniciativa.rodada}` : 'Iniciativa'}</strong>
        <span className="trilha-de-iniciativa-estado" aria-live="polite">
          {!emCombate
            ? 'Aguardando o Mestre iniciar'
            : daVez
              ? minhaVez
                ? 'Sua vez!'
                : `Vez de ${daVez.nome}`
              : 'Ninguém pode jogar agora'}
        </span>
        {possoEncerrar && (
          <button className="botao botao-primario" type="button" disabled={encerrando} onClick={() => void passarAVez()}>
            {souMestre && daVez?.donoUsuarioId ? 'Concluir Jogada' : 'Encerrar vez'}
          </button>
        )}
        <div className="trilha-de-iniciativa-controles">
          {!preferencias.minimizada && (
            <button
              className="botao botao-discreto trilha-de-iniciativa-controle"
              type="button"
              aria-label={preferencias.vertical ? 'Mostrar a trilha na horizontal' : 'Mostrar a trilha na vertical'}
              title={preferencias.vertical ? 'Horizontal' : 'Vertical'}
              onClick={() => alterarPreferencias({ vertical: !preferencias.vertical })}
            >
              {preferencias.vertical ? '⇄' : '⇅'}
            </button>
          )}
          <button
            className="botao botao-discreto trilha-de-iniciativa-controle"
            type="button"
            aria-expanded={!preferencias.minimizada}
            aria-label={preferencias.minimizada ? 'Expandir a trilha da iniciativa' : 'Minimizar a trilha da iniciativa'}
            title={preferencias.minimizada ? 'Expandir' : 'Minimizar'}
            onClick={() => alterarPreferencias({ minimizada: !preferencias.minimizada })}
          >
            {preferencias.minimizada ? '+' : '−'}
          </button>
        </div>
      </div>

      {/* O pedido segue visível com a trilha minimizada: é uma ação que o jogador ainda deve. */}
      {!souMestre && meuValor === null && (
        <div className="trilha-de-iniciativa-pedido">
          <span>O Mestre pediu a sua iniciativa:</span>
          <CampoDeIniciativa rotulo="Sua iniciativa" valor={null} rotuloEnviar="Enviar" aoEnviar={enviarMinha} />
        </div>
      )}

      {preferencias.minimizada ? null : ordem.length > 0 ? (
        <ol className="trilha-de-iniciativa-lista">
          {ordem.map(({ token, valor }) => {
            const atual = token.id === daVez?.id
            const itemClasses = [
              'trilha-de-iniciativa-item',
              atual && 'trilha-de-iniciativa-atual',
              jogaram.has(token.id) && 'trilha-de-iniciativa-jogou',
              token.caido && 'trilha-de-iniciativa-caido',
              token.id === selecionadoId && 'trilha-de-iniciativa-selecionado',
            ]
            return (
              <li key={token.id} aria-current={atual ? 'step' : undefined}>
                <button
                  className={itemClasses.filter(Boolean).join(' ')}
                  type="button"
                  aria-pressed={token.id === selecionadoId}
                  title={`Selecionar ${token.nome} no mapa`}
                  onClick={() => selecionarNoMapa(token.id)}
                >
                  <TokenIcone tipo={token.tipo} icone={token.icone} corBorda={token.corBorda} tamanho={26} />
                  <span className="trilha-de-iniciativa-nome">{token.nome}</span>
                  <span className="trilha-de-iniciativa-valor">{valor}</span>
                  {token.caido && <span className="selo selo-perigo">Caído</span>}
                </button>
              </li>
            )
          })}
        </ol>
      ) : (
        <p className="trilha-de-iniciativa-vazia">Ninguém informou a iniciativa ainda.</p>
      )}
    </section>
  )
}
