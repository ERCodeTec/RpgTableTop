import { Vector3 } from 'three'
import { ladoDoTamanho } from '../../shared/tamanhos'
import type { Estrutura, Token } from './mapaApi'

/** Raio do disco de um token Médio, em células. Os demais tamanhos o escalam (`escalaDoToken`). */
export const RAIO_DO_TOKEN = 0.38

type ComTamanho = Pick<Token, 'tamanho' | 'ladoGargantuesco'>

/** O lado, em células, do quadrado que o token ocupa a partir de (x, y). */
export const ladoDoToken = (token: ComTamanho) => ladoDoTamanho(token.tamanho, token.ladoGargantuesco)

/** A folga entre a borda do disco e a borda do espaço: a mesma do token Médio em qualquer tamanho. */
const FOLGA_DO_DISCO = 0.5 - RAIO_DO_TOKEN

/** O Minúsculo divide nada com ninguém, mas é desenhado menor dentro da célula. */
const ESCALA_DO_MINUSCULO = 0.55

/**
 * Quanto o disco do token é maior (ou menor) que o de um Médio. Num token maior, o disco preenche
 * o espaço inteiro (2x2, 3x3...) deixando só a folga de sempre até a borda do espaço.
 */
export function escalaDoToken(token: ComTamanho): number {
  if (token.tamanho === 'Minusculo') return ESCALA_DO_MINUSCULO
  const lado = ladoDoToken(token)
  return lado === 1 ? 1 : (lado / 2 - FOLGA_DO_DISCO) / RAIO_DO_TOKEN
}

/** Altura com que uma porta aberta é desenhada: baixa, só para marcar onde ela fica. */
const ALTURA_DA_PORTA_ABERTA = 0.08

/** O chão é uma placa fina: fica sobre a grade e o fundo, e abaixo do disco deitado do token (0,02). */
const ALTURA_DO_CHAO = 0.01

/** A entrada fica logo acima do chão (0,01), para aparecer sobre ele, e abaixo do destaque do alcance (0,015). */
const ALTURA_DA_ENTRADA = 0.013

/** O destaque das células alcançáveis: logo acima do chão (0,01) e abaixo do disco deitado do token (0,02). */
export const ALTURA_DO_ALCANCE = 0.015

/** A altura desenhada (e usada no picking) de uma estrutura. */
export const alturaDesenhada = (estrutura: Pick<Estrutura, 'tipo' | 'altura' | 'portaAberta'>) =>
  estrutura.tipo === 'Chao'
    ? ALTURA_DO_CHAO
    : estrutura.tipo === 'Entrada'
      ? ALTURA_DA_ENTRADA
      : estrutura.tipo === 'Porta' && estrutura.portaAberta
        ? ALTURA_DA_PORTA_ABERTA
        : estrutura.altura

/**
 * Quanto o disco do token sobe do chão para ficar em pé: de frente para a câmera, a borda de baixo
 * precisa continuar acima do chão. Na visão superior o disco fica deitado, rente ao chão.
 */
export const elevacaoDoToken = (inclinacao: number, escala = 1) => RAIO_DO_TOKEN * escala * Math.sin(inclinacao) + 0.02

/** O centro do disco do token no mundo, para a inclinação atual da câmera (em radianos): o meio do espaço dele. */
export function centroDoToken(token: Pick<Token, 'x' | 'y'> & ComTamanho, inclinacao: number, alvo: Vector3): Vector3 {
  const meio = ladoDoToken(token) / 2
  return alvo.set(token.x + meio, elevacaoDoToken(inclinacao, escalaDoToken(token)), token.y + meio)
}

/**
 * A âncora (canto superior esquerdo) de um espaço de `lado` células centrado no ponto do chão:
 * para o Médio, a célula sob o ponto.
 */
export const ancoraNoPonto = (px: number, pz: number, lado: number) =>
  [Math.floor(px - (lado - 1) / 2), Math.floor(pz - (lado - 1) / 2)] as const

/** true se os espaços dos dois tokens têm alguma célula em comum (Tamanhos.Sobrepoem no servidor). */
export function sobrepoem(
  a: { x: number; y: number; lado: number },
  b: { x: number; y: number; lado: number },
): boolean {
  return a.x < b.x + b.lado && b.x < a.x + a.lado && a.y < b.y + b.lado && b.y < a.y + a.lado
}

/** true se o espaço de `lado` a partir de (x, y) toca alguma estrutura que bloqueia. */
export function espacoBloqueado(estruturas: Record<string, Estrutura>, x: number, y: number, lado: number): boolean {
  for (const id in estruturas) {
    const e = estruturas[id]
    if (e.bloqueia && x < e.x + e.largura && e.x < x + lado && y < e.y + e.profundidade && e.y < y + lado) return true
  }
  return false
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
  /** A âncora atual do token (canto superior esquerdo do espaço). */
  origem: { x: number; y: number }
  /** O lado do espaço do token, em células; sem ele, 1. */
  lado?: number
  /** Quantos passos ainda dá para andar. */
  orcamento: number
  estruturas: Record<string, Estrutura>
  /** Os tokens que o usuário vê: dá para passar pela célula de cada um, mas não parar nela. */
  tokens: Record<string, Token>
  tokenId: string
}

/**
 * As âncoras que o token alcança com o movimento que lhe resta, com o custo do caminho mínimo
 * até cada uma (chave de `chaveDaCelula`). Cada passo, nas 8 direções, custa 1; o token não entra
 * em célula bloqueada nem corta na diagonal o canto de uma estrutura (contorna, e o desvio custa). Pela célula com outro
 * token ele passa, mas não para nela. Um token de `lado` maior que 1 só passa onde o espaço inteiro
 * cabe. A origem não entra no resultado. É o mesmo cálculo do servidor (Alcance.cs), que decide;
 * para o jogador, os tokens ocultos nem chegam ao cliente, como lá não contam.
 */
export function celulasAlcancaveis({ largura, altura, origem, lado = 1, orcamento, estruturas, tokens, tokenId }: OpcoesDoAlcance): Map<string, number> {
  const outros: { x: number; y: number; lado: number }[] = []
  for (const id in tokens) {
    if (id !== tokenId) outros.push({ x: tokens[id].x, y: tokens[id].y, lado: ladoDoToken(tokens[id]) })
  }
  const livre = (x: number, y: number) =>
    x >= 0 && y >= 0 && x + lado <= largura && y + lado <= altura && !espacoBloqueado(estruturas, x, y, lado)
  const bloqueada = (x: number, y: number) => !livre(x, y)

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
  // Atravessar é permitido; parar com o espaço sobre outro token, não.
  for (const chave of [...custos.keys()]) {
    const [x, y] = chave.split(',').map(Number)
    if (outros.some((outro) => sobrepoem({ x, y, lado }, outro))) custos.delete(chave)
  }
  return custos
}

export const grausParaRad = (graus: number) => (graus * Math.PI) / 180

/**
 * Os espaços dos dois tokens se encostam ou se sobrepõem (a diagonal conta), por qualquer célula
 * de cada um, como no servidor (Token.PodeSerExaminadaPor).
 */
export function adjacente(a: { x: number; y: number } & ComTamanho, b: { x: number; y: number } & ComTamanho): boolean {
  const [la, lb] = [ladoDoToken(a), ladoDoToken(b)]
  const dx = Math.max(0, b.x - (a.x + la - 1), a.x - (b.x + lb - 1))
  const dy = Math.max(0, b.y - (a.y + la - 1), a.y - (b.y + lb - 1))
  return Math.max(dx, dy) <= 1
}

/**
 * Pré-checagem do exame de uma Interação: revelada a todos, ou com o meu token visível ao lado.
 * Quem decide é o servidor; isto só evita o pedido e explica a recusa.
 */
export function possoExaminar(interacao: Token, meuToken: Token | undefined): boolean {
  if (interacao.tipo !== 'Interacao' || !interacao.visivel) return false
  return interacao.revelado || (meuToken !== undefined && meuToken.visivel && adjacente(interacao, meuToken))
}
