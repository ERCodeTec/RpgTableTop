import { useEffect, useState } from 'react'
import { apiBlob } from '../api/http'

// As imagens da mesa só saem da API com o token de login, e nem <img> nem o carregador de
// texturas do three.js enviam esse cabeçalho. Por isso cada imagem é buscada uma vez com fetch
// e passa a ser usada por uma blob URL local, guardada aqui enquanto a página estiver aberta.
const urls = new Map<string, Promise<string>>()
const elementos = new Map<string, Promise<HTMLImageElement>>()

const chave = (id: string, miniatura: boolean) => (miniatura ? `${id}:mini` : id)

/** A blob URL da imagem (ou da miniatura de 256 px). Uma falha não fica guardada: a próxima chamada tenta de novo. */
export function obterUrlDaImagem(id: string, miniatura = false): Promise<string> {
  const k = chave(id, miniatura)
  let promessa = urls.get(k)
  if (!promessa) {
    promessa = apiBlob(`/api/imagens/${id}${miniatura ? '/miniatura' : ''}`).then((blob) => URL.createObjectURL(blob))
    promessa.catch(() => urls.delete(k))
    urls.set(k, promessa)
  }
  return promessa
}

/** A imagem já decodificada, pronta para virar textura ou ser desenhada em um canvas. */
export function carregarImagem(id: string): Promise<HTMLImageElement> {
  let promessa = elementos.get(id)
  if (!promessa) {
    promessa = obterUrlDaImagem(id).then(
      (url) =>
        new Promise<HTMLImageElement>((resolver, rejeitar) => {
          const elemento = new Image()
          elemento.onload = () => resolver(elemento)
          elemento.onerror = () => rejeitar(new Error('Não foi possível decodificar a imagem.'))
          elemento.src = url
        }),
    )
    promessa.catch(() => elementos.delete(id))
    elementos.set(id, promessa)
  }
  return promessa
}

/** Libera a memória de uma imagem que foi excluída. */
export function esquecerImagem(id: string): void {
  for (const k of [chave(id, false), chave(id, true)]) {
    urls.get(k)?.then((url) => URL.revokeObjectURL(url)).catch(() => undefined)
    urls.delete(k)
  }
  elementos.delete(id)
}

/** A blob URL para usar em um <img>; null enquanto carrega, se não houver imagem ou se o download falhar. */
export function useImagemUrl(id: string | null | undefined, miniatura = false): string | null {
  const [url, setUrl] = useState<string | null>(null)

  useEffect(() => {
    setUrl(null)
    if (!id) return
    let cancelado = false
    obterUrlDaImagem(id, miniatura)
      .then((obtida) => {
        if (!cancelado) setUrl(obtida)
      })
      .catch(() => undefined)
    return () => {
      cancelado = true
    }
  }, [id, miniatura])

  return url
}

/** A imagem decodificada (com as dimensões reais); null enquanto carrega ou se falhar. */
export function useImagemCarregada(id: string | null | undefined): HTMLImageElement | null {
  const [imagem, setImagem] = useState<HTMLImageElement | null>(null)

  useEffect(() => {
    setImagem(null)
    if (!id) return
    let cancelado = false
    carregarImagem(id)
      .then((carregada) => {
        if (!cancelado) setImagem(carregada)
      })
      .catch(() => undefined)
    return () => {
      cancelado = true
    }
  }, [id])

  return imagem
}
