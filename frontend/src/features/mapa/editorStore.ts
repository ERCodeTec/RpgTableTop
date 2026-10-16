import { create } from 'zustand'
import type { DadosDoFundo, TipoEstrutura } from './mapaApi'
import { useMapaStore } from './mapaStore'

/**
 * jogar: mover tokens; editar: montar o cenário; fundo: ajustar a imagem de fundo ao grid;
 * nevoa: revelar ou cobrir células da névoa com o pincel.
 */
export type Modo = 'jogar' | 'editar' | 'fundo' | 'nevoa'

/** O que o pincel da névoa faz: revelar as células para todos ou cobri-las (apagando também o explorado). */
export type Pincel = 'revelar' | 'cobrir'

export type Ferramenta = 'selecionar' | 'parede' | 'chao' | 'porta' | 'pilar' | 'obstaculo' | 'entrada'

export const TIPO_DA_FERRAMENTA: Record<Exclude<Ferramenta, 'selecionar'>, TipoEstrutura> = {
  parede: 'Parede',
  porta: 'Porta',
  pilar: 'Pilar',
  obstaculo: 'Obstaculo',
  chao: 'Chao',
  entrada: 'Entrada',
}

/** As ferramentas na ordem da barra; a posição (1 a 7) é também o atalho de teclado. */
export const FERRAMENTAS: { id: Ferramenta; nome: string; dica: string }[] = [
  {
    id: 'selecionar',
    nome: 'Selecionar',
    dica: 'Clique em uma estrutura para selecionar; arraste para mover e puxe as alças para redimensionar. Tokens também podem ser arrastados. Arraste onde não há nada (ou com Shift) para selecionar vários tokens e movê-los juntos.',
  },
  { id: 'parede', nome: 'Parede', dica: 'Arraste no mapa para desenhar um segmento reto de parede.' },
  { id: 'chao', nome: 'Chão', dica: 'Arraste no mapa para desenhar uma área de chão; um clique cobre uma célula.' },
  { id: 'porta', nome: 'Porta', dica: 'Clique em uma célula para colocar uma porta.' },
  { id: 'pilar', nome: 'Pilar', dica: 'Clique em uma célula para colocar um pilar.' },
  { id: 'obstaculo', nome: 'Obstáculo', dica: 'Clique em uma célula para colocar um obstáculo.' },
  {
    id: 'entrada',
    nome: 'Entrada',
    dica: 'Arraste no mapa para marcar onde os jogadores nascem ao entrar no mapa; um clique marca uma célula. Só você a vê, no modo de edição.',
  },
]

/** Retângulo de células mostrado enquanto o Mestre desenha, move ou redimensiona. */
export interface Previa {
  x: number
  y: number
  largura: number
  profundidade: number
  valido: boolean
}

/** Uma ação do editor que pode ser desfeita e refeita; cada lado é uma chamada à API. */
export interface Acao {
  rotulo: string
  fazer: () => Promise<void>
  desfazer: () => Promise<void>
}

interface EstadoDoEditor {
  modo: Modo
  ferramenta: Ferramenta
  /** false: depois de criar uma estrutura, a ferramenta volta a ser a de selecionar. */
  manterFerramenta: boolean
  /** A estrutura selecionada no modo de edição. */
  selecionadaId: string | null
  previa: Previa | null
  /** O fundo sendo ajustado, antes de salvar. */
  fundo: DadosDoFundo | null
  pincel: Pincel
  pilhaDeDesfazer: Acao[]
  pilhaDeRefazer: Acao[]
  /** true enquanto uma ação do editor espera o servidor. */
  ocupado: boolean

  definirModo: (modo: Modo) => void
  /** Escolher de novo a ferramenta ativa volta para a de selecionar. */
  definirFerramenta: (ferramenta: Ferramenta) => void
  alternarManterFerramenta: () => void
  selecionar: (id: string | null) => void
  definirPrevia: (previa: Previa | null) => void
  definirFundo: (fundo: DadosDoFundo | null) => void
  definirPincel: (pincel: Pincel) => void
  /** Esquece seleção, rascunhos e histórico: o mapa na tela mudou. */
  reiniciar: () => void
}

export const LIMITE_DO_HISTORICO = 50

export const useEditorStore = create<EstadoDoEditor>((set) => ({
  modo: 'jogar',
  ferramenta: 'selecionar',
  manterFerramenta: false,
  selecionadaId: null,
  previa: null,
  fundo: null,
  pincel: 'revelar',
  pilhaDeDesfazer: [],
  pilhaDeRefazer: [],
  ocupado: false,

  definirModo: (modo) => {
    // O grupo de tokens é do editor: não sobra selecionado em outro modo.
    if (modo !== useEditorStore.getState().modo) useMapaStore.getState().selecionarGrupo([])
    set((atual) => ({
      modo,
      previa: null,
      fundo: modo === 'fundo' ? atual.fundo : null,
      // Quem entra no modo de edição começa selecionando, não com a ferramenta que ficou da última vez.
      ferramenta: modo === 'editar' && atual.modo !== 'editar' ? 'selecionar' : atual.ferramenta,
    }))
  },
  definirFerramenta: (ferramenta) =>
    set((atual) => ({
      ferramenta: ferramenta === atual.ferramenta && ferramenta !== 'selecionar' ? 'selecionar' : ferramenta,
      previa: null,
    })),
  alternarManterFerramenta: () => set((atual) => ({ manterFerramenta: !atual.manterFerramenta })),
  selecionar: (selecionadaId) => set({ selecionadaId }),
  definirPrevia: (previa) => set({ previa }),
  definirFundo: (fundo) => set({ fundo }),
  definirPincel: (pincel) => set({ pincel }),
  reiniciar: () =>
    set({ modo: 'jogar', ferramenta: 'selecionar', selecionadaId: null, previa: null, fundo: null, pilhaDeDesfazer: [], pilhaDeRefazer: [], ocupado: false }),
}))
