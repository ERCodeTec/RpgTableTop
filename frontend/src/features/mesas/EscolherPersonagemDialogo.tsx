import { useMutation, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Dialogo } from '../../shared/ui/Dialogo'
import { SeletorDePersonagem } from '../../shared/ui/SeletorDePersonagem'
import { CHAVE_PERSONAGENS, listarPersonagens } from '../personagens/personagensApi'
import {
  chaveDosPersonagensDoMembro,
  definirMeuPersonagem,
  definirPersonagemDoMembro,
  listarPersonagensDoMembro,
  type Membro,
} from './mesasApi'

interface EscolherPersonagemDialogoProps {
  mesaId: string
  membro: Membro
  /** true quando o próprio jogador troca; false quando é o Mestre escolhendo por ele. */
  proprio: boolean
  aoFechar: () => void
}

export function EscolherPersonagemDialogo({ mesaId, membro, proprio, aoFechar }: EscolherPersonagemDialogoProps) {
  const [selecionadoId, setSelecionadoId] = useState<string | null>(membro.personagemAtivo?.id ?? null)

  const meus = useQuery({ queryKey: CHAVE_PERSONAGENS, queryFn: listarPersonagens, enabled: proprio })
  const doMembro = useQuery({
    queryKey: chaveDosPersonagensDoMembro(mesaId, membro.usuarioId),
    queryFn: () => listarPersonagensDoMembro(mesaId, membro.usuarioId),
    enabled: !proprio,
  })
  const consulta = proprio ? meus : doMembro
  const personagens = consulta.data

  // O lobby é atualizado pelo evento PersonagemAtivoAlterado que o servidor envia ao grupo.
  const trocar = useMutation({
    mutationFn: (personagemId: string) =>
      proprio
        ? definirMeuPersonagem(mesaId, personagemId)
        : definirPersonagemDoMembro(mesaId, membro.usuarioId, personagemId),
    onSuccess: aoFechar,
  })

  return (
    <Dialogo titulo={proprio ? 'Trocar meu personagem' : `Personagem de ${membro.nomeExibicao}`} aoFechar={aoFechar}>
      {consulta.isPending && <p>Carregando personagens…</p>}
      {consulta.isError && (
        <p className="aviso-erro" role="alert">
          {consulta.error.message || 'Não foi possível carregar os personagens.'}
        </p>
      )}
      {personagens?.length === 0 &&
        (proprio ? (
          <p>
            Você não tem personagens. <Link to="/personagens">Crie um personagem</Link> e volte para a mesa.
          </p>
        ) : (
          <p>{membro.nomeExibicao} ainda não tem personagens cadastrados.</p>
        ))}
      {personagens && personagens.length > 0 && (
        <SeletorDePersonagem
          nome="personagem-da-mesa"
          personagens={personagens}
          selecionadoId={selecionadoId}
          aoSelecionar={setSelecionadoId}
        />
      )}
      {trocar.isError && (
        <p className="aviso-erro" role="alert">
          {trocar.error.message || 'Não foi possível trocar o personagem.'}
        </p>
      )}
      <div className="dialogo-acoes">
        <button className="botao" type="button" onClick={aoFechar}>
          Cancelar
        </button>
        <button
          className="botao botao-primario"
          type="button"
          disabled={!selecionadoId || trocar.isPending}
          onClick={() => selecionadoId && trocar.mutate(selecionadoId)}
        >
          {trocar.isPending ? 'Trocando…' : 'Usar este personagem'}
        </button>
      </div>
    </Dialogo>
  )
}
