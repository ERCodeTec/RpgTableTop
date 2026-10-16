import { Box3, Vector3 } from 'three'
import { alturaDesenhada, centroDoToken, RAIO_DO_TOKEN } from './geometria'
import { useEditorStore } from './editorStore'
import { useMapaStore } from './mapaStore'
import { estruturaEscondida, tokenEscondido } from './nevoa'
import type { Rig } from './rig'

export interface Apontado {
  tipo: 'token' | 'estrutura'
  id: string
}

const centro = new Vector3()
const ponto = new Vector3()
const caixa = new Box3()

/**
 * O token ou a estrutura sob uma posição da tela: o que estiver mais perto da câmera, como se vê.
 * Um token escondido atrás de uma parede não é atingido por um clique na parede.
 * A conta é feita com o raio da câmera contra o disco de cada token (que encara a câmera) e a
 * caixa de cada estrutura, então vale para qualquer inclinação e giro.
 */
export function apontar(
  rig: Rig,
  clienteX: number,
  clienteY: number,
  considerar: { tokens: boolean; estruturas: boolean } = { tokens: true, estruturas: true },
): Apontado | null {
  const raio = rig.raio(clienteX, clienteY)
  // O que a névoa esconde não é atingido: um clique no escuro não denuncia o que há lá.
  const { tokens, estruturas, nevoa } = useMapaStore.getState()
  let melhor: Apontado | null = null
  let menorDistancia = Infinity

  if (considerar.tokens) {
    for (const id in tokens) {
      if (tokenEscondido(nevoa, tokens[id])) continue
      centroDoToken(tokens[id], rig.inclinacao, centro)
      // O disco é perpendicular ao raio: basta a distância do centro até a reta.
      if (raio.distanceSqToPoint(centro) > RAIO_DO_TOKEN * RAIO_DO_TOKEN) continue
      const distancia = centro.sub(raio.origin).dot(raio.direction)
      if (distancia < menorDistancia) {
        menorDistancia = distancia
        melhor = { tipo: 'token', id }
      }
    }
  }

  if (considerar.estruturas) {
    const editando = useEditorStore.getState().modo === 'editar'
    for (const id in estruturas) {
      const e = estruturas[id]
      if (estruturaEscondida(nevoa, e)) continue
      // A entrada só é desenhada (e só pode ser clicada) no modo de edição.
      if (e.tipo === 'Entrada' && !editando) continue
      caixa.min.set(e.x, 0, e.y)
      caixa.max.set(e.x + e.largura, alturaDesenhada(e), e.y + e.profundidade)
      if (!raio.intersectBox(caixa, ponto)) continue
      const distancia = ponto.distanceTo(raio.origin)
      if (distancia < menorDistancia) {
        menorDistancia = distancia
        melhor = { tipo: 'estrutura', id }
      }
    }
  }

  return melhor
}
