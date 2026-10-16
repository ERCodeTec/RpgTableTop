import { alturaDesenhada } from './geometria'
import type { Estrutura, MemoriaDeNevoa, ModoDeNevoa, Token, TipoDeMemoria } from './mapaApi'

/**
 * A névoa de guerra. É visual: cada cliente calcula o que vê a partir dos tokens e das estruturas,
 * e o servidor só guarda a memória do que já foi visto (MapaDeBits.cs, no mesmo formato de bits).
 *
 * Uma camada tem um bit por célula, linha a linha: a célula (x, y) é o bit `y * largura + x`, do
 * menos para o mais significativo de cada byte.
 */

/** O estado de cada célula para quem olha: nunca vista, vista antes (escurecida) ou à vista agora. */
export const NUNCA_VISTA = 0
export const EXPLORADA = 1
export const A_VISTA = 2

/** Uma estrutura tapa a visão se barra o movimento e é alta: um obstáculo baixo (0,6) deixa ver por cima. */
const ALTURA_QUE_TAPA_A_VISAO = 1

export const tamanhoDaCamada = (celulas: number) => (celulas + 7) >> 3

export const camadaVazia = (celulas: number) => new Uint8Array(tamanhoDaCamada(celulas))

export const acesa = (bits: Uint8Array, indice: number) => (bits[indice >> 3] & (1 << (indice & 7))) !== 0

export function acender(bits: Uint8Array, indice: number) {
  bits[indice >> 3] |= 1 << (indice & 7)
}

export function deBase64(texto: string): Uint8Array {
  const binario = atob(texto)
  const bits = new Uint8Array(binario.length)
  for (let i = 0; i < binario.length; i++) bits[i] = binario.charCodeAt(i)
  return bits
}

export function paraBase64(bits: Uint8Array): string {
  let binario = ''
  for (let i = 0; i < bits.length; i++) binario += String.fromCharCode(bits[i])
  return btoa(binario)
}

/** true se `bits` acende alguma célula que `memoria` ainda não tem. */
export function acrescenta(bits: Uint8Array, memoria: Uint8Array | undefined): boolean {
  for (let i = 0; i < bits.length; i++) {
    if (bits[i] & ~(memoria?.[i] ?? 0)) return true
  }
  return false
}

/** Uma camada com as células do retângulo acesas (o pincel do Mestre). */
export function camadaDoRetangulo(
  largura: number,
  altura: number,
  area: { x: number; y: number; largura: number; profundidade: number },
): Uint8Array {
  const bits = camadaVazia(largura * altura)
  for (let y = Math.max(0, area.y); y < Math.min(altura, area.y + area.profundidade); y++) {
    for (let x = Math.max(0, area.x); x < Math.min(largura, area.x + area.largura); x++) acender(bits, y * largura + x)
  }
  return bits
}

/** A chave de uma camada nas memórias do store. */
export const chaveDaMemoria = (tipo: TipoDeMemoria, usuarioId: string | null) => `${tipo}|${usuarioId ?? ''}`

export function porChave(memorias: MemoriaDeNevoa[]): Record<string, Uint8Array> {
  const resultado: Record<string, Uint8Array> = {}
  for (const memoria of memorias) resultado[chaveDaMemoria(memoria.tipo, memoria.usuarioId)] = deBase64(memoria.celulas)
  return resultado
}

/** As células que tapam a visão, uma por posição (1 = tapa). */
export function celulasOpacas(largura: number, altura: number, estruturas: Record<string, Estrutura>): Uint8Array {
  const opacas = new Uint8Array(largura * altura)
  for (const id in estruturas) {
    const e = estruturas[id]
    if (e.tipo === 'Chao' || !e.bloqueia || alturaDesenhada(e) < ALTURA_QUE_TAPA_A_VISAO) continue
    for (let y = Math.max(0, e.y); y < Math.min(altura, e.y + e.profundidade); y++) {
      for (let x = Math.max(0, e.x); x < Math.min(largura, e.x + e.largura); x++) opacas[y * largura + x] = 1
    }
  }
  return opacas
}

/**
 * true se o segmento de (x0, y0) a (x1, y1), em coordenadas de célula, chega à célula do fim sem
 * atravessar uma célula opaca. As células do início e do fim não contam: quem está colado à parede
 * vê a parede. Passar exatamente por uma quina só é barrado se as duas células ao lado forem
 * opacas, como no movimento. Percorre as células cortadas pela reta (Amanatides e Woo).
 */
export function linhaLivre(
  x0: number,
  y0: number,
  x1: number,
  y1: number,
  opaca: (x: number, y: number) => boolean,
): boolean {
  let cx = Math.floor(x0)
  let cy = Math.floor(y0)
  const fx = Math.floor(x1)
  const fy = Math.floor(y1)
  const dx = x1 - x0
  const dy = y1 - y0
  const passoX = Math.sign(dx)
  const passoY = Math.sign(dy)
  const deltaX = dx !== 0 ? Math.abs(1 / dx) : Infinity
  const deltaY = dy !== 0 ? Math.abs(1 / dy) : Infinity
  let proximoX = dx > 0 ? (cx + 1 - x0) / dx : dx < 0 ? (cx - x0) / dx : Infinity
  let proximoY = dy > 0 ? (cy + 1 - y0) / dy : dy < 0 ? (cy - y0) / dy : Infinity

  // Cada passo avança uma célula em X ou em Y: o caminho nunca tem mais que |Δx| + |Δy| passos.
  for (let passos = Math.abs(fx - cx) + Math.abs(fy - cy); passos > 0 && (cx !== fx || cy !== fy); passos--) {
    if (Math.abs(proximoX - proximoY) < 1e-9) {
      const fechada = opaca(cx + passoX, cy) && opaca(cx, cy + passoY)
      cx += passoX
      cy += passoY
      proximoX += deltaX
      proximoY += deltaY
      passos--
      if (fechada) return false
    } else if (proximoX < proximoY) {
      cx += passoX
      proximoX += deltaX
    } else {
      cy += passoY
      proximoY += deltaY
    }
    if (cx === fx && cy === fy) return true
    if (opaca(cx, cy)) return false
  }
  return true
}

// Além do centro, a célula conta como vista se a linha chega a um destes pontos perto dos cantos:
// assim se enxerga a parede de lado e a quina de um corredor, e não só o que está bem de frente.
const PONTOS_DA_CELULA = [
  [0.5, 0.5],
  [0.1, 0.1],
  [0.9, 0.1],
  [0.1, 0.9],
  [0.9, 0.9],
] as const

/**
 * As células que um token na célula (ox, oy) vê com `alcance` células de visão, contadas como o
 * movimento (diagonal = 1): marca 1 em `vistas` (uma posição por célula). A própria célula sempre
 * é vista, mesmo com alcance 0.
 */
export function marcarVisao(
  largura: number,
  altura: number,
  opacas: Uint8Array,
  ox: number,
  oy: number,
  alcance: number,
  vistas: Uint8Array,
) {
  if (ox < 0 || oy < 0 || ox >= largura || oy >= altura) return
  const opaca = (x: number, y: number) => x < 0 || y < 0 || x >= largura || y >= altura || opacas[y * largura + x] === 1
  const [cx, cy] = [ox + 0.5, oy + 0.5]
  vistas[oy * largura + ox] = 1
  for (let y = Math.max(0, oy - alcance); y <= Math.min(altura - 1, oy + alcance); y++) {
    for (let x = Math.max(0, ox - alcance); x <= Math.min(largura - 1, ox + alcance); x++) {
      const indice = y * largura + x
      if (vistas[indice]) continue
      for (const [px, py] of PONTOS_DA_CELULA) {
        if (linhaLivre(cx, cy, x + px, y + py, opaca)) {
          vistas[indice] = 1
          break
        }
      }
    }
  }
}

/**
 * De quem é a visão: `null` é o grupo inteiro (todos os jogadores e aliados); um id é um jogador,
 * que vê pelo próprio token e pelos aliados (e, na névoa compartilhada, também pelos do grupo).
 */
export type Observador = string | null

/**
 * Os tokens pelos quais o observador enxerga: os de jogador conforme o modo, e os aliados (tokens
 * do Mestre com visão maior que 0). Um token oculto não empresta a visão a quem não o vê.
 */
export function fontesDeVisao(tokens: Record<string, Token>, modo: ModoDeNevoa, observador: Observador): Token[] {
  const fontes: Token[] = []
  for (const id in tokens) {
    const token = tokens[id]
    const dono = token.donoUsuarioId
    const proprio = dono !== null && dono === observador
    if (!token.visivel && !proprio) continue
    if (dono === null ? token.visao > 0 : modo === 'Compartilhada' || observador === null || proprio) fontes.push(token)
  }
  return fontes
}

export interface Nevoa {
  largura: number
  altura: number
  /** Uma posição por célula: NUNCA_VISTA, EXPLORADA ou A_VISTA. */
  estado: Uint8Array
  /** true: é o Mestre vendo tudo, com a névoa só sombreada para mostrar o que o grupo vê. */
  translucida: boolean
  /** O que os tokens do observador veem agora, em bits (sem o que o Mestre revelou): é o que se informa ao servidor. */
  vistas: Uint8Array
  /** A memória que o servidor guarda para este observador, para saber se as vistas acrescentam algo. */
  memoria: Uint8Array | undefined
}

interface OpcoesDaNevoa {
  largura: number
  altura: number
  modo: ModoDeNevoa
  tokens: Record<string, Token>
  opacas: Uint8Array
  memorias: Record<string, Uint8Array>
  observador: Observador
  translucida: boolean
  /** Visão já calculada de um token, para não refazer a de quem não se moveu. */
  visaoDe?: (token: Token) => Uint8Array
}

/** A névoa como o observador a vê: o que os tokens dele veem, o que o Mestre revelou e o que ficou na memória. */
export function calcularNevoa({
  largura,
  altura,
  modo,
  tokens,
  opacas,
  memorias,
  observador,
  translucida,
  visaoDe,
}: OpcoesDaNevoa): Nevoa {
  const celulas = largura * altura
  const calcular = (token: Token) => {
    const vistas = new Uint8Array(celulas)
    marcarVisao(largura, altura, opacas, token.x, token.y, token.visao, vistas)
    return vistas
  }

  const visao = new Uint8Array(celulas)
  for (const token of fontesDeVisao(tokens, modo, observador)) {
    const doToken = (visaoDe ?? calcular)(token)
    for (let i = 0; i < celulas; i++) visao[i] |= doToken[i]
  }

  // A memória do observador: a do grupo, a dele ou, para o Mestre olhando o grupo na névoa
  // individual, a soma das de todos.
  const camadasDaMemoria =
    modo === 'Compartilhada'
      ? [chaveDaMemoria('Explorada', null)]
      : observador !== null
        ? [chaveDaMemoria('Explorada', observador)]
        : Object.keys(memorias).filter((chave) => chave.startsWith('Explorada|') && chave !== chaveDaMemoria('Explorada', null))
  const revelada = memorias[chaveDaMemoria('Revelada', null)]
  const tamanho = tamanhoDaCamada(celulas)
  const memoria = camadasDaMemoria.length === 1 ? memorias[camadasDaMemoria[0]] : undefined
  // Uma camada de outro tamanho é de antes de o mapa ser redimensionado: até chegar a nova, não vale.
  const valida = (bits: Uint8Array | undefined) => (bits && bits.length === tamanho ? bits : undefined)
  const exploradas = camadasDaMemoria.map((chave) => valida(memorias[chave])).filter((bits) => bits !== undefined)

  const estado = new Uint8Array(celulas)
  const vistas = camadaVazia(celulas)
  const reveladaValida = valida(revelada)
  for (let i = 0; i < celulas; i++) {
    if (visao[i]) acender(vistas, i)
    if (visao[i] || (reveladaValida && acesa(reveladaValida, i))) estado[i] = A_VISTA
    else if (exploradas.some((bits) => acesa(bits, i))) estado[i] = EXPLORADA
  }

  return { largura, altura, estado, translucida, vistas, memoria: valida(memoria) }
}

/** O estado da célula na névoa; fora do mapa conta como nunca vista. */
export function estadoDaCelula(nevoa: Nevoa, x: number, y: number): number {
  if (x < 0 || y < 0 || x >= nevoa.largura || y >= nevoa.altura) return NUNCA_VISTA
  return nevoa.estado[y * nevoa.largura + x]
}

/** O melhor estado entre as células de uma área: uma parede aparece inteira se qualquer parte dela foi vista. */
export function estadoDaArea(nevoa: Nevoa, area: { x: number; y: number; largura: number; profundidade: number }): number {
  let melhor = NUNCA_VISTA
  for (let y = area.y; y < area.y + area.profundidade; y++) {
    for (let x = area.x; x < area.x + area.largura; x++) {
      melhor = Math.max(melhor, estadoDaCelula(nevoa, x, y))
      if (melhor === A_VISTA) return melhor
    }
  }
  return melhor
}

/** true se a névoa esconde o token de quem olha: só aparece o que está à vista. O Mestre vê tudo. */
export const tokenEscondido = (nevoa: Nevoa | null, token: Pick<Token, 'x' | 'y'>) =>
  nevoa !== null && !nevoa.translucida && estadoDaCelula(nevoa, token.x, token.y) !== A_VISTA

/** true se a névoa esconde a estrutura inteira (nenhuma célula dela foi vista). */
export const estruturaEscondida = (nevoa: Nevoa | null, estrutura: Estrutura) =>
  nevoa !== null && !nevoa.translucida && estadoDaArea(nevoa, estrutura) === NUNCA_VISTA
