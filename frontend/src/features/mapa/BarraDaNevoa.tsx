import { useEditorStore, type Pincel } from './editorStore'

const PINCEIS: { id: Pincel; nome: string; dica: string }[] = [
  { id: 'revelar', nome: 'Revelar', dica: 'As células marcadas aparecem para todos os jogadores, como se estivessem à vista.' },
  { id: 'cobrir', nome: 'Cobrir', dica: 'As células marcadas voltam ao escuro, também no que os jogadores já tinham explorado.' },
]

/** O pincel da névoa: arrastar no mapa marca um retângulo, que é revelado ou coberto ao soltar. */
export function BarraDaNevoa() {
  const pincel = useEditorStore((estado) => estado.pincel)
  const { definirPincel, definirModo } = useEditorStore.getState()
  const atual = PINCEIS.find((opcao) => opcao.id === pincel)!

  return (
    <div className="mapa-editor">
      <div className="mapa-editor-ferramentas" role="group" aria-label="Pincel da névoa">
        {PINCEIS.map((opcao) => (
          <button
            key={opcao.id}
            className="botao botao-discreto mapa-editor-ferramenta"
            type="button"
            aria-pressed={pincel === opcao.id}
            onClick={() => definirPincel(opcao.id)}
          >
            {opcao.nome}
          </button>
        ))}
        <span className="mapa-editor-separador" aria-hidden="true" />
        <button className="botao" type="button" onClick={() => definirModo('jogar')}>
          Concluir
        </button>
      </div>
      <p className="mapa-dica" aria-live="polite">
        {atual.dica} Arraste no mapa para marcar um retângulo; um clique marca uma célula. O que os tokens veem continua
        aparecendo.
      </p>
    </div>
  )
}
