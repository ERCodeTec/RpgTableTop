import { useImagemUrl } from './imagens'

interface MiniaturaProps {
  imagemId: string
  /** Texto alternativo; vazio quando o nome já aparece ao lado. */
  alt: string
  className?: string
}

/** A miniatura de uma imagem da galeria. Enquanto carrega, ocupa o mesmo espaço com um fundo neutro. */
export function Miniatura({ imagemId, alt, className = '' }: MiniaturaProps) {
  const url = useImagemUrl(imagemId, true)
  return url ? (
    <img className={`miniatura ${className}`} src={url} alt={alt} draggable={false} />
  ) : (
    <span className={`miniatura miniatura-vazia ${className}`} role="img" aria-label={alt || 'Carregando imagem'} />
  )
}
