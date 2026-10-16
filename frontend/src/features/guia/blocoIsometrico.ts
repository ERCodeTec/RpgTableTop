/**
 * Desenha, num canvas 2D, uma caixa em projeção isométrica com o ladrilho repetido em cada célula de
 * cada face, como o mapa faz em 3D. Serve só de prévia no guia: mostra que o ladrilho é desenhado
 * reto e a inclinação vem da câmera.
 */

type Vetor = [number, number]

const somar = (...vetores: Vetor[]): Vetor => vetores.reduce<Vetor>((total, v) => [total[0] + v[0], total[1] + v[1]], [0, 0])
const escalar = ([x, y]: Vetor, fator: number): Vetor => [x * fator, y * fator]

/** Sombra de cada face lateral, imitando a luz que vem de cima e da esquerda no mapa. */
const SOMBRA_DA_FRENTE = 0.18
const SOMBRA_DO_LADO = 0.38

export interface Caixa {
  /** Células no eixo X. */
  largura: number
  /** Células no eixo Z (profundidade do mapa). */
  profundidade: number
  /** Em células; 0 desenha só o topo, como o chão. */
  altura: number
  /** Como a porta no mapa: um ladrilho só, esticado pela altura das laterais. */
  umLadrilhoNaAltura?: boolean
}

/**
 * `lado` e `alto` são o tamanho do ladrilho em pixels. Como no mapa, nas laterais um ladrilho mais
 * alto que largo cobre `alto / lado` células na vertical.
 */
export function desenharBlocoIsometrico(canvas: HTMLCanvasElement, ladrilho: CanvasImageSource, lado: number, caixa: Caixa, alto = lado): void {
  const contexto = canvas.getContext('2d')
  if (!contexto) return

  // Uma célula: o eixo X vai para a direita e para baixo, o Z para a esquerda e para baixo (2:1).
  const celula = Math.min(canvas.width / (caixa.largura + caixa.profundidade) / 1.1, 48)
  const X: Vetor = [celula, celula / 2]
  const Z: Vetor = [-celula, celula / 2]
  const ALTO: Vetor = [0, celula]
  const W = escalar(X, caixa.largura)
  const D = escalar(Z, caixa.profundidade)
  const H = escalar(ALTO, caixa.altura)

  // Centraliza o desenho: a caixa ocupa de O + D (esquerda) a O + W (direita) e de O a O + W + D + H.
  const larguraTotal = W[0] - D[0]
  const alturaTotal = W[1] + D[1] + H[1]
  const O: Vetor = [(canvas.width - larguraTotal) / 2 - D[0], (canvas.height - alturaTotal) / 2]

  contexto.setTransform(1, 0, 0, 1, 0, 0)
  contexto.clearRect(0, 0, canvas.width, canvas.height)
  contexto.imageSmoothingEnabled = false

  /**
   * Uma face: `colunas` x `linhas` cópias do ladrilho, a partir de `origem`, com os eixos `u` e `v`
   * por célula. Como no 3D, as linhas contam de baixo: numa face de altura quebrada, a sobra fica no
   * alto e mostra só a parte de baixo do ladrilho.
   */
  const face = (origem: Vetor, u: Vetor, v: Vetor, colunas: number, linhas: number, sombra: number, h = lado) => {
    for (let i = 0; i < colunas; i++) {
      for (let j = 0; j < Math.ceil(linhas); j++) {
        const parte = Math.min(1, linhas - j)
        const [ex, ey] = somar(origem, escalar(u, i), escalar(v, linhas - j - 1))
        const corte = h * (1 - parte)
        contexto.setTransform(u[0] / lado, u[1] / lado, v[0] / h, v[1] / h, ex, ey)
        // Meio pixel a mais esconde as frestas entre as células.
        contexto.drawImage(ladrilho, 0, corte, lado, h - corte, 0, corte, lado + 0.5, h - corte + 0.5)
      }
    }
    if (sombra > 0) {
      contexto.setTransform(1, 0, 0, 1, 0, 0)
      const fim = somar(origem, escalar(u, colunas))
      const fundo = escalar(v, linhas)
      contexto.beginPath()
      contexto.moveTo(...origem)
      contexto.lineTo(...fim)
      contexto.lineTo(...somar(fim, fundo))
      contexto.lineTo(...somar(origem, fundo))
      contexto.closePath()
      contexto.fillStyle = `rgb(0 0 0 / ${sombra})`
      contexto.fill()
    }
  }

  // No topo, o ladrilho alto aparece inteiro dentro de uma célula, como no mapa.
  face(O, X, Z, caixa.largura, caixa.profundidade, 0, alto)
  if (caixa.altura > 0) {
    // Esticado, o ladrilho ocupa a altura inteira; senão, cada cópia cobre alto / lado células.
    const celulas = alto / lado
    const [vertical, linhas] = caixa.umLadrilhoNaAltura ? [H, 1] : [escalar(ALTO, celulas), caixa.altura / celulas]
    face(somar(O, D), X, vertical, caixa.largura, linhas, SOMBRA_DA_FRENTE, alto)
    face(somar(O, W), Z, vertical, caixa.profundidade, linhas, SOMBRA_DO_LADO, alto)
  }
  contexto.setTransform(1, 0, 0, 1, 0, 0)
}
