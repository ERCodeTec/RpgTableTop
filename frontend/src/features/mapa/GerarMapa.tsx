import { useId, useState } from 'react'
import { useCatalogoDeTexturas } from './texturasProntas'

const AO_ACASO = ''

interface GerarMapaProps {
  /** Recebe o tema escolhido (um dos temas geráveis do catálogo). */
  aoGerar: (tema: string) => void
  gerando: boolean
  /** Botão em destaque (tela sem mapa) ou discreto (painel). */
  destaque?: boolean
}

/**
 * Gera um mapa procedural com o tema escolhido: o gerador monta salas, corredores e paredes com as
 * texturas daquele tema. "Ao acaso" sorteia um tema a cada clique.
 */
export function GerarMapa({ aoGerar, gerando, destaque = false }: GerarMapaProps) {
  const id = useId()
  const catalogo = useCatalogoDeTexturas()
  const temas = catalogo.data?.temas.filter((tema) => tema.geravel) ?? []
  const [tema, setTema] = useState('masmorra')

  function gerar() {
    const escolhido = tema === AO_ACASO && temas.length ? temas[Math.floor(Math.random() * temas.length)].id : tema || 'masmorra'
    aoGerar(escolhido)
  }

  return (
    <div className="gerar-mapa">
      <label className="so-leitor" htmlFor={id}>
        Tema do mapa gerado
      </label>
      <select id={id} value={tema} disabled={gerando || temas.length === 0} onChange={(evento) => setTema(evento.target.value)}>
        {temas.map((opcao) => (
          <option key={opcao.id} value={opcao.id}>
            {opcao.nome}
          </option>
        ))}
        <option value={AO_ACASO}>Tema ao acaso</option>
      </select>
      <button
        className={`botao ${destaque ? '' : 'botao-discreto'}`}
        type="button"
        disabled={gerando}
        title="Gera um mapa novo a cada clique, com as texturas do tema"
        onClick={gerar}
      >
        {gerando ? 'Gerando…' : 'Gerar mapa'}
      </button>
    </div>
  )
}
