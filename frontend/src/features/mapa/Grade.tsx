import { useMemo } from 'react'
import { ShaderMaterial, Vector2 } from 'three'

const VERTICE = /* glsl */ `
  varying vec2 vUv;
  void main() {
    vUv = uv;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
`

// As linhas são calculadas por pixel a partir da coordenada da célula, com largura constante
// na tela em qualquer zoom e inclinação. A cada 5 células a linha é mais forte, para facilitar a
// contagem. Só as linhas são desenhadas; o resto é transparente, sobre a cor e a imagem de fundo.
const FRAGMENTO = /* glsl */ `
  uniform vec2 tamanho;
  varying vec2 vUv;

  float linha(vec2 coordenada) {
    vec2 distancia = abs(fract(coordenada - 0.5) - 0.5) / fwidth(coordenada);
    return 1.0 - min(min(distancia.x, distancia.y), 1.0);
  }

  void main() {
    vec2 celula = vUv * tamanho;
    float intensidade = max(linha(celula) * 0.2, linha(celula / 5.0) * 0.36);
    gl_FragColor = vec4(1.0, 1.0, 1.0, intensidade);
  }
`

interface GradeProps {
  /** Em células. */
  largura: number
  altura: number
  corFundo: string
}

/**
 * O chão do mapa: um plano com a cor de fundo e, por cima, a grade desenhada no shader
 * (uma draw call para o grid inteiro). A imagem de fundo, quando existe, fica entre os dois.
 */
export function Grade({ largura, altura, corFundo }: GradeProps) {
  const linhas = useMemo(
    () =>
      new ShaderMaterial({
        vertexShader: VERTICE,
        fragmentShader: FRAGMENTO,
        uniforms: { tamanho: { value: new Vector2() } },
        transparent: true,
        depthWrite: false,
      }),
    [],
  )
  linhas.uniforms.tamanho.value.set(largura, altura)

  return (
    <group position={[largura / 2, 0, altura / 2]} rotation-x={-Math.PI / 2}>
      <mesh>
        <planeGeometry args={[largura, altura]} />
        <meshBasicMaterial color={corFundo} />
      </mesh>
      <mesh position-z={0.006} material={linhas} renderOrder={1}>
        <planeGeometry args={[largura, altura]} />
      </mesh>
    </group>
  )
}
