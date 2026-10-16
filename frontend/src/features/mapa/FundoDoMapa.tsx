import { useImagemCarregada } from '../../shared/imagens/imagens'
import { useEditorStore } from './editorStore'
import { useMapaStore } from './mapaStore'
import { texturaDaImagem } from './texturas'

/**
 * A imagem de fundo do mapa, deitada no chão sob a grade. Durante o ajuste, mostra o rascunho
 * do Mestre em vez do que está salvo. Enquanto a imagem carrega (ou se falhar), fica só a cor do mapa.
 */
export function FundoDoMapa() {
  const rascunho = useEditorStore((estado) => estado.fundo)
  const imagemId = useMapaStore((estado) => estado.mapa?.imagemFundoId ?? null)
  const escala = useMapaStore((estado) => estado.mapa?.fundoEscala ?? 1)
  const offsetX = useMapaStore((estado) => estado.mapa?.fundoOffsetX ?? 0)
  const offsetY = useMapaStore((estado) => estado.mapa?.fundoOffsetY ?? 0)
  const larguraDoMapa = useMapaStore((estado) => estado.mapa?.largura ?? 1)

  const fundo = rascunho ?? { imagemId, escala, offsetX, offsetY }
  const imagem = useImagemCarregada(fundo.imagemId)
  if (!fundo.imagemId || !imagem) return null

  // Escala 1 = a imagem tem a largura do mapa; a altura segue a proporção da imagem.
  const largura = larguraDoMapa * fundo.escala
  const altura = largura * (imagem.naturalHeight / imagem.naturalWidth)

  return (
    <mesh position={[fundo.offsetX + largura / 2, 0.003, fundo.offsetY + altura / 2]} rotation-x={-Math.PI / 2}>
      <planeGeometry args={[largura, altura]} />
      <meshBasicMaterial
        map={texturaDaImagem(fundo.imagemId, false)}
        transparent
        opacity={rascunho ? 0.85 : 1}
        depthWrite={false}
      />
    </mesh>
  )
}
