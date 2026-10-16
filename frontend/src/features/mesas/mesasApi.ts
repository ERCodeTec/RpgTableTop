import { api } from '../../shared/api/http'
import type { ClasseDnd } from '../../shared/classes'

export type PapelNaMesa = 'Mestre' | 'Jogador'

export interface PersonagemResumo {
  id: string
  nome: string
  classe: ClasseDnd
  nivel: number
  /** Vem só na lista que o Mestre usa para escolher o personagem de um jogador; no lobby é nulo. */
  hpMaximo: number | null
  corBorda: string
}

export interface Membro {
  usuarioId: string
  nomeExibicao: string
  papel: PapelNaMesa
  personagemAtivo: PersonagemResumo | null
  entrouEm: string
}

export interface MesaResumo {
  id: string
  nome: string
  descricao: string | null
  sessaoAtiva: boolean
  meuPapel: PapelNaMesa
  quantidadeMembros: number
  criadaEm: string
}

/** Como os jogadores movem os próprios tokens. O Mestre arrasta qualquer token nos dois. */
export type ModoDeMovimentacao = 'PorBlocos' | 'Livre'

export interface MesaDetalhe {
  id: string
  nome: string
  descricao: string | null
  sessaoAtiva: boolean
  movimentacao: ModoDeMovimentacao
  meuPapel: PapelNaMesa
  criadaEm: string
  membros: Membro[]
}

export interface Convite {
  codigo: string
  url: string
  criadoEm: string
  expiraEm: string | null
  revogadoEm: string | null
  usosMaximos: number | null
  usosAtuais: number
  valido: boolean
}

export interface ConvitePrevia {
  codigo: string
  mesaId: string
  nomeDaMesa: string
  descricaoDaMesa: string | null
  nomeDoMestre: string
  quantidadeJogadores: number
  jaSouMembro: boolean
}

export interface DadosDaMesa {
  nome: string
  descricao: string
}

export const CHAVE_MESAS = ['mesas'] as const
export const chaveDaMesa = (id: string) => ['mesas', id] as const
export const chaveDosConvites = (mesaId: string) => ['mesas', mesaId, 'convites'] as const
export const chaveDosPersonagensDoMembro = (mesaId: string, usuarioId: string) =>
  ['mesas', mesaId, 'membros', usuarioId, 'personagens'] as const
export const chaveDaPreviaDoConvite = (codigo: string) => ['convites', codigo] as const

export const listarMesas = () => api<MesaResumo[]>('/api/mesas')

export const obterMesa = (id: string) => api<MesaDetalhe>(`/api/mesas/${id}`)

export const criarMesa = (dados: DadosDaMesa) => api<MesaDetalhe>('/api/mesas', { method: 'POST', body: dados })

export const atualizarMesa = (id: string, dados: DadosDaMesa) =>
  api<MesaDetalhe>(`/api/mesas/${id}`, { method: 'PUT', body: dados })

export const excluirMesa = (id: string) => api<void>(`/api/mesas/${id}`, { method: 'DELETE' })

export const definirSessao = (id: string, ativa: boolean) =>
  api<MesaDetalhe>(`/api/mesas/${id}/sessao`, { method: 'PUT', body: { ativa } })

export const definirMovimentacao = (id: string, modo: ModoDeMovimentacao) =>
  api<MesaDetalhe>(`/api/mesas/${id}/movimentacao`, { method: 'PUT', body: { modo } })

export const definirMeuPersonagem = (mesaId: string, personagemId: string) =>
  api<Membro>(`/api/mesas/${mesaId}/meu-personagem`, { method: 'PUT', body: { personagemId } })

export const definirPersonagemDoMembro = (mesaId: string, usuarioId: string, personagemId: string) =>
  api<Membro>(`/api/mesas/${mesaId}/membros/${usuarioId}/personagem`, { method: 'PUT', body: { personagemId } })

export const listarPersonagensDoMembro = (mesaId: string, usuarioId: string) =>
  api<PersonagemResumo[]>(`/api/mesas/${mesaId}/membros/${usuarioId}/personagens`)

/** O Mestre remove um jogador; o jogador sai passando o próprio id. */
export const removerMembro = (mesaId: string, usuarioId: string) =>
  api<void>(`/api/mesas/${mesaId}/membros/${usuarioId}`, { method: 'DELETE' })

export const listarConvites = (mesaId: string) => api<Convite[]>(`/api/mesas/${mesaId}/convites`)

export const gerarConvite = (mesaId: string, opcoes: { validadeHoras: number | null; usosMaximos: number | null }) =>
  api<Convite>(`/api/mesas/${mesaId}/convites`, { method: 'POST', body: opcoes })

export const revogarConvite = (codigo: string) => api<void>(`/api/convites/${codigo}`, { method: 'DELETE' })

export const obterPreviaDoConvite = (codigo: string) => api<ConvitePrevia>(`/api/convites/${codigo}`)

export const aceitarConvite = (codigo: string, personagemId: string) =>
  api<{ mesaId: string }>(`/api/convites/${codigo}/aceitar`, { method: 'POST', body: { personagemId } })
