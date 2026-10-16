import { useEffect } from 'react'
import { Vector3 } from 'three'
import { grausParaRad } from './geometria'
import type { Gesto, ModoDeInteracao } from './gestos'
import { useRig } from './rig'
import { INCLINACAO_MAXIMA, useVisaoStore } from './visaoStore'

/**
 * Deslocamento do ponteiro, em pixels, a partir do qual um clique vira arraste. O dedo treme mais
 * que o mouse: com a folga do mouse, um toque numa célula azul ou num token virava deslocamento do mapa.
 */
const FOLGA_DO_CLIQUE = 5
const FOLGA_DO_TOQUE = 12

const folgaDe = (evento: PointerEvent) => (evento.pointerType === 'mouse' ? FOLGA_DO_CLIQUE : FOLGA_DO_TOQUE)

/** Graus de giro e de inclinação por pixel arrastado com o botão direito. */
const GIRO_POR_PIXEL = 0.4
const INCLINACAO_POR_PIXEL = 0.25

const radParaGraus = (rad: number) => (rad * 180) / Math.PI

type Estado =
  | { tipo: 'deslocar'; x: number; y: number; moveu: boolean; clicavel: boolean }
  | { tipo: 'gesto'; gesto: Gesto; inicioX: number; inicioY: number; moveu: boolean }
  | { tipo: 'pinca'; distancia: number; meioX: number; meioY: number }
  /** Botão direito: o arraste horizontal gira a visão e o vertical inclina, a partir dos ângulos do início (em graus). */
  | { tipo: 'girar'; x: number; y: number; inclinacao: number; rotacao: number }

/**
 * Os ponteiros no canvas. A navegação é igual em todos os modos: deslocar com o botão do meio,
 * arrastando o fundo ou com dois dedos, girar e inclinar arrastando com o botão direito, e
 * aproximar com scroll ou pinça. O que o botão principal
 * faz sobre o mapa (mover token, desenhar parede, ajustar o fundo) vem do `modo`.
 */
export function Interacao({ modo }: { modo: ModoDeInteracao }) {
  const rig = useRig()

  useEffect(() => {
    const tela = rig.canvas
    const ponteiros = new Map<number, { x: number; y: number }>()
    const ancora = new Vector3()
    let estado: Estado | null = null

    const doisDedos = () => {
      const [a, b] = [...ponteiros.values()]
      return { distancia: Math.hypot(a.x - b.x, a.y - b.y), meioX: (a.x + b.x) / 2, meioY: (a.y + b.y) / 2 }
    }

    const cancelarGesto = () => {
      if (estado?.tipo === 'gesto') {
        estado.gesto.cancelar()
        estado = null
      }
      tela.style.cursor = ''
    }

    const aoPressionar = (evento: PointerEvent) => {
      ponteiros.set(evento.pointerId, { x: evento.clientX, y: evento.clientY })
      tela.setPointerCapture(evento.pointerId)

      if (ponteiros.size === 2) {
        cancelarGesto()
        estado = { tipo: 'pinca', ...doisDedos() }
        return
      }
      if (ponteiros.size > 2) return

      if (evento.button === 1) {
        // Evita a rolagem automática do navegador no botão do meio.
        evento.preventDefault()
        estado = { tipo: 'deslocar', x: evento.clientX, y: evento.clientY, moveu: true, clicavel: false }
        return
      }
      if (evento.button === 2) {
        // A câmera acompanha o ponteiro sem animação; a preferência é gravada ao soltar.
        const visao = useVisaoStore.getState()
        estado = { tipo: 'girar', x: evento.clientX, y: evento.clientY, inclinacao: visao.inclinacao, rotacao: visao.rotacao }
        return
      }
      if (evento.button !== 0) return

      const gesto = modo.iniciar(evento.clientX, evento.clientY, { shift: evento.shiftKey })
      estado = gesto
        ? { tipo: 'gesto', gesto, inicioX: evento.clientX, inicioY: evento.clientY, moveu: false }
        : { tipo: 'deslocar', x: evento.clientX, y: evento.clientY, moveu: false, clicavel: true }
    }

    const aoMover = (evento: PointerEvent) => {
      const ponteiro = ponteiros.get(evento.pointerId)
      if (!ponteiro && ponteiros.size === 0) {
        modo.pairar?.({ clienteX: evento.clientX, clienteY: evento.clientY })
        return
      }
      if (!ponteiro || !estado) return
      ponteiro.x = evento.clientX
      ponteiro.y = evento.clientY

      if (estado.tipo === 'pinca') {
        if (ponteiros.size < 2) return
        const atual = doisDedos()
        rig.chao(estado.meioX, estado.meioY, ancora)
        rig.escalar(atual.distancia / Math.max(1, estado.distancia))
        rig.manterSobOPonteiro(ancora, atual.meioX, atual.meioY)
        estado = { tipo: 'pinca', ...atual }
        return
      }

      if (estado.tipo === 'girar') {
        // Para a direita gira como a tecla E; para cima inclina em direção ao horizonte.
        const rotacao = estado.rotacao + (evento.clientX - estado.x) * GIRO_POR_PIXEL
        const inclinacao = Math.min(INCLINACAO_MAXIMA, Math.max(0, estado.inclinacao - (evento.clientY - estado.y) * INCLINACAO_POR_PIXEL))
        rig.girarPara(grausParaRad(inclinacao), grausParaRad(rotacao), false)
        tela.style.cursor = 'grabbing'
        return
      }

      if (estado.tipo === 'deslocar') {
        if (!estado.moveu && Math.hypot(evento.clientX - estado.x, evento.clientY - estado.y) < folgaDe(evento)) return
        rig.chao(estado.x, estado.y, ancora)
        rig.manterSobOPonteiro(ancora, evento.clientX, evento.clientY)
        estado.x = evento.clientX
        estado.y = evento.clientY
        estado.moveu = true
        tela.style.cursor = 'grabbing'
        return
      }

      if (!estado.moveu && Math.hypot(evento.clientX - estado.inicioX, evento.clientY - estado.inicioY) < folgaDe(evento)) return
      estado.moveu = true
      tela.style.cursor = 'grabbing'
      estado.gesto.arrastar(evento.clientX, evento.clientY)
    }

    const aoSoltar = (evento: PointerEvent) => {
      if (!ponteiros.delete(evento.pointerId)) return
      if (tela.hasPointerCapture(evento.pointerId)) tela.releasePointerCapture(evento.pointerId)
      const concluido = estado
      estado = null
      tela.style.cursor = ''
      if (!concluido) return

      if (concluido.tipo === 'girar') {
        // Grava onde a câmera parou (a transição para os mesmos ângulos não anda nada).
        useVisaoStore.getState().definirAngulos(radParaGraus(rig.inclinacao), radParaGraus(rig.rotacao))
        return
      }

      if (concluido.tipo === 'gesto') {
        if (evento.type === 'pointercancel') concluido.gesto.cancelar()
        else if (concluido.moveu) concluido.gesto.concluir()
        else concluido.gesto.clicar()
      } else if (concluido.tipo === 'deslocar' && concluido.clicavel && !concluido.moveu && evento.type !== 'pointercancel') {
        modo.clicarNoFundo(evento.clientX, evento.clientY)
      }
    }

    const aoSair = () => modo.pairar?.(null)

    // O botão direito gira a visão: o menu de contexto do navegador não abre sobre o mapa.
    const aoMenuDeContexto = (evento: MouseEvent) => evento.preventDefault()

    const aoRolar = (evento: WheelEvent) => {
      evento.preventDefault()
      // Em trackpads, a pinça chega como scroll com Ctrl e deltas pequenos.
      const sensibilidade = evento.ctrlKey ? 0.01 : 0.0015
      rig.aproximar(Math.exp(-evento.deltaY * sensibilidade), evento.clientX, evento.clientY)
    }

    // Na captura, antes dos atalhos do mapa: o Esc que cancela um arraste não faz mais nada.
    const aoTeclar = (evento: KeyboardEvent) => {
      if (evento.key === 'Escape' && estado?.tipo === 'gesto') {
        cancelarGesto()
        evento.preventDefault()
      }
    }

    tela.addEventListener('pointerdown', aoPressionar)
    tela.addEventListener('pointermove', aoMover)
    tela.addEventListener('pointerup', aoSoltar)
    tela.addEventListener('pointercancel', aoSoltar)
    tela.addEventListener('pointerleave', aoSair)
    tela.addEventListener('contextmenu', aoMenuDeContexto)
    tela.addEventListener('wheel', aoRolar, { passive: false })
    window.addEventListener('keydown', aoTeclar, true)
    return () => {
      tela.removeEventListener('pointerdown', aoPressionar)
      tela.removeEventListener('pointermove', aoMover)
      tela.removeEventListener('pointerup', aoSoltar)
      tela.removeEventListener('pointercancel', aoSoltar)
      tela.removeEventListener('pointerleave', aoSair)
      tela.removeEventListener('contextmenu', aoMenuDeContexto)
      tela.removeEventListener('wheel', aoRolar)
      window.removeEventListener('keydown', aoTeclar, true)
      cancelarGesto()
      aoSair()
    }
  }, [rig, modo])

  return null
}
