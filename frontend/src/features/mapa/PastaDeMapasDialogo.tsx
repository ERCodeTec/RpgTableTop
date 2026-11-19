import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useId, useState, type FormEvent } from 'react'
import { ApiError } from '../../shared/api/http'
import { Campo } from '../../shared/ui/Campo'
import { Dialogo } from '../../shared/ui/Dialogo'
import { chaveDasPastas, COR_PASTA_PADRAO, criarPasta, NOME_MAXIMO_DA_PASTA, renomearPasta, type PastaDeMapas } from './mapaApi'

interface PastaDeMapasDialogoProps {
  mesaId: string
  /** Pasta a renomear; ausente ao criar uma nova. */
  pasta?: PastaDeMapas
  aoSalvar?: (pasta: PastaDeMapas) => void
  aoFechar: () => void
}

export function PastaDeMapasDialogo({ mesaId, pasta, aoSalvar, aoFechar }: PastaDeMapasDialogoProps) {
  const queryClient = useQueryClient()
  const idCor = useId()
  const [nome, setNome] = useState(pasta?.nome ?? '')
  const [cor, setCor] = useState(pasta?.cor ?? COR_PASTA_PADRAO)

  const salvar = useMutation({
    mutationFn: () => (pasta ? renomearPasta(pasta.id, nome, cor) : criarPasta(mesaId, nome, cor)),
    onSuccess: async (salva) => {
      await queryClient.invalidateQueries({ queryKey: chaveDasPastas(mesaId) })
      aoSalvar?.(salva)
      aoFechar()
    },
  })

  const erro = salvar.error instanceof ApiError ? salvar.error : null
  // O nome repetido vem como 409, sem campo: aparece junto ao campo mesmo assim.
  const erroDoNome = erro?.erros.nome ?? (salvar.error ? [salvar.error.message || 'Não foi possível salvar.'] : undefined)

  function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    salvar.mutate()
  }

  return (
    <Dialogo titulo={pasta ? `Editar ${pasta.nome}` : 'Nova pasta'} aoFechar={aoFechar}>
      <form className="form-pilha" onSubmit={aoEnviar} noValidate>
        <Campo
          rotulo="Nome"
          value={nome}
          maxLength={NOME_MAXIMO_DA_PASTA}
          onChange={(evento) => setNome(evento.target.value)}
          erros={erroDoNome}
          dica="Por exemplo: Capítulo 1, Masmorras, Cidades."
          autoFocus
          required
        />
        <div className="campo campo-cor">
          <label htmlFor={idCor}>Cor da pasta</label>
          <input id={idCor} type="color" value={cor} onChange={(evento) => setCor(evento.target.value.toUpperCase())} />
          {erro?.erros.cor && <p className="campo-erro">{erro.erros.cor[0]}</p>}
        </div>
        <div className="dialogo-acoes">
          <button className="botao" type="button" onClick={aoFechar}>
            Cancelar
          </button>
          <button className="botao botao-primario" type="submit" disabled={salvar.isPending}>
            {salvar.isPending ? 'Salvando…' : pasta ? 'Salvar alterações' : 'Criar pasta'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
