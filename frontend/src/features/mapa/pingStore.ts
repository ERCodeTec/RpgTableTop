import { create } from 'zustand'

/** Quanto tempo um ping fica na tela, em milissegundos. */
export const DURACAO_DO_PING_MS = 2400

/** Acima disso, os pings mais antigos saem antes da hora: a tela não vira um mar de círculos. */
const MAXIMO_DE_PINGS = 12

export const COR_DO_MESTRE = '#e2b23c'
const CORES_DOS_JOGADORES = ['#4fc3f7', '#f06292', '#81c784', '#ba68c8', '#ff8a65', '#4db6ac', '#9575cd', '#dce775']

/** A cor de quem pinga: dourada para o Mestre; para cada jogador, sempre a mesma, tirada do id. */
export function corDoPing(usuarioId: string, ehMestre: boolean): string {
  if (ehMestre) return COR_DO_MESTRE
  let soma = 0
  for (const caractere of usuarioId) soma = (soma * 31 + caractere.charCodeAt(0)) >>> 0
  return CORES_DOS_JOGADORES[soma % CORES_DOS_JOGADORES.length]
}

/** Um ponto do chão apontado por alguém da mesa. x e y em células, com fração. */
export interface Ping {
  id: number
  mapaId: string
  nome: string
  cor: string
  x: number
  y: number
  /** performance.now() de quando o ping apareceu, para a animação. */
  inicio: number
}

interface EstadoDosPings {
  pings: Ping[]
  /** Pedido do Mestre para a câmera ir até um ponto; `vez` muda a cada pedido, mesmo no mesmo ponto. */
  foco: { x: number; y: number; vez: number } | null
  /** Texto para leitores de tela: quem pingou por último. */
  anuncio: string
  mostrar: (ping: Omit<Ping, 'id' | 'inicio'>, focar: boolean) => void
}

let proximoId = 1

/** Os pings visíveis agora. É estado só de tela: nada aqui vai ao servidor nem sobrevive à recarga. */
export const usePingStore = create<EstadoDosPings>()((set) => ({
  pings: [],
  foco: null,
  anuncio: '',
  mostrar(dados, focar) {
    const ping: Ping = { ...dados, id: proximoId++, inicio: performance.now() }
    set((estado) => ({
      pings: [...estado.pings, ping].slice(-MAXIMO_DE_PINGS),
      foco: focar ? { x: ping.x, y: ping.y, vez: ping.id } : estado.foco,
      anuncio: focar ? `${ping.nome} levou a sua visão até um ponto do mapa.` : `${ping.nome} marcou um ponto do mapa.`,
    }))
    window.setTimeout(() => set((estado) => ({ pings: estado.pings.filter((p) => p.id !== ping.id) })), DURACAO_DO_PING_MS)
  },
}))
