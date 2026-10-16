import type { Token } from '../mapa/mapaApi'
import type { ValorDeIniciativa } from './iniciativaApi'

/** Mesma regra do servidor (GrupoDeIniciativa.cs): mudar uma exige mudar a outra. */
const NUMERADO = /^(.*?\S)\s+(\d{1,6})$/

export const chaveDoGrupo = (nome: string) => nome.trim().toLowerCase()

/**
 * Tokens iguais dividem a iniciativa: "Goblin 1", "Goblin 2" e "Goblin 3" são o grupo "goblin" e
 * jogam na ordem do número. `nome` é o do grupo como aparece na tela ("Goblin").
 */
export function grupoDeIniciativa(nome: string): { chave: string; nome: string; sequencia: number } {
  const limpo = nome.trim()
  const numerado = NUMERADO.exec(limpo)
  return numerado
    ? { chave: chaveDoGrupo(numerado[1]), nome: numerado[1].trim(), sequencia: Number(numerado[2]) }
    : { chave: chaveDoGrupo(limpo), nome: limpo, sequencia: 0 }
}

export interface ItemDaOrdem {
  token: Token
  valor: number
}

/**
 * Os tokens visíveis que têm iniciativa, do primeiro ao último a jogar (OrdemDeIniciativa.cs): maior
 * valor primeiro; no empate, jogadores antes dos grupos e depois o nome; dentro do grupo, o número.
 */
export function ordemDeIniciativa(valores: ValorDeIniciativa[], tokens: Token[]): ItemDaOrdem[] {
  const dosJogadores = new Map<string, number>()
  const dosGrupos = new Map<string, number>()
  for (const valor of valores) {
    if (valor.usuarioId) dosJogadores.set(valor.usuarioId, valor.valor)
    else if (valor.grupo) dosGrupos.set(valor.grupo, valor.valor)
  }

  const itens: { token: Token; valor: number; tipo: number; nome: string; sequencia: number }[] = []
  for (const token of tokens) {
    // Objetos de interação não jogam, como no servidor (OrdemDeIniciativa.Calcular).
    if (!token.visivel || token.tipo === 'Interacao') continue
    if (token.donoUsuarioId) {
      const valor = dosJogadores.get(token.donoUsuarioId)
      if (valor !== undefined) itens.push({ token, valor, tipo: 0, nome: chaveDoGrupo(token.nome), sequencia: 0 })
      continue
    }
    const { chave, sequencia } = grupoDeIniciativa(token.nome)
    const valor = dosGrupos.get(chave)
    if (valor !== undefined) itens.push({ token, valor, tipo: 1, nome: chave, sequencia })
  }

  // Comparação por unidade de código, como o StringComparer.Ordinal do servidor.
  const comparar = (a: string, b: string) => (a < b ? -1 : a > b ? 1 : 0)
  itens.sort(
    (a, b) =>
      b.valor - a.valor ||
      a.tipo - b.tipo ||
      comparar(a.nome, b.nome) ||
      a.sequencia - b.sequencia ||
      comparar(a.token.id, b.token.id),
  )
  return itens.map(({ token, valor }) => ({ token, valor }))
}

/** Os grupos de inimigos e NPCs no mapa, com o nome como aparece nos tokens, para o Mestre definir o valor. */
export function gruposNoMapa(tokens: Token[]): { chave: string; nome: string; tokens: Token[] }[] {
  const grupos = new Map<string, { chave: string; nome: string; tokens: Token[] }>()
  for (const token of tokens) {
    if (token.donoUsuarioId || token.tipo === 'Interacao') continue
    const { chave, nome } = grupoDeIniciativa(token.nome)
    const grupo = grupos.get(chave) ?? { chave, nome, tokens: [] }
    grupo.tokens.push(token)
    grupos.set(chave, grupo)
  }
  return [...grupos.values()].sort((a, b) => a.nome.localeCompare(b.nome))
}
