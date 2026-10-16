import { useMutation } from '@tanstack/react-query'
import { definirMovimentacao, type ModoDeMovimentacao } from '../mesas/mesasApi'

interface SeletorDeMovimentacaoProps {
  mesaId: string
  movimentacao: ModoDeMovimentacao
  aoAvisar: (mensagem: string) => void
}

/**
 * Ação do Mestre que vale para a mesa inteira: os jogadores movem os próprios tokens por blocos (com
 * o custo da rodada) ou arrastando livremente. A mesa atualizada chega a todos pelo lobby.
 */
export function SeletorDeMovimentacao({ mesaId, movimentacao, aoAvisar }: SeletorDeMovimentacaoProps) {
  const alterar = useMutation({
    mutationFn: (modo: ModoDeMovimentacao) => definirMovimentacao(mesaId, modo),
    onError: (erro: Error) => aoAvisar(erro.message || 'Não foi possível alterar a movimentação.'),
  })
  const escolher = (modo: ModoDeMovimentacao) => {
    if (modo !== movimentacao) alterar.mutate(modo)
  }

  return (
    <div className="alternancia" role="group" aria-label="Movimentação dos jogadores">
      <button
        type="button"
        aria-pressed={movimentacao === 'PorBlocos'}
        disabled={alterar.isPending}
        title="Os jogadores clicam no token e escolhem uma célula do alcance, gastando o movimento da rodada"
        onClick={() => escolher('PorBlocos')}
      >
        Por blocos
      </button>
      <button
        type="button"
        aria-pressed={movimentacao === 'Livre'}
        disabled={alterar.isPending}
        title="Os jogadores arrastam o próprio token para qualquer célula livre, sem custo de movimento"
        onClick={() => escolher('Livre')}
      >
        Livre
      </button>
    </div>
  )
}
