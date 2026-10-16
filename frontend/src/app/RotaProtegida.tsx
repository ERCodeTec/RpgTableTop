import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../features/auth/AuthContext'

/** Leva ao login quem não está autenticado, guardando o destino para voltar depois. */
export function RotaProtegida() {
  const { usuario, carregando } = useAuth()
  const location = useLocation()

  if (carregando) {
    return <p className="estado estado-pagina">Carregando…</p>
  }

  if (!usuario) {
    return <Navigate to="/login" replace state={{ de: location.pathname + location.search }} />
  }

  return <Outlet />
}
