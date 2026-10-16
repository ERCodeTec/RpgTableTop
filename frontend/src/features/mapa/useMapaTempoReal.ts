import type { HubConnection } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useRef } from 'react'
import type { Iniciativa } from '../iniciativa/iniciativaApi'
import { useIniciativaStore } from '../iniciativa/iniciativaStore'
import type { ExtensaoDoLobby } from '../mesas/useLobby'
import { chaveDaCelula } from './geometria'
import {
  chaveDosMapas,
  obterEstadoDoMapa,
  type Estrutura,
  type HpDoToken,
  type Mapa,
  type MapaEstado,
  type MemoriaDeNevoa,
  type MovimentoDoToken,
  type MovimentoResultado,
  type Token,
} from './mapaApi'
import { alcanceAtual, useMapaStore } from './mapaStore'

/** O movimento por blocos e a rodada, pelo Hub. Os erros viram aviso; nada aqui lança. */
export interface AcoesDeMovimento {
  /** Anda até uma célula do alcance, descontando o custo; fica pendente até concluir ou desfazer. */
  moverPorBlocos: (tokenId: string, x: number, y: number) => Promise<void>
  desfazer: (tokenId: string) => Promise<void>
  concluir: (tokenId: string) => Promise<void>
  /** Disparada: soma mais um deslocamento ao movimento restante, uma vez por rodada. */
  disparar: (tokenId: string) => Promise<void>
  /** Volta atrás na Disparada, enquanto o movimento extra não foi gasto. */
  desfazerDisparada: (tokenId: string) => Promise<void>
  /** Só o Mestre: restaura o movimento de todos os tokens do mapa. */
  novaRodada: (mapaId: string) => Promise<void>
  /** Só o Mestre: restaura o movimento de um token. */
  restaurar: (tokenId: string) => Promise<void>
}

/** O motivo dado pelo servidor numa HubException (o cliente o prefixa com o método), ou o padrão. */
function mensagemDoServidor(erro: unknown, padrao: string) {
  const mensagem = erro instanceof Error ? erro.message : ''
  const inicio = mensagem.indexOf('HubException: ')
  return inicio >= 0 ? mensagem.slice(inicio + 'HubException: '.length) : padrao
}

interface OpcoesDoMapaTempoReal {
  mesaId: string
  /** Mostra um aviso curto (movimento rejeitado, falha de conexão). */
  aoAvisar: (mensagem: string) => void
}

/**
 * Mantém o mapa sincronizado pela mesma conexão do lobby. Os eventos do Hub alimentam o store;
 * a cada (re)conexão o estado local é trocado por inteiro pelo que o EntrarNaMesa devolve.
 */
export function useMapaTempoReal({ mesaId, aoAvisar }: OpcoesDoMapaTempoReal) {
  const queryClient = useQueryClient()
  const conexaoRef = useRef<HubConnection | null>(null)
  const aoAvisarRef = useRef(aoAvisar)
  aoAvisarRef.current = aoAvisar

  const extensao = useMemo<ExtensaoDoLobby>(() => {
    const store = useMapaStore.getState
    const recarregarLista = () => void queryClient.invalidateQueries({ queryKey: chaveDosMapas(mesaId) })

    // Todos os eventos trazem o id da mesa; ignoramos o que não for desta.
    const daMesa =
      <Args extends unknown[]>(tratar: (...args: Args) => void) =>
      (id: string, ...args: Args) => {
        if (id === mesaId) tratar(...args)
      }

    /** O Mestre pode estar com um mapa em preparação na tela, diferente do ativo. */
    const emPreparacao = (novoAtivoId: string | null) => {
      const { mapa, mapaAtivoId } = store()
      return mapa !== null && mapa.id !== mapaAtivoId && mapa.id !== novoAtivoId
    }

    return {
      registrar(conexao) {
        conexaoRef.current = conexao
        conexao.on('TokenCriado', daMesa((token: Token) => store().aplicarToken(token)))
        conexao.on('TokenAtualizado', daMesa((token: Token) => store().aplicarToken(token)))
        conexao.on(
          'TokenMovido',
          daMesa((mapaId: string, tokenId: string, x: number, y: number, versao: number) =>
            store().tokenMovido(mapaId, tokenId, x, y, versao),
          ),
        )
        conexao.on(
          'TokenRemovido',
          daMesa((mapaId: string, tokenId: string) => store().tokenRemovido(mapaId, tokenId)),
        )
        conexao.on('HpAlterado', daMesa((mapaId: string, hp: HpDoToken) => store().hpAlterado(mapaId, hp)))
        conexao.on(
          'MovimentoAlterado',
          daMesa((mapaId: string, movimento: MovimentoDoToken) => store().movimentoAlterado(mapaId, movimento)),
        )
        conexao.on('EstruturaAdicionada', daMesa((estrutura: Estrutura) => store().aplicarEstrutura(estrutura)))
        conexao.on('EstruturaAtualizada', daMesa((estrutura: Estrutura) => store().aplicarEstrutura(estrutura)))
        conexao.on(
          'EstruturaRemovida',
          daMesa((mapaId: string, estruturaId: string) => store().estruturaRemovida(mapaId, estruturaId)),
        )
        conexao.on('MemoriaDeNevoaAlterada', daMesa((memoria: MemoriaDeNevoa) => store().memoriaAlterada(memoria)))
        conexao.on('IniciativaAlterada', daMesa((iniciativa: Iniciativa) => useIniciativaStore.getState().definir(iniciativa)))
        conexao.on(
          'MapaAtivoAlterado',
          daMesa((estado: MapaEstado | null) => {
            const manterOQueEstaNaTela = estado === null && emPreparacao(null)
            store().definirMapaAtivo(estado?.mapa.id ?? null)
            if (!manterOQueEstaNaTela) store().exibir(estado)
            recarregarLista()
          }),
        )
        conexao.on(
          'MapaAtualizado',
          daMesa((mapa: Mapa) => {
            store().mapaAtualizado(mapa)
            recarregarLista()
          }),
        )
      },

      aoSincronizar({ mapaAtivo, iniciativa }) {
        useIniciativaStore.getState().definir(iniciativa)
        const mapaEmPreparacao = emPreparacao(mapaAtivo?.mapa.id ?? null) ? store().mapa : null
        store().definirMapaAtivo(mapaAtivo?.mapa.id ?? null)
        recarregarLista()
        if (!mapaEmPreparacao) {
          store().exibir(mapaAtivo)
          return
        }

        // O EntrarNaMesa só traz o mapa ativo; o que está em preparação é recarregado pela API.
        obterEstadoDoMapa(mapaEmPreparacao.id)
          .then((estado) => {
            if (store().mapa?.id === estado.mapa.id) store().exibir(estado)
          })
          .catch(() => store().exibir(mapaAtivo))
      },
    }
  }, [mesaId, queryClient])

  useEffect(
    () => () => {
      useMapaStore.getState().limpar()
      useIniciativaStore.getState().definir(null)
    },
    [mesaId],
  )

  /**
   * Move na tela na hora e pede ao servidor. Se ele recusar, o token volta para onde o servidor
   * diz que está e o motivo aparece em um aviso.
   */
  const moverToken = useCallback(async (tokenId: string, x: number, y: number) => {
    const store = useMapaStore.getState
    const token = store().tokens[tokenId]
    const conexao = conexaoRef.current
    if (!token || !conexao) return

    const desfazer = () => store().moverLocal(tokenId, token.x, token.y)
    store().moverLocal(tokenId, x, y)
    try {
      const resultado = await conexao.invoke<MovimentoResultado>('MoverToken', tokenId, x, y, token.versao)
      if (resultado.aceito) {
        if (resultado.token) store().aplicarToken(resultado.token)
        return
      }

      if (resultado.token) store().aplicarToken(resultado.token, true)
      else desfazer()
      aoAvisarRef.current(resultado.motivo ?? 'O servidor recusou o movimento.')
    } catch (erro) {
      if (erro instanceof Error && erro.message.includes('Token não encontrado')) {
        store().tokenRemovido(token.mapaId, tokenId)
        aoAvisarRef.current('Este token não está mais no mapa.')
        return
      }
      desfazer()
      aoAvisarRef.current('Sem conexão com o servidor. O movimento não foi enviado.')
    }
  }, [])

  /** Aplica a resposta de um método de movimento; na rejeição, corrige o token e avisa o motivo. */
  const aplicarResultado = useCallback((resultado: MovimentoResultado, voltar: () => void) => {
    const store = useMapaStore.getState
    if (resultado.aceito) {
      if (resultado.token) store().aplicarToken(resultado.token)
      return
    }
    if (resultado.token) store().aplicarToken(resultado.token, true)
    else voltar()
    aoAvisarRef.current(resultado.motivo ?? 'O servidor recusou o movimento.')
  }, [])

  /** Falha do Hub: token que sumiu sai da tela; o resto é falta de conexão (ou o motivo que o servidor deu). */
  const tratarFalha = useCallback((erro: unknown, token: Token, voltar: () => void) => {
    if (erro instanceof Error && erro.message.includes('Token não encontrado')) {
      useMapaStore.getState().tokenRemovido(token.mapaId, token.id)
      aoAvisarRef.current('Este token não está mais no mapa.')
      return
    }
    voltar()
    aoAvisarRef.current('Sem conexão com o servidor. O movimento não foi enviado.')
  }, [])

  const acoesDeMovimento = useMemo<AcoesDeMovimento>(() => {
    const store = useMapaStore.getState

    /** Desfazer, concluir e disparar: sem mudança otimista, a tela segue a resposta do servidor. */
    const invocar = async (
      metodo: 'DesfazerMovimento' | 'ConcluirMovimento' | 'Disparar' | 'DesfazerDisparada',
      tokenId: string,
    ) => {
      const token = store().tokens[tokenId]
      const conexao = conexaoRef.current
      if (!token || !conexao) return
      try {
        aplicarResultado(await conexao.invoke<MovimentoResultado>(metodo, tokenId, token.versao), () => undefined)
      } catch (erro) {
        tratarFalha(erro, token, () => undefined)
      }
    }

    return {
      async moverPorBlocos(tokenId, x, y) {
        const token = store().tokens[tokenId]
        const conexao = conexaoRef.current
        const custo = alcanceAtual(store())?.get(chaveDaCelula(x, y))
        if (!token || !conexao || custo === undefined || token.movimentoRestante === null) return

        // Na hora, o token anda e o destaque some (o movimento fica pendente); o servidor confirma.
        const voltar = () =>
          store().moverLocal(tokenId, token.x, token.y, {
            movimentoRestante: token.movimentoRestante,
            movimentoPendente: token.movimentoPendente,
          })
        store().moverLocal(tokenId, x, y, { movimentoRestante: token.movimentoRestante - custo, movimentoPendente: true })
        try {
          aplicarResultado(await conexao.invoke<MovimentoResultado>('MoverPorBlocos', tokenId, x, y, token.versao), voltar)
        } catch (erro) {
          tratarFalha(erro, token, voltar)
        }
      },

      desfazer: (tokenId) => invocar('DesfazerMovimento', tokenId),
      concluir: (tokenId) => invocar('ConcluirMovimento', tokenId),
      disparar: (tokenId) => invocar('Disparar', tokenId),
      desfazerDisparada: (tokenId) => invocar('DesfazerDisparada', tokenId),

      async novaRodada(mapaId) {
        try {
          await conexaoRef.current?.invoke('NovaRodada', mapaId)
        } catch (erro) {
          aoAvisarRef.current(mensagemDoServidor(erro, 'Não foi possível começar a nova rodada.'))
        }
      },

      async restaurar(tokenId) {
        try {
          const token = await conexaoRef.current?.invoke<Token>('RestaurarMovimento', tokenId)
          if (token) store().aplicarToken(token)
        } catch (erro) {
          aoAvisarRef.current(mensagemDoServidor(erro, 'Não foi possível restaurar o movimento.'))
        }
      },
    }
  }, [aplicarResultado, tratarFalha])

  /**
   * Informa as células que os tokens deste jogador veem. Uma falha não vale aviso: o cliente manda
   * de novo no próximo passo, e ao reconectar a memória vem inteira do servidor.
   */
  const explorar = useCallback((mapaId: string, celulas: string) => {
    conexaoRef.current?.invoke('ExplorarCelulas', mapaId, celulas).catch(() => undefined)
  }, [])

  return { extensao, moverToken, acoesDeMovimento, explorar }
}
