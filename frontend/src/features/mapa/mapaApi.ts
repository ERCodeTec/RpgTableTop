import { api } from '../../shared/api/http'

export type TipoToken = 'Personagem' | 'Inimigo' | 'Npc'

/** O que os jogadores (fora o dono) veem do HP de um token. */
export type ExibicaoHp = 'Oculto' | 'Barra' | 'Completo'

export const NOME_DA_EXIBICAO: Record<ExibicaoHp, string> = {
  Oculto: 'Nada',
  Barra: 'Só a barra',
  Completo: 'Barra e números',
}

export type TipoEstrutura = 'Parede' | 'Porta' | 'Pilar' | 'Obstaculo' | 'Chao'

/**
 * Desligada: os jogadores veem o mapa inteiro. Compartilhada: cada um vê o que qualquer token do
 * grupo vê. Individual: cada um vê pelo próprio token (e pelos aliados), com a própria memória.
 */
export type ModoDeNevoa = 'Desligada' | 'Compartilhada' | 'Individual'

export const NOME_DO_MODO_DE_NEVOA: Record<ModoDeNevoa, string> = {
  Desligada: 'Desligada',
  Compartilhada: 'Compartilhada pelo grupo',
  Individual: 'Individual por jogador',
}

/** Explorada: o que os tokens já viram. Revelada: o que o Mestre revelou à mão. */
export type TipoDeMemoria = 'Explorada' | 'Revelada'

/** Uma camada da névoa, com um bit por célula (veja nevoa.ts). */
export interface MemoriaDeNevoa {
  mapaId: string
  tipo: TipoDeMemoria
  /** Nulo na memória do grupo e na camada revelada; o dono, na memória de um jogador. */
  usuarioId: string | null
  /** Base64. */
  celulas: string
}

export interface Mapa {
  id: string
  mesaId: string
  nome: string
  /** Em células. */
  largura: number
  altura: number
  ativo: boolean
  corFundo: string
  criadoEm: string
  imagemFundoId: string | null
  /** 1 = a imagem tem a largura do mapa, mantendo a proporção. */
  fundoEscala: number
  /** Deslocamento do canto superior esquerdo da imagem, em células. */
  fundoOffsetX: number
  fundoOffsetY: number
  nevoa: ModoDeNevoa
}

export interface Token {
  id: string
  mapaId: string
  tipo: TipoToken
  nome: string
  /** Nulos para quem não pode ver os números: o servidor não os envia. */
  hpAtual: number | null
  hpMaximo: number | null
  /** Em faixas de 5%. Nulo para quem não vê nem a barra. */
  hpPercentual: number | null
  /** Com 0 de HP. Visível a todos, mesmo com o HP oculto. */
  caido: boolean
  exibicaoHp: ExibicaoHp
  x: number
  y: number
  corBorda: string
  /** Classe de D&D nos tokens de personagem; chave de ícone genérico nos demais. */
  icone: string
  personagemId: string | null
  /** Preenchido só no token que o servidor mantém para um jogador. */
  donoUsuarioId: string | null
  /** false só chega ao Mestre e ao dono: para os demais jogadores o token nem existe. */
  visivel: boolean
  versao: number
  /** Com imagem, o disco mostra a imagem no lugar do ícone. */
  imagemId: string | null
  /**
   * Movimento por blocos, em células: o total por rodada, o que resta e se há um movimento ainda
   * não concluído. Nulos para quem não é o Mestre nem o dono.
   */
  deslocamento: number | null
  movimentoRestante: number | null
  movimentoPendente: boolean | null
  /** A Disparada da rodada já foi usada: o movimento da rodada é o dobro do deslocamento. Nulo como os demais. */
  disparada: boolean | null
  /** Até onde o token enxerga, em células, com a névoa ligada. Nos tokens do Mestre, maior que 0 marca um aliado. */
  visao: number
}

/** O movimento de um token (evento MovimentoAlterado, só para o Mestre e o dono). */
export interface MovimentoDoToken {
  tokenId: string
  deslocamento: number
  movimentoRestante: number
  movimentoPendente: boolean
  versao: number
  disparada: boolean
}

/** O movimento inteiro da rodada: o deslocamento, dobrado se a Disparada foi usada. */
export function movimentoMaximo(token: Pick<Token, 'deslocamento' | 'disparada'>) {
  return (token.deslocamento ?? 0) * (token.disparada ? 2 : 1)
}

/** O HP de um token como este usuário pode vê-lo (evento HpAlterado). */
export interface HpDoToken {
  tokenId: string
  hpAtual: number | null
  hpMaximo: number | null
  hpPercentual: number | null
  caido: boolean
  versao: number
}

/** Um elemento fixo do cenário: ocupa o retângulo de células (x, y, largura, profundidade). */
export interface Estrutura {
  id: string
  mapaId: string
  tipo: TipoEstrutura
  x: number
  y: number
  /** Células no eixo X. */
  largura: number
  /** Células no eixo Y. */
  profundidade: number
  /** Em células. */
  altura: number
  /** 0, 90, 180 ou 270: orienta só o desenho. */
  rotacaoGraus: number
  cor: string
  texturaImagemId: string | null
  /** Chave de uma das texturas prontas (texturasProntas.ts); nunca junto com texturaImagemId. */
  texturaPadrao: string | null
  bloqueiaMovimento: boolean
  portaAberta: boolean
  /** O bloqueio efetivo, calculado pelo servidor: porta fechada, ou os demais com bloqueiaMovimento. */
  bloqueia: boolean
  versao: number
}

/** O que o Mestre define em uma estrutura; o restante (id, versão, bloqueio efetivo) é do servidor. */
export type DadosDaEstrutura = Omit<Estrutura, 'id' | 'mapaId' | 'bloqueia' | 'versao'>

export interface DadosDoFundo {
  imagemId: string | null
  escala: number
  offsetX: number
  offsetY: number
}

export interface MapaEstado {
  mapa: Mapa
  tokens: Token[]
  estruturas: Estrutura[]
  /** As camadas da névoa que este usuário pode ver (todas, para o Mestre). */
  memorias: MemoriaDeNevoa[]
}

/** Resposta do MoverToken e dos métodos do movimento por blocos; na rejeição, `token` traz o estado atual no servidor (quando avaliado). */
export interface MovimentoResultado {
  aceito: boolean
  motivo: string | null
  token: Token | null
}

export interface DadosDoMapa {
  nome: string
  largura: number
  altura: number
  corFundo: string
}

export interface DadosDoToken {
  nome: string
  hpAtual: number
  hpMaximo: number
  icone: string
  corBorda: string
  imagemId: string | null
  /** Ignorado no token de um jogador, que usa o deslocamento do personagem. */
  deslocamento?: number
  /** Ignorada no token de um jogador, que usa a visão do personagem. */
  visao?: number
}

export const NOME_DO_TIPO: Record<TipoToken, string> = {
  Personagem: 'Personagem',
  Inimigo: 'Inimigo',
  Npc: 'NPC',
}

export const NOME_DA_ESTRUTURA: Record<TipoEstrutura, string> = {
  Parede: 'Parede',
  Porta: 'Porta',
  Pilar: 'Pilar',
  Obstaculo: 'Obstáculo',
  Chao: 'Chão',
}

/**
 * Altura, cor, textura pronta e bloqueio com que cada tipo de estrutura nasce. A cor continua
 * valendo por baixo: é o que aparece se o Mestre remover a textura.
 */
export const PADRAO_DA_ESTRUTURA: Record<TipoEstrutura, Pick<Estrutura, 'altura' | 'cor' | 'texturaPadrao' | 'bloqueiaMovimento'>> = {
  Parede: { altura: 2, cor: '#6B6F76', texturaPadrao: 'parede-pedra', bloqueiaMovimento: true },
  Porta: { altura: 1.6, cor: '#8A5A2B', texturaPadrao: 'porta-madeira', bloqueiaMovimento: true },
  Pilar: { altura: 2.5, cor: '#8C8C84', texturaPadrao: 'parede-pedra', bloqueiaMovimento: true },
  Obstaculo: { altura: 0.6, cor: '#5E7A4A', texturaPadrao: null, bloqueiaMovimento: true },
  // O chão é desenhado rente ao piso; a altura só existe porque o servidor exige uma.
  Chao: { altura: 0.1, cor: '#4A4238', texturaPadrao: 'piso-lajotas', bloqueiaMovimento: false },
}

export const ALTURA_MINIMA = 0.1
export const ALTURA_MAXIMA = 3
export const FUNDO_ESCALA_MINIMA = 0.05
export const FUNDO_ESCALA_MAXIMA = 20

export const DIMENSAO_MINIMA = 5
export const DIMENSAO_MAXIMA = 100
export const COR_FUNDO_PADRAO = '#1B2A2F'
export const COR_TOKEN_PADRAO = '#C0503C'
/** Em células: 6 = 30 ft. */
export const DESLOCAMENTO_PADRAO = 6
export const DESLOCAMENTO_MINIMO = 1
export const DESLOCAMENTO_MAXIMO = 30
/** Em células, contadas como o movimento: 12 = 60 ft. */
export const VISAO_PADRAO = 12
export const VISAO_MAXIMA = 60

export const chaveDosMapas = (mesaId: string) => ['mesas', mesaId, 'mapas'] as const

export const listarMapas = (mesaId: string) => api<Mapa[]>(`/api/mesas/${mesaId}/mapas`)

export const criarMapa = (mesaId: string, dados: DadosDoMapa) =>
  api<Mapa>(`/api/mesas/${mesaId}/mapas`, { method: 'POST', body: dados })

/**
 * Cria um mapa inativo com uma masmorra gerada pelo servidor; cada chamada sem semente dá um mapa diferente.
 * O tema (um dos geráveis do catálogo de texturas) escolhe as texturas; sem ele, masmorra.
 */
export const gerarMapaProcedural = (
  mesaId: string,
  dados: { nome?: string; largura?: number; altura?: number; semente?: number; tema?: string } = {},
) =>
  api<MapaEstado>(`/api/mesas/${mesaId}/mapas/procedural`, { method: 'POST', body: dados })

export const atualizarMapa = (id: string, dados: DadosDoMapa) => api<Mapa>(`/api/mapas/${id}`, { method: 'PUT', body: dados })

export const excluirMapa = (id: string) => api<void>(`/api/mapas/${id}`, { method: 'DELETE' })

export const ativarMapa = (id: string) => api<MapaEstado>(`/api/mapas/${id}/ativar`, { method: 'PUT' })

export const obterEstadoDoMapa = (id: string) => api<MapaEstado>(`/api/mapas/${id}/estado`)

/** Sem posição, o servidor coloca o token na primeira célula livre. */
export const criarToken = (mapaId: string, dados: DadosDoToken & { tipo: TipoToken }) =>
  api<Token>(`/api/mapas/${mapaId}/tokens`, { method: 'POST', body: dados })

/**
 * Cópias de um modelo da biblioteca nas primeiras células livres, com HP cheio. Com mais de uma, os
 * nomes ganham número. Sem célula para todas, nenhuma é criada (409).
 */
export const colocarDaBiblioteca = (mapaId: string, dados: { modeloId: string; quantidade: number; visivel: boolean }) =>
  api<Token[]>(`/api/mapas/${mapaId}/tokens/da-biblioteca`, { method: 'POST', body: dados })

export const editarToken = (id: string, dados: DadosDoToken) => api<Token>(`/api/tokens/${id}`, { method: 'PUT', body: dados })

export const duplicarToken = (id: string) => api<Token>(`/api/tokens/${id}/duplicar`, { method: 'POST' })

export const removerToken = (id: string) => api<void>(`/api/tokens/${id}`, { method: 'DELETE' })

/** Dano (delta negativo), cura (positivo) ou edição direta. O servidor mantém o HP entre 0 e o máximo. */
export const alterarHp = (id: string, dados: { delta?: number; hpAtual?: number; hpMaximo?: number }) =>
  api<Token>(`/api/tokens/${id}/hp`, { method: 'PUT', body: dados })

/** Oculta ou revela o token e define o que os jogadores veem do HP. */
export const definirExibicao = (id: string, dados: { visivel?: boolean; exibicaoHp?: ExibicaoHp }) =>
  api<Token>(`/api/tokens/${id}/exibicao`, { method: 'PUT', body: dados })

export const definirFundo = (mapaId: string, dados: DadosDoFundo) =>
  api<Mapa>(`/api/mapas/${mapaId}/fundo`, { method: 'PUT', body: dados })

export const definirModoDaNevoa = (mapaId: string, modo: ModoDeNevoa) =>
  api<Mapa>(`/api/mapas/${mapaId}/nevoa`, { method: 'PUT', body: { modo } })

/** O pincel do Mestre: revela as células (base64, um bit por célula) para todos, ou as cobre. */
export const pintarNevoa = (mapaId: string, celulas: string, revelar: boolean) =>
  api<void>(`/api/mapas/${mapaId}/nevoa/revelada`, { method: 'PUT', body: { celulas, revelar } })

/** Esquece o que os jogadores exploraram; o que o Mestre revelou à mão fica. */
export const limparMemoriaDaNevoa = (mapaId: string) => api<void>(`/api/mapas/${mapaId}/nevoa/memoria`, { method: 'DELETE' })

export const criarEstrutura = (mapaId: string, dados: DadosDaEstrutura) =>
  api<Estrutura>(`/api/mapas/${mapaId}/estruturas`, { method: 'POST', body: dados })

/** `versao` é a que o cliente conhece; se a do servidor for outra, a resposta é 409. */
export const editarEstrutura = (id: string, dados: DadosDaEstrutura, versao: number | null) =>
  api<Estrutura>(`/api/estruturas/${id}`, { method: 'PUT', body: { ...dados, versao } })

export const definirPorta = (id: string, aberta: boolean) =>
  api<Estrutura>(`/api/estruturas/${id}/porta`, { method: 'PUT', body: { aberta } })

export const removerEstrutura = (id: string) => api<void>(`/api/estruturas/${id}`, { method: 'DELETE' })

export function dadosDaEstrutura(estrutura: Estrutura): DadosDaEstrutura {
  const { id: _id, mapaId: _mapaId, bloqueia: _bloqueia, versao: _versao, ...dados } = estrutura
  return dados
}
