import { useEffect, useState } from 'react'
import { VALOR_MAXIMO, VALOR_MINIMO } from './iniciativaApi'

interface CampoDeIniciativaProps {
  rotulo: string
  valor: number | null
  desabilitado?: boolean
  /** Rótulo do botão; sem ele, o valor é enviado ao sair do campo ou com Enter. */
  rotuloEnviar?: string
  aoEnviar: (valor: number) => Promise<unknown>
}

/** O número da iniciativa, entre os limites do servidor. Só envia quando o valor mudou e é válido. */
export function CampoDeIniciativa({ rotulo, valor, desabilitado, rotuloEnviar, aoEnviar }: CampoDeIniciativaProps) {
  const [texto, setTexto] = useState(valor?.toString() ?? '')
  const [enviando, setEnviando] = useState(false)
  useEffect(() => setTexto(valor?.toString() ?? ''), [valor])

  const numero = Number(texto)
  const valido = texto.trim() !== '' && Number.isInteger(numero) && numero >= VALOR_MINIMO && numero <= VALOR_MAXIMO

  const enviar = () => {
    if (!valido || numero === valor || enviando) {
      if (!valido) setTexto(valor?.toString() ?? '')
      return
    }
    setEnviando(true)
    void aoEnviar(numero).finally(() => setEnviando(false))
  }

  return (
    <form
      className="campo-de-iniciativa"
      onSubmit={(evento) => {
        evento.preventDefault()
        enviar()
      }}
    >
      <input
        type="number"
        inputMode="numeric"
        min={VALOR_MINIMO}
        max={VALOR_MAXIMO}
        step={1}
        aria-label={rotulo}
        placeholder="—"
        value={texto}
        disabled={desabilitado || enviando}
        onChange={(evento) => setTexto(evento.target.value)}
        onBlur={rotuloEnviar ? undefined : enviar}
      />
      {rotuloEnviar && (
        <button className="botao botao-primario" type="submit" disabled={!valido || enviando || desabilitado}>
          {enviando ? 'Enviando…' : rotuloEnviar}
        </button>
      )}
    </form>
  )
}
