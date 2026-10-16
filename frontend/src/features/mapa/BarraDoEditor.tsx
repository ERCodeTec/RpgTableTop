import { desfazer, refazer } from './acoesDoEditor'
import { FERRAMENTAS, useEditorStore } from './editorStore'

/** Ferramentas do modo de edição e o histórico de ações. */
export function BarraDoEditor() {
  const ferramenta = useEditorStore((estado) => estado.ferramenta)
  const manterFerramenta = useEditorStore((estado) => estado.manterFerramenta)
  const podeDesfazer = useEditorStore((estado) => estado.pilhaDeDesfazer.length > 0 && !estado.ocupado)
  const podeRefazer = useEditorStore((estado) => estado.pilhaDeRefazer.length > 0 && !estado.ocupado)
  const { definirFerramenta, alternarManterFerramenta } = useEditorStore.getState()
  const atual = FERRAMENTAS.find((opcao) => opcao.id === ferramenta)!
  const desenhando = ferramenta !== 'selecionar'

  return (
    <div className="mapa-editor">
      <div className="mapa-editor-ferramentas" role="group" aria-label="Ferramentas de edição">
        {FERRAMENTAS.map((opcao, indice) => (
          <button
            key={opcao.id}
            className="botao botao-discreto mapa-editor-ferramenta"
            type="button"
            aria-pressed={ferramenta === opcao.id}
            aria-keyshortcuts={String(indice + 1)}
            title={
              ferramenta === opcao.id && opcao.id !== 'selecionar'
                ? `Clique de novo (ou Esc) para voltar a Selecionar`
                : `${opcao.nome} (atalho: ${indice + 1})`
            }
            onClick={() => definirFerramenta(opcao.id)}
          >
            <kbd aria-hidden="true">{indice + 1}</kbd>
            {opcao.nome}
          </button>
        ))}
        <span className="mapa-editor-separador" aria-hidden="true" />
        <button
          className="botao botao-discreto"
          type="button"
          aria-pressed={manterFerramenta}
          title="Ligado: a ferramenta continua ativa para colocar várias peças seguidas. Desligado: volta para Selecionar depois de cada peça."
          onClick={alternarManterFerramenta}
        >
          Manter ferramenta
        </button>
        <span className="mapa-editor-separador" aria-hidden="true" />
        <button className="botao botao-discreto" type="button" disabled={!podeDesfazer} onClick={() => void desfazer()} title="Atalho: Ctrl+Z">
          Desfazer
        </button>
        <button className="botao botao-discreto" type="button" disabled={!podeRefazer} onClick={() => void refazer()} title="Atalho: Ctrl+Shift+Z">
          Refazer
        </button>
      </div>
      <p className="mapa-dica" aria-live="polite">
        {atual.dica}{' '}
        {desenhando ? (
          <>
            {manterFerramenta
              ? 'A ferramenta fica ativa até você trocar.'
              : 'Depois de colocar, volta para Selecionar com a peça nova selecionada.'}{' '}
            <button className="link-botao" type="button" onClick={() => definirFerramenta('selecionar')}>
              Voltar a Selecionar
            </button>{' '}
            (Esc)
          </>
        ) : (
          'Atalhos: 1 a 6 trocam a ferramenta, Delete remove, R gira, Ctrl+D duplica, Ctrl+Z desfaz, Esc tira a seleção.'
        )}
      </p>
    </div>
  )
}
