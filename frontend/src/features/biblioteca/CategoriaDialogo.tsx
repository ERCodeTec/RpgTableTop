import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { ApiError } from '../../shared/api/http'
import { Campo } from '../../shared/ui/Campo'
import { Dialogo } from '../../shared/ui/Dialogo'
import { CHAVE_CATEGORIAS, criarCategoria, NOME_MAXIMO_DA_CATEGORIA, renomearCategoria, type Categoria } from './bibliotecaApi'

interface CategoriaDialogoProps {
  /** Categoria a renomear; ausente ao criar uma nova. */
  categoria?: Categoria
  aoSalvar?: (categoria: Categoria) => void
  aoFechar: () => void
}

export function CategoriaDialogo({ categoria, aoSalvar, aoFechar }: CategoriaDialogoProps) {
  const queryClient = useQueryClient()
  const [nome, setNome] = useState(categoria?.nome ?? '')

  const salvar = useMutation({
    mutationFn: () => (categoria ? renomearCategoria(categoria.id, nome) : criarCategoria(nome)),
    onSuccess: async (salva) => {
      await queryClient.invalidateQueries({ queryKey: CHAVE_CATEGORIAS })
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
    <Dialogo titulo={categoria ? `Renomear ${categoria.nome}` : 'Nova categoria'} aoFechar={aoFechar}>
      <form className="form-pilha" onSubmit={aoEnviar} noValidate>
        <Campo
          rotulo="Nome"
          value={nome}
          maxLength={NOME_MAXIMO_DA_CATEGORIA}
          onChange={(evento) => setNome(evento.target.value)}
          erros={erroDoNome}
          dica="Por exemplo: NPCs, Vilões, Mortos-vivos."
          autoFocus
          required
        />
        <div className="dialogo-acoes">
          <button className="botao" type="button" onClick={aoFechar}>
            Cancelar
          </button>
          <button className="botao botao-primario" type="submit" disabled={salvar.isPending}>
            {salvar.isPending ? 'Salvando…' : categoria ? 'Renomear' : 'Criar categoria'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
