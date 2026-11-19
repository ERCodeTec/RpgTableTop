import type { Mapa, PastaDeMapas } from './mapaApi'

/** Um nó da árvore da aba Mapas. pasta nula: o grupo "Sem pasta". */
export interface GrupoDeMapas {
  pasta: PastaDeMapas | null
  mapas: Mapa[]
}

/**
 * Agrupa os mapas pelas pastas, na ordem recebida de cada lista. Todas as pastas aparecem, mesmo
 * vazias; "Sem pasta" vem por último e só se tiver mapa. Um mapa cuja pasta não está na lista
 * (excluída em outra aba, por exemplo) cai em "Sem pasta".
 */
export function agruparPorPasta(mapas: Mapa[], pastas: PastaDeMapas[]): GrupoDeMapas[] {
  const grupos = new Map<string, GrupoDeMapas>(pastas.map((pasta) => [pasta.id, { pasta, mapas: [] }]))
  const semPasta: Mapa[] = []
  for (const mapa of mapas) {
    const grupo = mapa.pastaId ? grupos.get(mapa.pastaId) : undefined
    if (grupo) grupo.mapas.push(mapa)
    else semPasta.push(mapa)
  }

  const resultado = [...grupos.values()]
  if (semPasta.length > 0) resultado.push({ pasta: null, mapas: semPasta })
  return resultado
}

const chaveDasRecolhidas = (mesaId: string) => `vtt:pastas-recolhidas:${mesaId}`

/**
 * As pastas que o Mestre deixou recolhidas nesta mesa, lembradas só neste navegador. Sem acesso ao
 * armazenamento (janela anônima, bloqueio), começa tudo aberto.
 */
export function lerRecolhidas(mesaId: string, armazenamento?: Pick<Storage, 'getItem'>): Set<string> {
  try {
    // Dentro do try: em alguns navegadores só acessar o localStorage já lança erro.
    const alvo = armazenamento ?? globalThis.localStorage
    const lidas: unknown = JSON.parse(alvo?.getItem(chaveDasRecolhidas(mesaId)) ?? '[]')
    return new Set(Array.isArray(lidas) ? lidas.filter((chave): chave is string => typeof chave === 'string') : [])
  } catch {
    return new Set()
  }
}

export function gravarRecolhidas(
  mesaId: string,
  recolhidas: Set<string>,
  armazenamento?: Pick<Storage, 'setItem' | 'removeItem'>,
) {
  try {
    const alvo = armazenamento ?? globalThis.localStorage
    if (recolhidas.size === 0) alvo?.removeItem(chaveDasRecolhidas(mesaId))
    else alvo?.setItem(chaveDasRecolhidas(mesaId), JSON.stringify([...recolhidas]))
  } catch {
    // Sem armazenamento: só não lembra na próxima visita.
  }
}
