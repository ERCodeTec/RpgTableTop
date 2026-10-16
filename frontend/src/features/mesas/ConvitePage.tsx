import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ApiError } from '../../shared/api/http'
import { SeletorDePersonagem } from '../../shared/ui/SeletorDePersonagem'
import { PersonagemForm } from '../personagens/PersonagemForm'
import { CHAVE_PERSONAGENS, listarPersonagens } from '../personagens/personagensApi'
import { aceitarConvite, CHAVE_MESAS, chaveDaPreviaDoConvite, obterPreviaDoConvite } from './mesasApi'

export function ConvitePage() {
  const { codigo = '' } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [selecionadoId, setSelecionadoId] = useState<string | null>(null)
  const [criandoPersonagem, setCriandoPersonagem] = useState(false)

  const previa = useQuery({
    queryKey: chaveDaPreviaDoConvite(codigo),
    queryFn: () => obterPreviaDoConvite(codigo),
    retry: false,
  })
  const personagens = useQuery({ queryKey: CHAVE_PERSONAGENS, queryFn: listarPersonagens })

  // Com um único personagem (inclusive o recém-criado aqui), ele já vem escolhido.
  useEffect(() => {
    if (personagens.data?.length === 1) {
      setSelecionadoId(personagens.data[0].id)
    }
  }, [personagens.data])

  const aceitar = useMutation({
    mutationFn: (personagemId: string) => aceitarConvite(codigo, personagemId),
    onSuccess: async ({ mesaId }) => {
      await queryClient.invalidateQueries({ queryKey: CHAVE_MESAS })
      navigate(`/mesas/${mesaId}`, { replace: true })
    },
  })

  if (previa.isPending) {
    return <p className="estado estado-pagina">Carregando convite…</p>
  }

  if (previa.isError) {
    const indisponivel = previa.error instanceof ApiError && previa.error.status === 404
    return (
      <main className="pagina pagina-estreita">
        <h1>Convite indisponível</h1>
        <div className="estado" role="alert">
          <p>
            {indisponivel
              ? 'Este link de convite não vale mais. Peça um novo link ao Mestre da mesa.'
              : previa.error.message}
          </p>
          <Link className="botao" to="/mesas">
            Ir para minhas mesas
          </Link>
        </div>
      </main>
    )
  }

  const convite = previa.data
  const jogadores =
    convite.quantidadeJogadores === 0
      ? 'Você será o primeiro jogador.'
      : convite.quantidadeJogadores === 1
        ? '1 jogador já entrou.'
        : `${convite.quantidadeJogadores} jogadores já entraram.`

  return (
    <main className="pagina pagina-estreita">
      <p className="convite-chamada">{convite.nomeDoMestre} convidou você para a mesa</p>
      <h1>{convite.nomeDaMesa}</h1>
      {convite.descricaoDaMesa && <p className="lobby-descricao">{convite.descricaoDaMesa}</p>}
      <p className="convite-jogadores">{jogadores}</p>

      {convite.jaSouMembro ? (
        <div className="estado">
          <p>Você já faz parte desta mesa.</p>
          <Link className="botao botao-primario" to={`/mesas/${convite.mesaId}`}>
            Abrir a mesa
          </Link>
        </div>
      ) : (
        <section className="secao">
          <h2>Com qual personagem você entra?</h2>

          {personagens.isPending && <p className="estado">Carregando seus personagens…</p>}
          {personagens.isError && (
            <p className="aviso-erro" role="alert">
              {personagens.error.message || 'Não foi possível carregar seus personagens.'}
            </p>
          )}

          {personagens.data?.length === 0 && (
            <div className="estado">
              <p>Você ainda não tem personagens. Crie um para entrar na mesa.</p>
              <button className="botao botao-primario" type="button" onClick={() => setCriandoPersonagem(true)}>
                Criar personagem
              </button>
            </div>
          )}

          {personagens.data && personagens.data.length > 0 && (
            <>
              <SeletorDePersonagem
                nome="personagem-do-convite"
                personagens={personagens.data}
                selecionadoId={selecionadoId}
                aoSelecionar={setSelecionadoId}
              />
              {aceitar.isError && (
                <p className="aviso-erro" role="alert">
                  {aceitar.error instanceof ApiError && aceitar.error.status === 404
                    ? 'Este link de convite não vale mais. Peça um novo link ao Mestre da mesa.'
                    : (aceitar.error instanceof ApiError && aceitar.error.erros.personagemId?.[0]) ||
                      aceitar.error.message}
                </p>
              )}
              <div className="convite-acoes">
                <button
                  className="botao botao-primario"
                  type="button"
                  disabled={!selecionadoId || aceitar.isPending}
                  onClick={() => selecionadoId && aceitar.mutate(selecionadoId)}
                >
                  {aceitar.isPending ? 'Entrando…' : 'Entrar na mesa'}
                </button>
                <button className="botao" type="button" onClick={() => setCriandoPersonagem(true)}>
                  Criar outro personagem
                </button>
              </div>
            </>
          )}
        </section>
      )}

      {criandoPersonagem && <PersonagemForm aoFechar={() => setCriandoPersonagem(false)} />}
    </main>
  )
}
