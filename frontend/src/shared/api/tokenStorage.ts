const CHAVE = 'vtt.token'

// O localStorage pode estar indisponível (modo privado, bloqueio do navegador); nesse caso
// o token vive só em memória e o login vale até recarregar a página.
let emMemoria: string | null = null

export function lerToken(): string | null {
  try {
    return localStorage.getItem(CHAVE) ?? emMemoria
  } catch {
    return emMemoria
  }
}

export function salvarToken(token: string): void {
  emMemoria = token
  try {
    localStorage.setItem(CHAVE, token)
  } catch {
    // segue só em memória
  }
}

/**
 * Quando o token foi emitido e quando vence, em milissegundos, lidos do próprio JWT (os campos
 * "iat" e "exp" não são segredo). null se não houver token ou se ele não puder ser lido.
 */
export function lerValidadeDoToken(): { emitidoEm: number; expiraEm: number } | null {
  const token = lerToken()
  const corpo = token?.split('.')[1]
  if (!corpo) return null
  try {
    const dados = JSON.parse(atob(corpo.replace(/-/g, '+').replace(/_/g, '/'))) as { iat?: number; exp?: number }
    return typeof dados.exp === 'number' ? { emitidoEm: (dados.iat ?? dados.exp) * 1000, expiraEm: dados.exp * 1000 } : null
  } catch {
    return null
  }
}

export function limparToken(): void {
  emMemoria = null
  try {
    localStorage.removeItem(CHAVE)
  } catch {
    // nada a limpar
  }
}
