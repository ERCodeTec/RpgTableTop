import { Vector3 } from 'three'
import type { Estrutura, Token } from './mapaApi'

/** Raio do disco do token, em células. */
export const RAIO_DO_TOKEN = 0.38

/** Altura com que uma porta aberta é desenhada: baixa, só para marcar onde ela fica. */
const ALTURA_DA_PORTA_ABERTA = 0.08

/** O chão é uma placa fina: fica sobre a grade e o fundo, e abaixo do disco deitado do token (0,02). */
const ALTURA_DO_CHAO = 0.01

/** O destaque das células alcançáveis: logo acima do chão (0,01) e abaixo do disco deitado do token (0,02). */
export const ALTURA_DO_ALCANCE = 0.015

/** A altura desenhada (e usada no picking) de uma estrutura. */
export const alturaDesenhada = (estrutura: Pick<Estrutura, 'tipo' | 'altura' | 'portaAberta'>) =>
  estrutura.tipo === 'Chao'
    ? ALTURA_DO_CHAO
    : estrutura.tipo === 'Porta' && estrutura.portaAberta
      ? ALTURA_DA_PORTA_ABERTA
      : estrutura.altura

/**
 * Quanto o disco do token sobe do chão para ficar em pé: de frente para a câmera, a borda de baixo
 * precisa continuar acima do chão. Na visão superior o disco fica deitado, rente ao chão.
 */
export const elevacaoDoToken = (inclinacao: number) => RAIO_DO_TOKEN * Math.sin(inclinacao) + 0.02

/** O centro do disco do token no mundo, para a inclinação atual da câmera (em radianos). */
export function centroDoToken(token: Pick<Token, 'x' | 'y'>, inclinacao: number, alvo: Vector3): Vector3 {
  return alvo.set(token.x + 0.5, elevacaoDoToken(inclinacao), token.y + 0.5)
}

/** true se a célula está na área de uma estrutura que bloqueia (o servidor decide; isto só antecipa o aviso). */
export function celulaBloqueada(estruturas: Record<string, Estrutura>, x: number, y: number): boolean {
  for (const id in estruturas) {
    const e = estruturas[id]
    if (e.bloqueia && x >= e.x && y >= e.y && x < e.x + e.largura && y < e.y + e.profundidade) return true
  }
  return false
}

/** Chave de uma célula nos mapas de alcance. */
export const chaveDaCelula = (x: number, y: number) => `${x},${y}`

const DIRECOES = [
  [0, -1], [1, 0], [0, 1], [-1, 0],
  [1, -1], [1, 1], [-1, 1], [-1, -1],
] as const

interface OpcoesDoAlcance {
  largura: number
  altura: number
  /** A célula atual do token. */
  origem: { x: number; y: number }
  /** Quantos passos ainda dá para andar. */
  orcamento: number
  estruturas: Record<string, Estrutura>
  /** Os tokens que o usuário vê: dá para passar pela célula de cada um, mas não parar nela. */
  tokens: Record<string, Token>
  tokenId: string
}

/**
 * As células que o token alcança com o movimento que lhe resta, com o custo do caminho mínimo
 * até cada uma (chave de `chaveDaCelula`). Cada passo, nas 8 direções, custa 1; o token não entra
 * em célula bloqueada nem corta na diagonal o canto de uma estrutura (contorna, e o desvio custa). Pela célula com outro
 * token ele passa, mas não para nela. A origem não entra no resultado. É o mesmo cálculo do servidor
 * (Alcance.cs), que decide; para o jogador, os tokens ocultos nem chegam ao cliente, como lá não contam.
 */
export function celulasAlcancaveis({ largura, altura, origem, orcamento, estruturas, tokens, tokenId }: OpcoesDoAlcance): Map<string, number> {
  const ocupadas = new Set<string>()
  for (const id in tokens) {
    if (id !== tokenId) ocupadas.add(chaveDaCelula(tokens[id].x, tokens[id].y))
  }
  const bloqueada = (x: number, y: number) => celulaBloqueada(estruturas, x, y)
  const livre = (x: number, y: number) => x >= 0 && y >= 0 && x < largura && y < altura && !bloqueada(x, y)

  const custos = new Map<string, number>([[chaveDaCelula(origem.x, origem.y), 0]])
  const fila: [number, number][] = [[origem.x, origem.y]]
  for (let i = 0; i < fila.length; i++) {
    const [x, y] = fila[i]
    const custo = custos.get(chaveDaCelula(x, y))!
    if (custo >= orcamento) continue

    for (const [dx, dy] of DIRECOES) {
      const [vx, vy] = [x + dx, y + dy]
      const chave = chaveDaCelula(vx, vy)
      if (custos.has(chave) || !livre(vx, vy)) continue
      // Na diagonal, uma estrutura em qualquer das duas ortogonais fecha o canto: o token contorna.
      if (dx !== 0 && dy !== 0 && (bloqueada(x + dx, y) || bloqueada(x, y + dy))) continue
      custos.set(chave, custo + 1)
      fila.push([vx, vy])
    }
  }

  custos.delete(chaveDaCelula(origem.x, origem.y))
  // Atravessar é permitido; parar sobre outro token, não.
  for (const chave of ocupadas) custos.delete(chave)
  return custos
}

export const grausParaRad = (graus: number) => (graus * Math.PI) / 180
