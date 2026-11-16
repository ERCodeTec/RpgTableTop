import { useMutation } from '@tanstack/react-query'
import { useId, useState } from 'react'
import { useEditorStore } from './editorStore'
import { comAltura, comLargura, escalaDaAltura } from './fundo'
import { definirFundo, FUNDO_ESCALA_MAXIMA, FUNDO_ESCALA_MINIMA, type DadosDoFundo } from './mapaApi'
import { useMapaStore } from './mapaStore'

// O controle anda em escala logarítmica: o mesmo deslocamento dobra ou reduz à metade, em qualquer ponto.
const paraControle = (escala: number) => Math.log(escala)
const doControle = (valor: number) => Math.round(Math.exp(valor) * 1000) / 1000

/** Ajuste da imagem de fundo ao grid: no mapa se desloca e redimensiona; aqui, o ajuste fino, salvar ou cancelar. */
export function BarraDoFundo({ mapaId }: { mapaId: string }) {
  const idLargura = useId()
  const idAltura = useId()
  const [manterProporcao, setManterProporcao] = useState(true)
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
        <label htmlFor={idLargura}>Largura</label>
        <input
          id={idLargura}
          type="range"
          min={paraControle(FUNDO_ESCALA_MINIMA)}
          max={paraControle(FUNDO_ESCALA_MAXIMA)}
          step={0.005}
          value={paraControle(fundo.escala)}
          onChange={(evento) => definirRascunho(comLargura(fundo, doControle(Number(evento.target.value)), manterProporcao))}
        />
        <output htmlFor={idLargura}>{Math.round(fundo.escala * 100)}%</output>
        <label htmlFor={idAltura}>Altura</label>
        <input
          id={idAltura}
          type="range"
          min={paraControle(FUNDO_ESCALA_MINIMA)}
          max={paraControle(FUNDO_ESCALA_MAXIMA)}
          step={0.005}
          value={paraControle(escalaDaAltura(fundo))}
          onChange={(evento) => definirRascunho(comAltura(fundo, doControle(Number(evento.target.value)), manterProporcao))}
        />
        <output htmlFor={idAltura}>{Math.round(escalaDaAltura(fundo) * 100)}%</output>
        <button
          className="botao botao-discreto"
          type="button"
          aria-pressed={manterProporcao}
          title="Ligado, mexer na largura ou na altura muda as duas juntas"
          onClick={() => setManterProporcao((atual) => !atual)}
        >
          Manter proporção
        </button>
        <button
          className="botao botao-discreto"
          type="button"
          disabled={fundo.escalaAltura === null}
          title="Volta a imagem à proporção original, mantendo a largura"
          onClick={() => definirRascunho({ ...fundo, escalaAltura: null })}
        >
          Restaurar proporção
        </button>
        <button
          className="botao botao-discreto"
          type="button"
          onClick={() => definirRascunho({ ...fundo, escala: 1, escalaAltura: null, offsetX: 0, offsetY: 0 })}
        >
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
          onClick={() => salvar.mutate({ imagemId: null, escala: 1, offsetX: 0, offsetY: 0, escalaAltura: null })}
        >
          Remover fundo
        </button>
      </div>
      {salvar.isError && (
        <p className="aviso-erro" role="alert">
          {salvar.error.message || 'Não foi possível salvar o fundo.'}
        </p>
      )}
      <p className="mapa-dica">
        Arraste no mapa para deslocar a imagem até as células dela coincidirem com a grade. Puxe as bordas para esticar um
        lado e os cantos para mudar o tamanho mantendo a forma.
      </p>
    </div>
  )
}
