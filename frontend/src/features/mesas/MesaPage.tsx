import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { ApiError } from '../../shared/api/http'
import { NOME_DA_CLASSE } from '../../shared/classes'
import { ClassIcon } from '../../shared/icones/ClassIcon'
import { StatusConexao, type StatusDaConexao } from '../../shared/realtime/StatusConexao'
import { ConfirmarDialogo } from '../../shared/ui/ConfirmarDialogo'
import { Toast } from '../../shared/ui/Toast'
import { useAuth } from '../auth/AuthContext'
import { MapaAba } from '../mapa/MapaAba'
import { useMapaTempoReal } from '../mapa/useMapaTempoReal'
import { ConvitesPainel } from './ConvitesPainel'
import { EscolherPersonagemDialogo } from './EscolherPersonagemDialogo'
import { MesaFormDialogo } from './MesaFormDialogo'
import {
  CHAVE_MESAS,
  chaveDaMesa,
  definirSessao,
  excluirMesa,
  obterMesa,
  removerMembro,
  type Membro,
  type MesaDetalhe,
} from './mesasApi'
import { useLobby, type MotivoDoEncerramento } from './useLobby'

const AVISO_AO_ENCERRAR: Record<MotivoDoEncerramento, string> = {
  removido: 'O Mestre removeu você da mesa.',
  saiu: 'Você saiu da mesa.',
  excluida: 'O Mestre excluiu a mesa.',
  'sem-acesso': 'Esta mesa não existe mais ou você não faz parte dela.',
}

const AVISO_DE_CONEXAO: Record<Exclude<StatusDaConexao, 'conectado'>, string> = {
  conectando: 'Conectando à mesa…',
  reconectando: 'A conexão com a mesa caiu. Tentando reconectar; o mapa fica bloqueado até ela voltar.',
  desconectado:
    'Sem conexão com o servidor. Confira sua internet; continuamos tentando a cada 20 segundos e, ao voltar, tudo é atualizado.',
}

type DialogoAberto =
  | { tipo: 'editar' }
  | { tipo: 'excluir' }
  | { tipo: 'sair' }
  | { tipo: 'remover'; membro: Membro }
  | { tipo: 'personagem'; membro: Membro }

export function MesaPage() {
  const { id = '' } = useParams()
  const { data: mesa, isPending, isError, error } = useQuery({
    queryKey: chaveDaMesa(id),
    queryFn: () => obterMesa(id),
  })

  if (isPending) {
    return <p className="estado estado-pagina">Carregando mesa…</p>
  }

  if (isError) {
    const naoEncontrada = error instanceof ApiError && error.status === 404
    return (
      <main className="pagina">
        <div className="estado" role="alert">
          <p>{naoEncontrada ? AVISO_AO_ENCERRAR['sem-acesso'] : error.message}</p>
          <Link className="botao" to="/mesas">
            Voltar para minhas mesas
          </Link>
        </div>
      </main>
    )
  }

  return <Lobby mesa={mesa} />
}

function Lobby({ mesa }: { mesa: MesaDetalhe }) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { usuario } = useAuth()
  const meuUsuarioId = usuario!.id
  const souMestre = mesa.meuPapel === 'Mestre'
  const [dialogo, setDialogo] = useState<DialogoAberto | null>(null)
  const fechar = () => setDialogo(null)
  const [parametros, setParametros] = useSearchParams()
  const aba = parametros.get('aba') === 'mapa' ? 'mapa' : 'lobby'
  const [aviso, setAviso] = useState<string | null>(null)
  const fecharAviso = useCallback(() => setAviso(null), [])

  const aoEncerrar = useCallback(
    (motivo: MotivoDoEncerramento) => navigate('/mesas', { replace: true, state: { aviso: AVISO_AO_ENCERRAR[motivo] } }),
    [navigate],
  )
  // O mapa usa a mesma conexão do lobby, então continua sincronizado com qualquer aba aberta.
  const { extensao, moverToken, acoesDeMovimento, explorar, pingar } = useMapaTempoReal({
    mesaId: mesa.id,
    meuUsuarioId,
    aoAvisar: setAviso,
  })
  const { status, online, reconectar } = useLobby({ mesaId: mesa.id, meuUsuarioId, aoEncerrar, extensao })
  // Sem conexão, o que se vê pode estar desatualizado e o que se faz não chega aos outros.
  const semConexao = status !== 'conectado'

  // O estado do lobby muda pelos eventos que o servidor envia ao grupo; aqui só disparamos a ação.
  const sessao = useMutation({ mutationFn: (ativa: boolean) => definirSessao(mesa.id, ativa) })

  const excluir = useMutation({
    mutationFn: () => excluirMesa(mesa.id),
    onSuccess: async () => {
      queryClient.removeQueries({ queryKey: chaveDaMesa(mesa.id) })
      await queryClient.invalidateQueries({ queryKey: CHAVE_MESAS })
      navigate('/mesas', { replace: true, state: { aviso: `A mesa "${mesa.nome}" foi excluída.` } })
    },
  })

  const remover = useMutation({
    mutationFn: (usuarioId: string) => removerMembro(mesa.id, usuarioId),
    onSuccess: async (_, usuarioId) => {
      if (usuarioId === meuUsuarioId) {
        queryClient.removeQueries({ queryKey: chaveDaMesa(mesa.id) })
        await queryClient.invalidateQueries({ queryKey: CHAVE_MESAS })
        navigate('/mesas', { replace: true, state: { aviso: AVISO_AO_ENCERRAR.saiu } })
      } else {
        fechar()
      }
    },
  })

  return (
    <main className={`pagina ${aba === 'mapa' ? 'pagina-larga' : ''}`}>
      <div className="lobby-topo">
        <div className="lobby-titulo">
          <h1>{mesa.nome}</h1>
          {mesa.descricao && <p className="lobby-descricao">{mesa.descricao}</p>}
          <p className="lobby-estado">
            <span className={`selo ${mesa.sessaoAtiva ? 'selo-ativo' : ''}`}>
              {mesa.sessaoAtiva ? 'Sessão ativa' : 'Sessão encerrada'}
            </span>
            <StatusConexao status={status} />
          </p>
        </div>
        <div className="lobby-acoes">
          {souMestre ? (
            <>
              <button
                className={`botao ${mesa.sessaoAtiva ? '' : 'botao-primario'}`}
                type="button"
                disabled={sessao.isPending}
                onClick={() => sessao.mutate(!mesa.sessaoAtiva)}
              >
                {mesa.sessaoAtiva ? 'Encerrar sessão' : 'Iniciar sessão'}
              </button>
              <button className="botao" type="button" onClick={() => setDialogo({ tipo: 'editar' })}>
                Editar mesa
              </button>
              <button className="botao botao-perigo" type="button" onClick={() => setDialogo({ tipo: 'excluir' })}>
                Excluir mesa
              </button>
            </>
          ) : (
            <button className="botao botao-perigo" type="button" onClick={() => setDialogo({ tipo: 'sair' })}>
              Sair da mesa
            </button>
          )}
        </div>
      </div>

      {sessao.isError && (
        <p className="aviso-erro" role="alert">
          {sessao.error.message || 'Não foi possível alterar a sessão.'}
        </p>
      )}

      <div className="abas" role="tablist" aria-label="Seções da mesa">
        <button
          className="aba"
          type="button"
          role="tab"
          aria-selected={aba === 'lobby'}
          onClick={() => setParametros({}, { replace: true })}
        >
          Lobby
        </button>
        <button
          className="aba"
          type="button"
          role="tab"
          aria-selected={aba === 'mapa'}
          onClick={() => setParametros({ aba: 'mapa' }, { replace: true })}
        >
          Mapa
        </button>
      </div>

      {semConexao && (
        <div className="aviso aviso-conexao" role="alert">
          <p>{AVISO_DE_CONEXAO[status]}</p>
          {status === 'desconectado' && (
            <button className="botao" type="button" onClick={reconectar}>
              Tentar agora
            </button>
          )}
        </div>
      )}

      <div className={semConexao ? 'sem-conexao' : undefined} inert={semConexao}>
      {aba === 'mapa' && (
        <MapaAba
          mesaId={mesa.id}
          meuUsuarioId={meuUsuarioId}
          souMestre={souMestre}
          movimentacao={mesa.movimentacao}
          aoMover={moverToken}
          movimento={acoesDeMovimento}
          aoExplorar={explorar}
          aoPingar={pingar}
          aoAvisar={setAviso}
        />
      )}

      {aba === 'lobby' && (
        <section className="secao">
          <h2>Membros</h2>
          <ul className="membros">
            {mesa.membros.map((membro) => {
              const souEu = membro.usuarioId === meuUsuarioId
              const estaOnline = online.has(membro.usuarioId)
              const trocaBloqueada = souEu && !souMestre && mesa.sessaoAtiva
              return (
                <li key={membro.usuarioId} className="membro">
                  {membro.personagemAtivo ? (
                    <ClassIcon
                      classe={membro.personagemAtivo.classe}
                      corBorda={membro.personagemAtivo.corBorda}
                      tamanho={56}
                    />
                  ) : (
                    <span className="membro-sem-icone" aria-hidden="true" />
                  )}
                  <div className="membro-dados">
                    <p className="membro-nome">
                      <strong>{membro.nomeExibicao}</strong>
                      {souEu && <span className="membro-voce">(você)</span>}
                      {membro.papel === 'Mestre' && <span className="selo selo-mestre">Mestre</span>}
                    </p>
                    <p className="membro-personagem">
                      {membro.personagemAtivo
                        ? `${membro.personagemAtivo.nome}, ${NOME_DA_CLASSE[membro.personagemAtivo.classe]} de nível ${membro.personagemAtivo.nivel}`
                        : membro.papel === 'Mestre'
                          ? 'Conduz a mesa'
                          : 'Sem personagem'}
                    </p>
                    <p className={`presenca ${estaOnline ? 'presenca-online' : ''}`}>
                      <span className="presenca-ponto" aria-hidden="true" />
                      {estaOnline ? 'Online' : 'Offline'}
                    </p>
                  </div>
                  {membro.papel === 'Jogador' && (souMestre || souEu) && (
                    <div className="membro-acoes">
                      <button
                        className="botao botao-discreto"
                        type="button"
                        disabled={trocaBloqueada}
                        aria-describedby={trocaBloqueada ? 'troca-bloqueada' : undefined}
                        onClick={() => setDialogo({ tipo: 'personagem', membro })}
                      >
                        Trocar personagem
                      </button>
                      {souMestre && (
                        <button
                          className="botao botao-discreto botao-perigo"
                          type="button"
                          onClick={() => setDialogo({ tipo: 'remover', membro })}
                        >
                          Remover
                        </button>
                      )}
                      {trocaBloqueada && (
                        <p className="membro-aviso" id="troca-bloqueada">
                          Com a sessão ativa, só o Mestre troca seu personagem.
                        </p>
                      )}
                    </div>
                  )}
                </li>
              )
            })}
          </ul>
        </section>

      )}

      {aba === 'lobby' && souMestre && <ConvitesPainel mesaId={mesa.id} />}
      </div>

      <Toast mensagem={aviso} aoFechar={fecharAviso} />

      {dialogo?.tipo === 'editar' && <MesaFormDialogo mesa={mesa} aoFechar={fechar} />}

      {dialogo?.tipo === 'personagem' && (
        <EscolherPersonagemDialogo
          mesaId={mesa.id}
          membro={dialogo.membro}
          proprio={dialogo.membro.usuarioId === meuUsuarioId}
          aoFechar={fechar}
        />
      )}

      {dialogo?.tipo === 'excluir' && (
        <ConfirmarDialogo
          titulo={`Excluir a mesa "${mesa.nome}"?`}
          rotuloConfirmar="Excluir mesa"
          rotuloEmAndamento="Excluindo…"
          emAndamento={excluir.isPending}
          erro={excluir.error?.message}
          aoConfirmar={() => excluir.mutate()}
          aoFechar={fechar}
        >
          <p>Todos os jogadores saem da mesa e os convites deixam de valer. Não dá para desfazer.</p>
        </ConfirmarDialogo>
      )}

      {dialogo?.tipo === 'sair' && (
        <ConfirmarDialogo
          titulo={`Sair da mesa "${mesa.nome}"?`}
          rotuloConfirmar="Sair da mesa"
          rotuloEmAndamento="Saindo…"
          emAndamento={remover.isPending}
          erro={remover.error?.message}
          aoConfirmar={() => remover.mutate(meuUsuarioId)}
          aoFechar={fechar}
        >
          <p>Para voltar, você vai precisar de um novo convite do Mestre.</p>
        </ConfirmarDialogo>
      )}

      {dialogo?.tipo === 'remover' && (
        <ConfirmarDialogo
          titulo={`Remover ${dialogo.membro.nomeExibicao} da mesa?`}
          rotuloConfirmar="Remover jogador"
          rotuloEmAndamento="Removendo…"
          emAndamento={remover.isPending}
          erro={remover.error?.message}
          aoConfirmar={() => remover.mutate(dialogo.membro.usuarioId)}
          aoFechar={fechar}
        >
          <p>Ele perde o acesso na hora. Convites ainda válidos permitem que volte; revogue-os se não quiser isso.</p>
        </ConfirmarDialogo>
      )}
    </main>
  )
}
