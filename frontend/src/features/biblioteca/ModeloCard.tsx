import { NOME_DA_EXIBICAO, NOME_DO_TIPO } from '../mapa/mapaApi'
import type { ModeloDeToken } from './bibliotecaApi'
import { DiscoDoModelo } from './DiscoDoModelo'

interface ModeloCardProps {
  modelo: ModeloDeToken
  /** Nome da categoria, para a aba "Todas". */
  categoria?: string
  aoEditar: () => void
  aoExcluir: () => void
}

function resumoDoConteudo(modelo: ModeloDeToken): string {
  const partes = [modelo.conteudoImagemId && 'imagem', modelo.descricao && 'descrição'].filter(Boolean)
  return partes.length ? `Ao examinar: ${partes.join(' e ')}` : 'Sem conteúdo para examinar'
}

export function ModeloCard({ modelo, categoria, aoEditar, aoExcluir }: ModeloCardProps) {
  return (
    <li className="personagem">
      <DiscoDoModelo modelo={modelo} tamanho={72} />
      <div className="personagem-dados">
        <h2>{modelo.nome}</h2>
        <p>
          {NOME_DO_TIPO[modelo.tipo]}
          {categoria && ` · ${categoria}`}
        </p>
        {modelo.tipo === 'Interacao' ? (
          <p>{resumoDoConteudo(modelo)}</p>
        ) : (
          <>
            <p className="personagem-hp">
              <span className="personagem-hp-valor">{modelo.hpMaximo}</span> de HP · jogadores veem:{' '}
              {NOME_DA_EXIBICAO[modelo.exibicaoHp].toLocaleLowerCase('pt-BR')}
            </p>
            <p>
              Deslocamento de {modelo.deslocamento} células
              {modelo.visao > 0 && ` · visão de aliado de ${modelo.visao} células`}
            </p>
          </>
        )}
      </div>
      <div className="personagem-acoes">
        <button className="botao botao-discreto" type="button" onClick={aoEditar} aria-label={`Editar ${modelo.nome}`}>
          Editar
        </button>
        <button className="botao botao-discreto botao-perigo" type="button" onClick={aoExcluir} aria-label={`Excluir ${modelo.nome}`}>
          Excluir
        </button>
      </div>
    </li>
  )
}
