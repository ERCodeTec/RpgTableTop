import { useEffect, useRef, type ReactNode } from 'react'

interface DialogoProps {
  titulo: string
  aoFechar: () => void
  children: ReactNode
}

/**
 * Modal sobre o <dialog> nativo: foco preso; fecha pelo botão do conteúdo ou pelo Esc. Clicar ou
 * tocar fora não fecha, para que um toque perdido não descarte o que está aberto.
 */
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
    >
      <div className="dialogo-conteudo">
        <h2>{titulo}</h2>
        {children}
      </div>
    </dialog>
  )
}
