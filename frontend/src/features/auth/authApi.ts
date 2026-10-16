import { api } from '../../shared/api/http'

export interface Usuario {
  id: string
  email: string
  nomeExibicao: string
}

export interface RespostaAuth {
  token: string
  expiraEm: string
  usuario: Usuario
}

export function registrar(dados: { email: string; senha: string; nomeExibicao: string }) {
  return api<RespostaAuth>('/api/auth/register', { method: 'POST', body: dados, autenticada: false })
}

export function entrar(dados: { email: string; senha: string }) {
  return api<RespostaAuth>('/api/auth/login', { method: 'POST', body: dados, autenticada: false })
}

/** Troca o token atual, ainda válido, por um novo. */
export function renovar() {
  return api<RespostaAuth>('/api/auth/renovar', { method: 'POST' })
}

export function obterUsuarioAtual() {
  return api<Usuario>('/api/auth/me')
}
