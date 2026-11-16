import { Vector3 } from 'three'
import { useEditorStore } from './editorStore'
import { alcasDoFundo, redimensionarFundo, retanguloDoFundo } from './fundo'
import type { Gesto, ModoDeInteracao } from './gestos'
import type { DadosDoFundo } from './mapaApi'
import { useMapaStore } from './mapaStore'
import { ALCANCE_DA_ALCA } from './modoDeEdicao'
import type { Rig } from './rig'

/**
 * Ajustar o fundo: puxar uma alça redimensiona a imagem (bordas esticam um lado, cantos mantêm a
 * forma); arrastar em qualquer outro lugar desloca a imagem sob a grade. Os controles finos ficam na barra.
 */
export function criarModoDeFundo(rig: Rig): ModoDeInteracao {
  const editor = useEditorStore.getState
  const ponto = new Vector3()

  function redimensionar(inicial: DadosDoFundo, alca: { dx: -1 | 0 | 1; dz: -1 | 0 | 1 }, larguraDoMapa: number, proporcao: number): Gesto {
    return {
      arrastar(x, y) {
        rig.chao(x, y, ponto)
        editor().definirFundo(redimensionarFundo(inicial, alca, { x: ponto.x, z: ponto.z }, larguraDoMapa, proporcao))
      },
      concluir: () => undefined,
      cancelar: () => editor().definirFundo(inicial),
      clicar: () => undefined,
    }
  }

  return {
    iniciar(clienteX, clienteY) {
      const inicial = editor().fundo
      if (!inicial) return null
      const origem = rig.chao(clienteX, clienteY, new Vector3())

      const larguraDoMapa = useMapaStore.getState().mapa?.largura ?? 1
      const proporcao = editor().proporcaoDoFundo
      const alca = alcasDoFundo(retanguloDoFundo(inicial, larguraDoMapa, proporcao))
        .map((a) => ({ a, distancia: Math.hypot(a.x - origem.x, a.z - origem.z) }))
        .filter(({ distancia }) => distancia <= ALCANCE_DA_ALCA)
        .sort((p, q) => p.distancia - q.distancia)[0]?.a
      if (alca) return redimensionar(inicial, alca, larguraDoMapa, proporcao)

      return {
        arrastar(x, y) {
          rig.chao(x, y, ponto)
          // Duas casas decimais bastam e mantêm o valor legível.
          editor().definirFundo({
            ...editor().fundo!,
            offsetX: Math.round((inicial.offsetX + ponto.x - origem.x) * 100) / 100,
            offsetY: Math.round((inicial.offsetY + ponto.z - origem.z) * 100) / 100,
          })
        },
        concluir: () => undefined,
        cancelar: () => editor().definirFundo({ ...editor().fundo!, offsetX: inicial.offsetX, offsetY: inicial.offsetY }),
        clicar: () => undefined,
      }
    },
    clicarNoFundo: () => undefined,
  }
}
