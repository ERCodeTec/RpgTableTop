import { useState } from 'react'
import { useImagemUrl } from '../../shared/imagens/imagens'
import { Miniatura } from '../../shared/imagens/Miniatura'
import { Dialogo } from '../../shared/ui/Dialogo'
import { NOME_DO_TIPO } from './mapaApi'
import { useMapaStore } from './mapaStore'

/**
 * Sobre o mapa, no canto: a foto do token selecionado, para o Mestre e para os jogadores. Clicar
 * nela abre a imagem inteira. Tokens sem foto não mostram nada.
 */
export function FotoDoToken() {
  const token = useMapaStore((estado) => (estado.selecionadoId ? estado.tokens[estado.selecionadoId] : undefined))
  const [ampliadaId, setAmpliadaId] = useState<string | null>(null)

  if (!token?.imagemId) return null
  const ampliada = ampliadaId === token.id

  return (
    <>
      <button
        className="foto-do-token"
        type="button"
        style={{ borderColor: token.corBorda }}
        aria-label={`Ampliar a foto de ${token.nome}`}
        onClick={() => setAmpliadaId(token.id)}
      >
        <Miniatura imagemId={token.imagemId} alt="" />
        <span className="foto-do-token-nome">{token.nome}</span>
      </button>
      {ampliada && (
        <Dialogo titulo={`${token.nome} (${NOME_DO_TIPO[token.tipo]})`} aoFechar={() => setAmpliadaId(null)}>
          <FotoAmpliada imagemId={token.imagemId} nome={token.nome} />
          <div className="dialogo-acoes">
            <button className="botao" type="button" onClick={() => setAmpliadaId(null)}>
              Fechar
            </button>
          </div>
        </Dialogo>
      )}
    </>
  )
}

function FotoAmpliada({ imagemId, nome }: { imagemId: string; nome: string }) {
  const url = useImagemUrl(imagemId)
  return url ? (
    <img className="foto-ampliada" src={url} alt={`Foto de ${nome}`} draggable={false} />
  ) : (
    <p className="painel-mestre-vazio">Carregando a foto…</p>
  )
}
