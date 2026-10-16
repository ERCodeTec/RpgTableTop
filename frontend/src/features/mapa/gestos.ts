/**
 * Um gesto com um ponteiro, do pressionar ao soltar. Se o ponteiro soltar sem ter saído do lugar,
 * foi um clique; senão, um arraste que termina concluído ou cancelado (Esc, segundo dedo...).
 */
export interface Gesto {
  arrastar: (clienteX: number, clienteY: number) => void
  concluir: () => void
  cancelar: () => void
  clicar: () => void
}

/** O que os ponteiros fazem no mapa em cada modo (jogar, editar, ajustar fundo). */
export interface ModoDeInteracao {
  /** O gesto que começa neste ponto, ou null para deslocar o mapa. */
  iniciar: (clienteX: number, clienteY: number, teclas: { shift: boolean }) => Gesto | null
  /** Clique em um ponto que não iniciou gesto. */
  clicarNoFundo: (clienteX: number, clienteY: number) => void
  /** O ponteiro passa sobre o mapa sem nada pressionado; null quando sai dele. */
  pairar?: (ponto: { clienteX: number; clienteY: number } | null) => void
  /** Duplo clique ou toque duplo no mesmo ponto pinga, em vez de clicar de novo. */
  pingaComToqueDuplo?: boolean
}
