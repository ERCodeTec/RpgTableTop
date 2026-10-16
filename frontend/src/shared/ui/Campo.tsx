import { useId, type InputHTMLAttributes } from 'react'

interface CampoProps extends InputHTMLAttributes<HTMLInputElement> {
  rotulo: string
  /** Mensagens de erro do campo; a primeira é exibida. */
  erros?: string[]
  dica?: string
}

export function Campo({ rotulo, erros, dica, ...input }: CampoProps) {
  const id = useId()
  const erro = erros?.[0]
  const descricao = erro ? `${id}-erro` : dica ? `${id}-dica` : undefined

  return (
    <div className="campo">
      <label htmlFor={id}>{rotulo}</label>
      <input id={id} aria-invalid={erro ? true : undefined} aria-describedby={descricao} {...input} />
      {erro ? (
        <p className="campo-erro" id={`${id}-erro`}>
          {erro}
        </p>
      ) : (
        dica && (
          <p className="campo-dica" id={`${id}-dica`}>
            {dica}
          </p>
        )
      )}
    </div>
  )
}
