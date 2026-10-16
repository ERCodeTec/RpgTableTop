import { useState } from 'react'
import { CLASSES, NOME_DA_CLASSE, type ClasseDnd } from '../../shared/classes'
import { ICONES_GENERICOS, NOME_DO_ICONE, type IconeGenericoChave } from '../../shared/icones/IconeGenerico'
import type { FonteDeImagem } from '../../shared/imagens/imagensApi'
import { Miniatura } from '../../shared/imagens/Miniatura'
import type { TipoToken } from './mapaApi'
import { SeletorDeImagem } from './SeletorDeImagem'
import { TokenIcone } from './TokenIcone'

/** Os campos que o formulário de token e o de modelo da biblioteca têm em comum. */

export const ICONE_INICIAL: Record<TipoToken, string> = { Inimigo: 'goblin', Npc: 'npc', Personagem: 'Guerreiro' }

const iconesDoTipo = (tipo: TipoToken): readonly string[] => (tipo === 'Personagem' ? CLASSES : ICONES_GENERICOS)
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
}

/** A imagem substitui o ícone no disco; a borda colorida continua. */
export function CampoDeImagem({ fonte, imagemId, aoMudar, titulo, erros }: CampoDeImagemProps) {
  const [escolhendo, setEscolhendo] = useState(false)

  return (
    <div className="campo">
      <span className="campo-rotulo">Imagem (opcional)</span>
      <div className="campo-imagem">
        {imagemId ? <Miniatura imagemId={imagemId} alt={titulo} /> : <span className="miniatura miniatura-vazia" aria-hidden="true" />}
        <button className="botao botao-discreto" type="button" onClick={() => setEscolhendo(true)}>
          {imagemId ? 'Trocar' : 'Escolher da galeria'}
        </button>
        {imagemId && (
          <button className="botao botao-discreto" type="button" onClick={() => aoMudar(null)}>
            Usar o ícone
          </button>
        )}
      </div>
      {erros?.[0] && <p className="campo-erro">{erros[0]}</p>}
      {escolhendo && <SeletorDeImagem fonte={fonte} titulo={titulo} aoEscolher={aoMudar} aoFechar={() => setEscolhendo(false)} />}
    </div>
  )
}
