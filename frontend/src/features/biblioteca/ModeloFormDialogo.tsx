import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useId, useState, type FormEvent } from 'react'
import { ApiError } from '../../shared/api/http'
import { Campo } from '../../shared/ui/Campo'
import { Dialogo } from '../../shared/ui/Dialogo'
import { CampoDeImagem, ICONE_INICIAL, SeletorDeIcone } from '../mapa/CamposDoToken'
import {
  COR_TOKEN_PADRAO,
  DESLOCAMENTO_MAXIMO,
  DESLOCAMENTO_MINIMO,
  DESLOCAMENTO_PADRAO,
  NOME_DA_EXIBICAO,
  NOME_DO_TIPO,
  VISAO_MAXIMA,
  type ExibicaoHp,
} from '../mapa/mapaApi'
import {
  atualizarModelo,
  CHAVE_CATEGORIAS,
  CHAVE_MODELOS,
  criarModelo,
  type Categoria,
  type ModeloDeToken,
  type TipoDoModelo,
} from './bibliotecaApi'

const TIPOS: TipoDoModelo[] = ['Inimigo', 'Npc']
const EXIBICOES: ExibicaoHp[] = ['Oculto', 'Barra', 'Completo']

interface ModeloFormDialogoProps {
  categorias: Categoria[]
  /** Modelo em edição; ausente ao criar um novo. */
  modelo?: ModeloDeToken
  /** Categoria já escolhida no novo modelo (a aba aberta na página). */
  categoriaInicial?: string | null
  aoFechar: () => void
}

export function ModeloFormDialogo({ categorias, modelo, categoriaInicial = null, aoFechar }: ModeloFormDialogoProps) {
  const queryClient = useQueryClient()
  const idCategoria = useId()
  const idTipo = useId()
  const idExibicao = useId()
  const idCor = useId()
  const [categoriaId, setCategoriaId] = useState<string | null>(modelo ? modelo.categoriaId : categoriaInicial)
  const [tipo, setTipo] = useState<TipoDoModelo>(modelo?.tipo ?? 'Inimigo')
  const [nome, setNome] = useState(modelo?.nome ?? '')
  const [hpMaximo, setHpMaximo] = useState(String(modelo?.hpMaximo ?? 10))
  const [icone, setIcone] = useState(modelo?.icone ?? ICONE_INICIAL.Inimigo)
  const [imagemId, setImagemId] = useState(modelo?.imagemId ?? null)
  const [corBorda, setCorBorda] = useState(modelo?.corBorda ?? COR_TOKEN_PADRAO)
  const [deslocamento, setDeslocamento] = useState(String(modelo?.deslocamento ?? DESLOCAMENTO_PADRAO))
  const [visao, setVisao] = useState(String(modelo?.visao ?? 0))
  const [exibicaoHp, setExibicaoHp] = useState<ExibicaoHp>(modelo?.exibicaoHp ?? 'Barra')

  const salvar = useMutation({
    mutationFn: () => {
      const dados = {
        categoriaId,
        tipo,
        nome,
        hpMaximo: Number(hpMaximo) || 0,
        icone,
        imagemId,
        corBorda,
        deslocamento: Number(deslocamento) || 0,
        visao: Number(visao) || 0,
        exibicaoHp,
      }
      return modelo ? atualizarModelo(modelo.id, dados) : criarModelo(dados)
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: CHAVE_MODELOS }),
        queryClient.invalidateQueries({ queryKey: CHAVE_CATEGORIAS }),
      ])
      aoFechar()
    },
  })

  const erro = salvar.error instanceof ApiError ? salvar.error : null
  const erroGeral = salvar.error && (!erro || Object.keys(erro.erros).length === 0)

  function aoTrocarTipo(novo: TipoDoModelo) {
    setTipo(novo)
    setIcone(ICONE_INICIAL[novo])
  }

  function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    salvar.mutate()
  }

  return (
    <Dialogo titulo={modelo ? `Editar ${modelo.nome}` : 'Novo modelo'} aoFechar={aoFechar}>
      <form className="form-pilha" onSubmit={aoEnviar} noValidate>
        {erroGeral && (
          <p className="aviso-erro" role="alert">
            {salvar.error.message || 'Não foi possível salvar. Tente de novo.'}
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

        <div className="form-linha">
          <div className="campo">
            <label htmlFor={idCategoria}>Categoria</label>
            <select id={idCategoria} value={categoriaId ?? ''} onChange={(evento) => setCategoriaId(evento.target.value || null)}>
              <option value="">Sem categoria</option>
              {categorias.map((categoria) => (
                <option key={categoria.id} value={categoria.id}>
                  {categoria.nome}
                </option>
              ))}
            </select>
            {erro?.erros.categoriaId && <p className="campo-erro">{erro.erros.categoriaId[0]}</p>}
          </div>
          <div className="campo">
            <label htmlFor={idTipo}>Tipo no mapa</label>
            <select id={idTipo} value={tipo} onChange={(evento) => aoTrocarTipo(evento.target.value as TipoDoModelo)}>
              {TIPOS.map((opcao) => (
                <option key={opcao} value={opcao}>
                  {NOME_DO_TIPO[opcao]}
                </option>
              ))}
            </select>
            {erro?.erros.tipo && <p className="campo-erro">{erro.erros.tipo[0]}</p>}
          </div>
        </div>

        <SeletorDeIcone tipo={tipo} icone={icone} corBorda={corBorda} aoEscolher={setIcone} erros={erro?.erros.icone} />

        <CampoDeImagem
          fonte="pessoal"
          imagemId={imagemId}
          aoMudar={setImagemId}
          titulo="Imagem do modelo"
          erros={erro?.erros.imagemId}
        />

        <div className="form-linha">
          <Campo
            rotulo="HP máximo"
            type="number"
            inputMode="numeric"
            min={1}
            max={999}
            value={hpMaximo}
            onChange={(evento) => setHpMaximo(evento.target.value)}
            erros={erro?.erros.hpMaximo}
            dica="Cada token colocado começa com o HP cheio."
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
            dica="Células por rodada: 6 = 30 ft."
            required
          />
          <Campo
            rotulo="Visão de aliado"
            type="number"
            inputMode="numeric"
            min={0}
            max={VISAO_MAXIMA}
            value={visao}
            onChange={(evento) => setVisao(evento.target.value)}
            erros={erro?.erros.visao}
            dica="Com a névoa ligada, os jogadores enxergam também por este token. 0 = não empresta visão."
          />
          <div className="campo">
            <label htmlFor={idExibicao}>HP para os jogadores</label>
            <select id={idExibicao} value={exibicaoHp} onChange={(evento) => setExibicaoHp(evento.target.value as ExibicaoHp)}>
              {EXIBICOES.map((opcao) => (
                <option key={opcao} value={opcao}>
                  {NOME_DA_EXIBICAO[opcao]}
                </option>
              ))}
            </select>
          </div>
          <div className="campo campo-cor">
            <label htmlFor={idCor}>Cor da borda</label>
            <input id={idCor} type="color" value={corBorda} onChange={(evento) => setCorBorda(evento.target.value.toUpperCase())} />
            {erro?.erros.corBorda && <p className="campo-erro">{erro.erros.corBorda[0]}</p>}
          </div>
        </div>

        <div className="dialogo-acoes">
          <button className="botao" type="button" onClick={aoFechar}>
            Cancelar
          </button>
          <button className="botao botao-primario" type="submit" disabled={salvar.isPending}>
            {salvar.isPending ? 'Salvando…' : modelo ? 'Salvar alterações' : 'Criar modelo'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
