import { useEffect, useMemo, useRef } from 'react'
import { useEditorStore } from './editorStore'
import type { Token } from './mapaApi'
import { useMapaStore } from './mapaStore'
import { acrescenta, calcularNevoa, celulasOpacas, marcarVisao, paraBase64 } from './nevoa'

/** Quanto esperar o token parar antes de informar o que ele viu: um movimento rápido vira um envio só. */
const ESPERA_DO_ENVIO_MS = 150

/** Visões guardadas por posição; a cena raramente tem mais tokens com visão que isto. */
const LIMITE_DO_CACHE = 256

interface OpcoesDaNevoa {
  meuUsuarioId: string
  souMestre: boolean
  /** Informa ao servidor as células que os tokens deste jogador veem agora (base64). */
  aoExplorar: (mapaId: string, celulas: string) => void
}

/**
 * Calcula a névoa desta tela e a deixa no store, onde a cena, os tokens e o picking a leem.
 * O jogador vê pelo modo do mapa; o Mestre vê tudo, com a névoa do grupo só sombreada, ou "como"
 * o grupo ou um jogador. Para o jogador, informa também ao servidor o que os tokens dele veem e
 * ainda não está na memória.
 */
export function useNevoa({ meuUsuarioId, souMestre, aoExplorar }: OpcoesDaNevoa) {
  const mapa = useMapaStore((estado) => estado.mapa)
  const tokens = useMapaStore((estado) => estado.tokens)
  const estruturas = useMapaStore((estado) => estado.estruturas)
  const memorias = useMapaStore((estado) => estado.memorias)
  // Editando o mapa ou pintando a névoa, o Mestre precisa ver tudo: o "ver como" só vale no modo Jogar.
  const verComo = useMapaStore((estado) => estado.verComo)
  const jogando = useEditorStore((estado) => estado.modo === 'jogar')
  const comoQuem = souMestre && !jogando ? undefined : verComo
  const aoExplorarRef = useRef(aoExplorar)
  aoExplorarRef.current = aoExplorar

  const largura = mapa?.largura ?? 0
  const altura = mapa?.altura ?? 0
  const modo = mapa?.nevoa ?? 'Desligada'
  const ligada = mapa !== null && modo !== 'Desligada'

  const opacas = useMemo(
    () => (ligada ? celulasOpacas(largura, altura, estruturas) : null),
    [ligada, largura, altura, estruturas],
  )
  // A visão de um token só depende da posição, do alcance e das paredes: quem não se moveu não é recalculado.
  const cache = useMemo(() => new Map<string, Uint8Array>(), [opacas])

  // HP e outros campos mudam os tokens sem mudar o que eles veem; a névoa só é refeita quando isto muda.
  const chaveDasFontes = useMemo(
    () =>
      Object.values(tokens)
        .map((t) => `${t.id}:${t.x},${t.y}:${t.visao}:${t.visivel ? 1 : 0}:${t.donoUsuarioId ?? ''}`)
        .sort()
        .join('|'),
    [tokens],
  )
  const tokensRef = useRef(tokens)
  tokensRef.current = tokens

  const nevoa = useMemo(() => {
    if (!ligada || !opacas) return null
    const visaoDe = (token: Token) => {
      const chave = `${token.x},${token.y},${token.visao}`
      let vistas = cache.get(chave)
      if (!vistas) {
        vistas = new Uint8Array(largura * altura)
        marcarVisao(largura, altura, opacas, token.x, token.y, token.visao, vistas)
        if (cache.size >= LIMITE_DO_CACHE) cache.clear()
        cache.set(chave, vistas)
      }
      return vistas
    }
    return calcularNevoa({
      largura,
      altura,
      modo,
      tokens: tokensRef.current,
      opacas,
      memorias,
      observador: souMestre ? (comoQuem ?? null) : meuUsuarioId,
      translucida: souMestre && comoQuem === undefined,
      visaoDe,
    })
    // chaveDasFontes resume os tokens que importam; tokensRef traz a versão atual deles.
  }, [ligada, opacas, cache, largura, altura, modo, chaveDasFontes, memorias, souMestre, comoQuem, meuUsuarioId])

  useEffect(() => {
    useMapaStore.getState().definirNevoa(nevoa)
  }, [nevoa])
  useEffect(() => () => useMapaStore.getState().definirNevoa(null), [])

  const mapaId = mapa?.id
  const ativo = mapa?.ativo ?? false
  useEffect(() => {
    if (!nevoa || souMestre || !ativo || !mapaId || !acrescenta(nevoa.vistas, nevoa.memoria)) return
    const espera = window.setTimeout(() => aoExplorarRef.current(mapaId, paraBase64(nevoa.vistas)), ESPERA_DO_ENVIO_MS)
    return () => window.clearTimeout(espera)
  }, [nevoa, souMestre, ativo, mapaId])
}
