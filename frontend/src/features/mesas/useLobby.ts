import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useRef, useState } from 'react'
import { CABECALHOS_PADRAO, perderSessao } from '../../shared/api/http'
import { lerToken } from '../../shared/api/tokenStorage'
import type { StatusDaConexao } from '../../shared/realtime/StatusConexao'
import type { Iniciativa } from '../iniciativa/iniciativaApi'
import type { MapaEstado } from '../mapa/mapaApi'
import { CHAVE_MESAS, chaveDaMesa, type Membro, type MesaDetalhe, type ModoDeMovimentacao, type PersonagemResumo } from './mesasApi'

export type MotivoDoEncerramento = 'removido' | 'saiu' | 'excluida' | 'sem-acesso'

interface Lobby {
  mesa: MesaDetalhe
  usuariosOnline: string[]
  mapaAtivo: MapaEstado | null
  iniciativa: Iniciativa
}

/**
 * Permite a outra parte da tela (o mapa) usar a mesma conexão do lobby: registra os próprios
 * handlers antes do EntrarNaMesa, para não perder eventos, e recebe o estado a cada sincronização.
 */
export interface ExtensaoDoLobby {
  registrar: (conexao: HubConnection) => void
  aoSincronizar: (lobby: Pick<Lobby, 'mapaAtivo' | 'iniciativa'>) => void
}

interface OpcoesDoLobby {
  mesaId: string
  meuUsuarioId: string
  /** Chamado quando o usuário deixa de ter lugar no lobby (removido, mesa excluída...). */
  aoEncerrar: (motivo: MotivoDoEncerramento) => void
  /** Deve ser estável (useMemo): só é lida quando a conexão é criada. */
  extensao?: ExtensaoDoLobby
}

/**
 * Esperas entre as tentativas de reconexão. Esgotada a sequência, a tela passa a dizer
 * "desconectado", mas as tentativas continuam, de 20 em 20 segundos.
 */
const ESPERAS_MS = [0, 2000, 5000, 10_000, 20_000]

/** Tempo em segundo plano a partir do qual, ao voltar, o estado é ressincronizado por garantia. */
const AUSENCIA_QUE_RESSINCRONIZA_MS = 15_000

/** Prazo para o servidor responder quando conferimos se a conexão ainda está viva. */
const PRAZO_DA_CONFERENCIA_MS = 4000

/**
 * Mantém o lobby da mesa sincronizado em tempo real. O estado da mesa vive no cache do
 * TanStack Query (mesma chave do GET /api/mesas/{id}); os eventos do Hub apenas o atualizam.
 * Ao conectar, a cada reconexão e ao voltar de um tempo em segundo plano, EntrarNaMesa devolve
 * o estado completo, que substitui o cache.
 */
export function useLobby({ mesaId, meuUsuarioId, aoEncerrar, extensao }: OpcoesDoLobby) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<StatusDaConexao>('conectando')
  const [online, setOnline] = useState<ReadonlySet<string>>(new Set())
  const aoEncerrarRef = useRef(aoEncerrar)
  aoEncerrarRef.current = aoEncerrar
  const reconectarRef = useRef<() => void>(() => undefined)

  useEffect(() => {
    let descartado = false
    let tentando = false
    const chave = chaveDaMesa(mesaId)

    const conexao = new HubConnectionBuilder()
      .withUrl('/hubs/mesa', { accessTokenFactory: () => lerToken() ?? '', headers: CABECALHOS_PADRAO })
      // Queda de rede: o próprio SignalR tenta voltar nessa sequência. Se ela se esgotar, a
      // conexão fecha e o laço de `conectar`, abaixo, continua tentando.
      .withAutomaticReconnect(ESPERAS_MS)
      .configureLogging(LogLevel.Warning)
      .build()
    // Sinal de vida a cada 5 s nos dois sentidos (o servidor usa o mesmo intervalo): uma conexão
    // que morreu sem aviso é percebida em até 15 s, em vez dos 30 s do padrão.
    conexao.keepAliveIntervalInMilliseconds = 5000
    conexao.serverTimeoutInMilliseconds = 15_000

    const atualizarMesa = (alterar: (mesa: MesaDetalhe) => MesaDetalhe) =>
      queryClient.setQueryData<MesaDetalhe>(chave, (mesa) => (mesa ? alterar(mesa) : mesa))

    const marcarOnline = (usuarioId: string, estaOnline: boolean) =>
      setOnline((atual) => {
        const proximo = new Set(atual)
        if (estaOnline) proximo.add(usuarioId)
        else proximo.delete(usuarioId)
        return proximo
      })

    const encerrar = (motivo: MotivoDoEncerramento) => {
      if (descartado) return
      descartado = true
      void conexao.stop()
      queryClient.removeQueries({ queryKey: chave })
      void queryClient.invalidateQueries({ queryKey: CHAVE_MESAS })
      aoEncerrarRef.current(motivo)
    }

    // Todos os eventos trazem o id da mesa; ignoramos o que não for desta.
    const daMesa =
      <Args extends unknown[]>(tratar: (...args: Args) => void) =>
      (id: string, ...args: Args) => {
        if (id === mesaId && !descartado) tratar(...args)
      }

    conexao.on(
      'MembroEntrou',
      daMesa((membro: Membro) =>
        atualizarMesa((mesa) =>
          mesa.membros.some((m) => m.usuarioId === membro.usuarioId)
            ? mesa
            : { ...mesa, membros: [...mesa.membros, membro] },
        ),
      ),
    )
    conexao.on(
      'MembroSaiu',
      daMesa((usuarioId: string) => {
        if (usuarioId === meuUsuarioId) {
          // Saí por outra aba: esta também deixa o lobby.
          encerrar('saiu')
          return
        }
        atualizarMesa((mesa) => ({ ...mesa, membros: mesa.membros.filter((m) => m.usuarioId !== usuarioId) }))
        marcarOnline(usuarioId, false)
      }),
    )
    conexao.on('MembroOnline', daMesa((usuarioId: string) => marcarOnline(usuarioId, true)))
    conexao.on('MembroOffline', daMesa((usuarioId: string) => marcarOnline(usuarioId, false)))
    conexao.on(
      'PersonagemAtivoAlterado',
      daMesa((usuarioId: string, personagem: PersonagemResumo | null) =>
        atualizarMesa((mesa) => ({
          ...mesa,
          membros: mesa.membros.map((m) => (m.usuarioId === usuarioId ? { ...m, personagemAtivo: personagem } : m)),
        })),
      ),
    )
    conexao.on('SessaoAlterada', daMesa((ativa: boolean) => atualizarMesa((mesa) => ({ ...mesa, sessaoAtiva: ativa }))))
    conexao.on(
      'MovimentacaoAlterada',
      daMesa((movimentacao: ModoDeMovimentacao) => atualizarMesa((mesa) => ({ ...mesa, movimentacao }))),
    )
    conexao.on('MesaExcluida', daMesa(() => encerrar('excluida')))
    conexao.on('RemovidoDaMesa', daMesa(() => encerrar('removido')))
    extensao?.registrar(conexao)

    async function sincronizar() {
      const lobby = await conexao.invoke<Lobby>('EntrarNaMesa', mesaId)
      if (descartado) return
      queryClient.setQueryData(chave, lobby.mesa)
      setOnline(new Set(lobby.usuariosOnline))
      extensao?.aoSincronizar(lobby)
      setStatus('conectado')
    }

    /** Interrompe a espera entre duas tentativas, se houver uma em curso. */
    let acordar: (() => void) | null = null
    const esperar = (ms: number) =>
      new Promise<void>((resolver) => {
        const temporizador = setTimeout(resolver, ms)
        acordar = () => {
          clearTimeout(temporizador)
          resolver()
        }
      }).finally(() => {
        acordar = null
      })

    /** Conecta e sincroniza, insistindo até conseguir ou até o servidor negar o acesso. */
    async function conectar() {
      if (tentando) return
      tentando = true
      try {
        await tentarAteConseguir()
      } finally {
        tentando = false
      }
    }

    async function tentarAteConseguir() {
      let tentativa = 0
      while (!descartado) {
        try {
          if (conexao.state === HubConnectionState.Disconnected) {
            await conexao.start()
          }
          await sincronizar()
          return
        } catch (erro) {
          if (descartado) return
          const mensagem = erro instanceof Error ? erro.message : ''
          // HubException do servidor: a mesa não existe mais ou não sou mais membro.
          if (mensagem.includes('Mesa não encontrada')) {
            encerrar('sem-acesso')
            return
          }
          // O servidor recusou o token (venceu enquanto estávamos fora): só entrando de novo.
          if (mensagem.includes('401') || mensagem.includes('Unauthorized')) {
            perderSessao()
            return
          }
          tentativa++
          setStatus(tentativa >= ESPERAS_MS.length ? 'desconectado' : 'reconectando')
          await esperar(ESPERAS_MS[Math.min(tentativa, ESPERAS_MS.length - 1)])
        }
      }
    }

    /** Tenta voltar já, sem esperar a próxima tentativa agendada. */
    const tentarAgora = () => {
      if (descartado || conexao.state === HubConnectionState.Connected) return
      if (acordar) acordar()
      else if (conexao.state === HubConnectionState.Disconnected) void conectar()
      // No meio da reconexão automática do SignalR: fechar faz o laço de `conectar` assumir.
      else void conexao.stop()
    }
    reconectarRef.current = tentarAgora

    conexao.onreconnecting(() => {
      if (!descartado) setStatus('reconectando')
    })
    conexao.onreconnected(() => {
      // A reconexão cria uma conexão nova no servidor: é preciso entrar nos grupos de novo, e
      // o estado devolvido substitui o local.
      void conectar()
    })
    conexao.onclose(() => {
      // As tentativas automáticas se esgotaram (ou a conferência fechou uma conexão morta).
      if (!descartado) {
        setStatus('desconectado')
        void conectar()
      }
    })

    /**
     * Confere se a conexão está viva de verdade pedindo o estado ao servidor, com prazo curto.
     * Se ele responde, o estado local já sai atualizado; se não, a conexão é fechada e o laço de
     * `conectar` assume. Os avisos "online" e "offline" do navegador só dizem se há alguma rede,
     * não se o servidor responde (em casa, ele pode estar na própria máquina), então servem de
     * gatilho para esta conferência, e não de resposta.
     */
    const conferirConexao = () => {
      if (descartado) return
      if (conexao.state !== HubConnectionState.Connected) {
        tentarAgora()
        return
      }
      let respondeu = false
      const prazo = setTimeout(() => {
        if (!respondeu && !descartado) void conexao.stop()
      }, PRAZO_DA_CONFERENCIA_MS)
      sincronizar()
        .then(() => {
          respondeu = true
        })
        .catch((erro: unknown) => {
          // O limite de chamadas do Hub prova que o servidor está respondendo.
          respondeu = erro instanceof Error && erro.message.includes('Muitas chamadas')
          if (!respondeu && !descartado) void conexao.stop()
        })
        .finally(() => clearTimeout(prazo))
    }

    // Aba em segundo plano ou aparelho que dormiu: os temporizadores param e a conexão pode ter
    // morrido sem aviso. Ao voltar, confere a conexão e busca o estado de novo.
    let ocultoDesde: number | null = null
    const aoMudarVisibilidade = () => {
      if (document.visibilityState === 'hidden') {
        ocultoDesde = Date.now()
        return
      }
      const ausencia = ocultoDesde === null ? 0 : Date.now() - ocultoDesde
      ocultoDesde = null
      if (conexao.state !== HubConnectionState.Connected || ausencia > AUSENCIA_QUE_RESSINCRONIZA_MS) {
        conferirConexao()
      }
    }
    document.addEventListener('visibilitychange', aoMudarVisibilidade)
    window.addEventListener('online', conferirConexao)
    window.addEventListener('offline', conferirConexao)

    setStatus('conectando')
    void conectar()

    return () => {
      descartado = true
      acordar?.()
      document.removeEventListener('visibilitychange', aoMudarVisibilidade)
      window.removeEventListener('online', conferirConexao)
      window.removeEventListener('offline', conferirConexao)
      void conexao.stop()
    }
  }, [mesaId, meuUsuarioId, queryClient, extensao])

  /** Para o botão "Tentar agora": não espera a próxima tentativa agendada. */
  const reconectar = useCallback(() => reconectarRef.current(), [])

  return { status, online, reconectar }
}
