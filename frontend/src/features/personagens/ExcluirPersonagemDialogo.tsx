import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Dialogo } from '../../shared/ui/Dialogo'
import { CHAVE_PERSONAGENS, excluirPersonagem, type Personagem } from './personagensApi'

interface ExcluirPersonagemDialogoProps {
  personagem: Personagem
  aoFechar: () => void
}

export function ExcluirPersonagemDialogo({ personagem, aoFechar }: ExcluirPersonagemDialogoProps) {
  const queryClient = useQueryClient()
  const excluir = useMutation({
    mutationFn: () => excluirPersonagem(personagem.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: CHAVE_PERSONAGENS })
      aoFechar()
    },
  })

  return (
    <Dialogo titulo={`Excluir ${personagem.nome}?`} aoFechar={aoFechar}>
      <p>O personagem será apagado de vez. Não dá para desfazer.</p>
      {excluir.isError && (
        <p className="aviso-erro" role="alert">
          {excluir.error.message || 'Não foi possível excluir. Tente de novo.'}
        </p>
      )}
      <div className="dialogo-acoes">
        <button className="botao" type="button" onClick={aoFechar} autoFocus>
          Cancelar
        </button>
        <button
          className="botao botao-perigo-cheio"
          type="button"
          onClick={() => excluir.mutate()}
          disabled={excluir.isPending}
        >
          {excluir.isPending ? 'Excluindo…' : 'Excluir personagem'}
        </button>
      </div>
    </Dialogo>
  )
}
