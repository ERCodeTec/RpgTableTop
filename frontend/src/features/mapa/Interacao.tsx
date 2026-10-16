import { useEffect, useRef } from 'react'
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

/** Segurar o ponteiro parado no fundo do mapa por este tempo faz um ping (o jeito do toque). */
const SEGURAR_PARA_PINGAR_MS = 550

/**
 * Toque duplo: o segundo clique precisa vir neste tempo e perto do primeiro. O dedo cai menos
 * preciso que o mouse; a distância curta evita que cliques rápidos em células vizinhas pinguem.
 */
const INTERVALO_DO_TOQUE_DUPLO_MS = 350
const DISTANCIA_DO_CLIQUE_DUPLO = 8
const DISTANCIA_DO_TOQUE_DUPLO = 30

/** Ping num ponto do chão (x, z do plano = x, y da grade). `focar` leva a câmera de todos até ele. */
export type AoPingar = (x: number, y: number, focar: boolean) => void

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
 * faz sobre o mapa (mover token, desenhar parede, ajustar o fundo) vem do `modo`. O ping também é
 * igual em todos: Alt + clique, ou segurar parado sobre o fundo; no modo de jogo, também o duplo
 * clique ou toque duplo. O primeiro clique age na hora (não espera para ver se vem outro): só o
 * segundo vira ping.
 */
export function Interacao({ modo, aoPingar }: { modo: ModoDeInteracao; aoPingar?: AoPingar }) {
  const rig = useRig()
  // Por ref: trocar o callback não reinicia os ouvintes (nem cancela um arraste em curso).
  const aoPingarRef = useRef(aoPingar)
  aoPingarRef.current = aoPingar

  useEffect(() => {
    const tela = rig.canvas
    const ponteiros = new Map<number, { x: number; y: number }>()
    const ancora = new Vector3()
    let estado: Estado | null = null
    let segurando: number | undefined
    let ultimoClique: { x: number; y: number; quando: number } | null = null

    /** Registra um clique e diz se ele fecha um toque duplo com o anterior. */
    const fechaToqueDuplo = (evento: PointerEvent) => {
      const anterior = ultimoClique
      const limite = evento.pointerType === 'mouse' ? DISTANCIA_DO_CLIQUE_DUPLO : DISTANCIA_DO_TOQUE_DUPLO
      const duplo =
        anterior !== null &&
        evento.timeStamp - anterior.quando <= INTERVALO_DO_TOQUE_DUPLO_MS &&
        Math.hypot(evento.clientX - anterior.x, evento.clientY - anterior.y) <= limite
      // Depois de um toque duplo, o terceiro toque começa uma contagem nova.
      ultimoClique = duplo ? null : { x: evento.clientX, y: evento.clientY, quando: evento.timeStamp }
      return duplo
    }

    const pararDeSegurar = () => {
      window.clearTimeout(segurando)
      segurando = undefined
    }

    const pingar = (clienteX: number, clienteY: number, focar: boolean) => {
      const ponto = rig.chao(clienteX, clienteY, new Vector3())
      aoPingarRef.current?.(ponto.x, ponto.z, focar)
    }

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
      pararDeSegurar()

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

      // Alt + clique pinga em qualquer modo, sem mexer em token ou estrutura; com Shift, o Mestre leva a visão de todos.
      if (evento.altKey && aoPingarRef.current) {
        pingar(evento.clientX, evento.clientY, evento.shiftKey)
        return
      }

      const gesto = modo.iniciar(evento.clientX, evento.clientY, { shift: evento.shiftKey })
      estado = gesto
        ? { tipo: 'gesto', gesto, inicioX: evento.clientX, inicioY: evento.clientY, moveu: false }
        : { tipo: 'deslocar', x: evento.clientX, y: evento.clientY, moveu: false, clicavel: true }

      if (!gesto && aoPingarRef.current) {
        const [x, y] = [evento.clientX, evento.clientY]
        segurando = window.setTimeout(() => {
          segurando = undefined
          if (estado?.tipo !== 'deslocar' || estado.moveu || !estado.clicavel) return
          // Segurou parado: vira ping, e soltar depois não conta como clique.
          estado.clicavel = false
          pingar(x, y, false)
        }, SEGURAR_PARA_PINGAR_MS)
      }
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
        pararDeSegurar()
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
      pararDeSegurar()
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

      const foiClique =
        evento.type !== 'pointercancel' &&
        (concluido.tipo === 'gesto' ? !concluido.moveu : concluido.tipo === 'deslocar' && concluido.clicavel && !concluido.moveu)
      if (foiClique && modo.pingaComToqueDuplo && aoPingarRef.current && fechaToqueDuplo(evento)) {
        if (concluido.tipo === 'gesto') concluido.gesto.cancelar()
        pingar(evento.clientX, evento.clientY, false)
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
      pararDeSegurar()
      cancelarGesto()
      aoSair()
    }
  }, [rig, modo])

  return null
}
