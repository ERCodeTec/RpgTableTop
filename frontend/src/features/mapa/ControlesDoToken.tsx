import { useMutation } from '@tanstack/react-query'
import { useId, useState, type FormEvent } from 'react'
import { alterarHp, definirExibicao, movimentoMaximo, NOME_DA_EXIBICAO, type ExibicaoHp, type Token } from './mapaApi'
import { useMapaStore } from './mapaStore'
import { TokenIcone } from './TokenIcone'
import type { AcoesDeMovimento } from './useMapaTempoReal'

const RAPIDOS = [-10, -5, -1, 1, 5, 10]
const EXIBICOES: ExibicaoHp[] = ['Oculto', 'Barra', 'Completo']

/**
 * HP, exibição e movimento do token selecionado, para o Mestre: dano e cura rápidos, valor livre,
 * HP máximo, visibilidade, o que os jogadores veem do HP e o movimento da rodada. O servidor
 * mantém o HP entre 0 e o máximo.
 */
export function ControlesDoToken({ token, movimento }: { token: Token; movimento: AcoesDeMovimento }) {
  const idValor = useId()
  const idMaximo = useId()
  const idExibicao = useId()
  const [valor, setValor] = useState('')
  const [maximo, setMaximo] = useState('')
  const [restaurando, setRestaurando] = useState(false)
  const deJogador = token.donoUsuarioId !== null

  // O evento do servidor traz o mesmo resultado; aplicar a resposta só adianta a tela.
  const aplicar = (atualizado: Token) => useMapaStore.getState().aplicarToken(atualizado)
  const hp = useMutation({ mutationFn: (dados: Parameters<typeof alterarHp>[1]) => alterarHp(token.id, dados), onSuccess: aplicar })
  const exibicao = useMutation({
    mutationFn: (dados: Parameters<typeof definirExibicao>[1]) => definirExibicao(token.id, dados),
    onSuccess: aplicar,
  })
  const erro = hp.error ?? exibicao.error

  function aplicarValor(sinal: 1 | -1) {
    const quantidade = Math.abs(Math.trunc(Number(valor)))
    if (!quantidade) return
    hp.mutate({ delta: sinal * quantidade })
    setValor('')
  }

  function aoDefinirMaximo(evento: FormEvent) {
    evento.preventDefault()
    const novo = Math.trunc(Number(maximo))
    if (!novo) return
    hp.mutate({ hpMaximo: novo })
    setMaximo('')
  }

  return (
    <div className="painel-item painel-item-atual" aria-label={`HP e exibição de ${token.nome}`}>
      <div className="painel-token painel-token-fixo">
        <TokenIcone tipo={token.tipo} icone={token.icone} corBorda={token.corBorda} tamanho={40} />
        <span className="painel-item-dados">
          <span className="painel-item-nome">
            <strong>{token.nome}</strong>
            {token.caido && <span className="selo selo-perigo">Caído</span>}
            {!token.visivel && <span className="selo">Oculto</span>}
          </span>
          <span className="hp-valor" aria-live="polite">
            HP {token.hpAtual} / {token.hpMaximo}
          </span>
        </span>
      </div>

      <div className="hp-livre">
        <span>
          Movimento {token.movimentoRestante}/{movimentoMaximo(token)}
          {token.disparada && ' (Disparada)'}
          {token.movimentoPendente && ' (não concluído)'}
        </span>
        <button
          className="botao botao-discreto"
          type="button"
          disabled={restaurando || (token.movimentoRestante === token.deslocamento && !token.movimentoPendente && !token.disparada)}
          onClick={() => {
            setRestaurando(true)
            void movimento.restaurar(token.id).finally(() => setRestaurando(false))
          }}
        >
          Restaurar movimento
        </button>
      </div>

      <div className="hp-rapidos" role="group" aria-label="Dano e cura rápidos">
        {RAPIDOS.map((delta) => (
          <button
            key={delta}
            className={`botao botao-discreto ${delta < 0 ? 'botao-perigo' : 'botao-cura'}`}
            type="button"
            disabled={hp.isPending}
            aria-label={delta < 0 ? `Dano de ${-delta}` : `Cura de ${delta}`}
            onClick={() => hp.mutate({ delta })}
          >
            {delta > 0 ? `+${delta}` : `−${-delta}`}
          </button>
        ))}
      </div>

      <div className="hp-livre">
        <label htmlFor={idValor}>Valor</label>
        <input
          id={idValor}
          type="number"
          inputMode="numeric"
          min={1}
          max={999}
          value={valor}
          placeholder="12"
          onChange={(evento) => setValor(evento.target.value)}
          onKeyDown={(evento) => {
            if (evento.key === 'Enter') aplicarValor(-1)
          }}
        />
        <button className="botao botao-discreto botao-perigo" type="button" disabled={hp.isPending || !valor} onClick={() => aplicarValor(-1)}>
          Dano
        </button>
        <button className="botao botao-discreto botao-cura" type="button" disabled={hp.isPending || !valor} onClick={() => aplicarValor(1)}>
          Cura
        </button>
      </div>

      {deJogador ? (
        <p className="campo-dica">O HP máximo acompanha o personagem do jogador. O HP atual vale só neste mapa: não volta para a ficha.</p>
      ) : (
        <form className="hp-livre" onSubmit={aoDefinirMaximo}>
          <label htmlFor={idMaximo}>HP máximo</label>
          <input
            id={idMaximo}
            type="number"
            inputMode="numeric"
            min={1}
            max={999}
            value={maximo}
            placeholder={String(token.hpMaximo ?? '')}
            onChange={(evento) => setMaximo(evento.target.value)}
          />
          <button className="botao botao-discreto" type="submit" disabled={hp.isPending || !maximo}>
            Definir
          </button>
        </form>
      )}

      <div className="hp-livre">
        <label htmlFor={idExibicao}>Jogadores veem do HP</label>
        <select
          id={idExibicao}
          value={token.exibicaoHp}
          disabled={exibicao.isPending}
          onChange={(evento) => exibicao.mutate({ exibicaoHp: evento.target.value as ExibicaoHp })}
        >
          {EXIBICOES.map((opcao) => (
            <option key={opcao} value={opcao}>
              {NOME_DA_EXIBICAO[opcao]}
            </option>
          ))}
        </select>
      </div>

      <button
        className="botao botao-discreto"
        type="button"
        aria-pressed={!token.visivel}
        disabled={exibicao.isPending}
        title="Atalho: H"
        onClick={() => exibicao.mutate({ visivel: !token.visivel })}
      >
        {token.visivel ? 'Ocultar dos jogadores' : 'Revelar aos jogadores'}
      </button>
      {deJogador && !token.visivel && <p className="campo-dica">O dono continua vendo e movendo o próprio token.</p>}

      {erro && (
        <p className="aviso-erro" role="alert">
          {erro.message || 'Não foi possível alterar o token.'}
        </p>
      )}
    </div>
  )
}
