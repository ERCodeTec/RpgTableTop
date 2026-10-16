export type StatusDaConexao = 'conectando' | 'conectado' | 'reconectando' | 'desconectado'

const TEXTO: Record<StatusDaConexao, string> = {
  conectando: 'Conectando…',
  conectado: 'Ao vivo',
  reconectando: 'Reconectando…',
  desconectado: 'Desconectado',
}

/** Indicador da conexão em tempo real. O estado vem escrito, não só na cor do ponto. */
export function StatusConexao({ status }: { status: StatusDaConexao }) {
  return (
    <span className={`status-conexao status-conexao-${status}`} role="status">
      <span className="status-conexao-ponto" aria-hidden="true" />
      {TEXTO[status]}
    </span>
  )
}
