import { api } from '../../shared/api/http'
import type { ClasseDnd } from '../../shared/classes'

export interface Personagem {
  id: string
  nome: string
  classe: ClasseDnd
  nivel: number
  hpMaximo: number
  corBorda: string
  /** Células por rodada no movimento por blocos (6 = 30 ft). */
  deslocamento: number
  /** Até onde o personagem enxerga, em células, nos mapas com névoa (12 = 60 ft). */
  visao: number
  criadoEm: string
  atualizadoEm: string
}

export interface DadosDoPersonagem {
  nome: string
  classe: ClasseDnd
  nivel: number
  hpMaximo: number
  corBorda: string
  deslocamento: number
  visao: number
}

export const CHAVE_PERSONAGENS = ['personagens'] as const

export function listarPersonagens() {
  return api<Personagem[]>('/api/personagens')
}

export function criarPersonagem(dados: DadosDoPersonagem) {
  return api<Personagem>('/api/personagens', { method: 'POST', body: dados })
}

export function atualizarPersonagem(id: string, dados: DadosDoPersonagem) {
  return api<Personagem>(`/api/personagens/${id}`, { method: 'PUT', body: dados })
}

export function excluirPersonagem(id: string) {
  return api<void>(`/api/personagens/${id}`, { method: 'DELETE' })
}
