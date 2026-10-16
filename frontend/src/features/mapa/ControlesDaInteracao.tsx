import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { ApiError } from '../../shared/api/http'
import { Dialogo } from '../../shared/ui/Dialogo'
import { CamposDoConteudo } from './CamposDoToken'
import { useInteracaoStore } from './interacaoStore'
import { editarInteracao, type Token } from './mapaApi'
import { useMapaStore } from './mapaStore'
import { TokenIcone } from './TokenIcone'

/**
 * O objeto de interação selecionado, para o Mestre: revelar o conteúdo a todos os jogadores (ou só
 * a quem está ao lado), ver o conteúdo como eles veem e trocar a imagem e a descrição.
 */
export function ControlesDaInteracao({ mesaId, token }: { mesaId: string; token: Token }) {
  const [editando, setEditando] = useState(false)
  const aplicar = (atualizado: Token) => useMapaStore.getState().aplicarToken(atualizado)
  const revelar = useMutation({
    mutationFn: (revelado: boolean) =>
      editarInteracao(token.id, { descricao: token.descricao ?? null, conteudoImagemId: token.conteudoImagemId ?? null, revelado }),
    onSuccess: aplicar,
  })

  return (
    <div className="painel-item painel-item-atual" aria-label={`Conteúdo de ${token.nome}`}>
      <div className="painel-token painel-token-fixo">
        <TokenIcone tipo={token.tipo} icone={token.icone} corBorda={token.corBorda} tamanho={40} />
        <span className="painel-item-dados">
          <span className="painel-item-nome">
            <strong>{token.nome}</strong>
            <span className="selo">Interação</span>
            {token.revelado && <span className="selo selo-ativo">Conteúdo revelado</span>}
            {!token.visivel && <span className="selo">Oculto</span>}
          </span>
          <span className="painel-item-detalhe">
            {token.revelado ? 'Todos os jogadores podem examinar.' : 'Só quem está ao lado pode examinar.'}
          </span>
        </span>
      </div>

      <label className="campo-caixa">
        <input
          type="checkbox"
          checked={token.revelado}
          disabled={revelar.isPending}
          onChange={(evento) => revelar.mutate(evento.target.checked)}
        />
        Revelar o conteúdo a todos os jogadores
      </label>

      <div className="hp-livre">
        <button className="botao botao-discreto" type="button" onClick={() => useInteracaoStore.getState().examinar(token.id)}>
          Ver conteúdo
        </button>
        <button className="botao botao-discreto" type="button" onClick={() => setEditando(true)}>
          Editar conteúdo
        </button>
      </div>

      {revelar.error && (
        <p className="campo-erro" role="alert">
          {revelar.error.message}
        </p>
      )}

      {editando && <InteracaoFormDialogo mesaId={mesaId} token={token} aoFechar={() => setEditando(false)} />}
    </div>
  )
}

function InteracaoFormDialogo({ mesaId, token, aoFechar }: { mesaId: string; token: Token; aoFechar: () => void }) {
  const [descricao, setDescricao] = useState(token.descricao ?? '')
  const [conteudoImagemId, setConteudoImagemId] = useState(token.conteudoImagemId ?? null)
  const salvar = useMutation({
    mutationFn: () => editarInteracao(token.id, { descricao: descricao.trim() || null, conteudoImagemId }),
    onSuccess: (atualizado) => {
      useMapaStore.getState().aplicarToken(atualizado)
      aoFechar()
    },
  })
  const erros = salvar.error instanceof ApiError ? salvar.error.erros : undefined

  return (
    <Dialogo titulo={`Conteúdo de ${token.nome}`} aoFechar={aoFechar}>
      <form
        className="form-pilha"
        onSubmit={(evento) => {
          evento.preventDefault()
          salvar.mutate()
        }}
      >
        <p>Muda só este objeto no mapa; o modelo da biblioteca continua como está.</p>
        <CamposDoConteudo
          fonte={{ mesaId }}
          descricao={descricao}
          conteudoImagemId={conteudoImagemId}
          aoMudarDescricao={setDescricao}
          aoMudarImagem={setConteudoImagemId}
          erros={erros}
        />
        {salvar.error && !erros?.descricao && (
          <p className="aviso-erro" role="alert">
            {salvar.error.message}
          </p>
        )}
        <div className="dialogo-acoes">
          <button className="botao" type="button" onClick={aoFechar}>
            Cancelar
          </button>
          <button className="botao botao-primario" type="submit" disabled={salvar.isPending}>
            {salvar.isPending ? 'Salvando…' : 'Salvar'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
