import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useId, useState, type FormEvent } from 'react'
import { ApiError } from '../../shared/api/http'
import { Campo } from '../../shared/ui/Campo'
import { Dialogo } from '../../shared/ui/Dialogo'
import {
  atualizarMapa,
  chaveDosMapas,
  COR_FUNDO_PADRAO,
  criarMapa,
  DIMENSAO_MAXIMA,
  DIMENSAO_MINIMA,
  type DadosDoMapa,
  type Mapa,
} from './mapaApi'
import { useMapaStore } from './mapaStore'

interface MapaFormDialogoProps {
  mesaId: string
  /** Mapa em edição; ausente ao criar um novo. */
  mapa?: Mapa
  aoFechar: () => void
  aoSalvar?: (mapa: Mapa) => void
}

export function MapaFormDialogo({ mesaId, mapa, aoFechar, aoSalvar }: MapaFormDialogoProps) {
  const queryClient = useQueryClient()
  const idCor = useId()
  const [nome, setNome] = useState(mapa?.nome ?? '')
  const [largura, setLargura] = useState(String(mapa?.largura ?? 20))
  const [altura, setAltura] = useState(String(mapa?.altura ?? 15))
  const [corFundo, setCorFundo] = useState(mapa?.corFundo ?? COR_FUNDO_PADRAO)

  const salvar = useMutation({
    mutationFn: (dados: DadosDoMapa) => (mapa ? atualizarMapa(mapa.id, dados) : criarMapa(mesaId, dados)),
    onSuccess: async (salvo) => {
      useMapaStore.getState().mapaAtualizado(salvo)
      await queryClient.invalidateQueries({ queryKey: chaveDosMapas(mesaId) })
      aoFechar()
      aoSalvar?.(salvo)
    },
  })

  const erro = salvar.error instanceof ApiError ? salvar.error : null
  const erroGeral = salvar.error && (!erro || Object.keys(erro.erros).length === 0)
  const dicaDeDimensao = `De ${DIMENSAO_MINIMA} a ${DIMENSAO_MAXIMA} células`

  function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    // Campos numéricos vazios viram 0 e a API devolve a mensagem de faixa válida.
    salvar.mutate({ nome, largura: Number(largura) || 0, altura: Number(altura) || 0, corFundo })
  }

  return (
    <Dialogo titulo={mapa ? `Editar ${mapa.nome}` : 'Novo mapa'} aoFechar={aoFechar}>
      <form className="form-pilha" onSubmit={aoEnviar} noValidate>
        {erroGeral && (
          <p className="aviso-erro" role="alert">
            {salvar.error.message || 'Não foi possível salvar. Tente de novo.'}
          </p>
        )}
        <Campo
          rotulo="Nome do mapa"
          value={nome}
          maxLength={80}
          onChange={(evento) => setNome(evento.target.value)}
          erros={erro?.erros.nome}
          autoFocus
          required
        />
        <div className="form-linha">
          <Campo
            rotulo="Largura"
            type="number"
            inputMode="numeric"
            min={DIMENSAO_MINIMA}
            max={DIMENSAO_MAXIMA}
            value={largura}
            onChange={(evento) => setLargura(evento.target.value)}
            erros={erro?.erros.largura}
            dica={dicaDeDimensao}
            required
          />
          <Campo
            rotulo="Altura"
            type="number"
            inputMode="numeric"
            min={DIMENSAO_MINIMA}
            max={DIMENSAO_MAXIMA}
            value={altura}
            onChange={(evento) => setAltura(evento.target.value)}
            erros={erro?.erros.altura}
            dica={dicaDeDimensao}
            required
          />
          <div className="campo campo-cor">
            <label htmlFor={idCor}>Cor de fundo</label>
            <input
              id={idCor}
              type="color"
              value={corFundo}
              onChange={(evento) => setCorFundo(evento.target.value.toUpperCase())}
            />
            {erro?.erros.corFundo && <p className="campo-erro">{erro.erros.corFundo[0]}</p>}
          </div>
        </div>
        <div className="dialogo-acoes">
          <button className="botao" type="button" onClick={aoFechar}>
            Cancelar
          </button>
          <button className="botao botao-primario" type="submit" disabled={salvar.isPending}>
            {salvar.isPending ? 'Salvando…' : mapa ? 'Salvar alterações' : 'Criar mapa'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
