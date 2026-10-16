import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { ExcluirPersonagemDialogo } from './ExcluirPersonagemDialogo'
import { PersonagemCard } from './PersonagemCard'
import { PersonagemForm } from './PersonagemForm'
import { CHAVE_PERSONAGENS, listarPersonagens, type Personagem } from './personagensApi'

type Dialogo =
  | { tipo: 'novo' }
  | { tipo: 'editar'; personagem: Personagem }
  | { tipo: 'excluir'; personagem: Personagem }

export function PersonagensPage() {
  const { data: personagens, isPending, isError, error, refetch } = useQuery({
    queryKey: CHAVE_PERSONAGENS,
    queryFn: listarPersonagens,
  })
  const [dialogo, setDialogo] = useState<Dialogo | null>(null)
  const fechar = () => setDialogo(null)

  return (
    <main className="pagina">
      <div className="pagina-topo">
        <h1>Seus personagens</h1>
        <button className="botao botao-primario" type="button" onClick={() => setDialogo({ tipo: 'novo' })}>
          Novo personagem
        </button>
      </div>

      {isPending && <p className="estado">Carregando personagens…</p>}

      {isError && (
        <div className="estado" role="alert">
          <p>{error.message || 'Não foi possível carregar os personagens.'}</p>
          <button className="botao" type="button" onClick={() => refetch()}>
            Tentar de novo
          </button>
        </div>
      )}

      {personagens?.length === 0 && (
        <div className="estado">
          <p>Você ainda não tem personagens. Crie o primeiro para levar a uma mesa.</p>
        </div>
      )}

      {personagens && personagens.length > 0 && (
        <ul className="personagens">
          {personagens.map((personagem) => (
            <PersonagemCard
              key={personagem.id}
              personagem={personagem}
              aoEditar={() => setDialogo({ tipo: 'editar', personagem })}
              aoExcluir={() => setDialogo({ tipo: 'excluir', personagem })}
            />
          ))}
        </ul>
      )}

      {dialogo?.tipo === 'novo' && <PersonagemForm aoFechar={fechar} />}
      {dialogo?.tipo === 'editar' && <PersonagemForm personagem={dialogo.personagem} aoFechar={fechar} />}
      {dialogo?.tipo === 'excluir' && <ExcluirPersonagemDialogo personagem={dialogo.personagem} aoFechar={fechar} />}
    </main>
  )
}
