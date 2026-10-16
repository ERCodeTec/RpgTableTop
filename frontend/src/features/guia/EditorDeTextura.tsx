import { useEffect, useId, useMemo, useRef, useState, type PointerEvent as EventoDePonteiro } from 'react'
import { porTema, urlDaTexturaPronta, useCatalogoDeTexturas } from '../mapa/texturasProntas'
import { desenharBlocoIsometrico, type Caixa } from './blocoIsometrico'

type Ferramenta = 'lapis' | 'borracha' | 'balde' | 'contaGotas'
/** Um pixel: cor #RRGGBB ou null (transparente). */
type Pixels = (string | null)[]

const TAMANHOS = [16, 32, 64] as const
const LADO_DA_TELA = 384
const LIMITE_DO_HISTORICO = 100

const FERRAMENTAS: { id: Ferramenta; nome: string }[] = [
  { id: 'lapis', nome: 'Lápis' },
  { id: 'borracha', nome: 'Borracha' },
  { id: 'balde', nome: 'Balde' },
  { id: 'contaGotas', nome: 'Conta-gotas' },
]

/** Paleta curta de masmorra: pedra, terra, madeira, musgo e alguns destaques. */
const PALETA = [
  '#1A1C2C', '#333C57', '#566C86', '#94B0C2', '#F4F4F4', '#5D275D', '#B13E53', '#EF7D57',
  '#FFCD75', '#A7F070', '#38B764', '#257179', '#29366F', '#3B5DC9', '#41A6F6', '#73EFF7',
  '#3E2A1F', '#6B4431', '#9C6B48', '#C9A27A',
]

const FORMAS: { id: string; nome: string; caixa: Caixa }[] = [
  { id: 'parede', nome: 'Parede (3 × 1, altura 2)', caixa: { largura: 3, profundidade: 1, altura: 2 } },
  { id: 'pilar', nome: 'Pilar (1 × 1, altura 2,5)', caixa: { largura: 1, profundidade: 1, altura: 2.5 } },
  { id: 'porta', nome: 'Porta (1 × 1, altura 1,6)', caixa: { largura: 1, profundidade: 1, altura: 1.6, umLadrilhoNaAltura: true } },
  { id: 'chao', nome: 'Chão (4 × 4)', caixa: { largura: 4, profundidade: 4, altura: 0 } },
]

const vazio = (lado: number): Pixels => Array<string | null>(lado * lado).fill(null)

const hex = (r: number, g: number, b: number) => `#${[r, g, b].map((c) => c.toString(16).padStart(2, '0')).join('')}`.toUpperCase()

/** Lê uma imagem para o tamanho do editor, pixel a pixel (sem suavizar); semitransparente vira transparente. */
function pixelsDaImagem(imagem: CanvasImageSource, lado: number): Pixels {
  const canvas = document.createElement('canvas')
  canvas.width = canvas.height = lado
  const contexto = canvas.getContext('2d')!
  contexto.imageSmoothingEnabled = false
  contexto.drawImage(imagem, 0, 0, lado, lado)
  const dados = contexto.getImageData(0, 0, lado, lado).data
  return Array.from({ length: lado * lado }, (_, i) => (dados[i * 4 + 3] < 128 ? null : hex(dados[i * 4], dados[i * 4 + 1], dados[i * 4 + 2])))
}

/** O ladrilho no tamanho real, para baixar e para as prévias. */
function desenharLadrilho(canvas: HTMLCanvasElement, pixels: Pixels, lado: number) {
  canvas.width = canvas.height = lado
  const contexto = canvas.getContext('2d')!
  contexto.clearRect(0, 0, lado, lado)
  pixels.forEach((cor, i) => {
    if (!cor) return
    contexto.fillStyle = cor
    contexto.fillRect(i % lado, Math.floor(i / lado), 1, 1)
  })
}

/** As células entre dois pontos (Bresenham), para um traço rápido não deixar buracos. */
function linha(x0: number, y0: number, x1: number, y1: number): [number, number][] {
  const celulas: [number, number][] = []
  const [dx, dy] = [Math.abs(x1 - x0), -Math.abs(y1 - y0)]
  const [sx, sy] = [x0 < x1 ? 1 : -1, y0 < y1 ? 1 : -1]
  let erro = dx + dy
  for (;;) {
    celulas.push([x0, y0])
    if (x0 === x1 && y0 === y1) return celulas
    const dobro = 2 * erro
    if (dobro >= dy) {
      erro += dy
      x0 += sx
    }
    if (dobro <= dx) {
      erro += dx
      y0 += sy
    }
  }
}

function preencher(pixels: Pixels, lado: number, inicio: number, cor: string | null): Pixels {
  const alvo = pixels[inicio]
  if (alvo === cor) return pixels
  const novo = [...pixels]
  const pilha = [inicio]
  while (pilha.length) {
    const i = pilha.pop()!
    if (novo[i] !== alvo) continue
    novo[i] = cor
    const [x, y] = [i % lado, Math.floor(i / lado)]
    if (x > 0) pilha.push(i - 1)
    if (x < lado - 1) pilha.push(i + 1)
    if (y > 0) pilha.push(i - lado)
    if (y < lado - 1) pilha.push(i + lado)
  }
  return novo
}

/**
 * Um editor de pixel art pequeno, para desenhar o ladrilho no próprio site: grade de 16 ou 32
 * pixels, lápis, borracha, balde e conta-gotas, desfazer e refazer, e duas prévias — o ladrilho
 * repetido (para achar emendas) e a estrutura em visão isométrica, como aparece no mapa.
 * Funciona com mouse, caneta e dedo.
 */
export function EditorDeTextura() {
  const ids = { cor: useId(), tamanho: useId(), forma: useId(), base: useId() }
  const catalogo = useCatalogoDeTexturas()
  // O editor só abre ladrilhos quadrados que cabem nele.
  const bases = catalogo.data
    ? porTema(catalogo.data, catalogo.data.texturas.filter((textura) => textura.largura === textura.altura && textura.largura <= 64))
    : []
  const [lado, setLado] = useState<number>(16)
  const [pixels, setPixels] = useState<Pixels>(() => vazio(16))
  const [desfazer, setDesfazer] = useState<Pixels[]>([])
  const [refazer, setRefazer] = useState<Pixels[]>([])
  const [ferramenta, setFerramenta] = useState<Ferramenta>('lapis')
  const [cor, setCor] = useState(PALETA[2])
  const [grade, setGrade] = useState(true)
  const [formaId, setFormaId] = useState(FORMAS[0].id)
  const [erro, setErro] = useState<string | null>(null)

  const tela = useRef<HTMLCanvasElement>(null)
  const repetido = useRef<HTMLCanvasElement>(null)
  const isometrico = useRef<HTMLCanvasElement>(null)
  const arquivo = useRef<HTMLInputElement>(null)
  const traco = useRef<{ ponteiro: number; ultimo: [number, number] } | null>(null)

  const ladrilho = useMemo(() => {
    const canvas = document.createElement('canvas')
    desenharLadrilho(canvas, pixels, lado)
    return canvas
  }, [pixels, lado])

  // A tela de edição: cada pixel ampliado, sobre um xadrez que indica a transparência.
  useEffect(() => {
    const canvas = tela.current
    const contexto = canvas?.getContext('2d')
    if (!canvas || !contexto) return
    const passo = LADO_DA_TELA / lado
    contexto.imageSmoothingEnabled = false
    for (let y = 0; y < lado; y++) {
      for (let x = 0; x < lado; x++) {
        contexto.fillStyle = (x + y) % 2 ? '#2A3236' : '#353F44'
        contexto.fillRect(x * passo, y * passo, passo, passo)
      }
    }
    contexto.drawImage(ladrilho, 0, 0, LADO_DA_TELA, LADO_DA_TELA)
    if (grade) {
      contexto.strokeStyle = 'rgb(255 255 255 / 0.12)'
      contexto.lineWidth = 1
      contexto.beginPath()
      for (let i = 1; i < lado; i++) {
        contexto.moveTo(i * passo + 0.5, 0)
        contexto.lineTo(i * passo + 0.5, LADO_DA_TELA)
        contexto.moveTo(0, i * passo + 0.5)
        contexto.lineTo(LADO_DA_TELA, i * passo + 0.5)
      }
      contexto.stroke()
    }
  }, [ladrilho, lado, grade])

  // Prévia repetida 3 x 3: uma emenda ruim aparece como linha ou degrau no meio.
  useEffect(() => {
    const canvas = repetido.current
    const contexto = canvas?.getContext('2d')
    if (!canvas || !contexto) return
    const passo = canvas.width / 3
    contexto.imageSmoothingEnabled = false
    contexto.clearRect(0, 0, canvas.width, canvas.height)
    for (let y = 0; y < 3; y++) for (let x = 0; x < 3; x++) contexto.drawImage(ladrilho, x * passo, y * passo, passo, passo)
  }, [ladrilho])

  useEffect(() => {
    const canvas = isometrico.current
    if (!canvas) return
    desenharBlocoIsometrico(canvas, ladrilho, lado, FORMAS.find((forma) => forma.id === formaId)!.caixa)
  }, [ladrilho, lado, formaId])

  /** Troca a imagem guardando a anterior no histórico. */
  function mudar(novo: Pixels) {
    setDesfazer((pilha) => [...pilha, pixels].slice(-LIMITE_DO_HISTORICO))
    setRefazer([])
    setPixels(novo)
  }

  function percorrer(sentido: 'desfazer' | 'refazer') {
    const [origem, setOrigem, setDestino] = sentido === 'desfazer' ? [desfazer, setDesfazer, setRefazer] : [refazer, setRefazer, setDesfazer]
    const anterior = origem.at(-1)
    if (!anterior) return
    setOrigem(origem.slice(0, -1))
    setDestino((pilha) => [...pilha, pixels])
    // Desfazer uma troca de tamanho volta também o tamanho.
    setLado(Math.sqrt(anterior.length))
    setPixels(anterior)
  }

  const celulaDo = (evento: EventoDePonteiro<HTMLCanvasElement>): [number, number] => {
    const caixa = evento.currentTarget.getBoundingClientRect()
    const x = Math.floor(((evento.clientX - caixa.left) / caixa.width) * lado)
    const y = Math.floor(((evento.clientY - caixa.top) / caixa.height) * lado)
    return [Math.min(lado - 1, Math.max(0, x)), Math.min(lado - 1, Math.max(0, y))]
  }

  function aoPressionar(evento: EventoDePonteiro<HTMLCanvasElement>) {
    if (traco.current || (evento.pointerType === 'mouse' && evento.button !== 0)) return
    const [x, y] = celulaDo(evento)
    const indice = y * lado + x
    if (ferramenta === 'contaGotas') {
      const escolhida = pixels[indice]
      if (escolhida) setCor(escolhida)
      setFerramenta('lapis')
      return
    }
    if (ferramenta === 'balde') {
      mudar(preencher(pixels, lado, indice, cor))
      return
    }
    // O traço inteiro é um passo só no histórico.
    evento.currentTarget.setPointerCapture(evento.pointerId)
    traco.current = { ponteiro: evento.pointerId, ultimo: [x, y] }
    const novo = [...pixels]
    novo[indice] = ferramenta === 'borracha' ? null : cor
    mudar(novo)
  }

  function aoMover(evento: EventoDePonteiro<HTMLCanvasElement>) {
    const atual = traco.current
    if (!atual || atual.ponteiro !== evento.pointerId) return
    const [x, y] = celulaDo(evento)
    if (x === atual.ultimo[0] && y === atual.ultimo[1]) return
    const valor = ferramenta === 'borracha' ? null : cor
    setPixels((anteriores) => {
      const novo = [...anteriores]
      for (const [cx, cy] of linha(atual.ultimo[0], atual.ultimo[1], x, y)) novo[cy * lado + cx] = valor
      return novo
    })
    atual.ultimo = [x, y]
  }

  function aoSoltar(evento: EventoDePonteiro<HTMLCanvasElement>) {
    if (traco.current?.ponteiro === evento.pointerId) traco.current = null
  }

  function trocarTamanho(novo: number) {
    if (novo === lado) return
    // O desenho acompanha, ampliado ou reduzido pixel a pixel.
    const convertido = pixelsDaImagem(ladrilho, novo)
    setDesfazer((pilha) => [...pilha, pixels].slice(-LIMITE_DO_HISTORICO))
    setRefazer([])
    setLado(novo)
    setPixels(convertido)
  }

  function carregarDe(url: string) {
    setErro(null)
    const imagem = new Image()
    imagem.onload = () => {
      if (imagem.naturalWidth !== imagem.naturalHeight) {
        setErro('A imagem precisa ser quadrada, como um ladrilho.')
        return
      }
      mudar(pixelsDaImagem(imagem, lado))
    }
    imagem.onerror = () => setErro('Não foi possível abrir essa imagem.')
    imagem.src = url
  }

  function baixar() {
    ladrilho.toBlob((blob) => {
      if (!blob) return
      const link = document.createElement('a')
      link.href = URL.createObjectURL(blob)
      link.download = `textura-${lado}x${lado}.png`
      link.click()
      setTimeout(() => URL.revokeObjectURL(link.href), 1000)
    }, 'image/png')
  }

  return (
    <div className="editor-textura">
      <div className="editor-textura-barra" role="toolbar" aria-label="Ferramentas de desenho">
        <div className="alternancia" role="group" aria-label="Ferramenta">
          {FERRAMENTAS.map((opcao) => (
            <button key={opcao.id} type="button" aria-pressed={ferramenta === opcao.id} onClick={() => setFerramenta(opcao.id)}>
              {opcao.nome}
            </button>
          ))}
        </div>
        <button className="botao botao-discreto" type="button" disabled={!desfazer.length} onClick={() => percorrer('desfazer')}>
          Desfazer
        </button>
        <button className="botao botao-discreto" type="button" disabled={!refazer.length} onClick={() => percorrer('refazer')}>
          Refazer
        </button>
        <button className="botao botao-discreto" type="button" aria-pressed={grade} onClick={() => setGrade(!grade)}>
          Grade
        </button>
      </div>

      <div className="editor-textura-corpo">
        <canvas
          ref={tela}
          className="editor-textura-tela"
          width={LADO_DA_TELA}
          height={LADO_DA_TELA}
          aria-label={`Tela de desenho de ${lado} por ${lado} pixels`}
          onPointerDown={aoPressionar}
          onPointerMove={aoMover}
          onPointerUp={aoSoltar}
          onPointerCancel={aoSoltar}
        />

        <div className="editor-textura-lado">
          <div className="campo campo-cor">
            <label htmlFor={ids.cor}>Cor</label>
            <input id={ids.cor} type="color" value={cor} onChange={(evento) => setCor(evento.target.value.toUpperCase())} />
          </div>
          <div className="editor-textura-paleta" role="group" aria-label="Paleta">
            {PALETA.map((opcao) => (
              <button
                key={opcao}
                type="button"
                className="editor-textura-amostra"
                style={{ background: opcao }}
                aria-label={opcao}
                aria-pressed={cor === opcao}
                onClick={() => {
                  setCor(opcao)
                  if (ferramenta === 'borracha' || ferramenta === 'contaGotas') setFerramenta('lapis')
                }}
              />
            ))}
          </div>

          <div className="campo">
            <label htmlFor={ids.tamanho}>Tamanho do ladrilho</label>
            <select id={ids.tamanho} value={lado} onChange={(evento) => trocarTamanho(Number(evento.target.value))}>
              {TAMANHOS.map((tamanho) => (
                <option key={tamanho} value={tamanho}>
                  {tamanho} × {tamanho} pixels
                </option>
              ))}
            </select>
          </div>

          <div className="campo">
            <label htmlFor={ids.base}>Começar de uma textura pronta</label>
            <select
              id={ids.base}
              value=""
              onChange={(evento) => {
                if (evento.target.value) carregarDe(urlDaTexturaPronta(evento.target.value))
              }}
            >
              <option value="">Escolha…</option>
              {bases.map(({ tema, texturas }) => (
                <optgroup key={tema.id} label={tema.nome}>
                  {texturas.map((textura) => (
                    <option key={textura.chave} value={textura.chave}>
                      {textura.nome} ({textura.largura} px)
                    </option>
                  ))}
                </optgroup>
              ))}
            </select>
          </div>

          <div className="estado-acoes">
            <button className="botao botao-discreto" type="button" onClick={() => arquivo.current?.click()}>
              Abrir PNG
            </button>
            <button className="botao botao-discreto" type="button" disabled={pixels.every((p) => p === null)} onClick={() => mudar(vazio(lado))}>
              Limpar
            </button>
            <button className="botao botao-primario" type="button" onClick={baixar}>
              Baixar PNG
            </button>
          </div>
          <input
            ref={arquivo}
            className="so-leitor"
            type="file"
            accept="image/png,image/gif,image/webp"
            tabIndex={-1}
            aria-label="Imagem para abrir no editor"
            onChange={(evento) => {
              const escolhido = evento.target.files?.[0]
              evento.target.value = ''
              if (!escolhido) return
              const url = URL.createObjectURL(escolhido)
              carregarDe(url)
              setTimeout(() => URL.revokeObjectURL(url), 5000)
            }}
          />
          {erro && (
            <p className="aviso-erro" role="alert">
              {erro}
            </p>
          )}
        </div>
      </div>

      <div className="editor-textura-previas">
        <figure>
          <canvas ref={repetido} className="editor-textura-previa" width={240} height={240} />
          <figcaption>Repetido 3 × 3: procure linhas ou degraus nas emendas.</figcaption>
        </figure>
        <figure>
          <canvas ref={isometrico} className="editor-textura-previa" width={320} height={240} />
          <figcaption>
            <label htmlFor={ids.forma}>No mapa, como </label>
            <select id={ids.forma} value={formaId} onChange={(evento) => setFormaId(evento.target.value)}>
              {FORMAS.map((forma) => (
                <option key={forma.id} value={forma.id}>
                  {forma.nome}
                </option>
              ))}
            </select>
          </figcaption>
        </figure>
      </div>
    </div>
  )
}
