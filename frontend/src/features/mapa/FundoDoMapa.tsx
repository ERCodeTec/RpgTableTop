import { useEffect } from 'react'
import { PlaneGeometry } from 'three'
import { useImagemCarregada } from '../../shared/imagens/imagens'
import { useEditorStore } from './editorStore'
import { alcasDoFundo, ehCanto, retanguloDoFundo, type RetanguloDoFundo } from './fundo'
import { useMapaStore } from './mapaStore'
import { texturaDaImagem } from './texturas'

const QUADRADO = new PlaneGeometry(1, 1)
const COR_DAS_ALCAS = '#e2b23c'
/** Espessura do contorno da imagem durante o ajuste, em células. */
const CONTORNO = 0.05

/**
 * A imagem de fundo do mapa, deitada no chão sob a grade. Durante o ajuste, mostra o rascunho
 * do Mestre em vez do que está salvo, com o contorno e as alças de redimensionar. Enquanto a
 * imagem carrega (ou se falhar), fica só a cor do mapa.
 */
export function FundoDoMapa() {
  const rascunho = useEditorStore((estado) => estado.fundo)
  const imagemId = useMapaStore((estado) => estado.mapa?.imagemFundoId ?? null)
  const escala = useMapaStore((estado) => estado.mapa?.fundoEscala ?? 1)
  const escalaAltura = useMapaStore((estado) => estado.mapa?.fundoEscalaAltura ?? null)
  const offsetX = useMapaStore((estado) => estado.mapa?.fundoOffsetX ?? 0)
  const offsetY = useMapaStore((estado) => estado.mapa?.fundoOffsetY ?? 0)
  const larguraDoMapa = useMapaStore((estado) => estado.mapa?.largura ?? 1)

  const fundo = rascunho ?? { imagemId, escala, offsetX, offsetY, escalaAltura }
  const imagem = useImagemCarregada(fundo.imagemId)
  const proporcao = imagem ? imagem.naturalHeight / imagem.naturalWidth : null

  // Os gestos do ajuste não são componentes: leem a proporção da imagem pelo store.
  useEffect(() => {
    if (proporcao) useEditorStore.getState().definirProporcaoDoFundo(proporcao)
  }, [proporcao])

  if (!fundo.imagemId || !imagem || !proporcao) return null

  const r = retanguloDoFundo(fundo, larguraDoMapa, proporcao)

  return (
    <>
      <mesh position={[r.x + r.largura / 2, 0.003, r.z + r.altura / 2]} rotation-x={-Math.PI / 2}>
        <planeGeometry args={[r.largura, r.altura]} />
        <meshBasicMaterial
          map={texturaDaImagem(fundo.imagemId, false)}
          transparent
          opacity={rascunho ? 0.85 : 1}
          depthWrite={false}
        />
      </mesh>
      {rascunho && <AlcasDoFundo retangulo={r} />}
    </>
  )
}

/** Desenhadas por cima da cena, como as alças das estruturas, para não sumirem atrás de nada. */
function AlcasDoFundo({ retangulo: r }: { retangulo: RetanguloDoFundo }) {
  const lados = [
    { x: r.x + r.largura / 2, z: r.z, largura: r.largura, altura: CONTORNO },
    { x: r.x + r.largura / 2, z: r.z + r.altura, largura: r.largura, altura: CONTORNO },
    { x: r.x, z: r.z + r.altura / 2, largura: CONTORNO, altura: r.altura },
    { x: r.x + r.largura, z: r.z + r.altura / 2, largura: CONTORNO, altura: r.altura },
  ]

  return (
    <>
      {lados.map((lado, i) => (
        <mesh
          key={i}
          geometry={QUADRADO}
          position={[lado.x, 0.025, lado.z]}
          rotation-x={-Math.PI / 2}
          scale={[lado.largura, lado.altura, 1]}
          renderOrder={21}
        >
          <meshBasicMaterial color={COR_DAS_ALCAS} transparent opacity={0.7} depthTest={false} depthWrite={false} />
        </mesh>
      ))}
      {alcasDoFundo(r).map((alca) => {
        // Os cantos são maiores: redimensionam mantendo a forma; as bordas esticam um lado só.
        const tamanho = ehCanto(alca) ? 0.34 : 0.26
        return (
          <mesh
            key={`${alca.dx}:${alca.dz}`}
            geometry={QUADRADO}
            position={[alca.x, 0.03, alca.z]}
            rotation-x={-Math.PI / 2}
            scale={[tamanho, tamanho, 1]}
            renderOrder={22}
          >
            <meshBasicMaterial color={COR_DAS_ALCAS} depthTest={false} depthWrite={false} />
          </mesh>
        )
      })}
    </>
  )
}
