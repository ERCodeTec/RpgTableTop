import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { MesaFormDialogo } from './MesaFormDialogo'
import { CHAVE_MESAS, listarMesas, type MesaResumo } from './mesasApi'

function ListaDeMesas({ mesas, vazio }: { mesas: MesaResumo[]; vazio: string }) {
  if (mesas.length === 0) {
    return <p className="estado">{vazio}</p>
  }

  return (
    <ul className="mesas">
      {mesas.map((mesa) => (
        <li key={mesa.id}>
          <Link className="mesa-card" to={`/mesas/${mesa.id}`}>
            <span className="mesa-card-nome">{mesa.nome}</span>
            {mesa.descricao && <span className="mesa-card-descricao">{mesa.descricao}</span>}
            <span className="mesa-card-rodape">
              <span>{mesa.quantidadeMembros === 1 ? '1 membro' : `${mesa.quantidadeMembros} membros`}</span>
              {mesa.sessaoAtiva && <span className="selo selo-ativo">Sessão ativa</span>}
            </span>
          </Link>
        </li>
      ))}
    </ul>
  )
}

export function MinhasMesasPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const aviso = (location.state as { aviso?: string } | null)?.aviso
  const [criando, setCriando] = useState(false)
  const { data: mesas, isPending, isError, error, refetch } = useQuery({
    queryKey: CHAVE_MESAS,
    queryFn: listarMesas,
  })

  return (
    <main className="pagina">
      <div className="pagina-topo">
        <h1>Minhas mesas</h1>
        <button className="botao botao-primario" type="button" onClick={() => setCriando(true)}>
          Criar mesa
        </button>
      </div>

      {aviso && (
        <p className="aviso" role="status">
          {aviso}
        </p>
      )}

      {isPending && <p className="estado">Carregando mesas…</p>}

      {isError && (
        <div className="estado" role="alert">
          <p>{error.message || 'Não foi possível carregar as mesas.'}</p>
          <button className="botao" type="button" onClick={() => refetch()}>
            Tentar de novo
          </button>
        </div>
      )}

      {mesas && (
        <>
          <section className="secao">
            <h2>Mesas que mestro</h2>
            <ListaDeMesas
              mesas={mesas.filter((mesa) => mesa.meuPapel === 'Mestre')}
              vazio="Você ainda não mestra nenhuma mesa. Crie uma e convide seus jogadores."
            />
          </section>
          <section className="secao">
            <h2>Mesas em que jogo</h2>
            <ListaDeMesas
              mesas={mesas.filter((mesa) => mesa.meuPapel === 'Jogador')}
              vazio="Você ainda não joga em nenhuma mesa. Abra o link de convite que o Mestre enviar."
            />
          </section>
        </>
      )}

      {criando && (
        <MesaFormDialogo aoFechar={() => setCriando(false)} aoSalvar={(mesa) => navigate(`/mesas/${mesa.id}`)} />
      )}
    </main>
  )
}
