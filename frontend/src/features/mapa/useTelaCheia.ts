import { useCallback, useEffect, useRef, useState, useSyncExternalStore } from 'react'

/** Classe no <html> enquanto o mapa está em tela cheia: trava a rolagem da página por baixo. */
const CLASSE_DA_PAGINA = 'com-tela-cheia'

/**
 * O mapa em tela cheia. Pede a tela cheia do navegador quando ele permite; onde não permite
 * (Safari no iPhone, por exemplo), o mapa ocupa a janela do mesmo jeito, só sem esconder a
 * interface do navegador. Sair pelo Esc do navegador também desliga o modo.
 */
export function useTelaCheia() {
  const [ativa, setAtiva] = useState(false)
  // true quando a tela cheia do navegador foi concedida: é ela que o Esc do navegador encerra.
  const nativa = useRef(false)

  const sair = useCallback(() => {
    setAtiva(false)
    if (nativa.current && document.fullscreenElement) void document.exitFullscreen().catch(() => undefined)
    nativa.current = false
  }, [])

  const entrar = useCallback(() => {
    setAtiva(true)
    const pagina = document.documentElement
    if (!document.fullscreenElement && pagina.requestFullscreen) {
      pagina
        .requestFullscreen()
        .then(() => {
          nativa.current = true
        })
        .catch(() => undefined)
    }
  }, [])

  const alternar = useCallback(() => (ativa ? sair() : entrar()), [ativa, entrar, sair])

  useEffect(() => {
    const aoMudar = () => {
      if (!document.fullscreenElement && nativa.current) {
        nativa.current = false
        setAtiva(false)
      }
    }
    document.addEventListener('fullscreenchange', aoMudar)
    return () => document.removeEventListener('fullscreenchange', aoMudar)
  }, [])

  useEffect(() => {
    document.documentElement.classList.toggle(CLASSE_DA_PAGINA, ativa)
    return () => document.documentElement.classList.remove(CLASSE_DA_PAGINA)
  }, [ativa])

  // Saindo da tela do mapa, sai também da tela cheia.
  useEffect(
    () => () => {
      if (nativa.current && document.fullscreenElement) void document.exitFullscreen().catch(() => undefined)
    },
    [],
  )

  return { ativa, alternar, sair }
}

/** Mesmo limite de styles.css: abaixo dele o painel do Mestre é uma gaveta. */
const TELA_ESTREITA = '(max-width: 63.99rem)'

function assinarTelaEstreita(aoMudar: () => void) {
  const consulta = window.matchMedia(TELA_ESTREITA)
  consulta.addEventListener('change', aoMudar)
  return () => consulta.removeEventListener('change', aoMudar)
}

export function useTelaEstreita() {
  return useSyncExternalStore(assinarTelaEstreita, () => window.matchMedia(TELA_ESTREITA).matches)
}
