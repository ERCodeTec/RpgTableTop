import { useEffect, useRef } from 'react'
import { desfazer, duplicarNoMapa, girarNoMapa, refazer, removerDoMapa } from './acoesDoEditor'
import { FERRAMENTAS, useEditorStore } from './editorStore'
import { alterarHp, definirExibicao, duplicarToken, type Token } from './mapaApi'
import { useMapaStore } from './mapaStore'
import { useVisaoStore } from './visaoStore'

/** true se o foco está em um campo onde as teclas são texto, ou se há um diálogo aberto. */
function digitando(evento: KeyboardEvent): boolean {
  const alvo = evento.target as HTMLElement | null
  return (
    alvo?.closest('input, textarea, select, [contenteditable="true"]') !== null ||
    document.querySelector('dialog[open]') !== null
  )
}

interface AtalhosDoMapaProps {
  souMestre: boolean
  aoAbrirAjuda: () => void
  aoAlternarTelaCheia: () => void
  aoAvisar: (mensagem: string) => void
}

/**
 * Atalhos de teclado do mapa (a lista completa aparece no diálogo aberto por "?").
 * Q e E giram a visão e F liga a tela cheia, para qualquer usuário. Para o Mestre: no modo Jogar, agem sobre o token
 * selecionado (ocultar, dano e cura, duplicar, remover); no modo Editar, sobre a estrutura.
 */
export function AtalhosDoMapa({ souMestre, aoAbrirAjuda, aoAlternarTelaCheia, aoAvisar }: AtalhosDoMapaProps) {
  const refs = useRef({ aoAbrirAjuda, aoAlternarTelaCheia, aoAvisar })
  refs.current = { aoAbrirAjuda, aoAlternarTelaCheia, aoAvisar }

  useEffect(() => {
    const aplicar = (acao: Promise<Token>) =>
      acao
        .then((token) => useMapaStore.getState().aplicarToken(token))
        .catch((erro: Error) => refs.current.aoAvisar(erro.message || 'Não foi possível concluir a ação.'))

    function noTokenSelecionado(evento: KeyboardEvent, tecla: string, comando: boolean) {
      const { selecionadoId, tokens, selecionar, pedirRemocao } = useMapaStore.getState()
      const token = selecionadoId ? tokens[selecionadoId] : undefined
      if (!token) return
      const deJogador = token.donoUsuarioId !== null

      if (!comando && tecla === 'h') {
        void aplicar(definirExibicao(token.id, { visivel: !token.visivel }))
      } else if (!comando && (tecla === '-' || tecla === '_')) {
        void aplicar(alterarHp(token.id, { delta: evento.shiftKey ? -5 : -1 }))
      } else if (!comando && (tecla === '+' || tecla === '=')) {
        void aplicar(alterarHp(token.id, { delta: evento.shiftKey ? 5 : 1 }))
      } else if (comando && tecla === 'd' && !deJogador) {
        evento.preventDefault()
        void aplicar(duplicarToken(token.id)).then(() => undefined)
      } else if (!comando && (tecla === 'delete' || tecla === 'backspace') && !deJogador) {
        evento.preventDefault()
        pedirRemocao(token.id)
      } else if (tecla === 'escape') {
        selecionar(null)
      }
    }

    function naEstruturaSelecionada(evento: KeyboardEvent, tecla: string, comando: boolean) {
      const editor = useEditorStore.getState()
      if (comando && tecla === 'z') {
        evento.preventDefault()
        void (evento.shiftKey ? refazer() : desfazer())
      } else if (comando && tecla === 'y') {
        evento.preventDefault()
        void refazer()
      } else if (tecla === 'escape') {
        // Primeiro larga a ferramenta de desenho; já selecionando, tira a seleção (da estrutura e do grupo de tokens).
        if (editor.ferramenta !== 'selecionar') editor.definirFerramenta('selecionar')
        else {
          editor.selecionar(null)
          useMapaStore.getState().selecionarGrupo([])
        }
      } else if (!comando && /^[1-9]$/.test(tecla) && FERRAMENTAS[Number(tecla) - 1]) {
        editor.definirFerramenta(FERRAMENTAS[Number(tecla) - 1].id)
      } else if (editor.selecionadaId) {
        if (comando && tecla === 'd') {
          evento.preventDefault()
          void duplicarNoMapa(editor.selecionadaId)
        } else if (!comando && (tecla === 'delete' || tecla === 'backspace')) {
          evento.preventDefault()
          void removerDoMapa(editor.selecionadaId)
        } else if (!comando && tecla === 'r') {
          void girarNoMapa(editor.selecionadaId)
        }
      }
    }

    const aoTeclar = (evento: KeyboardEvent) => {
      // defaultPrevented: o Esc já cancelou um arraste em curso.
      if (digitando(evento) || evento.altKey || evento.defaultPrevented) return
      const tecla = evento.key.toLowerCase()
      const comando = evento.ctrlKey || evento.metaKey

      if (!comando && tecla === '?') {
        refs.current.aoAbrirAjuda()
      } else if (!comando && tecla === 'f') {
        refs.current.aoAlternarTelaCheia()
      } else if (!comando && (tecla === 'q' || tecla === 'e')) {
        useVisaoStore.getState().girar(tecla === 'q' ? -1 : 1)
      } else if (souMestre) {
        const modo = useEditorStore.getState().modo
        if (modo === 'editar') naEstruturaSelecionada(evento, tecla, comando)
        else if (modo === 'jogar') noTokenSelecionado(evento, tecla, comando)
        // No pincel da névoa, o Esc (sem arraste em curso) volta a jogar.
        else if (modo === 'nevoa' && tecla === 'escape') useEditorStore.getState().definirModo('jogar')
      } else if (tecla === 'escape') {
        // Para o jogador, o Esc só larga o token e tira o destaque do movimento por blocos.
        useMapaStore.getState().selecionar(null)
      }
    }

    window.addEventListener('keydown', aoTeclar)
    return () => window.removeEventListener('keydown', aoTeclar)
  }, [souMestre])

  return null
}
