import { useId } from 'react'
import {
  LADO_GARGANTUESCO_LIMITE,
  LADO_GARGANTUESCO_MINIMO,
  NOME_DO_TAMANHO,
  TAMANHOS,
  type TamanhoDeCriatura,
} from '../tamanhos'
import { Campo } from './Campo'

interface CampoDeTamanhoProps {
  tamanho: TamanhoDeCriatura
  /** O lado do Gargantuesco, como digitado; só aparece com o Gargantuesco escolhido. */
  lado: string
  aoTrocarTamanho: (tamanho: TamanhoDeCriatura) => void
  aoTrocarLado: (lado: string) => void
  erros?: { tamanho?: string[]; ladoGargantuesco?: string[] }
}

/**
 * A categoria de tamanho de uma ficha (personagem, modelo ou token): define quantas células o token
 * ocupa no mapa. No Gargantuesco, o lado do espaço também é escolhido.
 */
export function CampoDeTamanho({ tamanho, lado, aoTrocarTamanho, aoTrocarLado, erros }: CampoDeTamanhoProps) {
  const id = useId()
  return (
    <>
      <div className="campo">
        <label htmlFor={id}>Tamanho</label>
        <select id={id} value={tamanho} onChange={(evento) => aoTrocarTamanho(evento.target.value as TamanhoDeCriatura)}>
          {TAMANHOS.map((opcao) => (
            <option key={opcao} value={opcao}>
              {NOME_DO_TAMANHO[opcao]}
            </option>
          ))}
        </select>
        {erros?.tamanho && <p className="campo-erro">{erros.tamanho[0]}</p>}
      </div>
      {tamanho === 'Gargantuesco' && (
        <Campo
          rotulo="Lado (células)"
          type="number"
          inputMode="numeric"
          min={LADO_GARGANTUESCO_MINIMO}
          max={LADO_GARGANTUESCO_LIMITE}
          value={lado}
          onChange={(evento) => aoTrocarLado(evento.target.value)}
          erros={erros?.ladoGargantuesco}
          dica={`De ${LADO_GARGANTUESCO_MINIMO} a ${LADO_GARGANTUESCO_LIMITE}: 4 = 6 m.`}
          required
        />
      )}
    </>
  )
}

/** O lado a enviar à API: só no Gargantuesco, e nulo se vazio (o servidor usa 4). */
export function ladoParaEnviar(tamanho: TamanhoDeCriatura, lado: string): number | null {
  return tamanho === 'Gargantuesco' && lado.trim() !== '' ? Number(lado) : null
}
