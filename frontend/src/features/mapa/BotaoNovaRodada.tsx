import { useState } from 'react'
import type { AcoesDeMovimento } from './useMapaTempoReal'

interface BotaoNovaRodadaProps {
  mapaId: string
  movimento: AcoesDeMovimento
}

/**
 * Ação do Mestre que vale para a mesa inteira: devolve o movimento a todos os tokens do mapa. Fica abaixo do
 * mapa, ao lado da câmera, para continuar à mão na tela cheia, quando o painel do Mestre vira gaveta.
 */
export function BotaoNovaRodada({ mapaId, movimento }: BotaoNovaRodadaProps) {
  const [iniciando, setIniciando] = useState(false)

  return (
    <button
      className="botao botao-primario"
      type="button"
      disabled={iniciando}
      title="Devolve o movimento inteiro a todos os tokens deste mapa"
      onClick={() => {
        setIniciando(true)
        void movimento.novaRodada(mapaId).finally(() => setIniciando(false))
      }}
    >
      {iniciando ? 'Iniciando…' : 'Nova rodada'}
    </button>
  )
}
