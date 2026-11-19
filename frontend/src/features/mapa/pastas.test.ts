import { describe, expect, it } from 'vitest'
import type { Mapa, PastaDeMapas } from './mapaApi'
import { agruparPorPasta, gravarRecolhidas, lerRecolhidas } from './pastas'

const mapa = (id: string, pastaId?: string | null) => ({ id, nome: id, pastaId }) as Mapa
const pasta = (id: string): PastaDeMapas => ({ id, nome: id, quantidadeDeMapas: 0, cor: '#E2B23C' })

describe('agruparPorPasta', () => {
  it('sem pastas, põe todos os mapas em "Sem pasta"', () => {
    const grupos = agruparPorPasta([mapa('a'), mapa('b', null)], [])

    expect(grupos).toHaveLength(1)
    expect(grupos[0].pasta).toBeNull()
    expect(grupos[0].mapas.map((m) => m.id)).toEqual(['a', 'b'])
  })

  it('mantém a ordem das pastas e dos mapas, e "Sem pasta" por último', () => {
    const grupos = agruparPorPasta([mapa('1', 'y'), mapa('2'), mapa('3', 'x'), mapa('4', 'y')], [pasta('x'), pasta('y')])

    expect(grupos.map((g) => g.pasta?.id ?? null)).toEqual(['x', 'y', null])
    expect(grupos[1].mapas.map((m) => m.id)).toEqual(['1', '4'])
    expect(grupos[2].mapas.map((m) => m.id)).toEqual(['2'])
  })

  it('mostra pasta vazia e esconde "Sem pasta" vazio', () => {
    const grupos = agruparPorPasta([mapa('1', 'x')], [pasta('x'), pasta('vazia')])

    expect(grupos.map((g) => g.pasta?.id)).toEqual(['x', 'vazia'])
    expect(grupos[1].mapas).toEqual([])
  })

  it('mapa de pasta desconhecida cai em "Sem pasta"', () => {
    const grupos = agruparPorPasta([mapa('1', 'sumiu')], [pasta('x')])

    expect(grupos[1].pasta).toBeNull()
    expect(grupos[1].mapas.map((m) => m.id)).toEqual(['1'])
  })
})

describe('lerRecolhidas e gravarRecolhidas', () => {
  const memoria = () => {
    const dados = new Map<string, string>()
    return {
      getItem: (chave: string) => dados.get(chave) ?? null,
      setItem: (chave: string, valor: string) => void dados.set(chave, valor),
      removeItem: (chave: string) => void dados.delete(chave),
    }
  }

  it('lembra por mesa', () => {
    const armazenamento = memoria()
    gravarRecolhidas('mesa-1', new Set(['a', 'sem-pasta']), armazenamento)

    expect([...lerRecolhidas('mesa-1', armazenamento)]).toEqual(['a', 'sem-pasta'])
    expect(lerRecolhidas('mesa-2', armazenamento).size).toBe(0)
  })

  it('conjunto vazio apaga a chave', () => {
    const armazenamento = memoria()
    gravarRecolhidas('mesa-1', new Set(['a']), armazenamento)
    gravarRecolhidas('mesa-1', new Set(), armazenamento)

    expect(armazenamento.getItem('vtt:pastas-recolhidas:mesa-1')).toBeNull()
  })

  it('valor estragado ou armazenamento que falha começa tudo aberto', () => {
    const estragado = { getItem: () => '{não é json' }
    const quebrado = {
      getItem: () => {
        throw new Error('bloqueado')
      },
    }

    expect(lerRecolhidas('mesa-1', estragado).size).toBe(0)
    expect(lerRecolhidas('mesa-1', quebrado).size).toBe(0)
  })
})
