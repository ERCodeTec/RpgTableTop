import { api } from '../../shared/api/http'
import type { ClasseDnd } from '../../shared/classes'
import { enviarArquivo } from '../../shared/imagens/imagensApi'

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
  /** A foto do personagem (uma só); o token dele no mapa a mostra no lugar do ícone da classe. */
  imagemId: string | null
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

/** Envia a foto do personagem, substituindo a anterior. `aoProgredir` recebe a fração enviada, de 0 a 1. */
export function enviarFoto(id: string, arquivo: File, aoProgredir: (fracao: number) => void) {
  return enviarArquivo<Personagem>(`/api/personagens/${id}/foto`, arquivo, aoProgredir, 'PUT')
}

export function removerFoto(id: string) {
  return api<Personagem>(`/api/personagens/${id}/foto`, { method: 'DELETE' })
}
