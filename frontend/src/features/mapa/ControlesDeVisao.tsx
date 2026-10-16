import { useId } from 'react'
import { INCLINACAO_MAXIMA, useVisaoStore, type Qualidade } from './visaoStore'

/**
 * Controles da câmera e da qualidade gráfica. Valem só para quem mexe: nada disso é enviado aos
 * outros jogadores, e a escolha fica guardada neste navegador.
 */
export function ControlesDeVisao() {
  const idInclinacao = useId()
  const idQualidade = useId()
  const inclinacao = useVisaoStore((estado) => estado.inclinacao)
  const qualidade = useVisaoStore((estado) => estado.qualidade)
  const { definirInclinacao, topo, isometrica, girar, definirQualidade } = useVisaoStore.getState()

  return (
    <div className="mapa-visao" role="group" aria-label="Visão do mapa">
      <div className="mapa-visao-grupo">
        <button className="botao botao-discreto" type="button" onClick={topo}>
          Topo
        </button>
        <button className="botao botao-discreto" type="button" onClick={isometrica}>
          Isométrica
        </button>
      </div>
      <div className="mapa-visao-grupo">
        <label htmlFor={idInclinacao}>Inclinação</label>
        <input
          id={idInclinacao}
          type="range"
          min={0}
          max={INCLINACAO_MAXIMA}
          step={1}
          value={inclinacao}
          onChange={(evento) => definirInclinacao(Number(evento.target.value))}
        />
        <output htmlFor={idInclinacao}>{Math.round(inclinacao)}°</output>
      </div>
      <div className="mapa-visao-grupo">
        <button className="botao botao-discreto" type="button" onClick={() => girar(-1)} title="Atalho: Q">
          Girar ↺
        </button>
        <button className="botao botao-discreto" type="button" onClick={() => girar(1)} title="Atalho: E">
          Girar ↻
        </button>
      </div>
      <div className="mapa-visao-grupo">
        <label htmlFor={idQualidade}>Qualidade</label>
        <select id={idQualidade} value={qualidade} onChange={(evento) => definirQualidade(evento.target.value as Qualidade)}>
          <option value="baixa">Baixa</option>
          <option value="media">Média</option>
        </select>
      </div>
    </div>
  )
}
