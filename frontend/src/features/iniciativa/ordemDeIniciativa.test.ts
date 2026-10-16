import { describe, expect, it } from 'vitest'
import type { Token } from '../mapa/mapaApi'
import type { ValorDeIniciativa } from './iniciativaApi'
import { grupoDeIniciativa, gruposNoMapa, ordemDeIniciativa } from './ordemDeIniciativa'

function token(id: string, nome: string, extra: Partial<Token> = {}): Token {
  return {
    id,
    mapaId: 'mapa',
    tipo: 'Inimigo',
    nome,
    hpAtual: 5,
    hpMaximo: 5,
    hpPercentual: 100,
    caido: false,
    exibicaoHp: 'Barra',
    x: 0,
    y: 0,
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
    ...extra,
  }
}

const jogador = (id: string, nome: string, usuarioId: string) => token(id, nome, { tipo: 'Personagem', donoUsuarioId: usuarioId })
const doJogador = (usuarioId: string, valor: number): ValorDeIniciativa => ({ usuarioId, grupo: null, valor })
const doGrupo = (grupo: string, valor: number): ValorDeIniciativa => ({ usuarioId: null, grupo, valor })
const ids = (itens: { token: Token }[]) => itens.map((item) => item.token.id)

describe('grupoDeIniciativa', () => {
  it('separa o número final do nome e ignora maiúsculas e espaços', () => {
    expect(grupoDeIniciativa('Goblin 3')).toEqual({ chave: 'goblin', nome: 'Goblin', sequencia: 3 })
    expect(grupoDeIniciativa('  Orc   Chefe  ')).toEqual({ chave: 'orc   chefe', nome: 'Orc   Chefe', sequencia: 0 })
    expect(grupoDeIniciativa('Esqueleto 12').chave).toBe(grupoDeIniciativa('ESQUELETO').chave)
  })

  it('não trata um nome só de número como grupo numerado', () => {
    expect(grupoDeIniciativa('42')).toEqual({ chave: '42', nome: '42', sequencia: 0 })
  })
})

describe('ordemDeIniciativa', () => {
  it('vai do maior ao menor, e o grupo joga na ordem do número', () => {
    const tokens = [
      token('g3', 'Goblin 3'),
      jogador('bob', 'Bob', 'u-bob'),
      token('g1', 'Goblin 1'),
      jogador('alice', 'Alice', 'u-alice'),
      token('g2', 'Goblin 2'),
    ]
    const valores = [doJogador('u-alice', 15), doJogador('u-bob', 10), doGrupo('goblin', 12)]

    expect(ids(ordemDeIniciativa(valores, tokens))).toEqual(['alice', 'g1', 'g2', 'g3', 'bob'])
  })

  it('no empate põe os jogadores antes dos grupos, e depois o nome', () => {
    const tokens = [token('orc', 'Orc'), token('aranha', 'Aranha'), jogador('zed', 'Zed', 'u-zed')]
    const valores = [doJogador('u-zed', 10), doGrupo('orc', 10), doGrupo('aranha', 10)]

    expect(ids(ordemDeIniciativa(valores, tokens))).toEqual(['zed', 'aranha', 'orc'])
  })

  it('deixa de fora os ocultos e quem não tem iniciativa', () => {
    const tokens = [token('g1', 'Goblin 1'), token('g2', 'Goblin 2', { visivel: false }), token('lobo', 'Lobo'), jogador('alice', 'Alice', 'u-alice')]

    expect(ids(ordemDeIniciativa([doGrupo('goblin', 8)], tokens))).toEqual(['g1'])
  })
})

describe('gruposNoMapa', () => {
  it('junta os tokens iguais e ignora os dos jogadores', () => {
    const grupos = gruposNoMapa([token('g1', 'Goblin 1'), token('g2', 'goblin 2'), token('orc', 'Orc'), jogador('alice', 'Alice', 'u')])

    expect(grupos.map((grupo) => [grupo.chave, grupo.tokens.length])).toEqual([
      ['goblin', 2],
      ['orc', 1],
    ])
  })
})
