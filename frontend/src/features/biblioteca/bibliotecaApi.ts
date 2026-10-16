import { api } from '../../shared/api/http'
import type { ExibicaoHp, TipoToken } from '../mapa/mapaApi'

/** Tipos que um modelo pode ter: token de personagem nasce do personagem do jogador. */
export type TipoDoModelo = Exclude<TipoToken, 'Personagem'>

export interface Categoria {
  id: string
  nome: string
  quantidadeDeModelos: number
}

export interface ModeloDeToken {
  id: string
  /** Nulo: "Sem categoria". */
  categoriaId: string | null
  tipo: TipoDoModelo
  nome: string
  hpMaximo: number
  icone: string
  /** Uma imagem da galeria pessoal. */
  imagemId: string | null
  corBorda: string
  deslocamento: number
  visao: number
  exibicaoHp: ExibicaoHp
  criadoEm: string
  atualizadoEm: string
}

export type DadosDoModelo = Omit<ModeloDeToken, 'id' | 'criadoEm' | 'atualizadoEm'>

export const NOME_MAXIMO_DA_CATEGORIA = 40
export const QUANTIDADE_MAXIMA = 20

export const CHAVE_CATEGORIAS = ['biblioteca', 'categorias'] as const
export const CHAVE_MODELOS = ['biblioteca', 'modelos'] as const

export const listarCategorias = () => api<Categoria[]>('/api/biblioteca/categorias')

export const criarCategoria = (nome: string) => api<Categoria>('/api/biblioteca/categorias', { method: 'POST', body: { nome } })

export const criarCategoriasSugeridas = () => api<Categoria[]>('/api/biblioteca/categorias/sugeridas', { method: 'POST' })

export const renomearCategoria = (id: string, nome: string) =>
  api<Categoria>(`/api/biblioteca/categorias/${id}`, { method: 'PUT', body: { nome } })

export const excluirCategoria = (id: string) => api<void>(`/api/biblioteca/categorias/${id}`, { method: 'DELETE' })

export const listarModelos = () => api<ModeloDeToken[]>('/api/biblioteca/modelos')

export const criarModelo = (dados: DadosDoModelo) => api<ModeloDeToken>('/api/biblioteca/modelos', { method: 'POST', body: dados })

export const atualizarModelo = (id: string, dados: DadosDoModelo) =>
  api<ModeloDeToken>(`/api/biblioteca/modelos/${id}`, { method: 'PUT', body: dados })

export const excluirModelo = (id: string) => api<void>(`/api/biblioteca/modelos/${id}`, { method: 'DELETE' })

/** Filtro das abas: uma categoria, todas, ou só as sem categoria. */
export type FiltroDeCategoria = 'todas' | 'sem-categoria' | string

export function filtrarModelos(modelos: ModeloDeToken[], filtro: FiltroDeCategoria, busca: string): ModeloDeToken[] {
  const termo = busca.trim().toLocaleLowerCase('pt-BR')
  return modelos.filter(
    (modelo) =>
      (filtro === 'todas' || (filtro === 'sem-categoria' ? modelo.categoriaId === null : modelo.categoriaId === filtro)) &&
      (termo === '' || modelo.nome.toLocaleLowerCase('pt-BR').includes(termo)),
  )
}
