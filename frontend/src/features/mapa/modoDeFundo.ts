import { Vector3 } from 'three'
import { useEditorStore } from './editorStore'
import type { ModoDeInteracao } from './gestos'
import type { Rig } from './rig'

/** Ajustar o fundo: arrastar em qualquer lugar desloca a imagem sob a grade. A escala fica na barra. */
export function criarModoDeFundo(rig: Rig): ModoDeInteracao {
  const editor = useEditorStore.getState
  const ponto = new Vector3()

  return {
    iniciar(clienteX, clienteY) {
      const inicial = editor().fundo
      if (!inicial) return null
      const origem = rig.chao(clienteX, clienteY, new Vector3())
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
