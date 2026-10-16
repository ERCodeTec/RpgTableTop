import { useId, useState } from 'react'
import { CLASSES, NOME_DA_CLASSE, type ClasseDnd } from '../../shared/classes'
import { ICONES_DE_OBJETO, ICONES_GENERICOS, NOME_DO_ICONE, type IconeGenericoChave } from '../../shared/icones/IconeGenerico'
import type { FonteDeImagem } from '../../shared/imagens/imagensApi'
import { Miniatura } from '../../shared/imagens/Miniatura'
import type { TipoToken } from './mapaApi'
import { SeletorDeImagem } from './SeletorDeImagem'
import { TokenIcone } from './TokenIcone'

/** Os campos que o formulário de token e o de modelo da biblioteca têm em comum. */

export const ICONE_INICIAL: Record<TipoToken, string> = { Inimigo: 'goblin', Npc: 'npc', Personagem: 'Guerreiro', Interacao: 'interacao' }

const iconesDoTipo = (tipo: TipoToken): readonly string[] =>
  tipo === 'Personagem' ? CLASSES : tipo === 'Interacao' ? ICONES_DE_OBJETO : ICONES_GENERICOS
const nomeDoIcone = (tipo: TipoToken, icone: string) =>
  tipo === 'Personagem' ? NOME_DA_CLASSE[icone as ClasseDnd] : NOME_DO_ICONE[icone as IconeGenericoChave]

interface SeletorDeIconeProps {
  tipo: TipoToken
  icone: string
  corBorda: string
  aoEscolher: (icone: string) => void
  erros?: string[]
}

export function SeletorDeIcone({ tipo, icone, corBorda, aoEscolher, erros }: SeletorDeIconeProps) {
  return (
    <fieldset className="classes">
      <legend>Ícone</legend>
      <div className="classes-grade">
        {iconesDoTipo(tipo).map((opcao) => (
          <label key={opcao} className="classe-opcao">
            <input type="radio" name="icone" value={opcao} checked={icone === opcao} onChange={() => aoEscolher(opcao)} />
            <TokenIcone tipo={tipo} icone={opcao} corBorda={icone === opcao ? corBorda : '#3d585e'} tamanho={44} />
            <span>{nomeDoIcone(tipo, opcao)}</span>
          </label>
        ))}
      </div>
      {erros?.[0] && <p className="campo-erro">{erros[0]}</p>}
    </fieldset>
  )
}

interface CampoDeImagemProps {
  fonte: FonteDeImagem
  imagemId: string | null
  aoMudar: (imagemId: string | null) => void
  /** Título do seletor, ex.: "Imagem do token". */
  titulo: string
  erros?: string[]
  rotulo?: string
  /** O botão que tira a imagem. */
  rotuloRemover?: string
}

/** A imagem substitui o ícone no disco; a borda colorida continua. */
export function CampoDeImagem({
  fonte,
  imagemId,
  aoMudar,
  titulo,
  erros,
  rotulo = 'Imagem (opcional)',
  rotuloRemover = 'Usar o ícone',
}: CampoDeImagemProps) {
  const [escolhendo, setEscolhendo] = useState(false)

  return (
    <div className="campo">
      <span className="campo-rotulo">{rotulo}</span>
      <div className="campo-imagem">
        {imagemId ? <Miniatura imagemId={imagemId} alt={titulo} /> : <span className="miniatura miniatura-vazia" aria-hidden="true" />}
        <button className="botao botao-discreto" type="button" onClick={() => setEscolhendo(true)}>
          {imagemId ? 'Trocar' : 'Escolher da galeria'}
        </button>
        {imagemId && (
          <button className="botao botao-discreto" type="button" onClick={() => aoMudar(null)}>
            {rotuloRemover}
          </button>
        )}
      </div>
      {erros?.[0] && <p className="campo-erro">{erros[0]}</p>}
      {escolhendo && <SeletorDeImagem fonte={fonte} titulo={titulo} aoEscolher={aoMudar} aoFechar={() => setEscolhendo(false)} />}
    </div>
  )
}

/** Até onde vai a descrição de uma Interação, como no servidor (Token.DescricaoTamanhoMaximo). */
export const DESCRICAO_MAXIMA = 4000

interface CamposDoConteudoProps {
  fonte: FonteDeImagem
  descricao: string
  conteudoImagemId: string | null
  aoMudarDescricao: (descricao: string) => void
  aoMudarImagem: (imagemId: string | null) => void
  erros?: Record<string, string[]>
}

/** O que os jogadores veem ao examinar um objeto de interação: uma imagem e uma descrição. */
export function CamposDoConteudo({ fonte, descricao, conteudoImagemId, aoMudarDescricao, aoMudarImagem, erros }: CamposDoConteudoProps) {
  const idDescricao = useId()
  return (
    <fieldset className="campo-grupo">
      <legend>Conteúdo, ao examinar</legend>
      <CampoDeImagem
        fonte={fonte}
        imagemId={conteudoImagemId}
        aoMudar={aoMudarImagem}
        titulo="Imagem do conteúdo"
        rotulo="Imagem do conteúdo (opcional)"
        rotuloRemover="Sem imagem"
        erros={erros?.conteudoImagemId}
      />
      <div className="campo">
        <label className="campo-rotulo" htmlFor={idDescricao}>
          Descrição
        </label>
        <textarea
          id={idDescricao}
          rows={5}
          maxLength={DESCRICAO_MAXIMA}
          value={descricao}
          placeholder="O que os personagens encontram ao examinar o objeto."
          onChange={(evento) => aoMudarDescricao(evento.target.value)}
        />
        {erros?.descricao?.[0] && <p className="campo-erro">{erros.descricao[0]}</p>}
      </div>
    </fieldset>
  )
}
