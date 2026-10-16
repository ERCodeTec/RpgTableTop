import { api } from '../../shared/api/http'

/** Inativa: sem ordem. Coletando: o Mestre pediu e os valores estão chegando. EmCombate: só o token da vez anda por blocos. */
export type EstadoDaIniciativa = 'Inativa' | 'Coletando' | 'EmCombate'

export interface ValorDeIniciativa {
  /** Preenchido no valor de um jogador. */
  usuarioId: string | null
  /** A chave do grupo (veja `grupoDeIniciativa`), no valor de inimigos e NPCs de mesmo nome. */
  grupo: string | null
  valor: number
}

/** A ordem não vem pronta: a tela a monta com os tokens que vê (ordemDeIniciativa.ts). */
export interface Iniciativa {
  estado: EstadoDaIniciativa
  rodada: number
  vezDoTokenId: string | null
  valores: ValorDeIniciativa[]
  /** Tokens do mapa ativo cuja vez já passou nesta rodada. */
  jogaramNaRodada: string[]
}

export const VALOR_MINIMO = -10
export const VALOR_MAXIMO = 99

export const INICIATIVA_INATIVA: Iniciativa = { estado: 'Inativa', rodada: 0, vezDoTokenId: null, valores: [], jogaramNaRodada: [] }

const base = (mesaId: string) => `/api/mesas/${mesaId}/iniciativa`

/** Só o Mestre: apaga os valores anteriores e pede a iniciativa de novo. */
export const solicitarIniciativa = (mesaId: string) => api<Iniciativa>(`${base(mesaId)}/solicitar`, { method: 'POST' })

/** O jogador informa a própria iniciativa, uma única vez. */
export const informarMinhaIniciativa = (mesaId: string, valor: number) =>
  api<Iniciativa>(`${base(mesaId)}/minha`, { method: 'POST', body: { valor } })

export const definirIniciativaDoJogador = (mesaId: string, usuarioId: string, valor: number) =>
  api<Iniciativa>(`${base(mesaId)}/jogadores/${usuarioId}`, { method: 'PUT', body: { valor } })

export const definirIniciativaDoGrupo = (mesaId: string, grupo: string, valor: number) =>
  api<Iniciativa>(`${base(mesaId)}/grupos`, { method: 'PUT', body: { grupo, valor } })

export const iniciarCombate = (mesaId: string) => api<Iniciativa>(`${base(mesaId)}/iniciar`, { method: 'POST' })

/** O dono do token da vez, ou o Mestre (pulando a vez), passa ao próximo. */
export const encerrarVez = (mesaId: string, tokenId: string) =>
  api<Iniciativa>(`${base(mesaId)}/encerrar-vez`, { method: 'POST', body: { tokenId } })

export const encerrarCombate = (mesaId: string) => api<Iniciativa>(base(mesaId), { method: 'DELETE' })
