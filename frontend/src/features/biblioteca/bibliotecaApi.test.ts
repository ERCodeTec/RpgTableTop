import { describe, expect, it } from 'vitest'
import { filtrarModelos, type ModeloDeToken } from './bibliotecaApi'

function modelo(nome: string, categoriaId: string | null): ModeloDeToken {
  return {
    id: nome,
    categoriaId,
    tipo: 'Inimigo',
    nome,
    hpMaximo: 7,
    icone: 'goblin',
    imagemId: null,
    corBorda: '#C0503C',
    deslocamento: 6,
    visao: 0,
    exibicaoHp: 'Barra',
    descricao: null,
    conteudoImagemId: null,
    criadoEm: '',
    atualizadoEm: '',
  }
}

const modelos = [modelo('Goblin', 'inimigos'), modelo('Lich Ancião', 'viloes'), modelo('Taverneiro', null)]

describe('filtrarModelos', () => {
  it('filtra por categoria, sem categoria ou todas', () => {
    expect(filtrarModelos(modelos, 'todas', '').map((m) => m.nome)).toEqual(['Goblin', 'Lich Ancião', 'Taverneiro'])
    expect(filtrarModelos(modelos, 'viloes', '').map((m) => m.nome)).toEqual(['Lich Ancião'])
    expect(filtrarModelos(modelos, 'sem-categoria', '').map((m) => m.nome)).toEqual(['Taverneiro'])
  })

  it('busca pelo nome sem diferenciar maiúsculas, junto com a categoria', () => {
    expect(filtrarModelos(modelos, 'todas', '  ANCIÃO ').map((m) => m.nome)).toEqual(['Lich Ancião'])
    expect(filtrarModelos(modelos, 'inimigos', 'lich')).toEqual([])
  })
})
