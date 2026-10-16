import { useMutation } from '@tanstack/react-query'
import { useId } from 'react'
import { useEditorStore } from './editorStore'
import { definirFundo, FUNDO_ESCALA_MAXIMA, FUNDO_ESCALA_MINIMA, type DadosDoFundo } from './mapaApi'
import { useMapaStore } from './mapaStore'

// O controle anda em escala logarítmica: o mesmo deslocamento dobra ou reduz à metade, em qualquer ponto.
const paraControle = (escala: number) => Math.log(escala)
const doControle = (valor: number) => Math.round(Math.exp(valor) * 1000) / 1000

/** Ajuste da imagem de fundo ao grid: arrastar no mapa desloca; aqui se escala, salva ou cancela. */
export function BarraDoFundo({ mapaId }: { mapaId: string }) {
  const idEscala = useId()
  const fundo = useEditorStore((estado) => estado.fundo)
  const { definirFundo: definirRascunho, definirModo } = useEditorStore.getState()

  const salvar = useMutation({
    mutationFn: (dados: DadosDoFundo) => definirFundo(mapaId, dados),
    onSuccess: (mapa) => {
      useMapaStore.getState().mapaAtualizado(mapa)
      definirModo('jogar')
    },
  })

  if (!fundo) return null

  return (
    <div className="mapa-editor">
      <div className="mapa-editor-ferramentas" role="group" aria-label="Ajuste do fundo">
        <label htmlFor={idEscala}>Escala</label>
        <input
          id={idEscala}
          type="range"
          min={paraControle(FUNDO_ESCALA_MINIMA)}
          max={paraControle(FUNDO_ESCALA_MAXIMA)}
          step={0.005}
          value={paraControle(fundo.escala)}
          onChange={(evento) => definirRascunho({ ...fundo, escala: doControle(Number(evento.target.value)) })}
        />
        <output htmlFor={idEscala}>{Math.round(fundo.escala * 100)}%</output>
        <button className="botao botao-discreto" type="button" onClick={() => definirRascunho({ ...fundo, escala: 1, offsetX: 0, offsetY: 0 })}>
          Encaixar na largura
        </button>
        <span className="mapa-editor-separador" aria-hidden="true" />
        <button className="botao botao-primario" type="button" disabled={salvar.isPending} onClick={() => salvar.mutate(fundo)}>
          {salvar.isPending ? 'Salvando…' : 'Salvar fundo'}
        </button>
        <button className="botao" type="button" onClick={() => definirModo('jogar')}>
          Cancelar
        </button>
        <button
          className="botao botao-discreto botao-perigo"
          type="button"
          disabled={salvar.isPending}
          onClick={() => salvar.mutate({ imagemId: null, escala: 1, offsetX: 0, offsetY: 0 })}
        >
          Remover fundo
        </button>
      </div>
      {salvar.isError && (
        <p className="aviso-erro" role="alert">
          {salvar.error.message || 'Não foi possível salvar o fundo.'}
        </p>
      )}
      <p className="mapa-dica">Arraste no mapa para deslocar a imagem até as células dela coincidirem com a grade.</p>
    </div>
  )
}
