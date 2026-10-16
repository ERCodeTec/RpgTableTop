import { LIMITE_DO_HISTORICO, useEditorStore, type Acao } from './editorStore'
import {
  criarEstrutura,
  dadosDaEstrutura,
  editarEstrutura,
  PADRAO_DA_ESTRUTURA,
  removerEstrutura,
  type DadosDaEstrutura,
  type Estrutura,
  type TipoEstrutura,
} from './mapaApi'
import { useMapaStore } from './mapaStore'

let avisar: (mensagem: string) => void = () => undefined

/** Por onde o editor mostra avisos curtos (o toast da página da mesa). */
export function definirAvisoDoEditor(funcao: (mensagem: string) => void): void {
  avisar = funcao
}

const editor = useEditorStore.getState
const mapa = useMapaStore.getState

// Desfazer uma exclusão recria a estrutura com outro id. Todas as ações sobre a mesma estrutura
// compartilham esta referência, então continuam valendo depois que o id muda.
const referencias = new Map<string, { id: string }>()

function referenciaDe(id: string): { id: string } {
  let ref = referencias.get(id)
  if (!ref) {
    ref = { id }
    referencias.set(id, ref)
  }
  return ref
}

function mensagemDe(erro: unknown): string {
  return erro instanceof Error && erro.message ? erro.message : 'Não foi possível concluir a ação.'
}

async function criar(ref: { id: string }, dados: DadosDaEstrutura): Promise<void> {
  const mapaId = mapa().mapa?.id
  if (!mapaId) throw new Error('Nenhum mapa aberto.')
  const criada = await criarEstrutura(mapaId, dados)
  ref.id = criada.id
  referencias.set(criada.id, ref)
  mapa().aplicarEstrutura(criada)
  editor().selecionar(criada.id)
}

async function remover(ref: { id: string }): Promise<void> {
  const estrutura = mapa().estruturas[ref.id]
  await removerEstrutura(ref.id)
  if (estrutura) mapa().estruturaRemovida(estrutura.mapaId, ref.id)
  if (editor().selecionadaId === ref.id) editor().selecionar(null)
}

/** Aplica na tela na hora e confirma com o servidor; se ele recusar, a tela volta ao que era. */
async function alterar(ref: { id: string }, dados: DadosDaEstrutura): Promise<void> {
  const atual = mapa().estruturas[ref.id]
  if (!atual) throw new Error('Esta estrutura não está mais no mapa.')
  mapa().aplicarEstrutura({ ...atual, ...dados }, true)
  try {
    mapa().aplicarEstrutura(await editarEstrutura(ref.id, dados, atual.versao), true)
  } catch (erro) {
    if (mapa().estruturas[ref.id]) mapa().aplicarEstrutura(atual, true)
    throw erro
  }
}

/** Executa a ação e a coloca no histórico. Uma ação que falha não entra nele; o retorno diz se deu certo. */
async function executar(acao: Acao): Promise<boolean> {
  if (editor().ocupado) return false
  useEditorStore.setState({ ocupado: true })
  try {
    await acao.fazer()
    useEditorStore.setState((estado) => ({
      pilhaDeDesfazer: [...estado.pilhaDeDesfazer, acao].slice(-LIMITE_DO_HISTORICO),
      pilhaDeRefazer: [],
    }))
    return true
  } catch (erro) {
    avisar(mensagemDe(erro))
    return false
  } finally {
    useEditorStore.setState({ ocupado: false, previa: null })
  }
}

/**
 * Cria a estrutura e a deixa selecionada. Sem "manter ferramenta", volta para a de selecionar,
 * para a peça nova já poder ser movida, redimensionada e ajustada no painel.
 */
export async function criarNoMapa(tipo: TipoEstrutura, area: Pick<Estrutura, 'x' | 'y' | 'largura' | 'profundidade'>): Promise<void> {
  const dados: DadosDaEstrutura = {
    tipo,
    ...area,
    ...PADRAO_DA_ESTRUTURA[tipo],
    rotacaoGraus: 0,
    texturaImagemId: null,
    portaAberta: false,
  }
  const ref = { id: '' }
  const criou = await executar({ rotulo: 'criar', fazer: () => criar(ref, dados), desfazer: () => remover(ref) })
  if (criou && !editor().manterFerramenta) editor().definirFerramenta('selecionar')
}

/** Altera campos de uma estrutura (posição, tamanho, cor, altura...). */
export function alterarNoMapa(id: string, mudancas: Partial<DadosDaEstrutura>): Promise<void> {
  const atual = mapa().estruturas[id]
  if (!atual) return Promise.resolve()
  const antes = dadosDaEstrutura(atual)
  const depois = { ...antes, ...mudancas }
  if (JSON.stringify(antes) === JSON.stringify(depois)) return Promise.resolve()
  const ref = referenciaDe(id)
  return executar({ rotulo: 'alterar', fazer: () => alterar(ref, depois), desfazer: () => alterar(ref, antes) }).then(() => undefined)
}

export function removerDoMapa(id: string): Promise<void> {
  const atual = mapa().estruturas[id]
  if (!atual) return Promise.resolve()
  const dados = dadosDaEstrutura(atual)
  const ref = referenciaDe(id)
  return executar({ rotulo: 'remover', fazer: () => remover(ref), desfazer: () => criar(ref, dados) }).then(() => undefined)
}

/** Gira 90°: troca largura e profundidade em torno do centro, sem deixar a estrutura sair do mapa. */
export function girarNoMapa(id: string): Promise<void> {
  const e = mapa().estruturas[id]
  const limites = mapa().mapa
  if (!e || !limites) return Promise.resolve()
  const [largura, profundidade] = [e.profundidade, e.largura]
  if (largura > limites.largura || profundidade > limites.altura) {
    avisar('Girada, a estrutura não cabe no mapa.')
    return Promise.resolve()
  }
  const x = Math.round(e.x + e.largura / 2 - largura / 2)
  const y = Math.round(e.y + e.profundidade / 2 - profundidade / 2)
  return alterarNoMapa(id, {
    largura,
    profundidade,
    x: Math.min(limites.largura - largura, Math.max(0, x)),
    y: Math.min(limites.altura - profundidade, Math.max(0, y)),
    rotacaoGraus: (e.rotacaoGraus + 90) % 360,
  })
}

/** Copia a estrutura uma célula para a direita e para baixo, até onde o mapa permitir. */
export function duplicarNoMapa(id: string): Promise<void> {
  const e = mapa().estruturas[id]
  const limites = mapa().mapa
  if (!e || !limites) return Promise.resolve()
  const dados: DadosDaEstrutura = {
    ...dadosDaEstrutura(e),
    x: Math.min(limites.largura - e.largura, e.x + 1),
    y: Math.min(limites.altura - e.profundidade, e.y + 1),
  }
  const ref = { id: '' }
  return executar({ rotulo: 'duplicar', fazer: () => criar(ref, dados), desfazer: () => remover(ref) }).then(() => undefined)
}

async function percorrerHistorico(sentido: 'desfazer' | 'refazer'): Promise<void> {
  const origem = sentido === 'desfazer' ? 'pilhaDeDesfazer' : 'pilhaDeRefazer'
  const destino = sentido === 'desfazer' ? 'pilhaDeRefazer' : 'pilhaDeDesfazer'
  const acao = editor()[origem].at(-1)
  if (!acao || editor().ocupado) return

  useEditorStore.setState((estado) => ({ ocupado: true, [origem]: estado[origem].slice(0, -1) }))
  try {
    await (sentido === 'desfazer' ? acao.desfazer() : acao.fazer())
    useEditorStore.setState((estado) => ({ [destino]: [...estado[destino], acao] }))
  } catch (erro) {
    // A estrutura mudou por outro caminho (outra aba, por exemplo): a ação sai do histórico.
    avisar(`Não foi possível ${sentido}: ${mensagemDe(erro)}`)
  } finally {
    useEditorStore.setState({ ocupado: false })
  }
}

export const desfazer = () => percorrerHistorico('desfazer')
export const refazer = () => percorrerHistorico('refazer')

/** O mapa na tela mudou: o histórico e as referências do anterior não valem mais. */
export function reiniciarEditor(): void {
  referencias.clear()
  editor().reiniciar()
}
