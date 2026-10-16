import { useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation } from 'react-router-dom'
import { ApiError } from '../../shared/api/http'
import { Campo } from '../../shared/ui/Campo'
import { useAuth } from './AuthContext'

export function CadastroPage() {
  const { usuario, cadastrar } = useAuth()
  const location = useLocation()
  const [nomeExibicao, setNomeExibicao] = useState('')
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
      await cadastrar(email, senha, nomeExibicao)
    } catch (falha) {
      setErro(falha instanceof ApiError ? falha : new ApiError(0, 'Não foi possível criar a conta. Tente de novo.'))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <main className="pagina-auth">
      <form className="painel painel-auth" onSubmit={aoEnviar} noValidate>
        <h1>Criar conta</h1>
        {erro && Object.keys(erro.erros).length === 0 && (
          <p className="aviso-erro" role="alert">
            {erro.message}
          </p>
        )}
        <Campo
          rotulo="Nome de exibição"
          dica="É como os outros jogadores vão ver você."
          autoComplete="nickname"
          maxLength={40}
          value={nomeExibicao}
          onChange={(evento) => setNomeExibicao(evento.target.value)}
          erros={erro?.erros.nomeExibicao}
          required
        />
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
          dica="Pelo menos 8 caracteres, com letras e números."
          type="password"
          autoComplete="new-password"
          value={senha}
          onChange={(evento) => setSenha(evento.target.value)}
          erros={erro?.erros.senha}
          required
        />
        <button className="botao botao-primario" type="submit" disabled={enviando}>
          {enviando ? 'Criando conta…' : 'Criar conta'}
        </button>
        <p className="painel-rodape">
          Já tem conta? <Link to="/login" state={location.state}>Entrar</Link>
        </p>
      </form>
    </main>
  )
}
