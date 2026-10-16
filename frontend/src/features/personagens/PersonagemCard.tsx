import { NOME_DA_CLASSE } from '../../shared/classes'
import { DiscoDoModelo } from '../biblioteca/DiscoDoModelo'
import type { Personagem } from './personagensApi'

interface PersonagemCardProps {
  personagem: Personagem
  aoEditar: () => void
  aoExcluir: () => void
}

export function PersonagemCard({ personagem, aoEditar, aoExcluir }: PersonagemCardProps) {
  return (
    <li className="personagem">
      <DiscoDoModelo
        modelo={{ tipo: 'Personagem', icone: personagem.classe, imagemId: personagem.imagemId, corBorda: personagem.corBorda }}
        tamanho={72}
      />
      <div className="personagem-dados">
        <h2>{personagem.nome}</h2>
        <p>
          {NOME_DA_CLASSE[personagem.classe]}, nível {personagem.nivel}
        </p>
        <p className="personagem-hp">
          <span className="personagem-hp-valor">{personagem.hpMaximo}</span> de HP máximo
        </p>
        <p>
          Deslocamento de {personagem.deslocamento} células ({personagem.deslocamento * 5} ft)
        </p>
        <p>
          Visão de {personagem.visao} células ({personagem.visao * 5} ft)
        </p>
      </div>
      <div className="personagem-acoes">
        <button className="botao botao-discreto" type="button" onClick={aoEditar} aria-label={`Editar ${personagem.nome}`}>
          Editar
        </button>
        <button className="botao botao-discreto botao-perigo" type="button" onClick={aoExcluir} aria-label={`Excluir ${personagem.nome}`}>
          Excluir
        </button>
      </div>
    </li>
  )
}
