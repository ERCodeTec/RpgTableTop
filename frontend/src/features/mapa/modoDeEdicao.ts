import { Vector3 } from 'three'
import { alterarNoMapa, criarNoMapa } from './acoesDoEditor'
import { TIPO_DA_FERRAMENTA, useEditorStore, type Previa } from './editorStore'
import { centroDoToken } from './geometria'
import type { Gesto, ModoDeInteracao } from './gestos'
import type { Estrutura } from './mapaApi'
import { useMapaStore } from './mapaStore'
import { arrastarToken } from './modoDeJogo'
import { tokenEscondido } from './nevoa'
import { apontar } from './picking'
import type { Rig } from './rig'

type Borda = 'esquerda' | 'direita' | 'cima' | 'baixo'

/** Distância, em células, até onde o ponteiro pega a alça de uma borda. */
const ALCANCE_DA_ALCA = 0.4

/** As alças de redimensionar ficam no meio de cada borda da área, no chão. */
export function alcasDe(e: Pick<Estrutura, 'x' | 'y' | 'largura' | 'profundidade'>): { borda: Borda; x: number; z: number }[] {
  return [
    { borda: 'esquerda', x: e.x, z: e.y + e.profundidade / 2 },
    { borda: 'direita', x: e.x + e.largura, z: e.y + e.profundidade / 2 },
    { borda: 'cima', x: e.x + e.largura / 2, z: e.y },
    { borda: 'baixo', x: e.x + e.largura / 2, z: e.y + e.profundidade },
  ]
}

/**
 * Editar o mapa: desenhar paredes e áreas de chão arrastando, colocar portas, pilares e obstáculos com um clique,
 * mover ou redimensionar a estrutura selecionada e, com a ferramenta de selecionar, arrastar tokens
 * como no modo de jogo. Arrastar onde não há nada (ou com Shift em qualquer lugar) desenha uma área
 * que seleciona vários tokens, para arrastá-los juntos. Tudo com snap ao grid.
 */
export function criarModoDeEdicao(rig: Rig, aoMover: (tokenId: string, x: number, y: number) => void): ModoDeInteracao {
  const editor = useEditorStore.getState
  const mapa = useMapaStore.getState
  const ponto = new Vector3()

  const limitar = (valor: number, minimo: number, maximo: number) => Math.min(maximo, Math.max(minimo, valor))
  const dentroDoMapa = (x: number, y: number) => {
    const m = mapa().mapa
    return m !== null && x >= 0 && y >= 0 && x < m.largura && y < m.altura
  }
  const mostrar = (area: Omit<Previa, 'valido'>) => editor().definirPrevia({ ...area, valido: true })

  /** Arrastar da célula inicial até outra cria um segmento reto, de 1 célula de espessura, no eixo dominante. */
  function desenharParede(inicio: { x: number; y: number }): Gesto {
    const area = (fim: { x: number; y: number }) => {
      const m = mapa().mapa!
      const fx = limitar(fim.x, 0, m.largura - 1)
      const fy = limitar(fim.y, 0, m.altura - 1)
      return Math.abs(fx - inicio.x) >= Math.abs(fy - inicio.y)
        ? { x: Math.min(inicio.x, fx), y: inicio.y, largura: Math.abs(fx - inicio.x) + 1, profundidade: 1 }
        : { x: inicio.x, y: Math.min(inicio.y, fy), largura: 1, profundidade: Math.abs(fy - inicio.y) + 1 }
    }
    let atual = area(inicio)
    mostrar(atual)

    return {
      arrastar(x, y) {
        atual = area(rig.celula(x, y))
        mostrar(atual)
      },
      concluir: () => void criarNoMapa('Parede', atual),
      cancelar: () => editor().definirPrevia(null),
      clicar: () => void criarNoMapa('Parede', atual),
    }
  }

  /** Arrastar da célula inicial até outra cria um retângulo de chão com esses dois cantos. */
  function desenharChao(inicio: { x: number; y: number }): Gesto {
    const area = (fim: { x: number; y: number }) => {
      const m = mapa().mapa!
      const fx = limitar(fim.x, 0, m.largura - 1)
      const fy = limitar(fim.y, 0, m.altura - 1)
      return {
        x: Math.min(inicio.x, fx),
        y: Math.min(inicio.y, fy),
        largura: Math.abs(fx - inicio.x) + 1,
        profundidade: Math.abs(fy - inicio.y) + 1,
      }
    }
    let atual = area(inicio)
    mostrar(atual)

    return {
      arrastar(x, y) {
        atual = area(rig.celula(x, y))
        mostrar(atual)
      },
      concluir: () => void criarNoMapa('Chao', atual),
      cancelar: () => editor().definirPrevia(null),
      clicar: () => void criarNoMapa('Chao', atual),
    }
  }

  /** Porta, pilar e obstáculo nascem com 1 célula, onde o ponteiro soltar. */
  function colocar(tipo: Estrutura['tipo'], inicio: { x: number; y: number }): Gesto {
    let celula = inicio
    mostrar({ ...celula, largura: 1, profundidade: 1 })
    const criar = () => {
      if (dentroDoMapa(celula.x, celula.y)) void criarNoMapa(tipo, { ...celula, largura: 1, profundidade: 1 })
      else editor().definirPrevia(null)
    }
    return {
      arrastar(x, y) {
        celula = rig.celula(x, y)
        editor().definirPrevia({ ...celula, largura: 1, profundidade: 1, valido: dentroDoMapa(celula.x, celula.y) })
      },
      concluir: criar,
      cancelar: () => editor().definirPrevia(null),
      clicar: criar,
    }
  }

  function mover(estrutura: Estrutura, clienteX: number, clienteY: number): Gesto {
    const inicio = rig.celula(clienteX, clienteY)
    let destino = { x: estrutura.x, y: estrutura.y }
    return {
      arrastar(x, y) {
        const m = mapa().mapa!
        const celula = rig.celula(x, y)
        destino = {
          x: limitar(estrutura.x + celula.x - inicio.x, 0, m.largura - estrutura.largura),
          y: limitar(estrutura.y + celula.y - inicio.y, 0, m.altura - estrutura.profundidade),
        }
        mostrar({ ...destino, largura: estrutura.largura, profundidade: estrutura.profundidade })
      },
      concluir: () => void alterarNoMapa(estrutura.id, destino).then(() => editor().definirPrevia(null)),
      cancelar: () => editor().definirPrevia(null),
      clicar: () => undefined,
    }
  }

  /** Puxa uma borda; a borda oposta fica onde está e a área nunca fica menor que 1 célula. */
  function redimensionar(estrutura: Estrutura, borda: Borda): Gesto {
    const direita = estrutura.x + estrutura.largura
    const baixo = estrutura.y + estrutura.profundidade
    let area = { x: estrutura.x, y: estrutura.y, largura: estrutura.largura, profundidade: estrutura.profundidade }
    return {
      arrastar(x, y) {
        const m = mapa().mapa!
        rig.chao(x, y, ponto)
        if (borda === 'esquerda') {
          const novo = limitar(Math.round(ponto.x), 0, direita - 1)
          area = { ...area, x: novo, largura: direita - novo }
        } else if (borda === 'direita') {
          area = { ...area, largura: limitar(Math.round(ponto.x), estrutura.x + 1, m.largura) - estrutura.x }
        } else if (borda === 'cima') {
          const novo = limitar(Math.round(ponto.z), 0, baixo - 1)
          area = { ...area, y: novo, profundidade: baixo - novo }
        } else {
          area = { ...area, profundidade: limitar(Math.round(ponto.z), estrutura.y + 1, m.altura) - estrutura.y }
        }
        mostrar(area)
      },
      concluir: () => void alterarNoMapa(estrutura.id, area).then(() => editor().definirPrevia(null)),
      cancelar: () => editor().definirPrevia(null),
      clicar: () => undefined,
    }
  }

  /** Os tokens visíveis cujo centro, na tela, está dentro do retângulo. */
  function tokensNoRetangulo(esquerda: number, topo: number, direita: number, base: number): string[] {
    const caixa = rig.canvas.getBoundingClientRect()
    const { tokens, ordem, nevoa } = mapa()
    return ordem.filter((id) => {
      const token = tokens[id]
      if (!token || tokenEscondido(nevoa, token)) return false
      centroDoToken(token, rig.inclinacao, ponto).project(rig.camera)
      const x = caixa.left + ((ponto.x + 1) / 2) * caixa.width
      const y = caixa.top + ((1 - ponto.y) / 2) * caixa.height
      return x >= esquerda && x <= direita && y >= topo && y <= base
    })
  }

  /**
   * Arrastar desenha um retângulo na tela e seleciona os tokens dentro dele. Com Shift, soma à seleção
   * que já havia, e um clique sem arrastar põe ou tira do grupo o token sob o ponteiro.
   */
  function selecionarArea(inicioX: number, inicioY: number, somar: boolean): Gesto {
    const { grupo, selecionadoId } = mapa()
    const anteriores = somar ? (grupo.length ? grupo : selecionadoId ? [selecionadoId] : []) : []
    const retangulo = document.createElement('div')
    retangulo.className = 'mapa-area-de-selecao'
    editor().selecionar(null)

    return {
      arrastar(x, y) {
        const [esquerda, topo, direita, base] = [Math.min(inicioX, x), Math.min(inicioY, y), Math.max(inicioX, x), Math.max(inicioY, y)]
        Object.assign(retangulo.style, {
          left: `${esquerda}px`,
          top: `${topo}px`,
          width: `${direita - esquerda}px`,
          height: `${base - topo}px`,
        })
        if (!retangulo.isConnected) document.body.append(retangulo)
        const dentro = tokensNoRetangulo(esquerda, topo, direita, base)
        mapa().selecionarGrupo([...anteriores, ...dentro.filter((id) => !anteriores.includes(id))])
      },
      concluir: () => retangulo.remove(),
      cancelar() {
        retangulo.remove()
        mapa().selecionarGrupo(anteriores)
      },
      clicar() {
        retangulo.remove()
        const alvo = somar ? apontar(rig, inicioX, inicioY, { tokens: true, estruturas: false }) : null
        if (!alvo) {
          if (!somar) mapa().selecionar(null)
          return
        }
        mapa().selecionarGrupo(anteriores.includes(alvo.id) ? anteriores.filter((id) => id !== alvo.id) : [...anteriores, alvo.id])
      },
    }
  }

  return {
    iniciar(clienteX, clienteY, { shift }) {
      if (editor().ocupado) return null
      const { ferramenta, selecionadaId } = editor()
      const celula = rig.celula(clienteX, clienteY)

      if (ferramenta !== 'selecionar') {
        if (!dentroDoMapa(celula.x, celula.y)) return null
        if (ferramenta === 'parede') return desenharParede(celula)
        if (ferramenta === 'chao') return desenharChao(celula)
        return colocar(TIPO_DA_FERRAMENTA[ferramenta], celula)
      }

      if (shift) return selecionarArea(clienteX, clienteY, true)

      // As alças da estrutura selecionada têm prioridade sobre o que estiver embaixo delas.
      const selecionada = selecionadaId ? mapa().estruturas[selecionadaId] : undefined
      if (selecionada) {
        rig.chao(clienteX, clienteY, ponto)
        const alca = alcasDe(selecionada).find((a) => Math.hypot(a.x - ponto.x, a.z - ponto.z) <= ALCANCE_DA_ALCA)
        if (alca) return redimensionar(selecionada, alca.borda)
      }

      const alvo = apontar(rig, clienteX, clienteY)
      if (!alvo) return selecionarArea(clienteX, clienteY, false)
      if (alvo.tipo === 'token') {
        editor().selecionar(null)
        // Fora do grupo, o token é arrastado sozinho e o grupo se desfaz.
        if (!mapa().grupo.includes(alvo.id)) mapa().selecionarGrupo([])
        // Só o Mestre edita o mapa, e para ele as estruturas não barram o movimento.
        return arrastarToken(rig, alvo.id, clienteX, clienteY, { souMestre: true, aoMover })
      }
      editor().selecionar(alvo.id)
      mapa().selecionarGrupo([])
      return mover(mapa().estruturas[alvo.id], clienteX, clienteY)
    },

    clicarNoFundo: () => editor().selecionar(null),
  }
}
