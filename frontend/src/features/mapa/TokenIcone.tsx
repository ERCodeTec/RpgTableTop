import { CLASSES, type ClasseDnd } from '../../shared/classes'
import { ClassIcon } from '../../shared/icones/ClassIcon'
import { IconeGenerico } from '../../shared/icones/IconeGenerico'
import type { TipoToken } from './mapaApi'

interface TokenIconeProps {
  tipo: TipoToken
  icone: string
  corBorda: string
  tamanho?: number
  className?: string
}

/** O disco de um token: ícone da classe para personagens, ícone genérico para inimigos e NPCs. */
export function TokenIcone({ tipo, icone, corBorda, tamanho, className }: TokenIconeProps) {
  return tipo === 'Personagem' && (CLASSES as readonly string[]).includes(icone) ? (
    <ClassIcon classe={icone as ClasseDnd} corBorda={corBorda} tamanho={tamanho} className={className} />
  ) : (
    <IconeGenerico icone={icone} corBorda={corBorda} tamanho={tamanho} className={className} />
  )
}
