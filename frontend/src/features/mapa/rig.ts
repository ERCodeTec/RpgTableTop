import { createContext, useContext } from 'react'
import { Plane, Raycaster, Vector2, Vector3, type OrthographicCamera, type Ray } from 'three'

const CHAO = new Plane(new Vector3(0, 1, 0), 0)
const DISTANCIA = 100
const DURACAO_DA_TRANSICAO_MS = 350

const suavizar = (t: number) => (t < 0.5 ? 4 * t * t * t : 1 - (-2 * t + 2) ** 3 / 2)

/**
 * A câmera do mapa: ortográfica, olhando para um ponto do chão (o alvo), com inclinação a partir
 * da visão superior e giro em torno do eixo vertical. É estado local de cada usuário.
 * Também converte posições da tela em pontos do chão, o que vale para qualquer ângulo.
 */
export class Rig {
  /** Ponto do chão no centro da tela. */
  readonly alvo = new Vector3()
  /** Inclinação atual, em radianos: 0 olha de cima. */
  inclinacao = 0
  /** Giro atual, em radianos. */
  rotacao = 0
  /** Dimensões do mapa, em células, para limitar o alvo. */
  largura = 1
  altura = 1
  /** Tamanho da área de desenho, em pixels. */
  tela = { width: 1, height: 1 }
  /**
   * true enquanto o usuário não deslocou nem aproximou desde o último enquadramento: nesse caso o
   * mapa continua inteiro na tela quando a câmera inclina, gira ou a janela muda de tamanho.
   */
  private enquadrado = false

  private transicao: { deInclinacao: number; deRotacao: number; ateInclinacao: number; ateRotacao: number; inicio: number } | null = null
  private readonly raycaster = new Raycaster()
  private readonly ndc = new Vector2()
  private readonly auxiliar = new Vector3()

  constructor(
    readonly camera: OrthographicCamera,
    readonly canvas: HTMLCanvasElement,
    readonly invalidate: () => void,
  ) {}

  /** Posiciona a câmera a partir do alvo, da inclinação e do giro. */
  aplicar(): void {
    const { camera, alvo, inclinacao, rotacao } = this
    const horizontal = Math.sin(inclinacao) * DISTANCIA
    camera.position.set(
      alvo.x + horizontal * Math.sin(rotacao),
      Math.cos(inclinacao) * DISTANCIA,
      alvo.z + horizontal * Math.cos(rotacao),
    )
    // "Para cima" na tela é a direção do chão que se afasta da câmera; assim a orientação é
    // contínua de 0° (onde olhar reto para baixo não define um "para cima") até a inclinação máxima.
    camera.up.set(-Math.sin(rotacao), 0, -Math.cos(rotacao))
    camera.lookAt(alvo)
    camera.updateMatrixWorld()
    this.invalidate()
  }

  /** Leva a câmera aos ângulos dados (radianos), com transição suave ou na hora. */
  girarPara(inclinacao: number, rotacao: number, animar: boolean): void {
    if (!animar) {
      this.transicao = null
      this.inclinacao = inclinacao
      this.rotacao = rotacao
      this.manterEnquadrado()
      this.aplicar()
      return
    }
    this.transicao = {
      deInclinacao: this.inclinacao,
      deRotacao: this.rotacao,
      ateInclinacao: inclinacao,
      ateRotacao: rotacao,
      inicio: performance.now(),
    }
    this.invalidate()
  }

  /** Avança a transição em curso. Chamado a cada quadro; devolve true enquanto ainda há o que animar. */
  avancar(agora: number): boolean {
    const t = this.transicao
    if (!t) return false
    const progresso = Math.min(1, (agora - t.inicio) / DURACAO_DA_TRANSICAO_MS)
    const s = suavizar(progresso)
    this.inclinacao = t.deInclinacao + (t.ateInclinacao - t.deInclinacao) * s
    this.rotacao = t.deRotacao + (t.ateRotacao - t.deRotacao) * s
    if (progresso >= 1) this.transicao = null
    this.manterEnquadrado()
    this.aplicar()
    return this.transicao !== null
  }

  /** O raio da câmera que passa por uma posição da tela. */
  raio(clienteX: number, clienteY: number): Ray {
    const caixa = this.canvas.getBoundingClientRect()
    this.ndc.set(((clienteX - caixa.left) / caixa.width) * 2 - 1, -((clienteY - caixa.top) / caixa.height) * 2 + 1)
    this.camera.updateMatrixWorld()
    this.raycaster.setFromCamera(this.ndc, this.camera)
    return this.raycaster.ray
  }

  /** O ponto do chão sob uma posição da tela. */
  chao(clienteX: number, clienteY: number, alvo: Vector3): Vector3 {
    return this.raio(clienteX, clienteY).intersectPlane(CHAO, alvo) ?? alvo.copy(this.alvo)
  }

  /** A célula sob uma posição da tela (pode ficar fora do mapa). */
  celula(clienteX: number, clienteY: number): { x: number; y: number } {
    const ponto = this.chao(clienteX, clienteY, this.auxiliar)
    return { x: Math.floor(ponto.x), y: Math.floor(ponto.z) }
  }

  /** Desloca o alvo para que o ponto do chão `ancora` fique sob a posição da tela dada. */
  manterSobOPonteiro(ancora: Vector3, clienteX: number, clienteY: number): void {
    const agora = this.chao(clienteX, clienteY, this.auxiliar)
    this.enquadrado = false
    this.alvo.x = Math.min(this.largura, Math.max(0, this.alvo.x + ancora.x - agora.x))
    this.alvo.z = Math.min(this.altura, Math.max(0, this.alvo.z + ancora.z - agora.z))
    this.aplicar()
  }

  /** Multiplica o zoom dentro dos limites, sem mexer no alvo. */
  escalar(fator: number): void {
    const { minimo, maximo } = this.limitesDeZoom()
    this.camera.zoom = Math.min(maximo, Math.max(minimo, this.camera.zoom * fator))
    this.camera.updateProjectionMatrix()
    this.enquadrado = false
    this.invalidate()
  }

  /** Multiplica o zoom, mantendo fixo o ponto do chão sob a posição da tela dada. */
  aproximar(fator: number, clienteX: number, clienteY: number): void {
    const ancora = this.chao(clienteX, clienteY, new Vector3())
    this.escalar(fator)
    this.manterSobOPonteiro(ancora, clienteX, clienteY)
  }

  /**
   * Zoom (pixels por célula) que mostra o mapa inteiro nos ângulos dados, e os limites em torno
   * dele: dá para afastar um pouco além do mapa e aproximar até umas três células na tela.
   */
  limitesDeZoom(inclinacao = this.inclinacao, rotacao = this.rotacao): { enquadre: number; minimo: number; maximo: number } {
    const { width, height } = this.tela
    const [seno, cosseno] = [Math.abs(Math.sin(rotacao)), Math.abs(Math.cos(rotacao))]
    const naTelaX = this.largura * cosseno + this.altura * seno
    const naTelaY = (this.largura * seno + this.altura * cosseno) * Math.cos(inclinacao)
    const enquadre = Math.max(4, Math.min(width / (naTelaX + 1), height / (naTelaY + 1.5)))
    return { enquadre, minimo: enquadre * 0.6, maximo: Math.max(enquadre, Math.min(width, height) / 3) }
  }

  /** Centraliza o mapa e ajusta o zoom para ele caber, considerando os ângulos de destino. */
  enquadrar(inclinacao: number, rotacao: number): void {
    this.alvo.set(this.largura / 2, 0, this.altura / 2)
    this.camera.zoom = this.limitesDeZoom(inclinacao, rotacao).enquadre
    this.camera.updateProjectionMatrix()
    this.enquadrado = true
    this.aplicar()
  }

  private manterEnquadrado(): void {
    if (!this.enquadrado) return
    this.camera.zoom = this.limitesDeZoom().enquadre
    this.camera.updateProjectionMatrix()
  }

  /** Depois de mudar o tamanho da tela: mantém o enquadramento do usuário dentro dos novos limites. */
  ajustarAosLimites(): void {
    const { enquadre, minimo, maximo } = this.limitesDeZoom()
    this.camera.zoom = this.enquadrado ? enquadre : Math.min(maximo, Math.max(minimo, this.camera.zoom))
    this.camera.updateProjectionMatrix()
    this.invalidate()
  }
}

export const RigContext = createContext<Rig | null>(null)

export function useRig(): Rig {
  const rig = useContext(RigContext)
  if (!rig) throw new Error('useRig precisa estar dentro da cena do mapa.')
  return rig
}
