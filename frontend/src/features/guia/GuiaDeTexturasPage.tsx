import { useState } from 'react'
import { NOME_DA_ESTRUTURA, type TipoEstrutura } from '../mapa/mapaApi'
import { porTema, urlDaTexturaPronta, useCatalogoDeTexturas } from '../mapa/texturasProntas'
import { EditorDeTextura } from './EditorDeTextura'

const TIPOS: TipoEstrutura[] = ['Parede', 'Pilar', 'Porta', 'Obstaculo', 'Chao']

/**
 * Guia para o Mestre criar texturas de estrutura em pixel art: como o mapa usa o ladrilho na visão
 * isométrica, as regras para ele emendar bem e um editor para desenhar e testar no próprio site.
 */
export function GuiaDeTexturasPage() {
  return (
    <main className="pagina guia">
      <div className="pagina-topo">
        <h1>Guia de texturas</h1>
      </div>

      <p className="guia-destaque">
        <strong>Desenhe reto, não em isométrico.</strong> A visão isométrica é da câmera: o mapa é 3D e inclina o desenho
        sozinho. A textura é um ladrilho quadrado e plano, visto de frente, que se repete em cada célula de cada face da
        estrutura.
      </p>

      <section className="secao" aria-labelledby="guia-como">
        <h2 id="guia-como">Como o mapa usa a textura</h2>
        <ul className="guia-lista">
          <li>
            <strong>Um ladrilho por célula.</strong> Uma parede de 3 células recebe 3 cópias lado a lado. A imagem não é
            esticada pela estrutura inteira.
          </li>
          <li>
            <strong>Ladrilho alto cobre mais altura.</strong> Nas laterais, um ladrilho com o dobro da altura (64 × 128, por
            exemplo) cobre 2 células na vertical: a parede padrão, de altura 2, mostra o desenho inteiro uma vez.
          </li>
          <li>
            <strong>A mesma imagem em todas as faces.</strong> O topo e as laterais usam o mesmo ladrilho. Uma parede de
            altura 2 mostra 2 cópias empilhadas em cada lateral. No chão, só o topo aparece.
          </li>
          <li>
            <strong>Altura quebrada corta o ladrilho.</strong> As cópias da lateral contam de baixo para cima: num pilar de
            altura 2,5, a cópia de cima mostra só a metade de baixo do ladrilho.
          </li>
          <li>
            <strong>A porta é a exceção:</strong> nas laterais, ela mostra um ladrilho só, esticado pela altura inteira. Desenhe
            a porta inteira no quadrado, que ela fica um pouco mais alta que larga.
          </li>
          <li>
            <strong>Girar a estrutura gira o topo.</strong> As laterais continuam em pé.
          </li>
          <li>
            <strong>Luz e névoa escurecem a imagem.</strong> As laterais ficam mais escuras que o topo, e o que os jogadores só
            exploraram aparece bem apagado. Evite texturas muito escuras.
          </li>
          <li>
            <strong>Com textura, a cor da estrutura não aparece.</strong> Ela volta se você remover a textura.
          </li>
        </ul>
      </section>

      <section className="secao" aria-labelledby="guia-regras">
        <h2 id="guia-regras">Regras para um bom ladrilho</h2>
        <ol className="guia-lista">
          <li>
            <strong>Pequeno:</strong> 16, 32 ou 64 pixels de largura. Quadrado para chão e obstáculos; para parede, pode ter o
            dobro da altura (32 × 64 ou 64 × 128). O mapa amplia sem borrar, então não aumente a imagem antes de enviar.
          </li>
          <li>
            <strong>PNG.</strong> JPEG cria manchas nas cores chapadas da pixel art.
          </li>
          <li>
            <strong>Emenda contínua:</strong> o que sai pela borda direita precisa continuar na esquerda, e o de baixo no de
            cima. Teste na prévia "Repetido 3 × 3" abaixo: se aparecer uma linha ou um degrau, a emenda está quebrada.
          </li>
          <li>
            <strong>Poucas cores:</strong> de 4 a 8 tons por textura bastam. Use um tom claro, um médio e um escuro do mesmo
            material.
          </li>
          <li>
            <strong>Sem contorno grosso na borda:</strong> numa parede, a borda vira uma grade escura entre as células.
            Prefira juntas finas, como entre os tijolos.
          </li>
          <li>
            <strong>Sem transparência.</strong> As partes transparentes aparecem pretas nas estruturas.
          </li>
          <li>
            <strong>Para parede, pense nela vista de frente</strong> (tijolos, pedras, tábuas). <strong>Para chão, pense nele
            visto de cima</strong> (lajotas, terra, areia).
          </li>
        </ol>
      </section>

      <section className="secao" aria-labelledby="guia-editor">
        <h2 id="guia-editor">Desenhe e teste aqui</h2>
        <p className="mapa-dica">
          Desenhe com mouse, caneta ou dedo. As prévias mostram o ladrilho repetido e a estrutura como aparece no mapa. Quando
          ficar bom, baixe o PNG e envie no mapa: no modo Editar, selecione a estrutura, toque em <em>Textura</em> e depois em{' '}
          <em>Enviar imagem</em>.
        </p>
        <EditorDeTextura />
      </section>

      <section className="secao" aria-labelledby="guia-prontas">
        <h2 id="guia-prontas">Texturas prontas</h2>
        <p className="mapa-dica">
          Vêm com a aplicação e estão disponíveis em todas as mesas, sem ocupar a galeria. Paredes, pilares, portas e chão novos
          já nascem com elas. Servem também de ponto de partida no editor acima.
        </p>
        <ul className="guia-lista">
          <li>
            <strong>Cada textura tem categorias:</strong> o <em>tema</em> (masmorra, caverna, natureza...) e os{' '}
            <em>tipos de estrutura</em> em que ela faz sentido. Ao trocar a textura de uma estrutura, o seletor mostra só as do
            tipo dela. O gerador de mapas usa o tema para montar o mapa inteiro com texturas que combinam.
          </li>
          <li>
            <strong>Água, lava e outros perigos</strong> são <em>Chão</em>. O chão não bloqueia por padrão: para um lago ou um rio
            de lava que ninguém atravessa, marque "Bloqueia o movimento dos jogadores".
          </li>
          <li>
            <strong>Árvores</strong> são blocos com a copa no ladrilho: use <em>Obstáculo</em> para um arbusto ou bosque baixo
            e <em>Pilar</em> para uma árvore alta.
          </li>
        </ul>
        <CatalogoNoGuia />
      </section>
    </main>
  )
}

/** As texturas prontas por tema, com um filtro pelo tipo de estrutura. */
function CatalogoNoGuia() {
  const catalogo = useCatalogoDeTexturas()
  const [tipo, setTipo] = useState<TipoEstrutura | null>(null)

  if (catalogo.isPending) return <p className="painel-mestre-vazio">Carregando texturas…</p>
  if (catalogo.isError) {
    return (
      <p className="aviso-erro" role="alert">
        Não foi possível carregar as texturas prontas.
      </p>
    )
  }

  const filtradas = tipo ? catalogo.data.texturas.filter((textura) => textura.estruturas.includes(tipo)) : catalogo.data.texturas
  const pacotes = [...new Set(catalogo.data.texturas.map((textura) => textura.pacote))].sort()

  return (
    <>
      <div className="alternancia guia-filtro" role="group" aria-label="Filtrar por tipo de estrutura">
        <button type="button" aria-pressed={tipo === null} onClick={() => setTipo(null)}>
          Todas ({catalogo.data.texturas.length})
        </button>
        {TIPOS.map((opcao) => (
          <button key={opcao} type="button" aria-pressed={tipo === opcao} onClick={() => setTipo(opcao)}>
            {NOME_DA_ESTRUTURA[opcao]}
          </button>
        ))}
      </div>
      {porTema(catalogo.data, filtradas).map(({ tema, texturas }) => (
        <div key={tema.id} className="guia-prontas-grupo">
          <h3>
            {tema.nome} <span className="texturas-prontas-quantidade">({texturas.length})</span>
          </h3>
          <ul className="guia-prontas">
            {texturas.map((textura) => (
              <li key={textura.chave} title={`${textura.largura} × ${textura.altura} px · ${textura.estruturas.map((t) => NOME_DA_ESTRUTURA[t]).join(', ')}`}>
                <img
                  className={`miniatura miniatura-pixel ${textura.altura > textura.largura ? 'miniatura-alta' : ''}`}
                  src={urlDaTexturaPronta(textura.chave)}
                  alt=""
                  loading="lazy"
                  draggable={false}
                />
                <span>{textura.nome}</span>
              </li>
            ))}
          </ul>
        </div>
      ))}
      <p className="campo-dica">
        Ladrilhos dos pacotes {pacotes.join(', ')}, da{' '}
        <a href="https://kenney.nl/assets" target="_blank" rel="noreferrer">
          Kenney
        </a>
        , em domínio público (CC0). Alguns ganharam um fundo sólido no lugar da transparência, e a lava pequena é a água
        recolorida.
      </p>
    </>
  )
}
