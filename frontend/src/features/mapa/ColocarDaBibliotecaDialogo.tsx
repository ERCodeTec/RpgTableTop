import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useId, useState, type FormEvent } from 'react'
import { ApiError } from '../../shared/api/http'
import { chaveDaGaleria } from '../../shared/imagens/imagensApi'
import { Campo } from '../../shared/ui/Campo'
import { Dialogo } from '../../shared/ui/Dialogo'
import {
  CHAVE_CATEGORIAS,
  CHAVE_MODELOS,
  filtrarModelos,
  listarCategorias,
  listarModelos,
  QUANTIDADE_MAXIMA,
  type FiltroDeCategoria,
} from '../biblioteca/bibliotecaApi'
import { DiscoDoModelo } from '../biblioteca/DiscoDoModelo'
import { colocarDaBiblioteca, NOME_DO_TIPO, type Mapa } from './mapaApi'
import { useMapaStore } from './mapaStore'

interface ColocarDaBibliotecaDialogoProps {
  mesaId: string
  mapa: Mapa
  aoFechar: () => void
}

/** Escolhe um modelo da biblioteca e coloca uma ou mais cópias no mapa aberto. */
export function ColocarDaBibliotecaDialogo({ mesaId, mapa, aoFechar }: ColocarDaBibliotecaDialogoProps) {
  const queryClient = useQueryClient()
  const idCategoria = useId()
  const categorias = useQuery({ queryKey: CHAVE_CATEGORIAS, queryFn: listarCategorias })
  const modelos = useQuery({ queryKey: CHAVE_MODELOS, queryFn: listarModelos })
  const [filtro, setFiltro] = useState<FiltroDeCategoria>('todas')
  const [busca, setBusca] = useState('')
  const [modeloId, setModeloId] = useState<string | null>(null)
  const [quantidade, setQuantidade] = useState('1')
  const [ocultos, setOcultos] = useState(false)

  const colocar = useMutation({
    mutationFn: () => colocarDaBiblioteca(mapa.id, { modeloId: modeloId!, quantidade: Number(quantidade) || 0, visivel: !ocultos }),
    onSuccess: (tokens) => {
      // Os eventos do servidor trazem os mesmos tokens; aplicar aqui só adianta a tela.
      const store = useMapaStore.getState()
      for (const token of tokens) store.aplicarToken(token)
      if (tokens[0]) store.selecionar(tokens[0].id)
      // Na primeira vez, a imagem do modelo é copiada para a galeria da mesa.
      void queryClient.invalidateQueries({ queryKey: chaveDaGaleria(mesaId) })
      aoFechar()
    },
    onError: (erro) => {
      if (erro instanceof ApiError && erro.status === 404) {
        setModeloId(null)
        void queryClient.invalidateQueries({ queryKey: CHAVE_MODELOS })
      }
    },
  })

  const erro = colocar.error instanceof ApiError ? colocar.error : null
  const listaDeModelos = modelos.data ?? []
  const visiveis = filtrarModelos(listaDeModelos, filtro, busca)
  const temSemCategoria = listaDeModelos.some((modelo) => modelo.categoriaId === null)

  function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    if (modeloId) colocar.mutate()
  }

  return (
    <Dialogo titulo={`Colocar da biblioteca em ${mapa.nome}`} aoFechar={aoFechar}>
      <form className="form-pilha" onSubmit={aoEnviar} noValidate>
        {modelos.isPending || categorias.isPending ? (
          <p className="painel-mestre-vazio">Carregando a biblioteca…</p>
        ) : modelos.isError || categorias.isError ? (
          <p className="aviso-erro" role="alert">
            {(modelos.error ?? categorias.error)?.message || 'Não foi possível carregar a biblioteca.'}
          </p>
        ) : listaDeModelos.length === 0 ? (
          <p className="painel-mestre-vazio">
            Sua biblioteca está vazia. Crie modelos de monstros e NPCs na página{' '}
            {/* Em outra aba: o Mestre não sai da mesa no meio da sessão. */}
            <a href="/biblioteca" target="_blank" rel="noreferrer">
              Biblioteca
            </a>{' '}
            e use-os em qualquer mesa.
          </p>
        ) : (
          <>
            <div className="form-linha">
              <div className="campo">
                <label htmlFor={idCategoria}>Categoria</label>
                <select id={idCategoria} value={filtro} onChange={(evento) => setFiltro(evento.target.value)}>
                  <option value="todas">Todas</option>
                  {categorias.data.map((categoria) => (
                    <option key={categoria.id} value={categoria.id}>
                      {categoria.nome}
                    </option>
                  ))}
                  {temSemCategoria && <option value="sem-categoria">Sem categoria</option>}
                </select>
              </div>
              <Campo rotulo="Buscar" type="search" value={busca} onChange={(evento) => setBusca(evento.target.value)} />
            </div>

            {visiveis.length === 0 ? (
              <p className="painel-mestre-vazio">Nenhum modelo aqui.</p>
            ) : (
              <ul className="biblioteca-opcoes" aria-label="Modelos">
                {visiveis.map((modelo) => (
                  <li key={modelo.id}>
                    <button
                      className="biblioteca-opcao"
                      type="button"
                      aria-pressed={modeloId === modelo.id}
                      onClick={() => setModeloId(modelo.id)}
                    >
                      <DiscoDoModelo modelo={modelo} tamanho={40} />
                      <span className="biblioteca-opcao-dados">
                        <span className="biblioteca-opcao-nome">{modelo.nome}</span>
                        <span className="biblioteca-opcao-detalhe">
                          {NOME_DO_TIPO[modelo.tipo]} · {modelo.hpMaximo} de HP
                        </span>
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            )}

            <div className="form-linha">
              <Campo
                rotulo="Quantidade"
                type="number"
                inputMode="numeric"
                min={1}
                max={QUANTIDADE_MAXIMA}
                value={quantidade}
                onChange={(evento) => setQuantidade(evento.target.value)}
                erros={erro?.erros.quantidade}
                dica="Com mais de um, os nomes ganham número: Goblin 1, Goblin 2…"
              />
              <label className="campo-caixa">
                <input type="checkbox" checked={ocultos} onChange={(evento) => setOcultos(evento.target.checked)} />
                Ocultos dos jogadores
              </label>
            </div>
          </>
        )}

        {colocar.isError && !erro?.erros.quantidade && (
          <p className="aviso-erro" role="alert">
            {colocar.error.message || 'Não foi possível colocar os tokens.'}
          </p>
        )}

        <div className="dialogo-acoes">
          <button className="botao" type="button" onClick={aoFechar}>
            Cancelar
          </button>
          <button className="botao botao-primario" type="submit" disabled={!modeloId || colocar.isPending}>
            {colocar.isPending ? 'Colocando…' : 'Colocar no mapa'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
