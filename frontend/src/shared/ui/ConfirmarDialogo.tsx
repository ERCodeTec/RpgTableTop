import type { ReactNode } from 'react'
import { Dialogo } from './Dialogo'

interface ConfirmarDialogoProps {
  titulo: string
  children: ReactNode
  /** Texto do botão que executa a ação, com o verbo da própria ação. */
  rotuloConfirmar: string
  rotuloEmAndamento: string
  emAndamento: boolean
  erro?: string | null
  aoConfirmar: () => void
  aoFechar: () => void
}

/** Confirmação de uma ação destrutiva. */
export function ConfirmarDialogo({
  titulo,
  children,
  rotuloConfirmar,
  rotuloEmAndamento,
  emAndamento,
  erro,
  aoConfirmar,
  aoFechar,
}: ConfirmarDialogoProps) {
  return (
    <Dialogo titulo={titulo} aoFechar={aoFechar}>
      {children}
      {erro && (
        <p className="aviso-erro" role="alert">
          {erro}
        </p>
      )}
      <div className="dialogo-acoes">
        <button className="botao" type="button" onClick={aoFechar} autoFocus>
          Cancelar
        </button>
        <button className="botao botao-perigo-cheio" type="button" onClick={aoConfirmar} disabled={emAndamento}>
          {emAndamento ? rotuloEmAndamento : rotuloConfirmar}
        </button>
      </div>
    </Dialogo>
  )
}
