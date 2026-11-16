import { describe, expect, it } from 'vitest'
import { alcasDoFundo, comAltura, comLargura, redimensionarFundo, retanguloDoFundo } from './fundo'
import { FUNDO_ESCALA_MAXIMA, FUNDO_ESCALA_MINIMA, type DadosDoFundo } from './mapaApi'

// Mapa de 10 células de largura e imagem com metade da altura da largura (ex.: 2000 × 1000).
const L = 10
const P = 0.5

function fundo(parcial: Partial<DadosDoFundo> = {}): DadosDoFundo {
  return { imagemId: 'img', escala: 1, offsetX: 0, offsetY: 0, escalaAltura: null, ...parcial }
}

describe('retanguloDoFundo', () => {
  it('sem escala da altura, segue a proporção da imagem', () => {
    expect(retanguloDoFundo(fundo({ escala: 2, offsetX: 1, offsetY: -1 }), L, P)).toEqual({ x: 1, z: -1, largura: 20, altura: 10 })
  })

  it('com escala da altura, a altura é independente da largura', () => {
    expect(retanguloDoFundo(fundo({ escala: 2, escalaAltura: 0.5 }), L, P)).toEqual({ x: 0, z: 0, largura: 20, altura: 2.5 })
  })
})

describe('alcasDoFundo', () => {
  it('tem quatro bordas e quatro cantos nos lugares certos', () => {
    const alcas = alcasDoFundo({ x: 0, z: 0, largura: 10, altura: 4 })
    expect(alcas).toHaveLength(8)
    expect(alcas).toContainEqual({ dx: 1, dz: 0, x: 10, z: 2 })
    expect(alcas).toContainEqual({ dx: 0, dz: -1, x: 5, z: 0 })
    expect(alcas).toContainEqual({ dx: -1, dz: 1, x: 0, z: 4 })
  })
})

describe('redimensionarFundo', () => {
  it('a borda direita só muda a largura e a altura fica parada', () => {
    const novo = redimensionarFundo(fundo(), { dx: 1, dz: 0 }, { x: 15, z: 99 }, L, P)
    expect(novo).toMatchObject({ escala: 1.5, escalaAltura: 1, offsetX: 0, offsetY: 0 })
  })

  it('a borda esquerda mantém o lado direito no lugar', () => {
    const novo = redimensionarFundo(fundo({ offsetX: 2 }), { dx: -1, dz: 0 }, { x: 4, z: 0 }, L, P)
    // O lado direito estava em 12; a imagem vai de 4 a 12.
    expect(novo).toMatchObject({ escala: 0.8, offsetX: 4 })
  })

  it('a borda de cima muda a altura e mantém a base no lugar', () => {
    // A altura inicial é 5 (0 a 5); puxando o topo até -5, vira 10.
    const novo = redimensionarFundo(fundo(), { dx: 0, dz: -1 }, { x: 0, z: -5 }, L, P)
    expect(novo).toMatchObject({ escala: 1, escalaAltura: 2, offsetY: -5 })
  })

  it('o canto muda o tamanho mantendo a forma da imagem', () => {
    const novo = redimensionarFundo(fundo(), { dx: 1, dz: 1 }, { x: 20, z: 10 }, L, P)
    expect(novo).toMatchObject({ escala: 2, escalaAltura: null, offsetX: 0, offsetY: 0 })
  })

  it('o canto mantém também uma forma já esticada', () => {
    const novo = redimensionarFundo(fundo({ escala: 1, escalaAltura: 2 }), { dx: 1, dz: 1 }, { x: 5, z: 5 }, L, P)
    expect(novo).toMatchObject({ escala: 0.5, escalaAltura: 1 })
  })

  it('não passa dos limites de escala', () => {
    expect(redimensionarFundo(fundo(), { dx: 1, dz: 0 }, { x: -50, z: 0 }, L, P).escala).toBe(FUNDO_ESCALA_MINIMA)
    expect(redimensionarFundo(fundo(), { dx: 0, dz: 1 }, { x: 0, z: 1e6 }, L, P).escalaAltura).toBe(FUNDO_ESCALA_MAXIMA)
  })
})

describe('controles da barra', () => {
  it('mantendo a proporção, a largura leva a altura junto', () => {
    expect(comLargura(fundo(), 2, true)).toMatchObject({ escala: 2, escalaAltura: null })
    expect(comLargura(fundo({ escalaAltura: 0.5 }), 2, true)).toMatchObject({ escala: 2, escalaAltura: 1 })
  })

  it('sem manter a proporção, cada controle mexe só no seu eixo', () => {
    expect(comLargura(fundo(), 2, false)).toMatchObject({ escala: 2, escalaAltura: 1 })
    expect(comAltura(fundo(), 3, false)).toMatchObject({ escala: 1, escalaAltura: 3 })
  })

  it('mantendo a proporção, a altura leva a largura junto', () => {
    expect(comAltura(fundo(), 3, true)).toMatchObject({ escala: 3, escalaAltura: null })
    expect(comAltura(fundo({ escalaAltura: 2 }), 4, true)).toMatchObject({ escala: 2, escalaAltura: 4 })
  })
})
