import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// Em desenvolvimento, o Vite repassa /api, /hubs (SignalR, com WebSocket) e /health para a API.
// Padrão: API do Docker Compose (porta 8080). Para `dotnet run`, use API_PROXY_TARGET=http://localhost:5080.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, '.', '')
  const alvo = env.API_PROXY_TARGET || 'http://localhost:8080'

  return {
    plugins: [react()],
    server: {
      port: 5173,
      proxy: {
        '/api': { target: alvo, changeOrigin: true },
        '/hubs': { target: alvo, changeOrigin: true, ws: true },
        '/health': { target: alvo, changeOrigin: true },
      },
    },
  }
})
