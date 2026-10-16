import { api, ApiError, CABECALHOS_PADRAO, erroDaResposta } from '../api/http'
import { lerToken } from '../api/tokenStorage'

export interface Imagem {
  id: string
  /** Nulo nas imagens pessoais (galeria da biblioteca de tokens). */
  mesaId: string | null
  nomeOriginal: string
  contentType: string
  tamanhoBytes: number
  largura: number
  altura: number
  criadaEm: string
}

/** A galeria (da mesa ou a pessoal) e os limites que o servidor aplica. */
export interface Galeria {
  imagens: Imagem[]
  bytesUsados: number
  cotaBytes: number
  cotaImagens: number
  tamanhoMaximoBytes: number
  dimensaoMaxima: number
}

export const TIPOS_ACEITOS = 'image/png,image/jpeg,image/webp,image/gif'

export const chaveDaGaleria = (mesaId: string) => ['mesas', mesaId, 'imagens'] as const

export const listarImagens = (mesaId: string) => api<Galeria>(`/api/mesas/${mesaId}/imagens`)

/** A galeria pessoal, usada pelos modelos da biblioteca de tokens em qualquer mesa. */
export const CHAVE_GALERIA_PESSOAL = ['biblioteca', 'imagens'] as const

export const listarImagensPessoais = () => api<Galeria>('/api/biblioteca/imagens')

/** De onde vêm as imagens que um seletor oferece: a galeria de uma mesa ou a pessoal. */
export type FonteDeImagem = { mesaId: string } | 'pessoal'

export const chaveDaFonte = (fonte: FonteDeImagem) => (fonte === 'pessoal' ? CHAVE_GALERIA_PESSOAL : chaveDaGaleria(fonte.mesaId))

export const listarDaFonte = (fonte: FonteDeImagem) => (fonte === 'pessoal' ? listarImagensPessoais() : listarImagens(fonte.mesaId))

export const enviarParaAFonte = (fonte: FonteDeImagem, arquivo: File, aoProgredir: (fracao: number) => void) =>
  enviarPara(fonte === 'pessoal' ? '/api/biblioteca/imagens' : `/api/mesas/${fonte.mesaId}/imagens`, arquivo, aoProgredir)

export const excluirImagem = (id: string) => api<void>(`/api/imagens/${id}`, { method: 'DELETE' })

/**
 * Envia um arquivo para a galeria. Usa XMLHttpRequest porque o fetch não informa o progresso do
 * envio; `aoProgredir` recebe a fração enviada, de 0 a 1.
 */
export function enviarImagem(mesaId: string, arquivo: File, aoProgredir: (fracao: number) => void): Promise<Imagem> {
  return enviarPara(`/api/mesas/${mesaId}/imagens`, arquivo, aoProgredir)
}

function enviarPara(url: string, arquivo: File, aoProgredir: (fracao: number) => void): Promise<Imagem> {
  return new Promise((resolver, rejeitar) => {
    const pedido = new XMLHttpRequest()
    pedido.open('POST', url)
    pedido.responseType = 'json'
    pedido.setRequestHeader('Accept', 'application/json')
    for (const [nome, valor] of Object.entries(CABECALHOS_PADRAO)) pedido.setRequestHeader(nome, valor)
    const token = lerToken()
    if (token) pedido.setRequestHeader('Authorization', `Bearer ${token}`)

    pedido.upload.onprogress = (evento) => {
      if (evento.lengthComputable) aoProgredir(evento.loaded / evento.total)
    }
    pedido.onerror = () => rejeitar(new ApiError(0, 'Sem conexão com o servidor. O envio não terminou.'))
    pedido.onload = () => {
      if (pedido.status >= 200 && pedido.status < 300) {
        resolver(pedido.response as Imagem)
      } else if (pedido.status === 413) {
        rejeitar(new ApiError(413, 'O arquivo é grande demais para ser enviado.'))
      } else {
        rejeitar(erroDaResposta(pedido.status, pedido.response ?? {}))
      }
    }

    const formulario = new FormData()
    formulario.append('arquivo', arquivo)
    pedido.send(formulario)
  })
}

export function formatarTamanho(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} kB`
  return `${(bytes / (1024 * 1024)).toLocaleString('pt-BR', { maximumFractionDigits: 1 })} MB`
}
