import { useQuery } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { useImagemUrl } from '../../shared/imagens/imagens'
import { Dialogo } from '../../shared/ui/Dialogo'
import { possoExaminar } from './geometria'
import { useInteracaoStore } from './interacaoStore'
import { obterInteracao } from './mapaApi'
import { useMapaStore } from './mapaStore'

const APROXIME_SE = 'Aproxime-se do objeto para examiná-lo.'

interface DialogoDeInteracaoProps {
  meuUsuarioId: string
  souMestre: boolean
  aoAvisar: (mensagem: string) => void
}

/**
 * O conteúdo do objeto de interação que o usuário pediu para examinar: imagem e descrição. O
 * jogador só abre com o token dele ao lado ou com o objeto revelado, e o diálogo fecha sozinho se
 * ele se afastar. A cada alteração do token (versão nova) o conteúdo é buscado de novo.
 */
export function DialogoDeInteracao({ meuUsuarioId, souMestre, aoAvisar }: DialogoDeInteracaoProps) {
  const examinandoId = useInteracaoStore((estado) => estado.examinandoId)
  const examinar = useInteracaoStore((estado) => estado.examinar)
  const token = useMapaStore((estado) => (examinandoId ? estado.tokens[examinandoId] : undefined))
  const meuToken = useMapaStore((estado) =>
    token ? Object.values(estado.tokens).find((t) => t.donoUsuarioId === meuUsuarioId && t.mapaId === token.mapaId) : undefined,
  )
  const permitido = token !== undefined && (souMestre || possoExaminar(token, meuToken))
  // Já mostrou o conteúdo: sair do alcance fecha sem aviso; só o primeiro clique de longe explica.
  const mostrou = useRef(false)

  const conteudo = useQuery({
    queryKey: ['interacao', examinandoId, token?.versao],
    queryFn: () => obterInteracao(examinandoId!),
    enabled: permitido,
    retry: false,
    staleTime: Infinity,
  })

  useEffect(() => {
    if (!examinandoId) {
      mostrou.current = false
      return
    }
    if (!token || !permitido) {
      if (token && !mostrou.current) aoAvisar(APROXIME_SE)
      examinar(null)
    }
  }, [examinandoId, token, permitido, examinar, aoAvisar])

  const carregou = conteudo.data?.tokenId === examinandoId
  useEffect(() => {
    if (carregou) mostrou.current = true
  }, [carregou])

  useEffect(() => {
    if (conteudo.isError && examinandoId) {
      aoAvisar(conteudo.error.message || APROXIME_SE)
      examinar(null)
    }
  }, [conteudo.isError, conteudo.error, examinandoId, examinar, aoAvisar])

  if (!examinandoId || !token || !permitido) return null
  const dados = carregou ? conteudo.data : undefined

  return (
    <Dialogo titulo={token.nome} aoFechar={() => examinar(null)}>
      {dados ? (
        <div className="interacao-conteudo">
          {dados.conteudoImagemId && <ImagemDoConteudo imagemId={dados.conteudoImagemId} nome={token.nome} />}
          {dados.descricao ? (
            <p className="interacao-descricao">{dados.descricao}</p>
          ) : (
            !dados.conteudoImagemId && <p className="painel-mestre-vazio">Não há nada de especial aqui.</p>
          )}
          {souMestre && (
            <p className="interacao-quem-ve">
              {dados.revelado
                ? 'Revelado: todos os jogadores podem ver, de qualquer lugar do mapa.'
                : 'Só os jogadores com o token ao lado do objeto podem ver.'}
            </p>
          )}
        </div>
      ) : (
        <p className="painel-mestre-vazio">Examinando…</p>
      )}
      <div className="dialogo-acoes">
        <button className="botao" type="button" onClick={() => examinar(null)}>
          Fechar
        </button>
      </div>
    </Dialogo>
  )
}

function ImagemDoConteudo({ imagemId, nome }: { imagemId: string; nome: string }) {
  const url = useImagemUrl(imagemId)
  return url ? (
    <img className="foto-ampliada" src={url} alt={`Imagem de ${nome}`} draggable={false} />
  ) : (
    <p className="painel-mestre-vazio">Carregando a imagem…</p>
  )
}
