import { Miniatura } from '../../shared/imagens/Miniatura'
import type { TipoToken } from '../mapa/mapaApi'
import { TokenIcone } from '../mapa/TokenIcone'
import type { ModeloDeToken } from './bibliotecaApi'

interface DiscoDoModeloProps {
  /** Um modelo da biblioteca, ou um personagem (a foto dele, ou o ícone da classe). */
  modelo: Pick<ModeloDeToken, 'icone' | 'imagemId' | 'corBorda'> & { tipo: TipoToken }
  tamanho: number
}

/** O modelo como vai aparecer no mapa: a imagem, se houver, ou o ícone, com a borda colorida. */
export function DiscoDoModelo({ modelo, tamanho }: DiscoDoModeloProps) {
  if (!modelo.imagemId) {
    return <TokenIcone tipo={modelo.tipo} icone={modelo.icone} corBorda={modelo.corBorda} tamanho={tamanho} />
  }

  return (
    <span className="disco-modelo" style={{ width: tamanho, height: tamanho, borderColor: modelo.corBorda }}>
      <Miniatura imagemId={modelo.imagemId} alt="" />
    </span>
  )
}
