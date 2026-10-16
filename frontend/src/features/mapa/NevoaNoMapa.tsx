import { useThree } from '@react-three/fiber'
import { useEffect, useLayoutEffect, useMemo } from 'react'
import { DataTexture, LinearFilter, RGBAFormat } from 'three'
import { useMapaStore } from './mapaStore'
import { A_VISTA, EXPLORADA } from './nevoa'

/**
 * Acima do chão das estruturas (0,01), do fundo e da grade, e abaixo do disco deitado do token
 * (0,02): cobre o piso, e os tokens que o jogador não vê nem são desenhados.
 */
const ALTURA_DA_NEVOA = 0.018

const COR_DA_NEVOA = [6, 10, 12] as const

/**
 * A opacidade de cada estado (nunca vista, explorada, à vista): para o jogador, o escuro esconde;
 * para o Mestre, que vê tudo, só sombreia o que o grupo não vê.
 */
const OPACIDADE_DO_JOGADOR = [255, 150, 0]
const OPACIDADE_DO_MESTRE = [120, 60, 0]

/**
 * A névoa de guerra sobre o mapa: um plano com uma textura de um texel por célula. A filtragem
 * linear suaviza a borda entre o que se vê e o escuro, sem custo por célula.
 */
export function NevoaNoMapa() {
  const nevoa = useMapaStore((estado) => estado.nevoa)
  const invalidate = useThree((estado) => estado.invalidate)
  const largura = nevoa?.largura ?? 0
  const altura = nevoa?.altura ?? 0

  const textura = useMemo(() => {
    if (largura === 0 || altura === 0) return null
    const nova = new DataTexture(new Uint8Array(largura * altura * 4), largura, altura, RGBAFormat)
    nova.magFilter = LinearFilter
    nova.minFilter = LinearFilter
    return nova
  }, [largura, altura])
  useEffect(() => () => textura?.dispose(), [textura])

  useLayoutEffect(() => {
    if (!nevoa || !textura) return
    const dados = textura.image.data as Uint8Array
    const opacidade = nevoa.translucida ? OPACIDADE_DO_MESTRE : OPACIDADE_DO_JOGADOR
    for (let y = 0; y < altura; y++) {
      // A primeira linha da textura fica embaixo (v = 0), que no plano deitado é a última linha do mapa.
      const linha = (altura - 1 - y) * largura
      for (let x = 0; x < largura; x++) {
        const estado = nevoa.estado[y * largura + x]
        const i = (linha + x) * 4
        dados[i] = COR_DA_NEVOA[0]
        dados[i + 1] = COR_DA_NEVOA[1]
        dados[i + 2] = COR_DA_NEVOA[2]
        dados[i + 3] = opacidade[estado === A_VISTA ? 2 : estado === EXPLORADA ? 1 : 0]
      }
    }
    textura.needsUpdate = true
    invalidate()
  }, [nevoa, textura, largura, altura, invalidate])

  if (!nevoa || !textura) return null

  return (
    <mesh position={[largura / 2, ALTURA_DA_NEVOA, altura / 2]} rotation-x={-Math.PI / 2} renderOrder={2}>
      <planeGeometry args={[largura, altura]} />
      <meshBasicMaterial map={textura} transparent depthWrite={false} />
    </mesh>
  )
}
