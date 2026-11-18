export const TAMANHOS = ['Minusculo', 'Pequeno', 'Medio', 'Grande', 'Enorme', 'Gargantuesco'] as const

/** Mesmo conjunto do enum TamanhoDeCriatura da API. */
export type TamanhoDeCriatura = (typeof TAMANHOS)[number]

export const TAMANHO_PADRAO: TamanhoDeCriatura = 'Medio'
export const LADO_GARGANTUESCO_MINIMO = 4
export const LADO_GARGANTUESCO_LIMITE = 10

export const NOME_DO_TAMANHO: Record<TamanhoDeCriatura, string> = {
  Minusculo: 'Minúsculo (0,75 m)',
  Pequeno: 'Pequeno (1,5 m)',
  Medio: 'Médio (1,5 m)',
  Grande: 'Grande (3 m · 2x2)',
  Enorme: 'Enorme (4,5 m · 3x3)',
  Gargantuesco: 'Gargantuesco (6 m ou mais)',
}

/**
 * O lado, em células, do quadrado que o tamanho ocupa a partir de x, y: o mesmo de
 * Tamanhos.Celulas no servidor. Sem tamanho (dado antigo), Médio.
 */
export function ladoDoTamanho(tamanho: TamanhoDeCriatura | undefined, ladoGargantuesco?: number | null): number {
  switch (tamanho) {
    case 'Grande':
      return 2
    case 'Enorme':
      return 3
    case 'Gargantuesco':
      return ladoGargantuesco ?? LADO_GARGANTUESCO_MINIMO
    default:
      return 1
  }
}
