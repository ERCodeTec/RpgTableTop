import { useEffect } from 'react'

const DURACAO_MS = 4000

interface ToastProps {
  /** Texto do aviso; null esconde. */
  mensagem: string | null
  aoFechar: () => void
}

/** Aviso curto que some sozinho. Para algo que o usuário precise ler com calma, use um diálogo. */
export function Toast({ mensagem, aoFechar }: ToastProps) {
  useEffect(() => {
    if (!mensagem) return
    const temporizador = setTimeout(aoFechar, DURACAO_MS)
    return () => clearTimeout(temporizador)
  }, [mensagem, aoFechar])

  return (
    <div className="toast-area" role="status" aria-live="polite">
      {mensagem && <p className="toast">{mensagem}</p>}
    </div>
  )
}
