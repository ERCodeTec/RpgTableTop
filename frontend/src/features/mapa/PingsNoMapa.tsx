import { useFrame, useThree } from '@react-three/fiber'
import { useEffect, useRef } from 'react'
import { CircleGeometry, Mesh, MeshBasicMaterial, RingGeometry, Sprite, Vector2 } from 'three'
import { useMapaStore } from './mapaStore'
import { DURACAO_DO_PING_MS, usePingStore, type Ping } from './pingStore'
import { useRig } from './rig'
import { PROPORCAO_DO_NOME, texturaDoNome } from './texturas'

const ANEL = new RingGeometry(0.86, 1, 48)
const CENTRO = new CircleGeometry(0.16, 24)
/** Ondas por ping, cada uma saindo um pouco depois da anterior. */
const ONDAS = 3
const RAIO_MAXIMO = 1.6
const ALTURA_DA_ETIQUETA = 0.3
/** A etiqueta fica acima do ponto na tela, não em cima dele. */
const ANCORA_DA_ETIQUETA = new Vector2(0.5, -0.9)

const semAnimacao = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches

/**
 * Os pings do mapa na tela: ondas que se abrem a partir do ponto, na cor de quem pingou, com o
 * nome acima. Desenhados por cima de tudo (paredes e névoa inclusive), pois servem para apontar.
 * Também leva a câmera até o ponto quando o Mestre pede foco.
 */
export function PingsNoMapa() {
  const mapaId = useMapaStore((estado) => estado.mapa?.id)
  const pings = usePingStore((estado) => estado.pings)
  const foco = usePingStore((estado) => estado.foco)
  const rig = useRig()

  useEffect(() => {
    if (foco) rig.centralizar(foco.x, foco.y, !semAnimacao())
  }, [rig, foco])

  return pings.filter((ping) => ping.mapaId === mapaId).map((ping) => <PingNoMapa key={ping.id} ping={ping} />)
}

function PingNoMapa({ ping }: { ping: Ping }) {
  const invalidate = useThree((estado) => estado.invalidate)
  const ondas = useRef<(Mesh | null)[]>([])
  const centro = useRef<Mesh>(null)
  const etiqueta = useRef<Sprite>(null)
  const parado = useRef(semAnimacao())

  // Com o frameloop sob demanda, cada quadro pede o próximo enquanto o ping existe.
  useFrame(() => {
    const t = Math.min(1, (performance.now() - ping.inicio) / DURACAO_DO_PING_MS)
    const sumindo = t > 0.75 ? 1 - (t - 0.75) / 0.25 : 1

    ondas.current.forEach((onda, i) => {
      if (!onda) return
      // Sem animação, as ondas ficam paradas em raios fixos e só esmaecem no fim.
      const fase = parado.current ? (i + 1) / (ONDAS + 1) : (t * 2.2 - i * 0.3) % 1
      const visivel = parado.current || t * 2.2 >= i * 0.3
      const raio = 0.2 + fase * RAIO_MAXIMO
      onda.scale.set(raio, raio, 1)
      ;(onda.material as MeshBasicMaterial).opacity = visivel ? (1 - fase) * 0.9 * sumindo : 0
    })
    if (centro.current) (centro.current.material as MeshBasicMaterial).opacity = 0.95 * sumindo
    if (etiqueta.current) etiqueta.current.material.opacity = sumindo
    if (t < 1) invalidate()
  })

  return (
    <group position={[ping.x, 0.05, ping.y]}>
      <group rotation-x={-Math.PI / 2}>
        {Array.from({ length: ONDAS }, (_, i) => (
          <mesh
            key={i}
            ref={(malha) => {
              ondas.current[i] = malha
            }}
            geometry={ANEL}
            renderOrder={30}
          >
            <meshBasicMaterial color={ping.cor} transparent opacity={0} depthTest={false} depthWrite={false} />
          </mesh>
        ))}
        <mesh ref={centro} geometry={CENTRO} renderOrder={31}>
          <meshBasicMaterial color={ping.cor} transparent depthTest={false} depthWrite={false} />
        </mesh>
      </group>
      <sprite
        ref={etiqueta}
        center={ANCORA_DA_ETIQUETA}
        scale={[ALTURA_DA_ETIQUETA * PROPORCAO_DO_NOME, ALTURA_DA_ETIQUETA, 1]}
        renderOrder={32}
      >
        <spriteMaterial map={texturaDoNome(ping.nome)} transparent depthTest={false} depthWrite={false} />
      </sprite>
    </group>
  )
}
