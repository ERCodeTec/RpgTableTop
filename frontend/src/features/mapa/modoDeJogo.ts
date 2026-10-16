import { Vector3 } from 'three'
import { useEditorStore } from './editorStore'
import { celulaBloqueada, chaveDaCelula } from './geometria'
import type { Gesto, ModoDeInteracao } from './gestos'
import type { Estrutura } from './mapaApi'
import { alcanceAtual, tokenNaCelula, useMapaStore } from './mapaStore'
import { apontar } from './picking'
import type { Rig } from './rig'

interface OpcoesDoJogo {
  meuUsuarioId: string
  souMestre: boolean
  /** Arraste livre do Mestre: chamado ao soltar um token em uma célula válida diferente da atual. */
  aoMover: (tokenId: string, x: number, y: number) => void
  /** Movimento por blocos: chamado ao clicar em uma célula do alcance do token em movimento. */
  aoMoverPorBlocos: (tokenId: string, x: number, y: number) => Promise<void>
  /** Chamado quando o Mestre clica em uma porta. */
  aoAlternarPorta: (porta: Estrutura) => void
}

/**
 * Arrastar um token com snap à célula; um clique sem arrastar o seleciona. Usado no modo de jogo
 * e, pelo Mestre, no editor do mapa. Se o token faz parte do grupo selecionado, o grupo inteiro vai
 * junto, com o mesmo deslocamento, e só pode soltar se todos couberem no destino.
 */
export function arrastarToken(
  rig: Rig,
  tokenId: string,
  clienteX: number,
  clienteY: number,
  { souMestre, aoMover }: Pick<OpcoesDoJogo, 'souMestre' | 'aoMover'>,
): Gesto {
  const store = useMapaStore.getState
  const ponto = new Vector3()
  const inicial = store().tokens[tokenId]
  // O ponteiro pega o disco em pé, não o centro da célula: guarda a diferença para o destino
  // acompanhar o token, e não o ponto do chão que está atrás do ponteiro.
  rig.chao(clienteX, clienteY, ponto)
  const folgaX = ponto.x - (inicial.x + 0.5)
  const folgaZ = ponto.z - (inicial.y + 0.5)
  const grupo = store().grupo.includes(tokenId) ? store().grupo.filter((id) => id !== tokenId && store().tokens[id]) : []
  const movidos = new Set([tokenId, ...grupo])

  return {
    arrastar(x, y) {
      rig.chao(x, y, ponto)
      const celulaX = Math.floor(ponto.x - folgaX)
      const celulaY = Math.floor(ponto.z - folgaZ)
      const atual = store().arraste
      if (atual && atual.x === celulaX && atual.y === celulaY) return

      const { mapa, tokens, estruturas } = store()
      const [dx, dy] = [celulaX - tokens[tokenId].x, celulaY - tokens[tokenId].y]
      const destinos = grupo.map((id) => ({ tokenId: id, x: tokens[id].x + dx, y: tokens[id].y + dy }))
      const cabe = ({ x: cx, y: cy }: { x: number; y: number }) => {
        const ocupante = tokenNaCelula(tokens, cx, cy)
        return (
          mapa !== null &&
          cx >= 0 &&
          cy >= 0 &&
          cx < mapa.largura &&
          cy < mapa.altura &&
          // Um token do próprio grupo sai da célula junto com os outros.
          (!ocupante || movidos.has(ocupante.id)) &&
          // As estruturas barram só os jogadores; o servidor confere de novo.
          (souMestre || !celulaBloqueada(estruturas, cx, cy))
        )
      }
      store().definirArraste({
        tokenId,
        x: celulaX,
        y: celulaY,
        valido: cabe({ x: celulaX, y: celulaY }) && destinos.every(cabe),
        grupo: destinos.length ? destinos : undefined,
      })
    },
    concluir() {
      const arraste = store().arraste
      const token = store().tokens[tokenId]
      store().definirArraste(null)
      if (!arraste?.valido || !token || (arraste.x === token.x && arraste.y === token.y)) return
      const [dx, dy] = [arraste.x - token.x, arraste.y - token.y]
      const movimentos = [{ tokenId, x: arraste.x, y: arraste.y }, ...(arraste.grupo ?? [])]
      // O servidor recusa uma célula ocupada: quem vai mais à frente no sentido do arraste sai primeiro
      // e libera a célula para o de trás. Os pedidos chegam ao servidor nesta ordem.
      movimentos.sort((a, b) => b.x * dx + b.y * dy - (a.x * dx + a.y * dy))
      for (const movimento of movimentos) aoMover(movimento.tokenId, movimento.x, movimento.y)
    },
    cancelar: () => store().definirArraste(null),
    clicar: () => store().selecionar(tokenId),
  }
}

/**
 * Jogar, com movimento por blocos: clicar em um token que eu posso mover destaca as células que ele
 * alcança, e clicar em uma delas o move. O Mestre ainda arrasta qualquer token livremente, sem
 * custo; o jogador só anda por blocos. O Mestre também abre e fecha portas com um clique.
 */
export function criarModoDeJogo(
  rig: Rig,
  { meuUsuarioId, souMestre, aoMover, aoMoverPorBlocos, aoAlternarPorta }: OpcoesDoJogo,
): ModoDeInteracao {
  const store = useMapaStore.getState
  const ponto = new Vector3()

  // Caído, o token do jogador não anda mais; o Mestre move qualquer um.
  const possoMover = (tokenId: string) => {
    const token = store().tokens[tokenId]
    return souMestre || (token?.donoUsuarioId === meuUsuarioId && !token.caido)
  }

  /** Seleciona o token e, se ele é meu (ou sou o Mestre), mostra o alcance dele. */
  const escolherToken = (tokenId: string) => {
    store().selecionar(tokenId)
    if (possoMover(tokenId)) store().mover(tokenId)
  }

  /** A célula alcançável sob o ponteiro, se houver token em movimento. */
  const celulaAlcancavel = (clienteX: number, clienteY: number) => {
    const alcance = alcanceAtual(store())
    if (!alcance) return null
    rig.chao(clienteX, clienteY, ponto)
    const [x, y] = [Math.floor(ponto.x), Math.floor(ponto.z)]
    const chave = chaveDaCelula(x, y)
    return alcance.has(chave) ? { x, y, chave } : null
  }

  return {
    iniciar(clienteX, clienteY) {
      if (!souMestre) return null
      const alvo = apontar(rig, clienteX, clienteY)
      if (alvo?.tipo !== 'token') return null
      // O arraste do Mestre continua livre; um clique sem arrastar entra no movimento por blocos.
      return { ...arrastarToken(rig, alvo.id, clienteX, clienteY, { souMestre, aoMover }), clicar: () => escolherToken(alvo.id) }
    },

    clicarNoFundo(clienteX, clienteY) {
      const alvo = apontar(rig, clienteX, clienteY)
      if (alvo?.tipo === 'token') {
        escolherToken(alvo.id)
        return
      }

      // As células destacadas não têm token, mas podem ter chão, tapete ou porta aberta por cima.
      const destino = celulaAlcancavel(clienteX, clienteY)
      const movendoId = store().movendoId
      if (destino && movendoId) {
        void aoMoverPorBlocos(movendoId, destino.x, destino.y)
        return
      }

      store().selecionar(null)
      const estrutura = alvo ? store().estruturas[alvo.id] : undefined
      if (souMestre && estrutura?.tipo === 'Porta' && useEditorStore.getState().modo === 'jogar') {
        aoAlternarPorta(estrutura)
      }
    },

    pairar(posicao) {
      const celula = posicao ? celulaAlcancavel(posicao.clienteX, posicao.clienteY) : null
      store().pairar(celula?.chave ?? null)
      rig.canvas.style.cursor = celula ? 'pointer' : ''
    },
  }
}
