import { invalidate } from '@react-three/fiber'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { useEffect, useState } from 'react'
import { CanvasTexture, ClampToEdgeWrapping, NearestFilter, RepeatWrapping, SRGBColorSpace, Texture } from 'three'
import { urlDaTexturaPronta } from './texturasProntas'
import { carregarImagem, obterUrlDaImagem } from '../../shared/imagens/imagens'
import type { TipoToken } from './mapaApi'
import { TokenIcone } from './TokenIcone'

const LADO_DO_DISCO = 256
const LARGURA_DO_NOME = 512
const ALTURA_DO_NOME = 64
const LIMITE_DE_NOMES = 300

const discos = new Map<string, CanvasTexture>()
const nomes = new Map<string, CanvasTexture>()
const imagens = new Map<string, Texture>()

function criarTextura(largura: number, altura: number) {
  const canvas = document.createElement('canvas')
  canvas.width = largura
  canvas.height = altura
  const textura = new CanvasTexture(canvas)
  textura.colorSpace = SRGBColorSpace
  textura.anisotropy = 4
  return { canvas, textura }
}

/**
 * Textura do disco do token. Sem imagem, é o mesmo SVG usado no restante da interface (ClassIcon
 * ou ícone genérico). Com imagem, ela aparece recortada em círculo dentro da borda colorida; o
 * ícone fica de reserva enquanto a imagem carrega ou se o download falhar.
 * Uma textura por combinação, reaproveitada entre tokens.
 */
export function texturaDoDisco(tipo: TipoToken, icone: string, corBorda: string, imagemId: string | null = null): CanvasTexture {
  const chave = `${tipo}|${icone}|${corBorda}|${imagemId ?? ''}`
  const existente = discos.get(chave)
  if (existente) return existente

  const { canvas, textura } = criarTextura(LADO_DO_DISCO, LADO_DO_DISCO)
  discos.set(chave, textura)
  const contexto = canvas.getContext('2d')

  const svg = renderToStaticMarkup(createElement(TokenIcone, { tipo, icone, corBorda, tamanho: LADO_DO_DISCO })).replace(
    '<svg ',
    '<svg xmlns="http://www.w3.org/2000/svg" ',
  )
  let comImagem = false
  const icone_ = new Image()
  icone_.onload = () => {
    if (comImagem) return
    contexto?.drawImage(icone_, 0, 0, LADO_DO_DISCO, LADO_DO_DISCO)
    textura.needsUpdate = true
    invalidate()
  }
  icone_.src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`

  if (imagemId && contexto) {
    carregarImagem(imagemId)
      .then((imagem) => {
        comImagem = true
        // Mesmas medidas do disco em SVG: raio 21 e borda 4 em uma caixa de 48.
        const escala = LADO_DO_DISCO / 48
        const [centro, raio, borda] = [24 * escala, 21 * escala, 4 * escala]
        contexto.clearRect(0, 0, LADO_DO_DISCO, LADO_DO_DISCO)
        contexto.save()
        contexto.beginPath()
        contexto.arc(centro, centro, raio, 0, Math.PI * 2)
        contexto.clip()
        // Cobre o círculo mantendo a proporção, cortando o que sobrar.
        const lado = Math.max((raio * 2) / imagem.naturalWidth, (raio * 2) / imagem.naturalHeight)
        const [largura, altura] = [imagem.naturalWidth * lado, imagem.naturalHeight * lado]
        contexto.drawImage(imagem, centro - largura / 2, centro - altura / 2, largura, altura)
        contexto.restore()
        contexto.beginPath()
        contexto.arc(centro, centro, raio, 0, Math.PI * 2)
        contexto.lineWidth = borda
        contexto.strokeStyle = corBorda
        contexto.stroke()
        textura.needsUpdate = true
        invalidate()
      })
      .catch(() => undefined)
  }

  return textura
}

/** Proporção largura/altura da etiqueta de nome, para dimensionar o plano que a exibe. */
export const PROPORCAO_DO_NOME = LARGURA_DO_NOME / ALTURA_DO_NOME

/** Etiqueta com o nome do token: texto claro sobre uma pílula escura, cortado com reticências se não couber. */
export function texturaDoNome(nome: string): CanvasTexture {
  const existente = nomes.get(nome)
  if (existente) return existente

  if (nomes.size >= LIMITE_DE_NOMES) {
    nomes.forEach((textura) => textura.dispose())
    nomes.clear()
  }

  const { canvas, textura } = criarTextura(LARGURA_DO_NOME, ALTURA_DO_NOME)
  nomes.set(nome, textura)

  const contexto = canvas.getContext('2d')
  if (!contexto) return textura

  contexto.font = "600 34px Figtree, system-ui, -apple-system, 'Segoe UI', sans-serif"
  contexto.textAlign = 'center'
  contexto.textBaseline = 'middle'

  const larguraMaxima = LARGURA_DO_NOME - 48
  let texto = nome
  while (texto.length > 1 && contexto.measureText(texto).width > larguraMaxima) {
    texto = `${texto.slice(0, -2).trimEnd()}…`
  }

  const larguraDaPilula = Math.min(LARGURA_DO_NOME, contexto.measureText(texto).width + 40)
  contexto.fillStyle = 'rgb(15 26 29 / 0.82)'
  contexto.beginPath()
  contexto.roundRect((LARGURA_DO_NOME - larguraDaPilula) / 2, 4, larguraDaPilula, ALTURA_DO_NOME - 8, 28)
  contexto.fill()

  contexto.fillStyle = '#eaf0ec'
  contexto.fillText(texto, LARGURA_DO_NOME / 2, ALTURA_DO_NOME / 2 + 2)
  return textura
}

/** Atraso de um quadro de GIF, em ms. Como nos navegadores, atrasos de até 10 ms valem 100 ms. */
function atrasoDoQuadro(duracaoEmMicrossegundos: number | null): number {
  const ms = (duracaoEmMicrossegundos ?? 0) / 1000
  return ms <= 10 ? 100 : ms
}

/**
 * Se a imagem for um GIF animado, passa a desenhar os quadros em um canvas que vira a imagem da
 * textura, no ritmo do próprio GIF. Usa a API ImageDecoder; sem ela, ou com o usuário pedindo
 * menos movimento, a textura fica parada no primeiro quadro. A animação para quando a textura
 * sai do cache e espera enquanto a aba está oculta.
 */
async function animarSeForGif(imagemId: string, chave: string, textura: Texture): Promise<void> {
  if (typeof ImageDecoder === 'undefined' || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return
  const blob = await (await fetch(await obterUrlDaImagem(imagemId))).blob()
  if (blob.type !== 'image/gif') return

  const decodificador = new ImageDecoder({ data: await blob.arrayBuffer(), type: 'image/gif' })
  await decodificador.completed
  const quadros = decodificador.tracks.selectedTrack?.frameCount ?? 0
  const primeiro = await decodificador.decode({ frameIndex: 0 })
  const { displayWidth: largura, displayHeight: altura } = primeiro.image
  primeiro.image.close()
  const canvas = document.createElement('canvas')
  canvas.width = largura
  canvas.height = altura
  const contexto = canvas.getContext('2d')
  if (quadros <= 1 || !contexto) {
    decodificador.close()
    return
  }

  let indice = 0
  // Um quadro que não decodifica encerra a animação; a textura fica no último quadro desenhado.
  const agendar = (ms: number) => setTimeout(() => desenhar().catch(() => decodificador.close()), ms)
  const desenhar = async () => {
    if (imagens.get(chave) !== textura) {
      decodificador.close()
      return
    }
    if (document.hidden) {
      agendar(500)
      return
    }
    // Cada quadro já vem composto com os anteriores, conforme o descarte definido no GIF.
    const { image: quadro } = await decodificador.decode({ frameIndex: indice })
    contexto.clearRect(0, 0, largura, altura)
    contexto.drawImage(quadro, 0, 0)
    const atraso = atrasoDoQuadro(quadro.duration)
    quadro.close()
    textura.image = canvas
    textura.needsUpdate = true
    invalidate()
    indice = (indice + 1) % quadros
    agendar(atraso)
  }
  await desenhar()
}

/**
 * Textura de uma imagem da galeria, para o fundo do mapa (`repetir` falso) ou para o
 * revestimento de estruturas (`repetir` verdadeiro: um ladrilho por célula).
 * Fica vazia até a imagem chegar; se o download falhar, quem usa mostra a cor no lugar.
 * Um GIF animado anima (veja `animarSeForGif`).
 */
export function texturaDaImagem(imagemId: string, repetir: boolean): Texture {
  const chave = `${imagemId}|${repetir}`
  const existente = imagens.get(chave)
  if (existente) return existente

  const textura = new Texture()
  textura.colorSpace = SRGBColorSpace
  textura.anisotropy = 4
  textura.wrapS = textura.wrapT = repetir ? RepeatWrapping : ClampToEdgeWrapping
  // Um ladrilho pequeno ampliado na célula: sem filtro, a pixel art fica nítida em vez de borrada.
  if (repetir) textura.magFilter = NearestFilter
  imagens.set(chave, textura)

  carregarImagem(imagemId)
    .then((imagem) => {
      textura.image = imagem
      textura.needsUpdate = true
      invalidate()
      // Se a animação falhar, a imagem parada continua valendo.
      animarSeForGif(imagemId, chave, textura).catch(() => undefined)
    })
    .catch(() => imagens.delete(chave))
  return textura
}

const prontas = new Map<string, Promise<Texture>>()

/** Textura de uma das texturas prontas, já carregada. Uma por chave; uma falha não fica guardada. */
function carregarTexturaPronta(chave: string): Promise<Texture> {
  let promessa = prontas.get(chave)
  if (!promessa) {
    promessa = new Promise<Texture>((resolver, rejeitar) => {
      const imagem = new Image()
      imagem.onload = () => {
        const textura = new Texture(imagem)
        textura.colorSpace = SRGBColorSpace
        textura.anisotropy = 4
        textura.wrapS = textura.wrapT = RepeatWrapping
        textura.magFilter = NearestFilter
        textura.needsUpdate = true
        resolver(textura)
      }
      imagem.onerror = () => rejeitar(new Error(`Textura pronta indisponível: ${chave}`))
      imagem.src = urlDaTexturaPronta(chave)
    })
    promessa.catch(() => prontas.delete(chave))
    prontas.set(chave, promessa)
  }
  return promessa
}

/** A textura pronta da chave; null enquanto carrega, sem chave ou se o arquivo faltar (quem usa mostra a cor). */
export function useTexturaPronta(chave: string | null | undefined): Texture | null {
  const [textura, setTextura] = useState<Texture | null>(null)

  useEffect(() => {
    setTextura(null)
    if (!chave) return
    let cancelado = false
    carregarTexturaPronta(chave)
      .then((carregada) => {
        if (cancelado) return
        setTextura(carregada)
        invalidate()
      })
      .catch(() => undefined)
    return () => {
      cancelado = true
    }
  }, [chave])

  return textura
}
