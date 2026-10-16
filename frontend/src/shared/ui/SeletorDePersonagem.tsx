import { NOME_DA_CLASSE, type ClasseDnd } from '../classes'
import { ClassIcon } from '../icones/ClassIcon'

interface OpcaoDePersonagem {
  id: string
  nome: string
  classe: ClasseDnd
  nivel: number
  corBorda: string
}

interface SeletorDePersonagemProps {
  /** Nome do grupo de radio; precisa ser único na tela. */
  nome: string
  personagens: OpcaoDePersonagem[]
  selecionadoId: string | null
  aoSelecionar: (id: string) => void
}

export function SeletorDePersonagem({ nome, personagens, selecionadoId, aoSelecionar }: SeletorDePersonagemProps) {
  return (
    <div className="seletor-personagem" role="radiogroup" aria-label="Personagem">
      {personagens.map((personagem) => (
        <label key={personagem.id} className="seletor-personagem-opcao">
          <input
            type="radio"
            name={nome}
            value={personagem.id}
            checked={selecionadoId === personagem.id}
            onChange={() => aoSelecionar(personagem.id)}
          />
          <ClassIcon classe={personagem.classe} corBorda={personagem.corBorda} tamanho={44} />
          <span className="seletor-personagem-texto">
            <strong>{personagem.nome}</strong>
            <span>
              {NOME_DA_CLASSE[personagem.classe]}, nível {personagem.nivel}
            </span>
          </span>
        </label>
      ))}
    </div>
  )
}
