import { useMutation } from '@tanstack/react-query'
import { useId, useState } from 'react'
import { ConfirmarDialogo } from '../../shared/ui/ConfirmarDialogo'
import { useEditorStore } from './editorStore'
import { definirModoDaNevoa, limparMemoriaDaNevoa, NOME_DO_MODO_DE_NEVOA, type ModoDeNevoa } from './mapaApi'
import { useMapaStore } from './mapaStore'

const MODOS: ModoDeNevoa[] = ['Desligada', 'Compartilhada', 'Individual']

/** Valor do seletor "Ver como" para a visão do grupo (o `null` de verComo). */
const GRUPO = '__grupo__'
/** Valor do seletor para o Mestre ver tudo (o `undefined` de verComo). */
const TUDO = '__tudo__'

/**
 * A névoa de guerra do mapa na tela: o modo, de quem o Mestre vê a névoa, o pincel e a limpeza
 * da memória. A visão de cada token vem da ficha do personagem e, nos tokens do Mestre, do campo
 * Visão (os aliados).
 */
export function PainelDaNevoa() {
  const idModo = useId()
  const idVerComo = useId()
  const mapa = useMapaStore((estado) => estado.mapa)
  const verComo = useMapaStore((estado) => estado.verComo)
  const ordem = useMapaStore((estado) => estado.ordem)
  const tokens = useMapaStore((estado) => estado.tokens)
  const pintando = useEditorStore((estado) => estado.modo === 'nevoa')
  const [confirmandoLimpeza, setConfirmandoLimpeza] = useState(false)

  const modo = useMutation({
    mutationFn: (novo: ModoDeNevoa) => definirModoDaNevoa(mapa!.id, novo),
    onSuccess: (atualizado) => useMapaStore.getState().mapaAtualizado(atualizado),
  })

  const limpar = useMutation({
    mutationFn: () => limparMemoriaDaNevoa(mapa!.id),
    onSuccess: () => setConfirmandoLimpeza(false),
  })

  if (!mapa) return null

  const ligada = mapa.nevoa !== 'Desligada'
  const jogadores = ordem.map((id) => tokens[id]).filter((token) => token?.donoUsuarioId)
  const aliados = ordem.map((id) => tokens[id]).filter((token) => token && !token.donoUsuarioId && token.visao > 0)
  const valorDoVerComo = verComo === undefined ? TUDO : verComo === null ? GRUPO : verComo

  return (
    <section className="painel-mestre-secao">
      <div className="painel-mestre-topo">
        <h2>Névoa de guerra</h2>
      </div>

      {(modo.error ?? limpar.error) && (
        <p className="aviso-erro" role="alert">
          {(modo.error ?? limpar.error)?.message || 'Não foi possível concluir a ação.'}
        </p>
      )}

      <div className="campo">
        <label htmlFor={idModo}>Névoa neste mapa</label>
        <select
          id={idModo}
          value={mapa.nevoa}
          disabled={modo.isPending}
          onChange={(evento) => modo.mutate(evento.target.value as ModoDeNevoa)}
        >
          {MODOS.map((opcao) => (
            <option key={opcao} value={opcao}>
              {NOME_DO_MODO_DE_NEVOA[opcao]}
            </option>
          ))}
        </select>
      </div>

      {!ligada ? (
        <p className="painel-mestre-vazio">Os jogadores veem o mapa inteiro. Ligue a névoa para que eles vejam só o que os tokens deles enxergam.</p>
      ) : (
        <>
          <p className="mapa-dica">
            {mapa.nevoa === 'Compartilhada'
              ? 'Cada jogador vê o que qualquer token do grupo vê, e o explorado é um só para todos.'
              : 'Cada jogador vê só pelo próprio token (e pelos aliados), e guarda o próprio explorado.'}{' '}
            Paredes, pilares e portas fechadas tapam a visão. O alcance de cada personagem vem da ficha
            {aliados.length > 0 ? `; aliados com visão: ${aliados.map((token) => token.nome).join(', ')}.` : '; dê Visão a um token seu para ele ser um aliado.'}
          </p>

          <div className="campo">
            <label htmlFor={idVerComo}>Ver como</label>
            <select
              id={idVerComo}
              value={valorDoVerComo}
              onChange={(evento) => {
                const valor = evento.target.value
                useMapaStore.getState().definirVerComo(valor === TUDO ? undefined : valor === GRUPO ? null : valor)
              }}
            >
              <option value={TUDO}>Mestre: tudo, com a névoa sombreada</option>
              <option value={GRUPO}>O grupo</option>
              {mapa.nevoa === 'Individual' &&
                jogadores.map((token) => (
                  <option key={token.id} value={token.donoUsuarioId!}>
                    {token.nome}
                  </option>
                ))}
            </select>
          </div>
          {verComo !== undefined && (
            <p className="mapa-dica">Só na sua tela: a névoa aparece como para os jogadores, no modo Jogar.</p>
          )}

          <div className="painel-mestre-topo-acoes">
            <button
              className="botao botao-discreto"
              type="button"
              aria-pressed={pintando}
              onClick={() => useEditorStore.getState().definirModo(pintando ? 'jogar' : 'nevoa')}
            >
              {pintando ? 'Parar de pintar' : 'Revelar ou cobrir à mão'}
            </button>
            <button className="botao botao-discreto botao-perigo" type="button" onClick={() => setConfirmandoLimpeza(true)}>
              Limpar memória
            </button>
          </div>
        </>
      )}

      {confirmandoLimpeza && (
        <ConfirmarDialogo
          titulo="Esquecer o que os jogadores exploraram?"
          rotuloConfirmar="Limpar memória"
          rotuloEmAndamento="Limpando…"
          emAndamento={limpar.isPending}
          erro={limpar.error?.message}
          aoConfirmar={() => limpar.mutate()}
          aoFechar={() => setConfirmandoLimpeza(false)}
        >
          <p>
            O mapa volta a ficar escuro para todos, menos o que os tokens veem agora e o que você revelou à mão. Não dá
            para desfazer.
          </p>
        </ConfirmarDialogo>
      )}
    </section>
  )
}
