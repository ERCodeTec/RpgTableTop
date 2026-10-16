import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useId, useState, type FormEvent } from 'react'
import { ApiError } from '../../shared/api/http'
import { chaveDosConvites, gerarConvite, listarConvites, revogarConvite, type Convite } from './mesasApi'

const VALIDADES = [
  { rotulo: 'Não expira', horas: null },
  { rotulo: '1 hora', horas: 1 },
  { rotulo: '24 horas', horas: 24 },
  { rotulo: '7 dias', horas: 24 * 7 },
] as const

const dataHora = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

function situacao(convite: Convite): string {
  if (convite.revogadoEm) return 'Revogado'
  if (convite.expiraEm && new Date(convite.expiraEm) <= new Date()) return 'Expirado'
  if (convite.usosMaximos !== null && convite.usosAtuais >= convite.usosMaximos) return 'Esgotado'
  return convite.expiraEm ? `Expira em ${dataHora.format(new Date(convite.expiraEm))}` : 'Não expira'
}

function usos(convite: Convite): string {
  const total = convite.usosMaximos === null ? '' : ` de ${convite.usosMaximos}`
  return `${convite.usosAtuais}${total} ${convite.usosAtuais === 1 && !total ? 'entrada' : 'entradas'}`
}

function LinhaDoConvite({ convite, aoRevogar, revogando }: { convite: Convite; aoRevogar: () => void; revogando: boolean }) {
  const [copiado, setCopiado] = useState(false)
  const idLink = useId()

  async function copiar() {
    try {
      await navigator.clipboard.writeText(convite.url)
    } catch {
      // Sem permissão ou fora de HTTPS: seleciona o texto para o usuário copiar com Ctrl+C.
      const campo = document.getElementById(idLink) as HTMLInputElement | null
      campo?.focus()
      campo?.select()
      return
    }
    setCopiado(true)
    setTimeout(() => setCopiado(false), 2000)
  }

  return (
    <li className={`convite${convite.valido ? '' : ' convite-invalido'}`}>
      <div className="convite-link">
        <label className="so-leitor" htmlFor={idLink}>
          Link do convite {convite.codigo}
        </label>
        <input id={idLink} readOnly value={convite.url} onFocus={(evento) => evento.target.select()} />
        {convite.valido && (
          <button className="botao" type="button" onClick={copiar}>
            {copiado ? 'Link copiado' : 'Copiar link'}
          </button>
        )}
      </div>
      <div className="convite-rodape">
        <span>
          {situacao(convite)}, {usos(convite)}
        </span>
        {convite.valido && (
          <button className="botao botao-discreto botao-perigo" type="button" onClick={aoRevogar} disabled={revogando}>
            Revogar
          </button>
        )}
      </div>
    </li>
  )
}

export function ConvitesPainel({ mesaId }: { mesaId: string }) {
  const queryClient = useQueryClient()
  const chave = chaveDosConvites(mesaId)
  const idValidade = useId()
  const idUsos = useId()
  const [validade, setValidade] = useState('')
  const [usosMaximos, setUsosMaximos] = useState('')

  const convites = useQuery({ queryKey: chave, queryFn: () => listarConvites(mesaId) })

  const gerar = useMutation({
    mutationFn: () =>
      gerarConvite(mesaId, {
        validadeHoras: validade ? Number(validade) : null,
        usosMaximos: usosMaximos ? Number(usosMaximos) : null,
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: chave }),
  })

  const revogar = useMutation({
    mutationFn: (codigo: string) => revogarConvite(codigo),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: chave }),
  })

  const erroAoGerar = gerar.error instanceof ApiError ? gerar.error : null

  function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    gerar.mutate()
  }

  return (
    <section className="secao">
      <h2>Convites</h2>
      <form className="convite-form" onSubmit={aoEnviar} noValidate>
        <div className="campo">
          <label htmlFor={idValidade}>Validade</label>
          <select id={idValidade} value={validade} onChange={(evento) => setValidade(evento.target.value)}>
            {VALIDADES.map((opcao) => (
              <option key={opcao.rotulo} value={opcao.horas ?? ''}>
                {opcao.rotulo}
              </option>
            ))}
          </select>
        </div>
        <div className="campo">
          <label htmlFor={idUsos}>Limite de entradas</label>
          <input
            id={idUsos}
            type="number"
            inputMode="numeric"
            min={1}
            max={1000}
            placeholder="Sem limite"
            value={usosMaximos}
            onChange={(evento) => setUsosMaximos(evento.target.value)}
            aria-invalid={erroAoGerar?.erros.usosMaximos ? true : undefined}
          />
        </div>
        <button className="botao botao-primario" type="submit" disabled={gerar.isPending}>
          {gerar.isPending ? 'Gerando…' : 'Gerar link de convite'}
        </button>
      </form>
      {gerar.isError && (
        <p className="aviso-erro" role="alert">
          {erroAoGerar?.erros.usosMaximos?.[0] ?? erroAoGerar?.erros.validadeHoras?.[0] ?? gerar.error.message}
        </p>
      )}
      {revogar.isError && (
        <p className="aviso-erro" role="alert">
          {revogar.error.message || 'Não foi possível revogar o convite.'}
        </p>
      )}

      {convites.isPending && <p className="estado">Carregando convites…</p>}
      {convites.isError && (
        <p className="aviso-erro" role="alert">
          {convites.error.message || 'Não foi possível carregar os convites.'}
        </p>
      )}
      {convites.data?.length === 0 && (
        <p className="estado">Nenhum convite ainda. Gere um link e envie aos jogadores.</p>
      )}
      {convites.data && convites.data.length > 0 && (
        <ul className="convites">
          {convites.data.map((convite) => (
            <LinhaDoConvite
              key={convite.codigo}
              convite={convite}
              aoRevogar={() => revogar.mutate(convite.codigo)}
              revogando={revogar.isPending && revogar.variables === convite.codigo}
            />
          ))}
        </ul>
      )}
    </section>
  )
}
