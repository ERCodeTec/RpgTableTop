import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from '../features/auth/AuthContext'
import { CadastroPage } from '../features/auth/CadastroPage'
import { LoginPage } from '../features/auth/LoginPage'
import { BibliotecaPage } from '../features/biblioteca/BibliotecaPage'
import { GuiaDeTexturasPage } from '../features/guia/GuiaDeTexturasPage'
import { ConvitePage } from '../features/mesas/ConvitePage'
import { MesaPage } from '../features/mesas/MesaPage'
import { MinhasMesasPage } from '../features/mesas/MinhasMesasPage'
import { PersonagensPage } from '../features/personagens/PersonagensPage'
import { ApiError } from '../shared/api/http'
import { Layout } from './Layout'
import { RotaProtegida } from './RotaProtegida'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      // Erros 4xx não melhoram tentando de novo.
      retry: (tentativas, erro) => !(erro instanceof ApiError && erro.status >= 400 && erro.status < 500) && tentativas < 2,
    },
  },
})

function Inicio() {
  const { usuario, carregando } = useAuth()
  if (carregando) {
    return <p className="estado estado-pagina">Carregando…</p>
  }
  return <Navigate to={usuario ? '/mesas' : '/login'} replace />
}

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <Routes>
            <Route path="/" element={<Inicio />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/cadastro" element={<CadastroPage />} />
            <Route element={<RotaProtegida />}>
              <Route element={<Layout />}>
                <Route path="/mesas" element={<MinhasMesasPage />} />
                <Route path="/mesas/:id" element={<MesaPage />} />
                <Route path="/convite/:codigo" element={<ConvitePage />} />
                <Route path="/personagens" element={<PersonagensPage />} />
                <Route path="/biblioteca" element={<BibliotecaPage />} />
                <Route path="/guia/texturas" element={<GuiaDeTexturasPage />} />
              </Route>
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
