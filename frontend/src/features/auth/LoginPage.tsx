import { useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation } from 'react-router-dom'
import { ApiError } from '../../shared/api/http'
import { Campo } from '../../shared/ui/Campo'
import { useAuth } from './AuthContext'

export function LoginPage() {
  const { usuario, entrar } = useAuth()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [erro, setErro] = useState<ApiError | null>(null)
  const [enviando, setEnviando] = useState(false)

  if (usuario) {
    const destino = (location.state as { de?: string } | null)?.de ?? '/mesas'
    return <Navigate to={destino} replace />
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    setErro(null)
    setEnviando(true)
    try {
      await entrar(email, senha)
    } catch (falha) {
      setErro(falha instanceof ApiError ? falha : new ApiError(0, 'Não foi possível entrar. Tente de novo.'))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <main className="pagina-auth">
      <form className="painel painel-auth" onSubmit={aoEnviar} noValidate>
        <h1>Entrar na mesa</h1>
        {erro && Object.keys(erro.erros).length === 0 && (
          <p className="aviso-erro" role="alert">
            {erro.message}
          </p>
        )}
        <Campo
          rotulo="E-mail"
          type="email"
          autoComplete="email"
          inputMode="email"
          value={email}
          onChange={(evento) => setEmail(evento.target.value)}
          erros={erro?.erros.email}
          required
        />
        <Campo
          rotulo="Senha"
          type="password"
          autoComplete="current-password"
          value={senha}
          onChange={(evento) => setSenha(evento.target.value)}
          erros={erro?.erros.senha}
          required
        />
        <button className="botao botao-primario" type="submit" disabled={enviando}>
          {enviando ? 'Entrando…' : 'Entrar'}
        </button>
        <p className="painel-rodape">
          Ainda não tem conta? <Link to="/cadastro" state={location.state}>Criar conta</Link>
        </p>
      </form>
    </main>
  )
}
