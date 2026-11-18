import { useFrame, useThree } from '@react-three/fiber'
import { memo, useLayoutEffect, useRef } from 'react'
import { CircleGeometry, Color, PlaneGeometry, RingGeometry, type Group, type Mesh, type MeshBasicMaterial } from 'three'
import { elevacaoDoToken, escalaDoToken, ladoDoToken, RAIO_DO_TOKEN } from './geometria'
import { useMapaStore } from './mapaStore'
import { tokenEscondido } from './nevoa'
import { useRig } from './rig'
import { PROPORCAO_DO_NOME, texturaDoDisco, texturaDoNome } from './texturas'

const DURACAO_DO_MOVIMENTO_MS = 200
// Disco e etiqueta somam meia célula para cada lado do centro: na visão superior, o nome não invade o token de baixo.
const ALTURA_DO_NOME = 0.24

// Geometrias compartilhadas por todos os tokens.
const DISCO = new CircleGeometry(RAIO_DO_TOKEN, 48)

interface Aneis {
  proprio: RingGeometry
  selecao: RingGeometry
  /** Marca dos objetos de interação: oito arcos em volta do disco, que lembram "dá para examinar". */
  interacao: RingGeometry[]
}

/**
 * Os anéis em volta de um disco de `raio`: a espessura não acompanha o tamanho do token, então
 * cada raio tem os seus, compartilhados pelos tokens do mesmo tamanho.
 */
const aneis = new Map<number, Aneis>()
function aneisDoRaio(raio: number): Aneis {
  let existentes = aneis.get(raio)
  if (!existentes) {
    const segmentos = Math.round(48 * Math.max(1, raio / RAIO_DO_TOKEN))
    existentes = {
      proprio: new RingGeometry(raio + 0.01, raio + 0.06, segmentos),
      selecao: new RingGeometry(raio + 0.07, raio + 0.1, segmentos),
      interacao: Array.from(
        { length: 8 },
        (_, i) => new RingGeometry(raio + 0.015, raio + 0.055, 8, 1, (i * Math.PI) / 4, Math.PI / 6),
      ),
    }
    aneis.set(raio, existentes)
  }
  return existentes
}
const ETIQUETA = new PlaneGeometry(ALTURA_DO_NOME * PROPORCAO_DO_NOME, ALTURA_DO_NOME)
const RETANGULO = new PlaneGeometry(1, 1)

// Barra de HP, logo acima do disco.
const LARGURA_DA_BARRA = 0.64
const ALTURA_DA_BARRA = 0.07
// Acima da borda do disco, que cresce com o tamanho do token.
const FOLGA_DA_BARRA = 0.09
const VERMELHO = new Color('#e05a47')
const AMARELO = new Color('#e2b23c')
const VERDE = new Color('#5fd38a')

/** Verde com o HP cheio, amarelo na metade e vermelho perto de zero, passando de um ao outro aos poucos. */
function corDoHp(fracao: number, alvo: Color): Color {
  return fracao >= 0.5 ? alvo.lerpColors(AMARELO, VERDE, (fracao - 0.5) * 2) : alvo.lerpColors(VERMELHO, AMARELO, fracao * 2)
}

/**
 * A fração de HP que este usuário pode ver: exata para quem recebe os números, em faixas de 5%
 * para quem recebe só a barra, e null para quem não recebe nada.
 */
function fracaoDeHp(token: { hpAtual: number | null; hpMaximo: number | null; hpPercentual: number | null }): number | null {
  if (token.hpAtual !== null && token.hpMaximo) return token.hpAtual / token.hpMaximo
  return token.hpPercentual === null ? null : token.hpPercentual / 100
}

const semAnimacao = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches

interface TokenNoMapaProps {
  id: string
  meuUsuarioId: string
}

/**
 * Um token no mapa. Assina só o próprio registro no store, então o movimento de outro token não o
 * renderiza de novo; a posição é interpolada no laço de quadros, fora do React.
 * O corpo é um billboard: fica sempre de frente para a câmera, deitado na visão superior e em pé
 * conforme ela inclina. O disco ocupa o espaço do tamanho do token (2x2 no Grande, menor no
 * Minúsculo), centrado nele; a barra de HP e o nome mantêm o tamanho e acompanham a borda do disco.
 * Mostra só o que o servidor enviou a este usuário: a barra e os números de HP aparecem para
 * quem os recebeu, e um token oculto (que só chega ao Mestre e ao dono) fica translúcido.
 */
export const TokenNoMapa = memo(function TokenNoMapa({ id, meuUsuarioId }: TokenNoMapaProps) {
  const token = useMapaStore((estado) => estado.tokens[id])
  const selecionado = useMapaStore((estado) => estado.selecionadoId === id || estado.grupo.includes(id))
  const arrastando = useMapaStore(
    (estado) => estado.arraste !== null && (estado.arraste.tokenId === id || estado.arraste.grupo?.some((g) => g.tokenId === id) === true),
  )
  // Fora da vista, na névoa: o token continua acompanhando a posição, só não é desenhado.
  const escondido = useMapaStore((estado) => estado.tokens[id] !== undefined && tokenEscondido(estado.nevoa, estado.tokens[id]))
  const invalidate = useThree((estado) => estado.invalidate)
  const rig = useRig()
  const grupo = useRef<Group>(null)
  const corpo = useRef<Group>(null)
  const animacao = useRef<{ deX: number; deZ: number; ateX: number; ateZ: number; inicio: number } | null>(null)
  const posicionado = useRef(false)
  const preenchimento = useRef<Mesh>(null)
  const fracaoNaTela = useRef<number | null>(null)

  // A Interação não tem HP: nem o Mestre vê barra nela.
  const fracao = token && token.tipo !== 'Interacao' ? fracaoDeHp(token) : null
  useLayoutEffect(() => invalidate(), [fracao, escondido, invalidate])

  const x = token?.x ?? 0
  const y = token?.y ?? 0
  const lado = token ? ladoDoToken(token) : 1
  const escala = token ? escalaDoToken(token) : 1
  const raio = RAIO_DO_TOKEN * escala
  const anel = aneisDoRaio(raio)
  useLayoutEffect(() => invalidate(), [escala, invalidate])

  useLayoutEffect(() => {
    const alvo = grupo.current
    if (!alvo) return
    const ateX = x + lado / 2
    const ateZ = y + lado / 2
    if (!posicionado.current || semAnimacao()) {
      alvo.position.set(ateX, 0, ateZ)
      animacao.current = null
      posicionado.current = true
    } else {
      animacao.current = { deX: alvo.position.x, deZ: alvo.position.z, ateX, ateZ, inicio: performance.now() }
    }
    invalidate()
  }, [x, y, lado, invalidate])

  useFrame(({ camera }, delta) => {
    // A barra de HP desliza até o novo valor, mudando de cor no caminho.
    const barra = preenchimento.current
    if (barra && fracao !== null) {
      const atual = fracaoNaTela.current ?? fracao
      const proxima = Math.abs(fracao - atual) < 0.002 || semAnimacao() ? fracao : atual + (fracao - atual) * Math.min(1, delta * 8)
      fracaoNaTela.current = proxima
      const largura = Math.max(proxima, 0.001) * LARGURA_DA_BARRA
      barra.scale.x = largura
      barra.position.x = (largura - LARGURA_DA_BARRA) / 2
      corDoHp(proxima, (barra.material as MeshBasicMaterial).color)
      if (proxima !== fracao) invalidate()
    }

    // Billboard: mesma orientação da câmera, e sobe o bastante para a borda de baixo não afundar no chão.
    if (corpo.current) {
      corpo.current.quaternion.copy(camera.quaternion)
      corpo.current.position.y = elevacaoDoToken(rig.inclinacao, escala)
    }

    const atual = animacao.current
    const alvo = grupo.current
    if (!atual || !alvo) return
    const progresso = Math.min(1, (performance.now() - atual.inicio) / DURACAO_DO_MOVIMENTO_MS)
    const suave = 1 - (1 - progresso) ** 3
    alvo.position.x = atual.deX + (atual.ateX - atual.deX) * suave
    alvo.position.z = atual.deZ + (atual.ateZ - atual.deZ) * suave
    if (progresso < 1) invalidate()
    else animacao.current = null
  })

  if (!token) return null

  // Translúcido enquanto é arrastado, e também quando está oculto dos jogadores.
  const translucido = arrastando || !token.visivel
  // O estado vai escrito na etiqueta: "caído" e "oculto" não dependem só da cor do disco.
  const interacao = token.tipo === 'Interacao'
  const rotulo = [
    token.nome,
    interacao ? (token.revelado ? 'revelado' : null) : token.caido ? 'caído' : token.hpAtual !== null ? `${token.hpAtual}/${token.hpMaximo}` : null,
    token.visivel ? null : 'oculto',
  ]
    .filter(Boolean)
    .join(' · ')

  return (
    <group ref={grupo} visible={!escondido}>
      <group ref={corpo}>
        {token.donoUsuarioId === meuUsuarioId && (
          <mesh geometry={anel.proprio} position-z={-0.004}>
            <meshBasicMaterial color="#e2b23c" />
          </mesh>
        )}
        {interacao &&
          anel.interacao.map((arco, i) => (
            <mesh key={i} geometry={arco} position-z={-0.004}>
              {/* Revelado a todos, o anel acende. */}
              <meshBasicMaterial color={token.revelado ? '#ffe08a' : '#8fc7d6'} />
            </mesh>
          ))}
        {selecionado && (
          <mesh geometry={anel.selecao} position-z={-0.004}>
            <meshBasicMaterial color="#ffffff" />
          </mesh>
        )}
        {/* Só o disco e a marca de caído crescem com o tamanho; os anéis têm espessura fixa. */}
        <group scale={escala}>
          <mesh geometry={DISCO}>
            {/* O recorte por alfa grava profundidade: uma parede alta na frente esconde parte do disco,
                sem ordenar nada à mão. Durante o arraste o disco fica translúcido, e aí não grava. */}
            <meshBasicMaterial
              key={translucido ? 'translucido' : 'opaco'}
              map={texturaDoDisco(token.tipo, token.icone, token.corBorda, token.imagemId, escala)}
              // Caído: o disco escurece e perde a cor.
              color={token.caido ? '#6f6f6f' : '#ffffff'}
              transparent={translucido}
              opacity={arrastando ? 0.4 : translucido ? 0.5 : 1}
              alphaTest={translucido ? 0 : 0.5}
              depthWrite={!translucido}
            />
          </mesh>
          {token.caido &&
            [1, -1].map((sentido) => (
              // Um X sobre o disco: caído se reconhece pela marca, não só pelo tom.
              <mesh
                key={sentido}
                geometry={RETANGULO}
                position-z={0.006}
                rotation-z={(sentido * Math.PI) / 4}
                scale={[RAIO_DO_TOKEN * 1.5, 0.07, 1]}
              >
                <meshBasicMaterial color="#f08a75" />
              </mesh>
            ))}
        </group>
        {fracao !== null && (
          <group position-y={raio + FOLGA_DA_BARRA}>
            <mesh geometry={RETANGULO} scale={[LARGURA_DA_BARRA + 0.04, ALTURA_DA_BARRA + 0.04, 1]}>
              <meshBasicMaterial color="#0f1a1d" />
            </mesh>
            <mesh ref={preenchimento} geometry={RETANGULO} position-z={0.004} scale={[LARGURA_DA_BARRA, ALTURA_DA_BARRA, 1]}>
              <meshBasicMaterial color="#5fd38a" />
            </mesh>
          </group>
        )}
        {/* O nome fica sempre legível: é desenhado por cima da cena, sem teste de profundidade. */}
        <mesh geometry={ETIQUETA} position={[0, -(raio + ALTURA_DO_NOME / 2), 0]} renderOrder={10}>
          <meshBasicMaterial map={texturaDoNome(rotulo)} transparent depthTest={false} depthWrite={false} />
        </mesh>
      </group>
    </group>
  )
})
