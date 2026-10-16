import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { ApiError } from '../../shared/api/http'
import { esquecerImagem } from '../../shared/imagens/imagens'
import {
  chaveDaGaleria,
  enviarImagem,
  excluirImagem,
  formatarTamanho,
  listarImagens,
  TIPOS_ACEITOS,
  type Imagem,
} from '../../shared/imagens/imagensApi'
import { Miniatura } from '../../shared/imagens/Miniatura'
import { ConfirmarDialogo } from '../../shared/ui/ConfirmarDialogo'
import { useEditorStore } from './editorStore'
import { useMapaStore } from './mapaStore'

interface Envio {
  chave: number
  nome: string
  /** Fração enviada, de 0 a 1. */
  progresso: number
  erro: string | null
}

let proximaChave = 0

/** A galeria de imagens da mesa: enviar com progresso, usar como fundo do mapa e excluir. */
export function GaleriaDeImagens({ mesaId }: { mesaId: string }) {
  const queryClient = useQueryClient()
  const campo = useRef<HTMLInputElement>(null)
  const galeria = useQuery({ queryKey: chaveDaGaleria(mesaId), queryFn: () => listarImagens(mesaId) })
  const mapaNaTela = useMapaStore((estado) => estado.mapa)
  const [envios, setEnvios] = useState<Envio[]>([])
  const [excluindo, setExcluindo] = useState<Imagem | null>(null)

  const atualizar = (chave: number, mudanca: Partial<Envio>) =>
    setEnvios((atuais) => atuais.map((envio) => (envio.chave === chave ? { ...envio, ...mudanca } : envio)))

  // Um arquivo por vez: o progresso de cada um fica legível e a cota é conferida a cada envio.
  async function enviar(arquivos: File[]) {
    const fila = arquivos.map((arquivo) => ({ arquivo, chave: proximaChave++ }))
    setEnvios((atuais) => [
      ...atuais.filter((envio) => envio.erro === null),
      ...fila.map(({ arquivo, chave }) => ({ chave, nome: arquivo.name, progresso: 0, erro: null })),
    ])

    for (const { arquivo, chave } of fila) {
      try {
        await enviarImagem(mesaId, arquivo, (progresso) => atualizar(chave, { progresso }))
        setEnvios((atuais) => atuais.filter((envio) => envio.chave !== chave))
        await queryClient.invalidateQueries({ queryKey: chaveDaGaleria(mesaId) })
      } catch (erro) {
        // A recusa de um arquivo vem como erro do campo "arquivo", com o motivo exato.
        const motivo = erro instanceof ApiError ? (Object.values(erro.erros)[0]?.[0] ?? erro.message) : null
        atualizar(chave, { erro: motivo || 'O envio falhou.' })
      }
    }
  }

  const excluir = useMutation({
    mutationFn: (imagem: Imagem) => excluirImagem(imagem.id),
    onSuccess: async (_, imagem) => {
      esquecerImagem(imagem.id)
      await queryClient.invalidateQueries({ queryKey: chaveDaGaleria(mesaId) })
      setExcluindo(null)
    },
  })

  function usarComoFundo(imagem: Imagem) {
    if (!mapaNaTela) return
    const jaEOFundo = mapaNaTela.imagemFundoId === imagem.id
    useEditorStore.getState().definirFundo({
      imagemId: imagem.id,
      escala: jaEOFundo ? mapaNaTela.fundoEscala : 1,
      offsetX: jaEOFundo ? mapaNaTela.fundoOffsetX : 0,
      offsetY: jaEOFundo ? mapaNaTela.fundoOffsetY : 0,
    })
    useEditorStore.getState().definirModo('fundo')
  }

  const dados = galeria.data

  return (
    <section className="painel-mestre-secao">
      <div className="painel-mestre-topo">
        <h2>Imagens</h2>
        <button className="botao botao-discreto" type="button" onClick={() => campo.current?.click()}>
          Enviar imagens
        </button>
        <input
          ref={campo}
          className="so-leitor"
          type="file"
          accept={TIPOS_ACEITOS}
          multiple
          tabIndex={-1}
          aria-label="Arquivos de imagem para enviar"
          onChange={(evento) => {
            const arquivos = [...(evento.target.files ?? [])]
            evento.target.value = ''
            if (arquivos.length > 0) void enviar(arquivos)
          }}
        />
      </div>

      {dados && (
        <p className="painel-item-detalhe">
          {dados.imagens.length} de {dados.cotaImagens} imagens · {formatarTamanho(dados.bytesUsados)} de{' '}
          {formatarTamanho(dados.cotaBytes)}. PNG, JPEG, WebP ou GIF, até {formatarTamanho(dados.tamanhoMaximoBytes)} e{' '}
          {dados.dimensaoMaxima} × {dados.dimensaoMaxima} px.
        </p>
      )}

      {envios.length > 0 && (
        <ul className="painel-lista" aria-label="Envios em andamento">
          {envios.map((envio) => (
            <li key={envio.chave} className="painel-item">
              <p className="painel-item-nome">
                <strong>{envio.nome}</strong>
              </p>
              {envio.erro ? (
                <>
                  <p className="aviso-erro" role="alert">
                    {envio.erro}
                  </p>
                  <div className="painel-item-acoes">
                    <button
                      className="botao botao-discreto"
                      type="button"
                      onClick={() => setEnvios((atuais) => atuais.filter((outro) => outro.chave !== envio.chave))}
                    >
                      Dispensar
                    </button>
                  </div>
                </>
              ) : (
                <progress className="envio-progresso" max={1} value={envio.progresso} aria-label={`Enviando ${envio.nome}`}>
                  {Math.round(envio.progresso * 100)}%
                </progress>
              )}
            </li>
          ))}
        </ul>
      )}

      {galeria.isPending ? (
        <p className="painel-mestre-vazio">Carregando imagens…</p>
      ) : galeria.isError ? (
        <p className="aviso-erro" role="alert">
          {galeria.error.message}
        </p>
      ) : dados!.imagens.length === 0 ? (
        <p className="painel-mestre-vazio">Nenhuma imagem ainda. Envie mapas, retratos de tokens e texturas.</p>
      ) : (
        <ul className="galeria">
          {dados!.imagens.map((imagem) => (
            <li key={imagem.id} className="galeria-item">
              <Miniatura imagemId={imagem.id} alt="" />
              <p className="painel-item-nome">
                <strong>{imagem.nomeOriginal}</strong>
              </p>
              <p className="painel-item-detalhe">
                {imagem.largura} × {imagem.altura} px · {formatarTamanho(imagem.tamanhoBytes)}
              </p>
              <div className="painel-item-acoes">
                <button
                  className="botao botao-discreto"
                  type="button"
                  disabled={!mapaNaTela}
                  title={mapaNaTela ? undefined : 'Abra um mapa primeiro'}
                  onClick={() => usarComoFundo(imagem)}
                >
                  Usar como fundo
                </button>
                <button className="botao botao-discreto botao-perigo" type="button" onClick={() => setExcluindo(imagem)}>
                  Excluir
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}

      {excluindo && (
        <ConfirmarDialogo
          titulo={`Excluir a imagem "${excluindo.nomeOriginal}"?`}
          rotuloConfirmar="Excluir imagem"
          rotuloEmAndamento="Excluindo…"
          emAndamento={excluir.isPending}
          erro={excluir.error?.message}
          aoConfirmar={() => excluir.mutate(excluindo)}
          aoFechar={() => {
            excluir.reset()
            setExcluindo(null)
          }}
        >
          <p>O arquivo é apagado do servidor. Uma imagem em uso por um mapa, token ou estrutura não pode ser excluída.</p>
        </ConfirmarDialogo>
      )}
    </section>
  )
}
