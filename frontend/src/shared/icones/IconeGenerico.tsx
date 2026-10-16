import type { ReactNode } from 'react'

export const ICONES_GENERICOS = [
  'goblin',
  'esqueleto',
  'dragao',
  'lobo',
  'humanoide',
  'besta',
  'morto-vivo',
  'npc',
] as const

/** Os ícones dos objetos de interação (baú, carta…). */
export const ICONES_DE_OBJETO = ['interacao', 'bau', 'pergaminho', 'livro', 'estatua', 'alavanca'] as const

/** Mesmo conjunto de chaves aceito pela API (IconesGenericos). */
export type IconeGenericoChave = (typeof ICONES_GENERICOS)[number] | (typeof ICONES_DE_OBJETO)[number]

export const NOME_DO_ICONE: Record<IconeGenericoChave, string> = {
  goblin: 'Goblin',
  esqueleto: 'Esqueleto',
  dragao: 'Dragão',
  lobo: 'Lobo',
  humanoide: 'Humanoide',
  besta: 'Besta',
  'morto-vivo': 'Morto-vivo',
  npc: 'NPC',
  interacao: 'Interação',
  bau: 'Baú',
  pergaminho: 'Pergaminho',
  livro: 'Livro',
  estatua: 'Estátua',
  alavanca: 'Alavanca',
}

// Glifos originais, na mesma grade de 24x24 e só com traços, como os das classes.
const GLIFOS: Record<IconeGenericoChave, ReactNode> = {
  // cabeça de orelhas pontudas
  goblin: (
    <>
      <path d="M7 10c0-3.5 2-6 5-6s5 2.5 5 6v2c0 3.5-2 7-5 7s-5-3.5-5-7Z" />
      <path d="M7 10 2.5 7 6.5 13.5M17 10l4.5-3-4 6.5" />
      <circle cx="9.8" cy="11" r="0.8" />
      <circle cx="14.2" cy="11" r="0.8" />
      <path d="M10 15.5h4" />
    </>
  ),
  // caveira
  esqueleto: (
    <>
      <path d="M6 11a6 6 0 1 1 12 0c0 2.2-1 3.7-2.5 4.6V19h-7v-3.4C7 14.7 6 13.2 6 11Z" />
      <circle cx="9.5" cy="11" r="1.5" />
      <circle cx="14.5" cy="11" r="1.5" />
      <path d="M10.8 19v-2M13.2 19v-2" />
    </>
  ),
  // asas abertas
  dragao: (
    <>
      <path d="M12 9C10 6 7 5 3 6c1 3 1 6 0 9 2-2 4-2 5 0 1-2 3-2 4 0 1-2 3-2 4 0 1-2 3-2 5 0-1-3-1-6 0-9-4-1-7 0-9 3Z" />
      <path d="M12 9v10" />
    </>
  ),
  // cabeça de lobo, de frente
  lobo: (
    <>
      <path d="M5 3.5 8 9h8l3-5.5 1 8.5-8 8.5L4 12Z" />
      <path d="M9 12.5l2 1M15 12.5l-2 1" />
      <circle cx="12" cy="17" r="0.8" />
    </>
  ),
  // busto
  humanoide: (
    <>
      <circle cx="12" cy="7.5" r="3.5" />
      <path d="M5 20.5c0-4.5 3-7 7-7s7 2.5 7 7" />
    </>
  ),
  // pegada
  besta: (
    <>
      <path d="M12 12.5c-3 0-5 2.5-5 4.7 0 1.5 1.2 2.5 2.7 2.5 1 0 1.5-.6 2.3-.6s1.3.6 2.3.6c1.5 0 2.7-1 2.7-2.5 0-2.2-2-4.7-5-4.7Z" />
      <circle cx="6" cy="10.5" r="1.5" />
      <circle cx="10" cy="6.5" r="1.5" />
      <circle cx="14" cy="6.5" r="1.5" />
      <circle cx="18" cy="10.5" r="1.5" />
    </>
  ),
  // lápide
  'morto-vivo': (
    <>
      <path d="M7 20V10a5 5 0 0 1 10 0v10" />
      <path d="M4.5 20h15M12 8.5v6M9.5 11h5" />
    </>
  ),
  // balão de fala
  npc: (
    <>
      <path d="M4.5 5h15v10h-7.5l-4 4v-4H4.5Z" />
      <path d="M8.5 10h.01M12 10h.01M15.5 10h.01" />
    </>
  ),
  // lupa
  interacao: (
    <>
      <circle cx="10.5" cy="10.5" r="5.5" />
      <path d="m14.5 14.5 5 5M10.5 8v5M8 10.5h5" />
    </>
  ),
  // baú
  bau: (
    <>
      <path d="M4 11h16v8H4Z" />
      <path d="M4 11c0-3.5 3.5-5.5 8-5.5s8 2 8 5.5" />
      <path d="M11 11v3h2v-3" />
    </>
  ),
  // pergaminho enrolado
  pergaminho: (
    <>
      <path d="M7 4.5h11v12.5a2.5 2.5 0 0 1-2.5 2.5H6.5" />
      <path d="M7 4.5a2 2 0 0 0-2 2v1h2M6.5 19.5A2.5 2.5 0 0 1 4 17v-1h9v1a2.5 2.5 0 0 0 2.5 2.5" />
      <path d="M10 8.5h5M10 11.5h5" />
    </>
  ),
  // livro aberto
  livro: (
    <>
      <path d="M12 6.5C10 5 7 4.5 3.5 5v13c3.5-.5 6.5 0 8.5 1.5 2-1.5 5-2 8.5-1.5V5c-3.5-.5-6.5 0-8.5 1.5Z" />
      <path d="M12 6.5v13" />
    </>
  ),
  // estátua sobre pedestal
  estatua: (
    <>
      <circle cx="12" cy="5.5" r="2" />
      <path d="M9 15V10.5c0-1.4 1.3-2.5 3-2.5s3 1.1 3 2.5V15" />
      <path d="M7 15h10v2.5H7ZM5.5 20h13l-1.5-2.5H7Z" />
    </>
  ),
  // alavanca
  alavanca: (
    <>
      <path d="M5 19.5h14l-2-4H7Z" />
      <path d="M12 15.5 16.5 5" />
      <circle cx="17" cy="4.5" r="1.5" />
    </>
  ),
}

export function ehIconeGenerico(icone: string): icone is IconeGenericoChave {
  return (ICONES_GENERICOS as readonly string[]).includes(icone) || (ICONES_DE_OBJETO as readonly string[]).includes(icone)
}

interface IconeGenericoProps {
  icone: string
  /** Cor da borda do disco, em #RRGGBB. */
  corBorda: string
  /** Lado do ícone em pixels. */
  tamanho?: number
  className?: string
}

/** Disco de inimigos, NPCs e objetos, no mesmo desenho do ClassIcon. Uma chave desconhecida cai no ícone de NPC. */
export function IconeGenerico({ icone, corBorda, tamanho = 64, className }: IconeGenericoProps) {
  const chave = ehIconeGenerico(icone) ? icone : 'npc'
  return (
    <svg
      className={className}
      width={tamanho}
      height={tamanho}
      viewBox="0 0 48 48"
      role="img"
      aria-label={NOME_DO_ICONE[chave]}
    >
      <circle cx="24" cy="24" r="21" fill="#0f1a1d" stroke={corBorda} strokeWidth="4" />
      <g
        transform="translate(9.6 9.6) scale(1.2)"
        fill="none"
        stroke="#eaf0ec"
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        {GLIFOS[chave]}
      </g>
    </svg>
  )
}
