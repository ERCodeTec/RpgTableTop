import { useQuery } from '@tanstack/react-query'
import { api } from '../../shared/api/http'
import type { TipoEstrutura } from './mapaApi'

/**
 * Texturas de estrutura que vêm com a aplicação, sem ocupar a galeria da mesa: ladrilhos dos
 * pacotes da Kenney (CC0) em public/texturas. O catálogo vem do servidor, que também o usa para
 * validar a escolha e para o gerador de mapas. Arquivos e catálogo são gerados juntos por
 * scripts/texturas/montar_texturas.py (origem de cada uma em public/texturas/CREDITOS.md).
 */

/** Ambiente que agrupa as texturas (masmorra, caverna, natureza...). */
export interface TemaDeTextura {
  id: string
  nome: string
  /** Se o gerador de mapas sabe montar um mapa com este tema. */
  geravel: boolean
}

export interface TexturaPronta {
  chave: string
  nome: string
  tema: string
  /** Os tipos de estrutura em que a textura faz sentido: o seletor mostra só as do tipo selecionado. */
  estruturas: TipoEstrutura[]
  /** Onde o gerador de mapas pode usá-la (piso, corredor, parede...). */
  papeis: string[]
  /** Em pixels. Mais alta que larga, cobre mais de uma célula na vertical das laterais. */
  largura: number
  altura: number
  pacote: string
}

export interface CatalogoDeTexturas {
  temas: TemaDeTextura[]
  texturas: TexturaPronta[]
}

export const CHAVE_DO_CATALOGO = ['texturas-prontas'] as const

/** O catálogo só muda com uma versão nova da aplicação: buscado uma vez por sessão. */
export function useCatalogoDeTexturas() {
  return useQuery({
    queryKey: CHAVE_DO_CATALOGO,
    queryFn: () => api<CatalogoDeTexturas>('/api/texturas-prontas'),
    staleTime: Infinity,
    gcTime: Infinity,
  })
}

export const urlDaTexturaPronta = (chave: string) => `/texturas/${chave}.png`

/** As texturas agrupadas por tema, na ordem dos temas; temas sem nenhuma ficam de fora. */
export function porTema(catalogo: CatalogoDeTexturas, texturas: TexturaPronta[] = catalogo.texturas) {
  return catalogo.temas
    .map((tema) => ({ tema, texturas: texturas.filter((textura) => textura.tema === tema.id) }))
    .filter((grupo) => grupo.texturas.length > 0)
}
