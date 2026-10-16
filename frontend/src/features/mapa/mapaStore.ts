import { create } from 'zustand'
import { celulasAlcancaveis } from './geometria'
import type { Estrutura, HpDoToken, Mapa, MapaEstado, MemoriaDeNevoa, MovimentoDoToken, Token } from './mapaApi'
import { chaveDaMemoria, deBase64, NUNCA_VISTA, porChave, type Nevoa, type Observador } from './nevoa'

/** Token sendo arrastado e a célula sob o ponteiro. */
export interface Arraste {
  tokenId: string
  x: number
  y: number
  /** false quando o destino (de algum token do grupo) está fora do mapa ou ocupado por outro token. */
  valido: boolean
  /** Os outros tokens do grupo selecionado, que acompanham o arrastado com o mesmo deslocamento. */
  grupo?: { tokenId: string; x: number; y: number }[]
}

interface EstadoDoMapa {
  /** O mapa ativo da mesa, que é o que os jogadores veem. */
  mapaAtivoId: string | null
  /** O mapa na tela. Para o Mestre pode ser outro, em preparação. */
  mapa: Mapa | null
  tokens: Record<string, Token>
  /** Ids dos tokens em ordem estável; só muda quando um token entra ou sai. */
  ordem: string[]
  estruturas: Record<string, Estrutura>
  /** Ids das estruturas em ordem estável, como `ordem`. */
  ordemDasEstruturas: string[]
  selecionadoId: string | null
  /** Tokens selecionados juntos pela área de seleção do editor; arrastar um deles move todos. */
  grupo: string[]
  /**
   * Token no modo de movimento por blocos: as células que ele alcança ficam destacadas. É estado
   * só desta tela; os demais jogadores não veem o destaque.
   */
  movendoId: string | null
  /** Célula alcançável sob o ponteiro (chave de `chaveDaCelula`), destacada com mais força. */
  pairando: string | null
  arraste: Arraste | null
  /** Incrementado para pedir à câmera que enquadre o mapa de novo. */
  enquadramentos: number
  /** Token que o Mestre pediu para remover pelo teclado; o painel abre a confirmação. */
  pedidoDeRemocaoId: string | null
  /** As camadas da névoa do mapa na tela, por `chaveDaMemoria`, que este usuário recebeu do servidor. */
  memorias: Record<string, Uint8Array>
  /**
   * Só do Mestre: de quem ele está vendo a névoa (`null` = o grupo, um id = um jogador), ou
   * `undefined` para ver tudo, com a névoa só sombreada.
   */
  verComo: Observador | undefined
  /** A névoa calculada para esta tela; null com a névoa desligada. Ver useNevoa. */
  nevoa: Nevoa | null

  /** Troca o mapa na tela pelo estado completo recebido do servidor. */
  exibir: (estado: MapaEstado | null) => void
  definirMapaAtivo: (mapaAtivoId: string | null) => void
  mapaAtualizado: (mapa: Mapa) => void
  /**
   * Insere ou atualiza um token do mapa na tela. Uma versão mais antiga que a local é ignorada,
   * a menos que `forcar` (o servidor rejeitou um movimento e mandou o estado certo).
   */
  aplicarToken: (token: Token, forcar?: boolean) => void
  tokenMovido: (mapaId: string, tokenId: string, x: number, y: number, versao: number) => void
  tokenRemovido: (mapaId: string, tokenId: string) => void
  hpAlterado: (mapaId: string, hp: HpDoToken) => void
  movimentoAlterado: (mapaId: string, movimento: MovimentoDoToken) => void
  /** Insere ou atualiza uma estrutura do mapa na tela, com a mesma regra de versão de `aplicarToken`. */
  aplicarEstrutura: (estrutura: Estrutura, forcar?: boolean) => void
  estruturaRemovida: (mapaId: string, estruturaId: string) => void
  memoriaAlterada: (memoria: MemoriaDeNevoa) => void
  definirVerComo: (verComo: Observador | undefined) => void
  definirNevoa: (nevoa: Nevoa | null) => void
  /**
   * Muda a posição na tela sem mexer na versão: movimento otimista ou a volta dele. `movimento`
   * adianta também o restante e a pendência, no movimento por blocos.
   */
  moverLocal: (tokenId: string, x: number, y: number, movimento?: Pick<Token, 'movimentoRestante' | 'movimentoPendente'>) => void
  /** Seleciona um token (ou nenhum), desfazendo o grupo. Sair do token que estava em movimento tira o destaque. */
  selecionar: (tokenId: string | null) => void
  /** Troca o grupo selecionado (vazio desfaz o grupo). */
  selecionarGrupo: (tokenIds: string[]) => void
  /** Entra (ou sai, com null) no movimento por blocos do token. */
  mover: (tokenId: string | null) => void
  pairar: (celula: string | null) => void
  pedirRemocao: (tokenId: string | null) => void
  definirArraste: (arraste: Arraste | null) => void
  enquadrar: () => void
  limpar: () => void
}

const VAZIO = {
  mapaAtivoId: null,
  mapa: null,
  tokens: {},
  ordem: [],
  estruturas: {},
  ordemDasEstruturas: [],
  selecionadoId: null,
  grupo: [],
  movendoId: null,
  pairando: null,
  arraste: null,
  pedidoDeRemocaoId: null,
  memorias: {},
  verComo: undefined,
  nevoa: null,
}

/**
 * Estado do mapa em exibição. Fica fora do React Query porque muda a cada movimento: com o store,
 * cada token assina só o próprio registro e a cena não é renderizada de novo a cada evento.
 */
export const useMapaStore = create<EstadoDoMapa>((set) => ({
  ...VAZIO,
  enquadramentos: 0,

  exibir: (estado) =>
    set((atual) => {
      if (!estado) {
        return {
          mapa: null,
          tokens: {},
          ordem: [],
          estruturas: {},
          ordemDasEstruturas: [],
          selecionadoId: null,
          grupo: [],
          movendoId: null,
          pairando: null,
          arraste: null,
          memorias: {},
          verComo: undefined,
        }
      }
      const tokens: Record<string, Token> = {}
      for (const token of estado.tokens) {
        tokens[token.id] = token
      }
      const estruturas: Record<string, Estrutura> = {}
      for (const estrutura of estado.estruturas) {
        estruturas[estrutura.id] = estrutura
      }
      return {
        mapa: estado.mapa,
        tokens,
        ordem: estado.tokens.map((token) => token.id),
        estruturas,
        ordemDasEstruturas: estado.estruturas.map((estrutura) => estrutura.id),
        memorias: porChave(estado.memorias),
        // "Ver como" um jogador só vale no mapa em que foi escolhido.
        verComo: atual.mapa?.id === estado.mapa.id ? atual.verComo : undefined,
        selecionadoId: atual.selecionadoId && tokens[atual.selecionadoId] ? atual.selecionadoId : null,
        grupo: atual.grupo.filter((id) => tokens[id]),
        movendoId: atual.movendoId && tokens[atual.movendoId] ? atual.movendoId : null,
        arraste: null,
      }
    }),

  definirMapaAtivo: (mapaAtivoId) =>
    set((atual) => ({
      mapaAtivoId,
      mapa: atual.mapa ? { ...atual.mapa, ativo: atual.mapa.id === mapaAtivoId } : null,
    })),

  mapaAtualizado: (mapa) => set((atual) => (atual.mapa?.id === mapa.id ? { mapa } : atual)),

  aplicarToken: (token, forcar = false) =>
    set((atual) => {
      if (atual.mapa?.id !== token.mapaId) return atual
      const existente = atual.tokens[token.id]
      if (existente && !forcar && token.versao < existente.versao) return atual
      return {
        tokens: { ...atual.tokens, [token.id]: token },
        ordem: existente ? atual.ordem : [...atual.ordem, token.id],
      }
    }),

  tokenMovido: (mapaId, tokenId, x, y, versao) =>
    set((atual) => {
      const token = atual.tokens[tokenId]
      if (atual.mapa?.id !== mapaId || !token || versao < token.versao) return atual
      return { tokens: { ...atual.tokens, [tokenId]: { ...token, x, y, versao } } }
    }),

  tokenRemovido: (mapaId, tokenId) =>
    set((atual) => {
      if (atual.mapa?.id !== mapaId || !atual.tokens[tokenId]) return atual
      const { [tokenId]: _removido, ...tokens } = atual.tokens
      return {
        tokens,
        ordem: atual.ordem.filter((id) => id !== tokenId),
        selecionadoId: atual.selecionadoId === tokenId ? null : atual.selecionadoId,
        grupo: atual.grupo.includes(tokenId) ? atual.grupo.filter((id) => id !== tokenId) : atual.grupo,
        movendoId: atual.movendoId === tokenId ? null : atual.movendoId,
        arraste: atual.arraste?.tokenId === tokenId || atual.arraste?.grupo?.some((g) => g.tokenId === tokenId) ? null : atual.arraste,
      }
    }),

  aplicarEstrutura: (estrutura, forcar = false) =>
    set((atual) => {
      if (atual.mapa?.id !== estrutura.mapaId) return atual
      const existente = atual.estruturas[estrutura.id]
      if (existente && !forcar && estrutura.versao < existente.versao) return atual
      return {
        estruturas: { ...atual.estruturas, [estrutura.id]: estrutura },
        ordemDasEstruturas: existente ? atual.ordemDasEstruturas : [...atual.ordemDasEstruturas, estrutura.id],
      }
    }),

  estruturaRemovida: (mapaId, estruturaId) =>
    set((atual) => {
      if (atual.mapa?.id !== mapaId || !atual.estruturas[estruturaId]) return atual
      const { [estruturaId]: _removida, ...estruturas } = atual.estruturas
      return { estruturas, ordemDasEstruturas: atual.ordemDasEstruturas.filter((id) => id !== estruturaId) }
    }),

  memoriaAlterada: (memoria) =>
    set((atual) =>
      atual.mapa?.id !== memoria.mapaId
        ? atual
        : { memorias: { ...atual.memorias, [chaveDaMemoria(memoria.tipo, memoria.usuarioId)]: deBase64(memoria.celulas) } },
    ),

  definirVerComo: (verComo) => set({ verComo }),
  definirNevoa: (nevoa) => set({ nevoa }),

  hpAlterado: (mapaId, hp) =>
    set((atual) => {
      const token = atual.tokens[hp.tokenId]
      if (atual.mapa?.id !== mapaId || !token || hp.versao < token.versao) return atual
      const { tokenId: _tokenId, ...campos } = hp
      return { tokens: { ...atual.tokens, [hp.tokenId]: { ...token, ...campos } } }
    }),

  movimentoAlterado: (mapaId, movimento) =>
    set((atual) => {
      const token = atual.tokens[movimento.tokenId]
      if (atual.mapa?.id !== mapaId || !token || movimento.versao < token.versao) return atual
      const { tokenId: _tokenId, ...campos } = movimento
      return { tokens: { ...atual.tokens, [movimento.tokenId]: { ...token, ...campos } } }
    }),

  moverLocal: (tokenId, x, y, movimento) =>
    set((atual) => {
      const token = atual.tokens[tokenId]
      return token ? { tokens: { ...atual.tokens, [tokenId]: { ...token, ...movimento, x, y } } } : atual
    }),

  selecionar: (selecionadoId) =>
    set((atual) => ({
      selecionadoId,
      grupo: atual.grupo.length ? [] : atual.grupo,
      movendoId: atual.movendoId === selecionadoId ? atual.movendoId : null,
      pairando: atual.movendoId === selecionadoId ? atual.pairando : null,
    })),
  selecionarGrupo: (grupo) =>
    set((atual) =>
      grupo.length === atual.grupo.length && grupo.every((id, i) => id === atual.grupo[i])
        ? atual
        : { grupo, selecionadoId: grupo.length ? null : atual.selecionadoId, movendoId: grupo.length ? null : atual.movendoId },
    ),
  mover: (movendoId) => set({ movendoId, pairando: null }),
  pairar: (pairando) => set((atual) => (atual.pairando === pairando ? atual : { pairando })),
  pedirRemocao: (pedidoDeRemocaoId) => set({ pedidoDeRemocaoId }),
  definirArraste: (arraste) => set({ arraste }),
  enquadrar: () => set((atual) => ({ enquadramentos: atual.enquadramentos + 1 })),
  limpar: () => set(VAZIO),
}))

/**
 * As células que o token em movimento por blocos alcança agora, com o custo de cada uma; null
 * sem token em movimento, sem movimento conhecido ou com um movimento pendente (é preciso concluir
 * ou desfazer antes de andar de novo). Com a névoa, só entram as células já vistas: o desenho do
 * alcance contorna as paredes e denunciaria o formato do que está no escuro.
 */
export function alcanceAtual({
  mapa,
  tokens,
  estruturas,
  movendoId,
  nevoa,
}: Pick<EstadoDoMapa, 'mapa' | 'tokens' | 'estruturas' | 'movendoId' | 'nevoa'>): Map<string, number> | null {
  const token = movendoId ? tokens[movendoId] : undefined
  if (!mapa || !token || token.movimentoRestante === null || token.movimentoPendente) return null
  const alcance = celulasAlcancaveis({
    largura: mapa.largura,
    altura: mapa.altura,
    origem: token,
    orcamento: token.movimentoRestante,
    estruturas,
    tokens,
    tokenId: token.id,
  })
  if (nevoa && !nevoa.translucida && nevoa.largura === mapa.largura) {
    for (const chave of alcance.keys()) {
      const [x, y] = chave.split(',').map(Number)
      if (nevoa.estado[y * nevoa.largura + x] === NUNCA_VISTA) alcance.delete(chave)
    }
  }
  return alcance
}

/** O token que ocupa a célula, se houver. */
export function tokenNaCelula(tokens: Record<string, Token>, x: number, y: number): Token | undefined {
  for (const id in tokens) {
    const token = tokens[id]
    if (token.x === x && token.y === y) return token
  }
  return undefined
}
