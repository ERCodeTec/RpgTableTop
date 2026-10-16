import { describe, expect, it } from 'vitest'
import type { Estrutura, Token } from './mapaApi'
import {
  A_VISTA,
  acesa,
  calcularNevoa,
  camadaDoRetangulo,
  celulasOpacas,
  chaveDaMemoria,
  deBase64,
  EXPLORADA,
  fontesDeVisao,
  linhaLivre,
  marcarVisao,
  NUNCA_VISTA,
  paraBase64,
  tokenEscondido,
} from './nevoa'

function token(id: string, x: number, y: number, extra: Partial<Token> = {}): Token {
  return {
    id,
    mapaId: 'mapa',
    tipo: 'Personagem',
    nome: id,
    hpAtual: 5,
    hpMaximo: 5,
    hpPercentual: 100,
    caido: false,
    exibicaoHp: 'Completo',
    x,
    y,
    corBorda: '#E2B23C',
    icone: 'Mago',
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
    ...extra,
  }
}

function estrutura(id: string, x: number, y: number, extra: Partial<Estrutura> = {}): Estrutura {
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

/** Desenha as células vistas como texto, uma linha por y: `#` vista, `.` não. */
function desenhar(largura: number, altura: number, vistas: Uint8Array) {
  const linhas: string[] = []
  for (let y = 0; y < altura; y++) {
    let linha = ''
    for (let x = 0; x < largura; x++) linha += vistas[y * largura + x] ? '#' : '.'
    linhas.push(linha)
  }
  return linhas
}

describe('camadas de bits', () => {
  it('ida e volta em base64, no mesmo formato do servidor', () => {
    const bits = camadaDoRetangulo(10, 8, { x: 1, y: 0, largura: 2, profundidade: 1 })
    // Células 1 e 2 da primeira linha: bits 1 e 2 do primeiro byte.
    expect(bits[0]).toBe(0b110)
    expect(bits.length).toBe(10)
    expect(deBase64(paraBase64(bits))).toEqual(bits)
  })
})

describe('celulasOpacas', () => {
  it('parede e porta fechada tapam; porta aberta, chão e obstáculo baixo não', () => {
    const opacas = celulasOpacas(5, 1, {
      parede: estrutura('parede', 0, 0),
      fechada: estrutura('fechada', 1, 0, { tipo: 'Porta', altura: 1.6 }),
      aberta: estrutura('aberta', 2, 0, { tipo: 'Porta', altura: 1.6, portaAberta: true, bloqueia: false }),
      chao: estrutura('chao', 3, 0, { tipo: 'Chao', bloqueia: false, bloqueiaMovimento: false }),
      pedra: estrutura('pedra', 4, 0, { tipo: 'Obstaculo', altura: 0.6 }),
    })
    expect([...opacas]).toEqual([1, 1, 0, 0, 0])
  })
})

describe('linhaLivre', () => {
  const opacaEm = (celulas: string[]) => (x: number, y: number) => celulas.includes(`${x},${y}`)

  it('uma parede no caminho barra; a própria célula de destino não', () => {
    expect(linhaLivre(0.5, 0.5, 4.5, 0.5, opacaEm(['2,0']))).toBe(false)
    expect(linhaLivre(0.5, 0.5, 2.5, 0.5, opacaEm(['2,0']))).toBe(true)
  })

  it('pela quina entre duas células opacas não passa; com uma só, passa', () => {
    expect(linhaLivre(0.5, 0.5, 2.5, 2.5, opacaEm(['1,0', '0,1']))).toBe(false)
    expect(linhaLivre(0.5, 0.5, 2.5, 2.5, opacaEm(['1,0']))).toBe(true)
  })
})

describe('marcarVisao', () => {
  it('em campo aberto vê um quadrado do alcance (diagonal = 1), cortado pelas bordas do mapa', () => {
    const vistas = new Uint8Array(7 * 5)
    marcarVisao(7, 5, new Uint8Array(7 * 5), 1, 2, 2, vistas)
    expect(desenhar(7, 5, vistas)).toEqual(['####...', '####...', '####...', '####...', '####...'])
  })

  it('a parede aparece, mas o que está atrás dela não', () => {
    // Parede vertical em x = 3, de y = 0 a y = 4; o token em (1, 2) vê até a parede.
    const opacas = celulasOpacas(7, 5, { parede: estrutura('parede', 3, 0, { profundidade: 5 }) })
    const vistas = new Uint8Array(7 * 5)
    marcarVisao(7, 5, opacas, 1, 2, 10, vistas)
    expect(desenhar(7, 5, vistas)).toEqual(['####...', '####...', '####...', '####...', '####...'])
  })

  it('pela porta aberta vê o outro lado em leque', () => {
    const estruturas = {
      cima: estrutura('cima', 3, 0, { profundidade: 2 }),
      porta: estrutura('porta', 3, 2, { tipo: 'Porta', portaAberta: true, bloqueia: false }),
      baixo: estrutura('baixo', 3, 3, { profundidade: 2 }),
    }
    const vistas = new Uint8Array(7 * 5)
    marcarVisao(7, 5, celulasOpacas(7, 5, estruturas), 1, 2, 10, vistas)
    const desenho = desenhar(7, 5, vistas)
    expect(desenho[2]).toBe('#######')
    expect(desenho[0].slice(4)).toBe('...')
    expect(desenho[4].slice(4)).toBe('...')
  })

  it('com alcance 0 vê só a própria célula', () => {
    const vistas = new Uint8Array(9)
    marcarVisao(3, 3, new Uint8Array(9), 1, 1, 0, vistas)
    expect(desenhar(3, 3, vistas)).toEqual(['...', '.#.', '...'])
  })
})

describe('fontesDeVisao', () => {
  const tokens = {
    alice: token('alice', 0, 0, { donoUsuarioId: 'u-alice', visao: 5 }),
    bob: token('bob', 9, 0, { donoUsuarioId: 'u-bob', visao: 5 }),
    guia: token('guia', 5, 5, { tipo: 'Npc', visao: 3 }),
    goblin: token('goblin', 6, 6, { tipo: 'Inimigo' }),
    aliadoOculto: token('aliadoOculto', 7, 7, { tipo: 'Npc', visao: 3, visivel: false }),
  }
  const ids = (fontes: Token[]) => fontes.map((t) => t.id).sort()

  it('compartilhada: todos os jogadores e os aliados visíveis', () => {
    expect(ids(fontesDeVisao(tokens, 'Compartilhada', 'u-alice'))).toEqual(['alice', 'bob', 'guia'])
  })

  it('individual: o próprio token e os aliados', () => {
    expect(ids(fontesDeVisao(tokens, 'Individual', 'u-alice'))).toEqual(['alice', 'guia'])
    expect(ids(fontesDeVisao(tokens, 'Individual', null))).toEqual(['alice', 'bob', 'guia'])
  })
})

describe('calcularNevoa', () => {
  const largura = 10
  const altura = 3
  const tokens = { alice: token('alice', 0, 1, { donoUsuarioId: 'u-alice', visao: 2 }) }
  const base = { largura, altura, tokens, opacas: new Uint8Array(largura * altura), translucida: false }

  it('à vista, explorada pela memória e revelada pelo Mestre', () => {
    const explorada = camadaDoRetangulo(largura, altura, { x: 5, y: 1, largura: 1, profundidade: 1 })
    const revelada = camadaDoRetangulo(largura, altura, { x: 9, y: 1, largura: 1, profundidade: 1 })
    const nevoa = calcularNevoa({
      ...base,
      modo: 'Compartilhada',
      observador: 'u-alice',
      memorias: { [chaveDaMemoria('Explorada', null)]: explorada, [chaveDaMemoria('Revelada', null)]: revelada },
    })

    const linha = [...nevoa.estado.slice(largura, 2 * largura)]
    expect(linha).toEqual([A_VISTA, A_VISTA, A_VISTA, NUNCA_VISTA, NUNCA_VISTA, EXPLORADA, NUNCA_VISTA, NUNCA_VISTA, NUNCA_VISTA, A_VISTA])
    // O que se informa ao servidor é só o que os tokens veem, sem o revelado.
    expect(acesa(nevoa.vistas, largura + 2)).toBe(true)
    expect(acesa(nevoa.vistas, largura + 9)).toBe(false)
    expect(nevoa.memoria).toBe(explorada)
  })

  it('individual: usa a memória do próprio jogador, não a de outro', () => {
    const daAlice = camadaDoRetangulo(largura, altura, { x: 6, y: 0, largura: 1, profundidade: 1 })
    const doBob = camadaDoRetangulo(largura, altura, { x: 7, y: 0, largura: 1, profundidade: 1 })
    const nevoa = calcularNevoa({
      ...base,
      modo: 'Individual',
      observador: 'u-alice',
      memorias: { [chaveDaMemoria('Explorada', 'u-alice')]: daAlice, [chaveDaMemoria('Explorada', 'u-bob')]: doBob },
    })

    expect(nevoa.estado[6]).toBe(EXPLORADA)
    expect(nevoa.estado[7]).toBe(NUNCA_VISTA)
  })

  it('esconde o token fora da vista, a menos que a névoa seja a translúcida do Mestre', () => {
    const nevoa = calcularNevoa({ ...base, modo: 'Compartilhada', observador: 'u-alice', memorias: {} })
    expect(tokenEscondido(nevoa, { x: 1, y: 1 })).toBe(false)
    expect(tokenEscondido(nevoa, { x: 8, y: 1 })).toBe(true)
    expect(tokenEscondido({ ...nevoa, translucida: true }, { x: 8, y: 1 })).toBe(false)
    expect(tokenEscondido(null, { x: 8, y: 1 })).toBe(false)
  })
})
