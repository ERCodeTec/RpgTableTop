import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useRef, useState, type ReactNode } from 'react'
import { ApiError } from '../../shared/api/http'
import {
  chaveDaFonte,
  enviarParaAFonte,
  listarDaFonte,
  TIPOS_ACEITOS,
  type FonteDeImagem,
} from '../../shared/imagens/imagensApi'
import { Miniatura } from '../../shared/imagens/Miniatura'
import { Dialogo } from '../../shared/ui/Dialogo'

interface SeletorDeImagemProps {
  /** A galeria de uma mesa (token, textura) ou a pessoal (modelo da biblioteca). */
  fonte: FonteDeImagem
  titulo: string
  aoEscolher: (imagemId: string) => void
  aoFechar: () => void
  /** Conteúdo antes da galeria (as texturas prontas, no seletor de textura). */
  antes?: ReactNode
}

/** Escolhe uma imagem da galeria, ou envia uma nova e já a escolhe. */
export function SeletorDeImagem({ fonte, titulo, aoEscolher, aoFechar, antes }: SeletorDeImagemProps) {
  const queryClient = useQueryClient()
  const galeria = useQuery({ queryKey: chaveDaFonte(fonte), queryFn: () => listarDaFonte(fonte) })
  const campo = useRef<HTMLInputElement>(null)
  const [progresso, setProgresso] = useState<number | null>(null)
  const [erroDoEnvio, setErroDoEnvio] = useState<string | null>(null)

  function escolher(imagemId: string) {
    aoEscolher(imagemId)
    aoFechar()
  }

  async function enviar(arquivo: File) {
    setErroDoEnvio(null)
    setProgresso(0)
    try {
      const imagem = await enviarParaAFonte(fonte, arquivo, setProgresso)
      await queryClient.invalidateQueries({ queryKey: chaveDaFonte(fonte) })
      escolher(imagem.id)
    } catch (erro) {
      // A recusa de um arquivo vem como erro do campo "arquivo", com o motivo exato.
      const motivo = erro instanceof ApiError ? (Object.values(erro.erros)[0]?.[0] ?? erro.message) : null
      setErroDoEnvio(motivo || 'O envio falhou.')
    } finally {
      setProgresso(null)
    }
  }

  const vazia = fonte === 'pessoal' ? 'Você ainda não enviou imagens para a biblioteca.' : 'A galeria desta mesa está vazia.'

  return (
    <Dialogo titulo={titulo} aoFechar={aoFechar}>
      {antes}
      <div className="campo-imagem">
        <button className="botao botao-discreto" type="button" disabled={progresso !== null} onClick={() => campo.current?.click()}>
          {progresso === null ? 'Enviar imagem' : `Enviando… ${Math.round(progresso * 100)}%`}
        </button>
        <input
          ref={campo}
          className="so-leitor"
          type="file"
          accept={TIPOS_ACEITOS}
          tabIndex={-1}
          aria-label="Arquivo de imagem para enviar"
          onChange={(evento) => {
            const arquivo = evento.target.files?.[0]
            evento.target.value = ''
            if (arquivo) void enviar(arquivo)
          }}
        />
      </div>
      {erroDoEnvio && (
        <p className="aviso-erro" role="alert">
          {erroDoEnvio}
        </p>
      )}

      {galeria.isPending ? (
        <p className="painel-mestre-vazio">Carregando imagens…</p>
      ) : galeria.isError ? (
        <p className="aviso-erro" role="alert">
          {galeria.error.message}
        </p>
      ) : galeria.data.imagens.length === 0 ? (
        <p className="painel-mestre-vazio">{vazia} Envie uma imagem acima.</p>
      ) : (
        <ul className="galeria galeria-seletor">
          {galeria.data.imagens.map((imagem) => (
            <li key={imagem.id}>
              <button className="galeria-opcao" type="button" onClick={() => escolher(imagem.id)}>
                <Miniatura imagemId={imagem.id} alt="" />
                <span>{imagem.nomeOriginal}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
      <div className="dialogo-acoes">
        <button className="botao" type="button" onClick={aoFechar}>
          Cancelar
        </button>
      </div>
    </Dialogo>
  )
}
