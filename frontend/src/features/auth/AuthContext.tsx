import { useQueryClient } from '@tanstack/react-query'
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { definirAoPerderSessao } from '../../shared/api/http'
import { lerToken, lerValidadeDoToken, limparToken, salvarToken } from '../../shared/api/tokenStorage'
import {
  entrar as entrarNaApi,
  obterUsuarioAtual,
  registrar as registrarNaApi,
  renovar as renovarNaApi,
  type Usuario,
} from './authApi'

interface AuthContextValue {
  usuario: Usuario | null
  /** true enquanto um token salvo ainda está sendo conferido com a API. */
  carregando: boolean
  entrar: (email: string, senha: string) => Promise<void>
  cadastrar: (email: string, senha: string, nomeExibicao: string) => Promise<void>
  sair: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [usuario, setUsuario] = useState<Usuario | null>(null)
  const [carregando, setCarregando] = useState(() => lerToken() !== null)

  const sair = useCallback(() => {
    limparToken()
    setUsuario(null)
    queryClient.clear()
  }, [queryClient])

  // Um 401 em qualquer requisição autenticada encerra a sessão; as rotas protegidas levam ao login.
  useEffect(() => {
    definirAoPerderSessao(sair)
    return () => definirAoPerderSessao(null)
  }, [sair])

  useEffect(() => {
    if (lerToken() === null) {
      return
    }
    let cancelado = false
    obterUsuarioAtual()
      .then((atual) => {
        if (!cancelado) setUsuario(atual)
      })
      .catch(() => {
        // 401 já encerrou a sessão; em falha de rede o usuário cai no login e pode tentar de novo.
      })
      .finally(() => {
        if (!cancelado) setCarregando(false)
      })
    return () => {
      cancelado = true
    }
  }, [])

  // O login é renovado em silêncio enquanto a pessoa usa o sistema: passado dois terços da
  // validade, o token é trocado por um novo. Conferimos a cada minuto e quando a aba volta ao
  // foco (o temporizador não roda com o aparelho dormindo). Quem ficou fora além da validade
  // vai para o login, que devolve a pessoa à página em que estava.
  const logado = usuario !== null
  useEffect(() => {
    if (!logado) return
    let renovando = false

    async function conferir() {
      const validade = lerValidadeDoToken()
      if (!validade || renovando) return
      const agora = Date.now()
      if (agora >= validade.expiraEm) {
        sair()
        return
      }
      if (agora < validade.emitidoEm + ((validade.expiraEm - validade.emitidoEm) * 2) / 3) return

      renovando = true
      try {
        salvarToken((await renovarNaApi()).token)
      } catch {
        // 401 já encerrou a sessão; em falha de rede, a próxima conferência tenta de novo.
      } finally {
        renovando = false
      }
    }

    const aoVoltar = () => {
      if (document.visibilityState === 'visible') void conferir()
    }
    void conferir()
    const intervalo = setInterval(() => void conferir(), 60_000)
    document.addEventListener('visibilitychange', aoVoltar)
    window.addEventListener('online', aoVoltar)
    return () => {
      clearInterval(intervalo)
      document.removeEventListener('visibilitychange', aoVoltar)
      window.removeEventListener('online', aoVoltar)
    }
  }, [logado, sair])

  const entrar = useCallback(async (email: string, senha: string) => {
    const resposta = await entrarNaApi({ email, senha })
    salvarToken(resposta.token)
    setUsuario(resposta.usuario)
  }, [])

  const cadastrar = useCallback(async (email: string, senha: string, nomeExibicao: string) => {
    const resposta = await registrarNaApi({ email, senha, nomeExibicao })
    salvarToken(resposta.token)
    setUsuario(resposta.usuario)
  }, [])

  const valor = useMemo(
    () => ({ usuario, carregando, entrar, cadastrar, sair }),
    [usuario, carregando, entrar, cadastrar, sair],
  )

  return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const contexto = useContext(AuthContext)
  if (!contexto) {
    throw new Error('useAuth deve ser usado dentro de AuthProvider')
  }
  return contexto
}
