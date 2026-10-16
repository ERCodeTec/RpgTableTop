import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { ApiError } from '../../shared/api/http'
import { CLASSES, COR_BORDA_PADRAO, NOME_DA_CLASSE, type ClasseDnd } from '../../shared/classes'
import { ClassIcon } from '../../shared/icones/ClassIcon'
import { Campo } from '../../shared/ui/Campo'
import { Dialogo } from '../../shared/ui/Dialogo'
import { DESLOCAMENTO_MAXIMO, DESLOCAMENTO_MINIMO, DESLOCAMENTO_PADRAO, VISAO_MAXIMA, VISAO_PADRAO } from '../mapa/mapaApi'
import {
  atualizarPersonagem,
  CHAVE_PERSONAGENS,
  criarPersonagem,
  type DadosDoPersonagem,
  type Personagem,
} from './personagensApi'

interface PersonagemFormProps {
  /** Personagem em edição; ausente ao criar um novo. */
  personagem?: Personagem
  aoFechar: () => void
}

export function PersonagemForm({ personagem, aoFechar }: PersonagemFormProps) {
  const queryClient = useQueryClient()
  const [nome, setNome] = useState(personagem?.nome ?? '')
  const [classe, setClasse] = useState<ClasseDnd>(personagem?.classe ?? 'Guerreiro')
  const [nivel, setNivel] = useState(String(personagem?.nivel ?? 1))
  const [hpMaximo, setHpMaximo] = useState(String(personagem?.hpMaximo ?? 10))
  const [corBorda, setCorBorda] = useState(personagem?.corBorda ?? COR_BORDA_PADRAO)
  const [deslocamento, setDeslocamento] = useState(String(personagem?.deslocamento ?? DESLOCAMENTO_PADRAO))
  const [visao, setVisao] = useState(String(personagem?.visao ?? VISAO_PADRAO))

  const salvar = useMutation({
    mutationFn: (dados: DadosDoPersonagem) =>
      personagem ? atualizarPersonagem(personagem.id, dados) : criarPersonagem(dados),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: CHAVE_PERSONAGENS })
      aoFechar()
    },
  })

  const erro = salvar.error instanceof ApiError ? salvar.error : null
  const erroGeral = salvar.error && (!erro || Object.keys(erro.erros).length === 0)

  function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    // Campos numéricos vazios viram 0 e a API devolve a mensagem de faixa válida.
    salvar.mutate({
      nome,
      classe,
      nivel: Number(nivel) || 0,
      hpMaximo: Number(hpMaximo) || 0,
      corBorda,
      deslocamento: Number(deslocamento) || 0,
      visao: Number(visao) || 0,
    })
  }

  return (
    <Dialogo titulo={personagem ? `Editar ${personagem.nome}` : 'Novo personagem'} aoFechar={aoFechar}>
      <form className="form-personagem" onSubmit={aoEnviar} noValidate>
        {erroGeral && (
          <p className="aviso-erro" role="alert">
            {erro?.message ?? 'Não foi possível salvar. Tente de novo.'}
          </p>
        )}

        <Campo
          rotulo="Nome"
          value={nome}
          maxLength={60}
          onChange={(evento) => setNome(evento.target.value)}
          erros={erro?.erros.nome}
          autoFocus
          required
        />

        <fieldset className="classes">
          <legend>Classe</legend>
          <div className="classes-grade">
            {CLASSES.map((opcao) => (
              <label key={opcao} className="classe-opcao">
                <input
                  type="radio"
                  name="classe"
                  value={opcao}
                  checked={classe === opcao}
                  onChange={() => setClasse(opcao)}
                />
                <ClassIcon classe={opcao} corBorda={classe === opcao ? corBorda : '#3d585e'} tamanho={44} />
                <span>{NOME_DA_CLASSE[opcao]}</span>
              </label>
            ))}
          </div>
          {erro?.erros.classe && <p className="campo-erro">{erro.erros.classe[0]}</p>}
        </fieldset>

        <div className="form-linha">
          <Campo
            rotulo="Nível"
            type="number"
            inputMode="numeric"
            min={1}
            max={20}
            value={nivel}
            onChange={(evento) => setNivel(evento.target.value)}
            erros={erro?.erros.nivel}
            required
          />
          <Campo
            rotulo="HP máximo"
            type="number"
            inputMode="numeric"
            min={1}
            max={999}
            value={hpMaximo}
            onChange={(evento) => setHpMaximo(evento.target.value)}
            erros={erro?.erros.hpMaximo}
            required
          />
          <Campo
            rotulo="Deslocamento"
            type="number"
            inputMode="numeric"
            min={DESLOCAMENTO_MINIMO}
            max={DESLOCAMENTO_MAXIMO}
            value={deslocamento}
            onChange={(evento) => setDeslocamento(evento.target.value)}
            erros={erro?.erros.deslocamento}
            dica="Em células: 6 = 30 ft."
            required
          />
          <Campo
            rotulo="Visão"
            type="number"
            inputMode="numeric"
            min={0}
            max={VISAO_MAXIMA}
            value={visao}
            onChange={(evento) => setVisao(evento.target.value)}
            erros={erro?.erros.visao}
            dica="Até onde enxerga nos mapas com névoa, em células: 12 = 60 ft."
            required
          />
          <div className="campo campo-cor">
            <label htmlFor="cor-borda">Cor da borda</label>
            <input
              id="cor-borda"
              type="color"
              value={corBorda}
              onChange={(evento) => setCorBorda(evento.target.value.toUpperCase())}
            />
            {erro?.erros.corBorda && <p className="campo-erro">{erro.erros.corBorda[0]}</p>}
          </div>
        </div>

        <div className="dialogo-acoes">
          <button className="botao" type="button" onClick={aoFechar}>
            Cancelar
          </button>
          <button className="botao botao-primario" type="submit" disabled={salvar.isPending}>
            {salvar.isPending ? 'Salvando…' : personagem ? 'Salvar alterações' : 'Criar personagem'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
