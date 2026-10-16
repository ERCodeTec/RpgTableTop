import { useState } from 'react'
import { useEditorStore } from './editorStore'
import { movimentoMaximo } from './mapaApi'
import { useMapaStore } from './mapaStore'
import type { AcoesDeMovimento } from './useMapaTempoReal'

interface BarraDeMovimentoProps {
  meuUsuarioId: string
  souMestre: boolean
  movimento: AcoesDeMovimento
}

/**
 * Sobre o mapa, para o token selecionado que eu posso mover por blocos: o movimento que resta na
 * rodada, Disparada (dobra o movimento, uma vez por rodada; dá para voltar atrás enquanto o extra
 * não foi gasto) e, depois de andar, Concluir (o gasto fica) ou Desfazer (volta e devolve o movimento).
 * Tudo em botões: no toque não há teclado nem dica ao pairar.
 */
export function BarraDeMovimento({ meuUsuarioId, souMestre, movimento }: BarraDeMovimentoProps) {
  const token = useMapaStore((estado) => (estado.selecionadoId ? estado.tokens[estado.selecionadoId] : undefined))
  const movendo = useMapaStore((estado) => estado.movendoId !== null && estado.movendoId === estado.selecionadoId)
  const jogando = useEditorStore((estado) => estado.modo === 'jogar')
  const [ocupado, setOcupado] = useState(false)

  if (!jogando || !token || token.movimentoRestante === null || token.deslocamento === null) return null
  if (!souMestre && token.donoUsuarioId !== meuUsuarioId) return null

  const enviar = (acao: (tokenId: string) => Promise<void>) => {
    setOcupado(true)
    void acao(token.id).finally(() => setOcupado(false))
  }

  const cabecalho = (
    <>
      <span className="barra-de-movimento-nome">{token.nome}</span>
      <span className="barra-de-movimento-valor" aria-live="polite">
        Movimento {token.movimentoRestante}/{movimentoMaximo(token)}
      </span>
    </>
  )

  // Caído, o jogador não faz mais nada com o token, nem desfazer a jogada; o Mestre segue livre.
  if (token.caido && !souMestre) {
    return (
      <div className="barra-de-movimento" role="group" aria-label={`Movimento de ${token.nome}`}>
        {cabecalho}
        <span className="selo selo-perigo">Caído</span>
        <span className="barra-de-movimento-dica">Caído: não pode se mover nem desfazer a jogada.</span>
      </div>
    )
  }

  const dica = token.movimentoPendente
    ? null
    : token.movimentoRestante === 0
      ? 'Sem movimento nesta rodada.'
      : movendo
        ? 'Toque ou clique em uma célula azul para mover; fora dela (ou Esc) cancela.'
        : 'Toque ou clique no token para ver até onde ele anda.'
  // O extra da Disparada é um deslocamento inteiro; gasto em parte, só volta desfazendo o movimento.
  const podeDesfazerDisparada = token.disparada && token.movimentoRestante >= token.deslocamento

  return (
    <div className="barra-de-movimento" role="group" aria-label={`Movimento de ${token.nome}`}>
      {cabecalho}
      {podeDesfazerDisparada ? (
        <button className="botao botao-discreto" type="button" disabled={ocupado} onClick={() => enviar(movimento.desfazerDisparada)}>
          Desfazer Disparada
        </button>
      ) : token.disparada ? (
        <span className="selo">Disparada</span>
      ) : (
        <button
          className="botao botao-discreto"
          type="button"
          disabled={ocupado}
          title={`Soma mais ${token.deslocamento} células ao movimento desta rodada`}
          onClick={() => enviar(movimento.disparar)}
        >
          Disparada
        </button>
      )}
      {token.movimentoPendente ? (
        <>
          <button className="botao botao-primario" type="button" disabled={ocupado} onClick={() => enviar(movimento.concluir)}>
            Concluir
          </button>
          <button className="botao botao-discreto" type="button" disabled={ocupado} onClick={() => enviar(movimento.desfazer)}>
            Desfazer movimento
          </button>
        </>
      ) : (
        <span className="barra-de-movimento-dica">{dica}</span>
      )}
    </div>
  )
}
