import { create } from 'zustand'
import { useMapaStore } from '../mapa/mapaStore'
import { INICIATIVA_INATIVA, type Iniciativa } from './iniciativaApi'

interface IniciativaStore {
  iniciativa: Iniciativa
  /** Troca pelo estado que veio do servidor (evento, sincronização ou resposta de uma ação). */
  definir: (iniciativa: Iniciativa | null | undefined) => void
}

export const useIniciativaStore = create<IniciativaStore>((set) => ({
  iniciativa: INICIATIVA_INATIVA,
  definir: (iniciativa) => set({ iniciativa: iniciativa ?? INICIATIVA_INATIVA }),
}))

/**
 * true se a iniciativa vale no mapa na tela: combate em andamento, movimentação por blocos e o mapa
 * é o ativo (o Mestre pode estar preparando outro).
 */
export function iniciativaValendo(iniciativa: Iniciativa, movimentoLivre: boolean) {
  const { mapa, mapaAtivoId } = useMapaStore.getState()
  return iniciativa.estado === 'EmCombate' && !movimentoLivre && mapa !== null && mapa.id === mapaAtivoId
}

/** true quando o token não pode andar por blocos agora porque não é a vez dele. O servidor confere de novo. */
export function foraDaVez(tokenId: string, movimentoLivre: boolean) {
  const { iniciativa } = useIniciativaStore.getState()
  return iniciativaValendo(iniciativa, movimentoLivre) && iniciativa.vezDoTokenId !== tokenId
}
