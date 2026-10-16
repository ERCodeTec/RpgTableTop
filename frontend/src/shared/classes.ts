export const CLASSES = [
  'Barbaro',
  'Bardo',
  'Bruxo',
  'Clerigo',
  'Druida',
  'Feiticeiro',
  'Guerreiro',
  'Ladino',
  'Mago',
  'Monge',
  'Paladino',
  'Patrulheiro',
] as const

/** Mesmo conjunto do enum ClasseDnd da API. */
export type ClasseDnd = (typeof CLASSES)[number]

export const NOME_DA_CLASSE: Record<ClasseDnd, string> = {
  Barbaro: 'Bárbaro',
  Bardo: 'Bardo',
  Bruxo: 'Bruxo',
  Clerigo: 'Clérigo',
  Druida: 'Druida',
  Feiticeiro: 'Feiticeiro',
  Guerreiro: 'Guerreiro',
  Ladino: 'Ladino',
  Mago: 'Mago',
  Monge: 'Monge',
  Paladino: 'Paladino',
  Patrulheiro: 'Patrulheiro',
}

export const COR_BORDA_PADRAO = '#E2B23C'
