import { create } from 'zustand'

interface EstadoDaInteracao {
  /** A Interação cujo conteúdo está aberto na tela, ou null. */
  examinandoId: string | null
  examinar: (tokenId: string | null) => void
}

/**
 * Qual objeto de interação o usuário está examinando. O clique no mapa (modoDeJogo) e o painel do
 * Mestre pedem; o DialogoDeInteracao, em MapaAba, busca o conteúdo e o mostra.
 */
export const useInteracaoStore = create<EstadoDaInteracao>((set) => ({
  examinandoId: null,
  examinar: (tokenId) => set({ examinandoId: tokenId }),
}))
