import { Dialogo } from '../../shared/ui/Dialogo'

const NAVEGACAO: [string, string][] = [
  ['Arrastar o fundo, botão do meio ou dois dedos', 'Deslocar o mapa'],
  ['Scroll ou pinça', 'Aproximar e afastar'],
  ['Arrastar com o botão direito', 'Girar (para os lados) e inclinar (para cima e para baixo) a visão'],
  ['Q / E', 'Girar a visão 90°'],
  ['F', 'Entrar ou sair da tela cheia'],
  ['Esc', 'Cancelar o arraste ou o movimento por blocos; no modo Editar, voltar para Selecionar'],
  ['?', 'Abrir esta ajuda'],
]

const POR_BLOCOS: [string, string] = ['Clique no token, depois numa célula azul', 'Mover por blocos, gastando o movimento da rodada']
const LIVRE: [string, string] = ['Arrastar o token', 'Mover para qualquer célula livre, sem custo de movimento']

const TOKEN_SELECIONADO: [string, string][] = [
  ['H', 'Ocultar ou revelar aos jogadores'],
  ['− / +', 'Dano ou cura de 1'],
  ['Shift com − / +', 'Dano ou cura de 5'],
  ['Ctrl+D', 'Duplicar'],
  ['Delete', 'Remover (pede confirmação)'],
]

const EDICAO: [string, string][] = [
  ['1 a 6', 'Escolher a ferramenta (Selecionar, Parede, Chão, Porta, Pilar, Obstáculo)'],
  ['Arrastar onde não há nada', 'Selecionar vários tokens numa área; arrastar um deles move todos'],
  ['Shift + arrastar / clicar', 'Somar tokens à seleção; o clique põe ou tira um token do grupo'],
  ['R', 'Girar a estrutura selecionada'],
  ['Ctrl+D', 'Duplicar a estrutura'],
  ['Delete', 'Remover a estrutura'],
  ['Ctrl+Z / Ctrl+Shift+Z', 'Desfazer e refazer'],
]

function Lista({ titulo, atalhos }: { titulo: string; atalhos: [string, string][] }) {
  return (
    <section>
      <h3 className="ajuda-titulo">{titulo}</h3>
      <dl className="ajuda-lista">
        {atalhos.map(([tecla, acao]) => (
          <div key={tecla}>
            <dt>
              <kbd>{tecla}</kbd>
            </dt>
            <dd>{acao}</dd>
          </div>
        ))}
      </dl>
    </section>
  )
}

/** Os atalhos de teclado do mapa. As seções do Mestre só aparecem para ele. */
export function AjudaDeAtalhos({
  souMestre,
  movimentoLivre,
  aoFechar,
}: {
  souMestre: boolean
  movimentoLivre: boolean
  aoFechar: () => void
}) {
  // O Mestre arrasta sempre; o jogador, só com a movimentação livre, quando não há blocos.
  const movimento = movimentoLivre ? [LIVRE] : souMestre ? [LIVRE, POR_BLOCOS] : [POR_BLOCOS]
  return (
    <Dialogo titulo="Atalhos de teclado" aoFechar={aoFechar}>
      <Lista titulo="Mapa" atalhos={[...NAVEGACAO.slice(0, -2), ...movimento, ...NAVEGACAO.slice(-2)]} />
      {souMestre && <Lista titulo="Token selecionado (modo Jogar)" atalhos={TOKEN_SELECIONADO} />}
      {souMestre && <Lista titulo="Modo Editar mapa" atalhos={EDICAO} />}
      <div className="dialogo-acoes">
        <button className="botao botao-primario" type="button" onClick={aoFechar} autoFocus>
          Entendi
        </button>
      </div>
    </Dialogo>
  )
}
