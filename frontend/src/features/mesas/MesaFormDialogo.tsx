import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useId, useState, type FormEvent } from 'react'
import { ApiError } from '../../shared/api/http'
import { Campo } from '../../shared/ui/Campo'
import { Dialogo } from '../../shared/ui/Dialogo'
import { atualizarMesa, CHAVE_MESAS, chaveDaMesa, criarMesa, type DadosDaMesa, type MesaDetalhe } from './mesasApi'

interface MesaFormDialogoProps {
  /** Mesa em edição; ausente ao criar uma nova. */
  mesa?: MesaDetalhe
  aoFechar: () => void
  aoSalvar?: (mesa: MesaDetalhe) => void
}

export function MesaFormDialogo({ mesa, aoFechar, aoSalvar }: MesaFormDialogoProps) {
  const queryClient = useQueryClient()
  const idDescricao = useId()
  const [nome, setNome] = useState(mesa?.nome ?? '')
  const [descricao, setDescricao] = useState(mesa?.descricao ?? '')

  const salvar = useMutation({
    mutationFn: (dados: DadosDaMesa) => (mesa ? atualizarMesa(mesa.id, dados) : criarMesa(dados)),
    onSuccess: async (salva) => {
      queryClient.setQueryData(chaveDaMesa(salva.id), salva)
      await queryClient.invalidateQueries({ queryKey: CHAVE_MESAS, exact: true })
      aoFechar()
      aoSalvar?.(salva)
    },
  })

  const erro = salvar.error instanceof ApiError ? salvar.error : null
  const erroGeral = salvar.error && (!erro || Object.keys(erro.erros).length === 0)

  function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    salvar.mutate({ nome, descricao })
  }

  return (
    <Dialogo titulo={mesa ? 'Editar mesa' : 'Criar mesa'} aoFechar={aoFechar}>
      <form className="form-pilha" onSubmit={aoEnviar} noValidate>
        {erroGeral && (
          <p className="aviso-erro" role="alert">
            {salvar.error.message || 'Não foi possível salvar. Tente de novo.'}
          </p>
        )}
        <Campo
          rotulo="Nome da mesa"
          value={nome}
          maxLength={80}
          onChange={(evento) => setNome(evento.target.value)}
          erros={erro?.erros.nome}
          autoFocus
          required
        />
        <div className="campo">
          <label htmlFor={idDescricao}>Descrição (opcional)</label>
          <textarea
            id={idDescricao}
            rows={3}
            maxLength={500}
            value={descricao}
            onChange={(evento) => setDescricao(evento.target.value)}
            aria-invalid={erro?.erros.descricao ? true : undefined}
          />
          {erro?.erros.descricao && <p className="campo-erro">{erro.erros.descricao[0]}</p>}
        </div>
        <div className="dialogo-acoes">
          <button className="botao" type="button" onClick={aoFechar}>
            Cancelar
          </button>
          <button className="botao botao-primario" type="submit" disabled={salvar.isPending}>
            {salvar.isPending ? 'Salvando…' : mesa ? 'Salvar alterações' : 'Criar mesa'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
