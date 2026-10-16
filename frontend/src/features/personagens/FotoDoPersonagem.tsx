import { useQueryClient } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { ApiError } from '../../shared/api/http'
import { esquecerImagem } from '../../shared/imagens/imagens'
import { TIPOS_ACEITOS } from '../../shared/imagens/imagensApi'
import { DiscoDoModelo } from '../biblioteca/DiscoDoModelo'
import { CHAVE_PERSONAGENS, enviarFoto, removerFoto, type Personagem } from './personagensApi'

interface FotoDoPersonagemProps {
  personagem: Personagem
  /** Classe e cor como estão no formulário, para a prévia sem foto acompanhar a edição. */
  classe: Personagem['classe']
  corBorda: string
}

/**
 * A foto do personagem, uma só: enviar, trocar ou remover. Vale na hora, fora do "Salvar" do
 * formulário, e o token do personagem nos mapas acompanha.
 */
export function FotoDoPersonagem({ personagem, classe, corBorda }: FotoDoPersonagemProps) {
  const queryClient = useQueryClient()
  const campo = useRef<HTMLInputElement>(null)
  const [imagemId, setImagemId] = useState(personagem.imagemId)
  const [progresso, setProgresso] = useState<number | null>(null)
  const [removendo, setRemovendo] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const ocupado = progresso !== null || removendo

  async function aplicar(acao: () => Promise<Personagem>) {
    setErro(null)
    const anterior = imagemId
    try {
      const salvo = await acao()
      setImagemId(salvo.imagemId)
      if (anterior && anterior !== salvo.imagemId) esquecerImagem(anterior)
      await queryClient.invalidateQueries({ queryKey: CHAVE_PERSONAGENS })
    } catch (falha) {
      // A recusa de um arquivo vem como erro do campo "arquivo", com o motivo exato.
      const motivo = falha instanceof ApiError ? (Object.values(falha.erros)[0]?.[0] ?? falha.message) : null
      setErro(motivo || 'Não foi possível alterar a foto.')
    }
  }

  async function enviar(arquivo: File) {
    setProgresso(0)
    await aplicar(() => enviarFoto(personagem.id, arquivo, setProgresso))
    setProgresso(null)
  }

  async function remover() {
    setRemovendo(true)
    await aplicar(() => removerFoto(personagem.id))
    setRemovendo(false)
  }

  return (
    <div className="campo">
      <span className="campo-rotulo">Foto</span>
      <div className="campo-imagem">
        <DiscoDoModelo modelo={{ tipo: 'Personagem', icone: classe, imagemId, corBorda }} tamanho={64} />
        <button className="botao botao-discreto" type="button" disabled={ocupado} onClick={() => campo.current?.click()}>
          {progresso !== null ? `Enviando… ${Math.round(progresso * 100)}%` : imagemId ? 'Trocar foto' : 'Enviar foto'}
        </button>
        {imagemId && (
          <button className="botao botao-discreto botao-perigo" type="button" disabled={ocupado} onClick={() => void remover()}>
            {removendo ? 'Removendo…' : 'Remover foto'}
          </button>
        )}
        <input
          ref={campo}
          className="so-leitor"
          type="file"
          accept={TIPOS_ACEITOS}
          tabIndex={-1}
          aria-label="Arquivo da foto do personagem"
          onChange={(evento) => {
            const arquivo = evento.target.files?.[0]
            evento.target.value = ''
            if (arquivo) void enviar(arquivo)
          }}
        />
      </div>
      <p className="campo-dica">PNG, JPEG, WebP ou GIF. Aparece no token do personagem e todos na mesa podem vê-la.</p>
      {erro && (
        <p className="aviso-erro" role="alert">
          {erro}
        </p>
      )}
    </div>
  )
}
