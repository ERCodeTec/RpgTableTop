import type { ReactNode } from 'react'
import { COR_BORDA_PADRAO, NOME_DA_CLASSE, type ClasseDnd } from '../classes'

// Glifos originais, desenhados em uma grade de 24x24 só com traços.
const GLIFOS: Record<ClasseDnd, ReactNode> = {
  // machado
  Barbaro: (
    <>
      <path d="M7 21 15.5 4" />
      <path d="M14 6.5C17 3.5 21 5 21.5 9.5 18.5 9 16.5 10.5 15.5 13" />
      <path d="M14 6.5 15.5 13" />
    </>
  ),
  // nota musical
  Bardo: (
    <>
      <path d="M9 18V5.5l10-2.5v13" />
      <circle cx="6.5" cy="18" r="2.5" />
      <circle cx="16.5" cy="16" r="2.5" />
    </>
  ),
  // olho
  Bruxo: (
    <>
      <path d="M2.5 12C6 6.5 18 6.5 21.5 12 18 17.5 6 17.5 2.5 12Z" />
      <path d="M12 8.5c1.6 2 1.6 5 0 7-1.6-2-1.6-5 0-7Z" />
    </>
  ),
  // sol
  Clerigo: (
    <>
      <circle cx="12" cy="12" r="3.5" />
      <path d="M12 2.5v3M12 18.5v3M2.5 12h3M18.5 12h3M5.3 5.3l2.1 2.1M16.6 16.6l2.1 2.1M5.3 18.7l2.1-2.1M16.6 7.4l2.1-2.1" />
    </>
  ),
  // folha
  Druida: (
    <>
      <path d="M5 19C5 10 10 4 20 4c0 10-6 15-15 15Z" />
      <path d="M5 19 14 10" />
    </>
  ),
  // chama
  Feiticeiro: (
    <path d="M12 3c1 4 6 6 6 11.5a6 6 0 0 1-12 0c0-2.5 1.5-4 2.5-5.5.500 2 1.5 3 2.5 3-.500-3-.500-6 1-9Z" />
  ),
  // escudo
  Guerreiro: (
    <>
      <path d="M12 3 19.5 6v5.5c0 4.5-3 8-7.5 9.5-4.5-1.5-7.5-5-7.5-9.5V6Z" />
      <path d="M12 3v18" />
    </>
  ),
  // adaga
  Ladino: (
    <>
      <path d="M12 2.5 14.5 13h-5Z" />
      <path d="M7 13h10M12 13v6" />
      <circle cx="12" cy="20.3" r="1.2" />
    </>
  ),
  // livro aberto
  Mago: (
    <>
      <path d="M12 6.5C10 5 6.5 4.5 3.5 5.5v13c3-1 6.5-.500 8.5 1 2-1.5 5.5-2 8.5-1v-13c-3-1-6.5-.500-8.5 1Z" />
      <path d="M12 6.5v13" />
    </>
  ),
  // equilíbrio
  Monge: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 3a4.5 4.5 0 0 1 0 9 4.5 4.5 0 0 0 0 9" />
      <circle cx="12" cy="7.5" r="0.9" />
      <circle cx="12" cy="16.5" r="0.9" />
    </>
  ),
  // martelo
  Paladino: (
    <>
      <path d="M6 4h12v6.5H6Z" />
      <path d="M12 10.5V21M9.5 21h5" />
    </>
  ),
  // arco e flecha
  Patrulheiro: (
    <>
      <path d="M7 3c10 3 10 15 0 18" />
      <path d="M7 3v18" />
      <path d="M3 12h17.5M17.5 9l3 3-3 3" />
    </>
  ),
}

interface ClassIconProps {
  classe: ClasseDnd
  /** Cor da borda do disco, em #RRGGBB. */
  corBorda?: string
  /** Lado do ícone em pixels. */
  tamanho?: number
  className?: string
}

/** Disco do personagem: a mesma peça que representará o token no mapa. */
export function ClassIcon({ classe, corBorda = COR_BORDA_PADRAO, tamanho = 64, className }: ClassIconProps) {
  return (
    <svg
      className={className}
      width={tamanho}
      height={tamanho}
      viewBox="0 0 48 48"
      role="img"
      aria-label={NOME_DA_CLASSE[classe]}
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
        {GLIFOS[classe]}
      </g>
    </svg>
  )
}
