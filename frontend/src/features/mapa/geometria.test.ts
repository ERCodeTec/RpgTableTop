import { describe, expect, it } from 'vitest'
import { adjacente, celulasAlcancaveis, chaveDaCelula, possoExaminar } from './geometria'
import type { Estrutura, Token } from './mapaApi'

function token(id: string, x: number, y: number): Token {
  return {
    id,
    mapaId: 'mapa',
    tipo: 'Inimigo',
    nome: id,
    hpAtual: 5,
    hpMaximo: 5,
    hpPercentual: 100,
    caido: false,
    exibicaoHp: 'Barra',
    x,
    y,
    corBorda: '#C0503C',
    icone: 'goblin',
    personagemId: null,
    donoUsuarioId: null,
    visivel: true,
    versao: 1,
    imagemId: null,
    deslocamento: 6,
    movimentoRestante: 6,
    movimentoPendente: false,
    disparada: false,
    visao: 0,
    revelado: false,
  }
}

function parede(id: string, x: number, y: number, extra: Partial<Estrutura> = {}): Estrutura {
  return {
    id,
    mapaId: 'mapa',
    tipo: 'Parede',
    x,
    y,
    largura: 1,
    profundidade: 1,
    altura: 2,
    rotacaoGraus: 0,
    cor: '#6b6f76',
    texturaImagemId: null,
    texturaPadrao: null,
    bloqueiaMovimento: true,
    portaAberta: false,
    bloqueia: true,
    versao: 1,
    ...extra,
  }
}

const porId = <T extends { id: string }>(...itens: T[]) => Object.fromEntries(itens.map((item) => [item.id, item]))

/** Alcance do token "eu" em um mapa 10x8, com o orçamento, as estruturas e os demais tokens dados. */
function alcance(
  origem: { x: number; y: number },
  orcamento: number,
  { estruturas = [] as Estrutura[], outros = [] as Token[] } = {},
) {
  return celulasAlcancaveis({
    largura: 10,
    altura: 8,
    origem,
    orcamento,
    estruturas: porId(...estruturas),
    tokens: porId(token('eu', origem.x, origem.y), ...outros),
    tokenId: 'eu',
  })
}

const custo = (celulas: Map<string, number>, x: number, y: number) => celulas.get(chaveDaCelula(x, y))

describe('celulasAlcancaveis', () => {
  it('cobre as 8 direções e a diagonal custa 1, como na ortogonal', () => {
    const celulas = alcance({ x: 4, y: 4 }, 2)

    expect(custo(celulas, 5, 5)).toBe(1)
    expect(custo(celulas, 3, 4)).toBe(1)
    expect(custo(celulas, 6, 6)).toBe(2)
    expect(custo(celulas, 6, 3)).toBe(2)
    // Um quadrado 5x5 em volta da origem, menos ela.
    expect(celulas.size).toBe(24)
  })

  it('não passa do orçamento, não inclui a origem e com orçamento 0 não alcança nada', () => {
    const celulas = alcance({ x: 4, y: 4 }, 1)

    expect(custo(celulas, 6, 4)).toBeUndefined()
    expect(celulas.has(chaveDaCelula(4, 4))).toBe(false)
    expect(alcance({ x: 4, y: 4 }, 0).size).toBe(0)
  })

  it('fica dentro do mapa', () => {
    const celulas = alcance({ x: 0, y: 0 }, 2)

    expect(celulas.size).toBe(8)
    for (const chave of celulas.keys()) {
      const [x, y] = chave.split(',').map(Number)
      expect(x).toBeGreaterThanOrEqual(0)
      expect(y).toBeGreaterThanOrEqual(0)
    }
    expect(custo(alcance({ x: 9, y: 7 }, 1), 10, 7)).toBeUndefined()
  })

  it('não entra em estrutura que bloqueia e a contorna pelo caminho mínimo', () => {
    // Parede vertical em x = 5, de y = 2 a 6.
    const estruturas = [parede('muro', 5, 2, { profundidade: 5 })]
    const celulas = alcance({ x: 4, y: 4 }, 8, { estruturas })

    expect(custo(celulas, 5, 4)).toBeUndefined()
    expect(custo(celulas, 5, 6)).toBeUndefined()
    // Por cima, sem raspar as pontas do muro: (4,3), (4,2), (4,1), (5,1), (6,1), (6,2), (6,3), (6,4).
    // Com um passo a menos, não chega.
    expect(custo(celulas, 6, 4)).toBe(8)
    expect(custo(alcance({ x: 4, y: 4 }, 7, { estruturas }), 6, 4)).toBeUndefined()
  })

  it('não corta o canto de uma estrutura na diagonal: contorna, e o desvio entra no custo', () => {
    const umaSo = alcance({ x: 0, y: 0 }, 6, { estruturas: [parede('a', 1, 0)] })
    const quina = alcance({ x: 0, y: 0 }, 6, { estruturas: [parede('a', 1, 0), parede('b', 0, 1)] })

    // (0,0) → (0,1) → (1,1): a diagonal direta rasparia a parede em (1,0).
    expect(custo(umaSo, 1, 1)).toBe(2)
    // Do outro lado da parede: (0,1), (1,1), (2,1), (2,0).
    expect(custo(umaSo, 2, 0)).toBe(4)
    expect(quina.size).toBe(0)
  })

  it('contorna a ponta de um muro passando por um token, como no corredor', () => {
    // Corredor em y = 3 entre muros em y = 2 e y = 4 (de x = 0 a 1); um token em (1,3).
    const estruturas = [parede('cima', 0, 2, { largura: 2 }), parede('baixo', 0, 4, { largura: 2 })]
    const celulas = alcance({ x: 0, y: 3 }, 6, { estruturas, outros: [token('a', 1, 3)] })

    // (1,3) pelo token, (2,3), (2,2), (2,1), (1,1), (0,1).
    expect(custo(celulas, 2, 2)).toBe(3)
    expect(custo(celulas, 0, 1)).toBe(6)
  })

  it('ignora porta aberta e obstáculo que não bloqueia', () => {
    const estruturas = [
      parede('porta', 5, 4, { tipo: 'Porta', portaAberta: true, bloqueia: false }),
      parede('tapete', 6, 4, { tipo: 'Obstaculo', bloqueiaMovimento: false, bloqueia: false }),
    ]
    const celulas = alcance({ x: 4, y: 4 }, 2, { estruturas })

    expect(custo(celulas, 5, 4)).toBe(1)
    expect(custo(celulas, 6, 4)).toBe(2)
  })

  it('passa por célula com outro token, mas não para nela, e o próprio token não conta', () => {
    const outros = [token('a', 1, 0), token('b', 0, 1), token('c', 1, 1)]
    const celulas = alcance({ x: 0, y: 0 }, 3, { outros })

    expect(custo(celulas, 1, 0)).toBeUndefined()
    expect(custo(celulas, 0, 1)).toBeUndefined()
    expect(custo(celulas, 1, 1)).toBeUndefined()
    // Cercado por tokens, ainda atravessa: (1,1) e então (2,2).
    expect(custo(celulas, 2, 2)).toBe(2)
    expect(custo(celulas, 2, 0)).toBe(2)
  })

  it('atravessa um token em um túnel de uma célula', () => {
    // Paredes em y = 1 deixam só a linha y = 0; um token no meio dela.
    const estruturas = [parede('muro', 0, 1, { largura: 10 })]
    const celulas = alcance({ x: 0, y: 0 }, 3, { estruturas, outros: [token('a', 1, 0)] })

    expect(custo(celulas, 1, 0)).toBeUndefined()
    expect(custo(celulas, 2, 0)).toBe(2)
    expect(custo(celulas, 3, 0)).toBe(3)
  })
})

describe('possoExaminar', () => {
  const bau = { ...token('bau', 5, 5), tipo: 'Interacao' as const }

  it('aceita as 8 células ao redor, com diagonal, e recusa a 2 células', () => {
    expect(adjacente(bau, { x: 4, y: 4 })).toBe(true)
    expect(adjacente(bau, { x: 6, y: 5 })).toBe(true)
    expect(adjacente(bau, { x: 7, y: 5 })).toBe(false)
    expect(possoExaminar(bau, token('meu', 6, 6))).toBe(true)
    expect(possoExaminar(bau, token('meu', 3, 5))).toBe(false)
  })

  it('revelada, vale de qualquer lugar; sem token meu no mapa, só revelada', () => {
    expect(possoExaminar({ ...bau, revelado: true }, token('meu', 0, 0))).toBe(true)
    expect(possoExaminar({ ...bau, revelado: true }, undefined)).toBe(true)
    expect(possoExaminar(bau, undefined)).toBe(false)
  })

  it('não vale para tokens que não são interação', () => {
    expect(possoExaminar(token('goblin', 5, 5), token('meu', 5, 6))).toBe(false)
  })
})
