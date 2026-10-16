import { memo } from 'react'
import { useEditorStore } from './editorStore'
import { NOME_DA_ESTRUTURA } from './mapaApi'
import { useMapaStore } from './mapaStore'
import { PainelDaEstrutura } from './PainelDaEstrutura'

/** Aba Estruturas do painel: as propriedades da selecionada e a lista do mapa na tela. */
export function PainelDeEstruturas({ mesaId }: { mesaId: string }) {
  const ordem = useMapaStore((estado) => estado.ordemDasEstruturas)
  const modo = useEditorStore((estado) => estado.modo)
  const selecionadaId = useEditorStore((estado) => estado.selecionadaId)
  const selecionada = useMapaStore((estado) => (selecionadaId ? estado.estruturas[selecionadaId] : undefined))
  const editando = modo === 'editar'

  return (
    <section className="painel-mestre-secao">
      <div className="painel-mestre-topo">
        <h2>Estruturas</h2>
        {!editando && (
          <button className="botao botao-discreto" type="button" onClick={() => useEditorStore.getState().definirModo('editar')}>
            Editar mapa
          </button>
        )}
      </div>

      {!editando ? (
        <p className="painel-mestre-vazio">
          Entre em "Editar mapa" para desenhar paredes e colocar portas, pilares e obstáculos. Jogando, um clique em uma
          porta abre ou fecha.
        </p>
      ) : selecionada ? (
        <PainelDaEstrutura key={selecionada.id} mesaId={mesaId} estrutura={selecionada} />
      ) : (
        <p className="painel-mestre-vazio">Escolha uma ferramenta acima do mapa, ou selecione uma estrutura para alterá-la.</p>
      )}

      {ordem.length > 0 && (
        <ul className="painel-lista">
          {ordem.map((id) => (
            <LinhaDaEstrutura key={id} id={id} selecionada={editando && id === selecionadaId} />
          ))}
        </ul>
      )}
    </section>
  )
}

const LinhaDaEstrutura = memo(function LinhaDaEstrutura({ id, selecionada }: { id: string; selecionada: boolean }) {
  const estrutura = useMapaStore((estado) => estado.estruturas[id])
  if (!estrutura) return null

  return (
    <li className={`painel-item ${selecionada ? 'painel-item-atual' : ''}`}>
      <button
        className="painel-token"
        type="button"
        aria-pressed={selecionada}
        onClick={() => {
          const editor = useEditorStore.getState()
          if (editor.modo !== 'editar') editor.definirModo('editar')
          editor.definirFerramenta('selecionar')
          editor.selecionar(selecionada ? null : id)
        }}
      >
        <span className="painel-cor" style={{ background: estrutura.cor }} aria-hidden="true" />
        <span className="painel-item-dados">
          <span className="painel-item-nome">
            <strong>{NOME_DA_ESTRUTURA[estrutura.tipo]}</strong>
            {estrutura.tipo === 'Porta' && <span className="selo">{estrutura.portaAberta ? 'Aberta' : 'Fechada'}</span>}
          </span>
          <span className="painel-item-detalhe">
            {estrutura.largura} × {estrutura.profundidade} em ({estrutura.x}, {estrutura.y})
          </span>
        </span>
      </button>
    </li>
  )
})
