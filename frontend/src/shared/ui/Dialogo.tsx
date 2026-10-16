import { useEffect, useRef, type ReactNode } from 'react'

interface DialogoProps {
  titulo: string
  aoFechar: () => void
  children: ReactNode
}

/** Modal sobre o <dialog> nativo: foco preso, Esc e clique fora fecham. */
export function Dialogo({ titulo, aoFechar, children }: DialogoProps) {
  const ref = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const dialogo = ref.current
    if (dialogo && !dialogo.open) {
      dialogo.showModal()
    }
    return () => dialogo?.close()
  }, [])

  return (
    <dialog
      ref={ref}
      className="dialogo"
      aria-label={titulo}
      onCancel={(evento) => {
        evento.preventDefault()
        aoFechar()
      }}
      onMouseDown={(evento) => {
        if (evento.target === ref.current) aoFechar()
      }}
    >
      <div className="dialogo-conteudo">
        <h2>{titulo}</h2>
        {children}
      </div>
    </dialog>
  )
}
