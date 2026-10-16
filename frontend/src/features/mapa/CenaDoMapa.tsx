import { Canvas, useThree } from '@react-three/fiber'
import { useEffect, useLayoutEffect, useMemo, useRef } from 'react'
import { DirectionalLight, InstancedMesh, MeshBasicMaterial, Object3D, PlaneGeometry } from 'three'
import { CameraDoMapa } from './CameraDoMapa'
import { useEditorStore } from './editorStore'
import { EstruturaNoMapa } from './EstruturaNoMapa'
import { FundoDoMapa } from './FundoDoMapa'
import { ALTURA_DO_ALCANCE, RAIO_DO_TOKEN } from './geometria'
import { Grade } from './Grade'
import { Interacao, type AoPingar } from './Interacao'
import { definirPorta, type Estrutura } from './mapaApi'
import { alcanceAtual, useMapaStore } from './mapaStore'
import { alcasDe, criarModoDeEdicao } from './modoDeEdicao'
import { criarModoDaNevoa } from './modoDaNevoa'
import { criarModoDeFundo } from './modoDeFundo'
import { criarModoDeJogo } from './modoDeJogo'
import { NevoaNoMapa } from './NevoaNoMapa'
import { PingsNoMapa } from './PingsNoMapa'
import { useRig } from './rig'
import { texturaDoDisco } from './texturas'
import { TokenNoMapa } from './TokenNoMapa'
import { useVisaoStore } from './visaoStore'

interface CenaDoMapaProps {
  meuUsuarioId: string
  souMestre: boolean
  /** Movimentação livre: o jogador também arrasta o próprio token, e não há movimento por blocos. */
  movimentoLivre: boolean
  /** Arraste livre: chamado ao soltar um token em uma célula livre diferente da atual. */
  aoMover: (tokenId: string, x: number, y: number) => void
  /** Chamado ao clicar em uma célula do alcance do token em movimento por blocos. */
  aoMoverPorBlocos: (tokenId: string, x: number, y: number) => Promise<void>
  aoAvisar: (mensagem: string) => void
  /** Alt + clique ou segurar sobre o fundo: aponta um ponto do mapa para a mesa. */
  aoPingar: AoPingar
}

/**
 * A cena do mapa, com câmera ortográfica. O chão é o plano XZ (Y para cima) e a célula (x, y)
 * ocupa de (x, y) a (x + 1, y + 1) nesse plano. A câmera vai da visão superior à isométrica;
 * câmera e qualidade gráfica são de cada usuário.
 */
export default function CenaDoMapa(props: CenaDoMapaProps) {
  const mapa = useMapaStore((estado) => estado.mapa)
  const qualidade = useVisaoStore((estado) => estado.qualidade)
  if (!mapa) return null

  const media = qualidade === 'media'
  return (
    <Canvas
      // Antialiasing e sombras são definidos na criação do contexto gráfico: trocar a qualidade recria a cena.
      key={qualidade}
      className="mapa-canvas"
      orthographic
      flat
      frameloop="demand"
      dpr={media ? [1, 2] : 1}
      shadows={media ? 'percentage' : false}
      gl={{ antialias: media }}
      camera={{ position: [0, 100, 0], near: 0.1, far: 400, zoom: 40 }}
      aria-label={`Mapa ${mapa.nome}, grade de ${mapa.largura} por ${mapa.altura} células`}
    >
      <color attach="background" args={['#0f1a1d']} />
      <CameraDoMapa>
        <Luzes largura={mapa.largura} altura={mapa.altura} sombras={media} />
        <Grade largura={mapa.largura} altura={mapa.altura} corFundo={mapa.corFundo} />
        <FundoDoMapa />
        {media && <SombraNoChao largura={mapa.largura} altura={mapa.altura} />}
        <Estruturas sombras={media} />
        <NevoaNoMapa />
        <AlcanceDoToken />
        <Tokens meuUsuarioId={props.meuUsuarioId} />
        <FantasmaDoArraste />
        <PreviaDoEditor />
        <PingsNoMapa />
        <Interacoes {...props} />
      </CameraDoMapa>
    </Canvas>
  )
}

/** Uma luz ambiente e uma direcional fraca, vinda de cima e de um lado, para dar volume às caixas. */
function Luzes({ largura, altura, sombras }: { largura: number; altura: number; sombras: boolean }) {
  const direcional = useMemo(() => new DirectionalLight('#fff4e0', 1.5), [])
  useEffect(() => () => direcional.dispose(), [direcional])

  const [centroX, centroZ] = [largura / 2, altura / 2]
  const alcance = Math.hypot(largura, altura) / 2 + 4
  // Luz alta: sombras curtas, que dão volume sem cobrir as células vizinhas.
  direcional.position.set(centroX - alcance * 0.28, alcance * 1.3, centroZ - alcance * 0.42)
  direcional.target.position.set(centroX, 0, centroZ)
  direcional.castShadow = sombras
  if (sombras) {
    const camera = direcional.shadow.camera
    camera.left = camera.bottom = -alcance
    camera.right = camera.top = alcance
    camera.near = 0.5
    camera.far = alcance * 3
    camera.updateProjectionMatrix()
    direcional.shadow.mapSize.set(2048, 2048)
    direcional.shadow.bias = -0.0005
    direcional.shadow.normalBias = 0.03
  }

  return (
    <>
      <ambientLight intensity={2.1} />
      <primitive object={direcional} />
      <primitive object={direcional.target} />
    </>
  )
}

/** Plano invisível que só recebe a sombra das estruturas (a grade é um shader próprio e não a receberia). */
function SombraNoChao({ largura, altura }: { largura: number; altura: number }) {
  return (
    <mesh position={[largura / 2, 0.004, altura / 2]} rotation-x={-Math.PI / 2} receiveShadow>
      <planeGeometry args={[largura, altura]} />
      <shadowMaterial transparent opacity={0.26} depthWrite={false} />
    </mesh>
  )
}

function Estruturas({ sombras }: { sombras: boolean }) {
  const ordem = useMapaStore((estado) => estado.ordemDasEstruturas)
  return ordem.map((id) => <EstruturaNoMapa key={id} id={id} sombras={sombras} />)
}

function Tokens({ meuUsuarioId }: { meuUsuarioId: string }) {
  const ordem = useMapaStore((estado) => estado.ordem)
  return ordem.map((id) => <TokenNoMapa key={id} id={id} meuUsuarioId={meuUsuarioId} />)
}

const CELULA = new PlaneGeometry(0.94, 0.94)
const DISCO_FANTASMA = new PlaneGeometry(RAIO_DO_TOKEN * 2, RAIO_DO_TOKEN * 2)
const QUADRADO = new PlaneGeometry(1, 1)

/**
 * Durante o arraste, marca a célula de destino de cada token arrastado (o do ponteiro e os do grupo):
 * verde se o arraste pode ser solto ali, vermelha se não.
 */
function FantasmaDoArraste() {
  const arraste = useMapaStore((estado) => estado.arraste)
  if (!arraste) return null

  return [arraste, ...(arraste.grupo ?? [])].map((destino) => (
    <FantasmaDoToken key={destino.tokenId} tokenId={destino.tokenId} x={destino.x} y={destino.y} valido={arraste.valido} />
  ))
}

function FantasmaDoToken({ tokenId, x, y, valido }: { tokenId: string; x: number; y: number; valido: boolean }) {
  const token = useMapaStore((estado) => estado.tokens[tokenId])
  if (!token) return null
  const arraste = { x, y, valido }

  return (
    <group position={[arraste.x + 0.5, 0.01, arraste.y + 0.5]} rotation-x={-Math.PI / 2}>
      <mesh geometry={CELULA} renderOrder={4}>
        <meshBasicMaterial color={arraste.valido ? '#5fd38a' : '#f08a75'} transparent opacity={0.4} depthWrite={false} />
      </mesh>
      {arraste.valido && (
        <mesh geometry={DISCO_FANTASMA} position-z={0.004} renderOrder={5}>
          <meshBasicMaterial
            map={texturaDoDisco(token.tipo, token.icone, token.corBorda, token.imagemId)}
            transparent
            opacity={0.7}
            depthWrite={false}
          />
        </mesh>
      )}
    </group>
  )
}

const COR_DO_ALCANCE = '#3d8bfd'
const MATERIAL_DO_ALCANCE = new MeshBasicMaterial({ color: COR_DO_ALCANCE, transparent: true, opacity: 0.32, depthWrite: false })
const MATERIAL_DO_PAIRANDO = new MeshBasicMaterial({ color: '#8cc0ff', transparent: true, opacity: 0.5, depthWrite: false })

/**
 * As células que o token em movimento por blocos alcança, em azul translúcido (como no FFT), uma
 * instância por célula; a célula sob o ponteiro fica mais forte. Só aparece na tela de quem está
 * movendo. Fica entre o chão das estruturas e o disco do token, e as paredes a cobrem.
 */
function AlcanceDoToken() {
  const mapa = useMapaStore((estado) => estado.mapa)
  const tokens = useMapaStore((estado) => estado.tokens)
  const estruturas = useMapaStore((estado) => estado.estruturas)
  const movendoId = useMapaStore((estado) => estado.movendoId)
  const nevoa = useMapaStore((estado) => estado.nevoa)
  const pairando = useMapaStore((estado) => estado.pairando)
  const jogando = useEditorStore((estado) => estado.modo === 'jogar')
  const invalidate = useThree((estado) => estado.invalidate)
  const malha = useRef<InstancedMesh>(null)

  const celulas = useMemo(
    () => (jogando ? alcanceAtual({ mapa, tokens, estruturas, movendoId, nevoa }) : null),
    [jogando, mapa, tokens, estruturas, movendoId, nevoa],
  )
  // Cabe o mapa inteiro: o número de instâncias desenhadas muda sem recriar a malha.
  const capacidade = mapa ? mapa.largura * mapa.altura : 1

  useLayoutEffect(() => {
    const instancias = malha.current
    if (!instancias) return
    const auxiliar = new Object3D()
    auxiliar.rotation.x = -Math.PI / 2
    let i = 0
    for (const chave of celulas?.keys() ?? []) {
      const [x, y] = chave.split(',').map(Number)
      auxiliar.position.set(x + 0.5, ALTURA_DO_ALCANCE, y + 0.5)
      auxiliar.updateMatrix()
      instancias.setMatrixAt(i++, auxiliar.matrix)
    }
    instancias.count = i
    instancias.instanceMatrix.needsUpdate = true
    invalidate()
  }, [celulas, capacidade, invalidate])

  const [px, py] = pairando && celulas?.has(pairando) ? pairando.split(',').map(Number) : [null, null]
  return (
    <>
      <instancedMesh
        key={capacidade}
        ref={malha}
        args={[CELULA, MATERIAL_DO_ALCANCE, capacidade]}
        renderOrder={3}
        frustumCulled={false}
      />
      {px !== null && (
        <mesh
          geometry={CELULA}
          material={MATERIAL_DO_PAIRANDO}
          position={[px + 0.5, ALTURA_DO_ALCANCE + 0.001, py! + 0.5]}
          rotation-x={-Math.PI / 2}
          renderOrder={3}
        />
      )}
    </>
  )
}

/**
 * No modo de edição: a área que está sendo desenhada, movida ou redimensionada e, na estrutura
 * selecionada, as alças no meio de cada borda. Desenhadas por cima da cena para não sumirem
 * atrás de uma parede alta.
 */
function PreviaDoEditor() {
  const previa = useEditorStore((estado) => estado.previa)
  const comAlcas = useEditorStore((estado) => estado.modo === 'editar' && estado.ferramenta === 'selecionar')
  const selecionadaId = useEditorStore((estado) => estado.selecionadaId)
  const selecionada = useMapaStore((estado) => (selecionadaId ? estado.estruturas[selecionadaId] : undefined))
  const area = previa ?? (comAlcas ? selecionada : undefined)

  return (
    <>
      {previa && (
        <mesh
          geometry={QUADRADO}
          position={[previa.x + previa.largura / 2, 0.02, previa.y + previa.profundidade / 2]}
          rotation-x={-Math.PI / 2}
          scale={[previa.largura, previa.profundidade, 1]}
          renderOrder={21}
        >
          <meshBasicMaterial color={previa.valido ? '#5fd38a' : '#f08a75'} transparent opacity={0.45} depthTest={false} depthWrite={false} />
        </mesh>
      )}
      {comAlcas &&
        area &&
        alcasDe(area).map((alca) => (
          <mesh
            key={alca.borda}
            geometry={QUADRADO}
            position={[alca.x, 0.03, alca.z]}
            rotation-x={-Math.PI / 2}
            scale={[0.26, 0.26, 1]}
            renderOrder={22}
          >
            <meshBasicMaterial color="#e2b23c" depthTest={false} depthWrite={false} />
          </mesh>
        ))}
    </>
  )
}

/** Escolhe o que os ponteiros fazem conforme o modo: jogar, editar o mapa ou ajustar o fundo. */
function Interacoes({ meuUsuarioId, souMestre, movimentoLivre, aoMover, aoMoverPorBlocos, aoAvisar, aoPingar }: CenaDoMapaProps) {
  const rig = useRig()
  const modo = useEditorStore((estado) => estado.modo)

  const interacao = useMemo(() => {
    if (modo === 'editar') return criarModoDeEdicao(rig, aoMover)
    if (modo === 'fundo') return criarModoDeFundo(rig)
    if (modo === 'nevoa') return criarModoDaNevoa(rig, aoAvisar)
    return criarModoDeJogo(rig, {
      meuUsuarioId,
      souMestre,
      movimentoLivre,
      aoMover,
      aoMoverPorBlocos,
      aoAlternarPorta: (porta: Estrutura) => {
        definirPorta(porta.id, !porta.portaAberta)
          .then((atualizada) => useMapaStore.getState().aplicarEstrutura(atualizada))
          .catch((erro: Error) => aoAvisar(erro.message || 'Não foi possível abrir ou fechar a porta.'))
      },
    })
  }, [rig, modo, meuUsuarioId, souMestre, movimentoLivre, aoMover, aoMoverPorBlocos, aoAvisar])

  return <Interacao modo={interacao} aoPingar={aoPingar} />
}
