import { Link } from 'react-router-dom'
import { NOME_DA_ESTRUTURA, type TipoEstrutura } from './mapaApi'
import { SeletorDeImagem } from './SeletorDeImagem'
import { porTema, urlDaTexturaPronta, useCatalogoDeTexturas } from './texturasProntas'

interface SeletorDeTexturaProps {
  mesaId: string
  /** O tipo da estrutura selecionada: só aparecem as texturas prontas feitas para ele. */
  tipo: TipoEstrutura
  aoEscolherPronta: (chave: string) => void
  aoEscolherImagem: (imagemId: string) => void
  aoFechar: () => void
}

/** Textura da estrutura: uma das prontas para o tipo dela, por tema, ou uma imagem da galeria da mesa. */
export function SeletorDeTextura({ mesaId, tipo, aoEscolherPronta, aoEscolherImagem, aoFechar }: SeletorDeTexturaProps) {
  const catalogo = useCatalogoDeTexturas()
  const grupos = catalogo.data ? porTema(catalogo.data, catalogo.data.texturas.filter((textura) => textura.estruturas.includes(tipo))) : []

  const prontas = (
    <section className="texturas-prontas" aria-label="Texturas prontas">
      <h3>Texturas prontas para {NOME_DA_ESTRUTURA[tipo].toLowerCase()}</h3>
      {catalogo.isPending ? (
        <p className="painel-mestre-vazio">Carregando texturas…</p>
      ) : catalogo.isError ? (
        <p className="aviso-erro" role="alert">
          Não foi possível carregar as texturas prontas.
        </p>
      ) : grupos.length === 0 ? (
        <p className="painel-mestre-vazio">Nenhuma textura pronta para este tipo.</p>
      ) : (
        grupos.map(({ tema, texturas }) => (
          <details key={tema.id} className="texturas-prontas-grupo" open>
            <summary className="campo-rotulo">
              {tema.nome} <span className="texturas-prontas-quantidade">({texturas.length})</span>
            </summary>
            <ul className="galeria galeria-texturas">
              {texturas.map((textura) => (
                <li key={textura.chave}>
                  <button
                    className="galeria-opcao"
                    type="button"
                    onClick={() => {
                      aoEscolherPronta(textura.chave)
                      aoFechar()
                    }}
                  >
                    <img className="miniatura miniatura-pixel" src={urlDaTexturaPronta(textura.chave)} alt="" loading="lazy" draggable={false} />
                    <span>{textura.nome}</span>
                  </button>
                </li>
              ))}
            </ul>
          </details>
        ))
      )}
      <h3>Da galeria da mesa</h3>
      <p className="campo-dica">
        Envie a sua própria textura. Veja no{' '}
        <Link to="/guia/texturas" target="_blank" rel="noreferrer">
          guia de texturas
        </Link>{' '}
        como desenhar um ladrilho que emenda bem no mapa (abre em outra aba).
      </p>
    </section>
  )

  return <SeletorDeImagem fonte={{ mesaId }} titulo="Textura da estrutura" aoEscolher={aoEscolherImagem} aoFechar={aoFechar} antes={prontas} />
}
