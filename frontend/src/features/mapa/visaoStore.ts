import { create } from 'zustand'

export type Qualidade = 'baixa' | 'media'

export const INCLINACAO_MAXIMA = 45
/** Inclinação e giro da visão isométrica: com 45° de giro a grade aparece em losango. */
export const INCLINACAO_ISOMETRICA = 45

interface EstadoDaVisao {
  /** Graus a partir da visão superior: 0 olha de cima, 45 é o máximo. */
  inclinacao: number
  /** Giro da visão em torno do eixo vertical, em graus. Cresce sem voltar a zero, para a animação seguir o menor caminho. */
  rotacao: number
  qualidade: Qualidade
  definirInclinacao: (graus: number) => void
  /** Visão superior, com a grade alinhada à tela. */
  topo: () => void
  isometrica: () => void
  /** Gira 90° no sentido dado (1 = horário visto de cima). */
  girar: (sentido: 1 | -1) => void
  /** Inclinação e giro livres, em graus (arraste com o botão direito). A inclinação fica entre 0 e o máximo. */
  definirAngulos: (inclinacao: number, rotacao: number) => void
  definirQualidade: (qualidade: Qualidade) => void
}

const CHAVE = 'vtt.visao'

// A câmera é de cada usuário e nunca vai ao servidor; só a preferência fica guardada neste navegador.
function lerPreferencias(): Pick<EstadoDaVisao, 'inclinacao' | 'rotacao' | 'qualidade'> {
  // Em telas de toque estreitas (celulares), começar pela qualidade Baixa poupa bateria e GPU.
  const celular = window.matchMedia('(pointer: coarse) and (max-width: 48rem)').matches
  const padrao = { inclinacao: 0, rotacao: 0, qualidade: (celular ? 'baixa' : 'media') as Qualidade }
  try {
    const salvo = JSON.parse(localStorage.getItem(CHAVE) ?? '{}') as Partial<EstadoDaVisao>
    return {
      inclinacao: typeof salvo.inclinacao === 'number' ? Math.min(INCLINACAO_MAXIMA, Math.max(0, salvo.inclinacao)) : padrao.inclinacao,
      rotacao: typeof salvo.rotacao === 'number' ? ((salvo.rotacao % 360) + 360) % 360 : padrao.rotacao,
      qualidade: salvo.qualidade === 'baixa' || salvo.qualidade === 'media' ? salvo.qualidade : padrao.qualidade,
    }
  } catch {
    return padrao
  }
}

function guardar(estado: EstadoDaVisao) {
  try {
    localStorage.setItem(
      CHAVE,
      JSON.stringify({ inclinacao: estado.inclinacao, rotacao: estado.rotacao, qualidade: estado.qualidade }),
    )
  } catch {
    // sem localStorage, a preferência vale só até recarregar
  }
}

/** O múltiplo de `passo` (deslocado de `resto`) mais próximo de `graus`. */
const maisProximo = (graus: number, passo: number, resto: number) => Math.round((graus - resto) / passo) * passo + resto

export const useVisaoStore = create<EstadoDaVisao>((set, get) => {
  const alterar = (parcial: Partial<EstadoDaVisao>) => {
    set(parcial)
    guardar(get())
  }

  return {
    ...lerPreferencias(),
    definirInclinacao: (graus) => alterar({ inclinacao: Math.min(INCLINACAO_MAXIMA, Math.max(0, graus)) }),
    topo: () => alterar({ inclinacao: 0, rotacao: maisProximo(get().rotacao, 90, 0) }),
    isometrica: () => alterar({ inclinacao: INCLINACAO_ISOMETRICA, rotacao: maisProximo(get().rotacao, 90, 45) }),
    girar: (sentido) => alterar({ rotacao: get().rotacao + sentido * 90 }),
    definirAngulos: (inclinacao, rotacao) => alterar({ inclinacao: Math.min(INCLINACAO_MAXIMA, Math.max(0, inclinacao)), rotacao }),
    definirQualidade: (qualidade) => alterar({ qualidade }),
  }
})
