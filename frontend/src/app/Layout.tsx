import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../features/auth/AuthContext'

/** Moldura das páginas autenticadas. */
export function Layout() {
  const { usuario, sair } = useAuth()

  return (
    <>
      <header className="cabecalho">
        <span className="cabecalho-marca">Mesa Virtual</span>
        <nav className="cabecalho-nav" aria-label="Principal">
          <NavLink to="/mesas">Mesas</NavLink>
          <NavLink to="/biblioteca">Biblioteca</NavLink>
          <NavLink to="/personagens">Personagens</NavLink>
          <NavLink to="/guia/texturas">Guia de texturas</NavLink>
        </nav>
        <div className="cabecalho-conta">
          <span className="cabecalho-nome">{usuario?.nomeExibicao}</span>
          <button className="botao botao-discreto" type="button" onClick={sair}>
            Sair
          </button>
        </div>
      </header>
      <Outlet />
    </>
  )
}
