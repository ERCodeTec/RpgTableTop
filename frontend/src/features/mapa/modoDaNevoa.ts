import { useEditorStore } from './editorStore'
import type { Gesto, ModoDeInteracao } from './gestos'
import { pintarNevoa } from './mapaApi'
import { useMapaStore } from './mapaStore'
import { camadaDoRetangulo, paraBase64 } from './nevoa'
import type { Rig } from './rig'

/**
 * O pincel da névoa: arrastar marca um retângulo de células (um clique, uma célula), que ao soltar
 * é revelado para todos ou coberto, conforme o pincel escolhido na barra.
 */
export function criarModoDaNevoa(rig: Rig, aoAvisar: (mensagem: string) => void): ModoDeInteracao {
  const editor = useEditorStore.getState
  const mapa = useMapaStore.getState
  const limitar = (valor: number, minimo: number, maximo: number) => Math.min(maximo, Math.max(minimo, valor))

  function pintar(inicio: { x: number; y: number }): Gesto {
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
    editor().definirPrevia({ ...atual, valido: editor().pincel === 'revelar' })

    const aplicar = () => {
      const m = mapa().mapa
      const revelar = editor().pincel === 'revelar'
      if (!m) return
      pintarNevoa(m.id, paraBase64(camadaDoRetangulo(m.largura, m.altura, atual)), revelar)
        .catch((erro: Error) => aoAvisar(erro.message || 'Não foi possível pintar a névoa.'))
        .finally(() => editor().definirPrevia(null))
    }

    return {
      arrastar(x, y) {
        atual = area(rig.celula(x, y))
        // A prévia fica verde ao revelar e vermelha ao cobrir.
        editor().definirPrevia({ ...atual, valido: editor().pincel === 'revelar' })
      },
      concluir: aplicar,
      clicar: aplicar,
      cancelar: () => editor().definirPrevia(null),
    }
  }

  return {
    iniciar(clienteX, clienteY) {
      const m = mapa().mapa
      const celula = rig.celula(clienteX, clienteY)
      if (!m || celula.x < 0 || celula.y < 0 || celula.x >= m.largura || celula.y >= m.altura) return null
      return pintar(celula)
    },
    clicarNoFundo: () => undefined,
  }
}
