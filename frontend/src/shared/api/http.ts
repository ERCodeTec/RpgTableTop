import { lerToken } from './tokenStorage'

/** Erro no formato ProblemDetails devolvido pela API. */
export class ApiError extends Error {
  readonly status: number
  /** Mensagens por campo (ValidationProblemDetails), com as chaves em camelCase. */
  readonly erros: Record<string, string[]>

  constructor(status: number, mensagem: string, erros: Record<string, string[]> = {}) {
    super(mensagem)
    this.name = 'ApiError'
    this.status = status
    this.erros = erros
  }
}

let aoPerderSessao: (() => void) | null = null

/** Registrado pelo AuthProvider: chamado quando a API responde 401 a uma requisição autenticada. */
export function definirAoPerderSessao(callback: (() => void) | null): void {
  aoPerderSessao = callback
}

/** Encerra a sessão por fora do fetch (o Hub recusou o token, ou ele venceu). */
export function perderSessao(): void {
  aoPerderSessao?.()
}

/**
 * No plano gratuito, o ngrok responde a requisições de navegador com uma página de aviso em vez
 * do conteúdo. Este cabeçalho pede a resposta de verdade; fora do ngrok ele é ignorado.
 */
export const CABECALHOS_PADRAO: Record<string, string> = { 'ngrok-skip-browser-warning': '1' }

const MENSAGENS_PADRAO: Record<number, string> = {
  400: 'Confira os dados informados.',
  401: 'Sua sessão expirou. Entre de novo.',
  404: 'Não encontrado.',
  429: 'Muitas tentativas. Aguarde um minuto e tente de novo.',
}

interface OpcoesRequisicao {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  /** false para login/cadastro: não envia o token e um 401 não encerra a sessão. */
  autenticada?: boolean
}

export async function api<T>(caminho: string, opcoes: OpcoesRequisicao = {}): Promise<T> {
  const { method = 'GET', body, autenticada = true } = opcoes
  const headers: Record<string, string> = { ...CABECALHOS_PADRAO, Accept: 'application/json' }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }
  const token = autenticada ? lerToken() : null
  if (token) {
    headers.Authorization = `Bearer ${token}`
  }

  let resposta: Response
  try {
    resposta = await fetch(caminho, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, 'Sem conexão com o servidor. Verifique sua internet e tente de novo.')
  }

  if (resposta.ok) {
    return resposta.status === 204 ? (undefined as T) : ((await resposta.json()) as T)
  }

  if (resposta.status === 401 && autenticada) {
    aoPerderSessao?.()
  }

  const problema = await lerProblema(resposta)
  throw new ApiError(
    resposta.status,
    // `detail` traz as mensagens escritas pela API; `title` é o texto padrão do ASP.NET, em inglês.
    problema.detail ?? MENSAGENS_PADRAO[resposta.status] ?? 'Algo deu errado no servidor. Tente de novo.',
    problema.errors ?? {},
  )
}

/** Monta o ApiError de uma resposta de erro já lida (usado também pelo upload, que não passa pelo fetch). */
export function erroDaResposta(status: number, problema: { detail?: string; errors?: Record<string, string[]> }): ApiError {
  return new ApiError(
    status,
    problema.detail ?? MENSAGENS_PADRAO[status] ?? 'Algo deu errado no servidor. Tente de novo.',
    problema.errors ?? {},
  )
}

/** GET autenticado de um arquivo. As imagens da mesa exigem o token, então não servem direto em um <img src>. */
export async function apiBlob(caminho: string): Promise<Blob> {
  const token = lerToken()
  let resposta: Response
  try {
    resposta = await fetch(caminho, {
      headers: { ...CABECALHOS_PADRAO, ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    })
  } catch {
    throw new ApiError(0, 'Sem conexão com o servidor. Verifique sua internet e tente de novo.')
  }

  if (resposta.ok) {
    return resposta.blob()
  }
  if (resposta.status === 401) {
    aoPerderSessao?.()
  }
  throw erroDaResposta(resposta.status, await lerProblema(resposta))
}

async function lerProblema(resposta: Response): Promise<{ detail?: string; errors?: Record<string, string[]> }> {
  try {
    return await resposta.json()
  } catch {
    return {}
  }
}
