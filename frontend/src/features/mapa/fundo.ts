import { FUNDO_ESCALA_MAXIMA, FUNDO_ESCALA_MINIMA, type DadosDoFundo } from './mapaApi'

/** A área da imagem de fundo no chão, em células: canto superior esquerdo, largura e altura. */
export interface RetanguloDoFundo {
  x: number
  z: number
  largura: number
  altura: number
}

/**
 * Uma alça do ajuste do fundo. `dx` e `dz` dizem qual lado ela puxa: -1 (esquerda/cima), 1
 * (direita/baixo) ou 0 (não mexe naquele eixo). Nas bordas só um deles é diferente de 0; nos cantos, os dois.
 */
export interface AlcaDoFundo {
  dx: -1 | 0 | 1
  dz: -1 | 0 | 1
  x: number
  z: number
}

export const ehCanto = (alca: Pick<AlcaDoFundo, 'dx' | 'dz'>) => alca.dx !== 0 && alca.dz !== 0

/** Sem escala própria, a altura acompanha a largura e a imagem mantém a proporção original. */
export const escalaDaAltura = (fundo: Pick<DadosDoFundo, 'escala' | 'escalaAltura'>) => fundo.escalaAltura ?? fundo.escala

/**
 * Escala 1 = a imagem tem a largura do mapa; na altura, 1 = a altura proporcional a essa largura.
 * `proporcao` é a da imagem (altura / largura naturais).
 */
export function retanguloDoFundo(fundo: DadosDoFundo, larguraDoMapa: number, proporcao: number): RetanguloDoFundo {
  return {
    x: fundo.offsetX,
    z: fundo.offsetY,
    largura: larguraDoMapa * fundo.escala,
    altura: larguraDoMapa * escalaDaAltura(fundo) * proporcao,
  }
}

/** Quatro bordas (no meio de cada lado) e quatro cantos. */
export function alcasDoFundo(r: RetanguloDoFundo): AlcaDoFundo[] {
  const alcas: AlcaDoFundo[] = []
  for (const dz of [-1, 0, 1] as const) {
    for (const dx of [-1, 0, 1] as const) {
      if (dx === 0 && dz === 0) continue
      alcas.push({ dx, dz, x: r.x + (r.largura * (dx + 1)) / 2, z: r.z + (r.altura * (dz + 1)) / 2 })
    }
  }
  return alcas
}

const limitar = (escala: number) => Math.min(FUNDO_ESCALA_MAXIMA, Math.max(FUNDO_ESCALA_MINIMA, escala))
const arredondarEscala = (valor: number) => Math.round(valor * 1000) / 1000
// Duas casas decimais bastam e mantêm o valor legível.
const arredondarOffset = (valor: number) => Math.round(valor * 100) / 100

/**
 * Puxa uma alça até `ponto` (no chão, em células). O lado ou canto oposto fica onde está.
 * Bordas esticam só o seu eixo; cantos mudam o tamanho mantendo a forma atual (esticada ou não).
 */
export function redimensionarFundo(
  inicial: DadosDoFundo,
  alca: Pick<AlcaDoFundo, 'dx' | 'dz'>,
  ponto: { x: number; z: number },
  larguraDoMapa: number,
  proporcao: number,
): DadosDoFundo {
  const r = retanguloDoFundo(inicial, larguraDoMapa, proporcao)
  // O ponto que não se mexe: o lado oposto à alça em cada eixo puxado.
  const fixoX = alca.dx < 0 ? r.x + r.largura : r.x
  const fixoZ = alca.dz < 0 ? r.z + r.altura : r.z
  const alturaPorEscala = larguraDoMapa * proporcao

  let escala = inicial.escala
  let escalaAltura = inicial.escalaAltura

  if (ehCanto(alca)) {
    // Projeta o ponteiro na diagonal do canto fixo ao canto puxado: o fator acompanha o arraste suave.
    const diagonalX = alca.dx * r.largura
    const diagonalZ = alca.dz * r.altura
    const fator = ((ponto.x - fixoX) * diagonalX + (ponto.z - fixoZ) * diagonalZ) / (diagonalX ** 2 + diagonalZ ** 2)
    const alturaAtual = escalaDaAltura(inicial)
    // O fator respeita os limites dos dois eixos, para a forma não mudar quando um deles encosta.
    const maior = Math.max(inicial.escala, alturaAtual)
    const menor = Math.min(inicial.escala, alturaAtual)
    const fatorLimitado = Math.min(FUNDO_ESCALA_MAXIMA / maior, Math.max(FUNDO_ESCALA_MINIMA / menor, fator))
    escala = arredondarEscala(inicial.escala * fatorLimitado)
    escalaAltura = inicial.escalaAltura === null ? null : arredondarEscala(inicial.escalaAltura * fatorLimitado)
  } else if (alca.dx !== 0) {
    escala = arredondarEscala(limitar((alca.dx * (ponto.x - fixoX)) / larguraDoMapa))
    // A altura fica parada: deixa de acompanhar a largura.
    escalaAltura = escalaDaAltura(inicial)
  } else {
    escalaAltura = arredondarEscala(limitar((alca.dz * (ponto.z - fixoZ)) / alturaPorEscala))
  }

  const largura = larguraDoMapa * escala
  const altura = alturaPorEscala * (escalaAltura ?? escala)
  return {
    ...inicial,
    escala,
    escalaAltura,
    offsetX: alca.dx < 0 ? arredondarOffset(fixoX - largura) : inicial.offsetX,
    offsetY: alca.dz < 0 ? arredondarOffset(fixoZ - altura) : inicial.offsetY,
  }
}

/** Controle de largura da barra. Mantendo a proporção, a altura muda pelo mesmo fator. */
export function comLargura(fundo: DadosDoFundo, nova: number, manterProporcao: boolean): DadosDoFundo {
  const escala = limitar(nova)
  if (!manterProporcao) return { ...fundo, escala, escalaAltura: escalaDaAltura(fundo) }
  const escalaAltura = fundo.escalaAltura === null ? null : arredondarEscala(limitar((fundo.escalaAltura * escala) / fundo.escala))
  return { ...fundo, escala, escalaAltura }
}

/** Controle de altura da barra. Mantendo a proporção, a largura muda pelo mesmo fator. */
export function comAltura(fundo: DadosDoFundo, nova: number, manterProporcao: boolean): DadosDoFundo {
  const altura = limitar(nova)
  if (!manterProporcao) return { ...fundo, escalaAltura: altura }
  const escala = arredondarEscala(limitar((fundo.escala * altura) / escalaDaAltura(fundo)))
  return { ...fundo, escala, escalaAltura: fundo.escalaAltura === null ? null : altura }
}
