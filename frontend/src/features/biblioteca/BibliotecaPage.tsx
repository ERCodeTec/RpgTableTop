import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { ConfirmarDialogo } from '../../shared/ui/ConfirmarDialogo'
import {
  CHAVE_CATEGORIAS,
  CHAVE_MODELOS,
  criarCategoriasSugeridas,
  excluirCategoria,
  excluirModelo,
  filtrarModelos,
  listarCategorias,
  listarModelos,
  type Categoria,
  type FiltroDeCategoria,
  type ModeloDeToken,
} from './bibliotecaApi'
import { CategoriaDialogo } from './CategoriaDialogo'
import { ModeloCard } from './ModeloCard'
import { ModeloFormDialogo } from './ModeloFormDialogo'

type Dialogo =
  | { tipo: 'nova-categoria' }
  | { tipo: 'renomear-categoria'; categoria: Categoria }
  | { tipo: 'excluir-categoria'; categoria: Categoria }
  | { tipo: 'novo-modelo' }
  | { tipo: 'editar-modelo'; modelo: ModeloDeToken }
  | { tipo: 'excluir-modelo'; modelo: ModeloDeToken }

/** Os monstros e NPCs do Mestre, para colocar em qualquer mesa. */
export function BibliotecaPage() {
  const queryClient = useQueryClient()
  const categorias = useQuery({ queryKey: CHAVE_CATEGORIAS, queryFn: listarCategorias })
  const modelos = useQuery({ queryKey: CHAVE_MODELOS, queryFn: listarModelos })
  const [filtro, setFiltro] = useState<FiltroDeCategoria>('todas')
  const [busca, setBusca] = useState('')
  const [dialogo, setDialogo] = useState<Dialogo | null>(null)
  const fechar = () => setDialogo(null)

  const recarregar = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: CHAVE_CATEGORIAS }),
      queryClient.invalidateQueries({ queryKey: CHAVE_MODELOS }),
    ])

  const sugeridas = useMutation({ mutationFn: criarCategoriasSugeridas, onSuccess: recarregar })

  const removerCategoria = useMutation({
    mutationFn: (categoria: Categoria) => excluirCategoria(categoria.id),
    onSuccess: async () => {
      setFiltro('todas')
      await recarregar()
      fechar()
    },
  })

  const removerModelo = useMutation({
    mutationFn: (modelo: ModeloDeToken) => excluirModelo(modelo.id),
    onSuccess: async () => {
      await recarregar()
      fechar()
    },
  })

  const listaDeCategorias = categorias.data ?? []
  const listaDeModelos = modelos.data ?? []
  const semCategoria = listaDeModelos.filter((modelo) => modelo.categoriaId === null).length
  const categoriaAberta = listaDeCategorias.find((categoria) => categoria.id === filtro)
  const nomeDaCategoria = new Map(listaDeCategorias.map((categoria) => [categoria.id, categoria.nome]))
  const visiveis = filtrarModelos(listaDeModelos, filtro, busca)
  const carregando = categorias.isPending || modelos.isPending
  const erro = categorias.error ?? modelos.error
  const vazia = !carregando && !erro && listaDeCategorias.length === 0 && listaDeModelos.length === 0

  // Se a categoria aberta sumiu (excluída em outra aba do navegador), volta para "Todas".
  if (filtro !== 'todas' && filtro !== 'sem-categoria' && categorias.data && !categoriaAberta) {
    setFiltro('todas')
  }

  const abas: { id: FiltroDeCategoria; nome: string; quantidade: number }[] = [
    { id: 'todas', nome: 'Todas', quantidade: listaDeModelos.length },
    ...listaDeCategorias.map((categoria) => ({ id: categoria.id, nome: categoria.nome, quantidade: categoria.quantidadeDeModelos })),
    ...(semCategoria > 0 || filtro === 'sem-categoria' ? [{ id: 'sem-categoria', nome: 'Sem categoria', quantidade: semCategoria }] : []),
  ]

  return (
    <main className="pagina">
      <div className="pagina-topo">
        <h1>Biblioteca de tokens</h1>
        <div className="pagina-topo-acoes">
          <button className="botao" type="button" onClick={() => setDialogo({ tipo: 'nova-categoria' })}>
            Nova categoria
          </button>
          <button className="botao botao-primario" type="button" onClick={() => setDialogo({ tipo: 'novo-modelo' })}>
            Novo modelo
          </button>
        </div>
      </div>

      {carregando && <p className="estado">Carregando a biblioteca…</p>}

      {erro && (
        <div className="estado" role="alert">
          <p>{erro.message || 'Não foi possível carregar a biblioteca.'}</p>
          <button className="botao" type="button" onClick={() => void recarregar()}>
            Tentar de novo
          </button>
        </div>
      )}

      {vazia && (
        <div className="estado">
          <p>
            Guarde aqui os monstros e NPCs que você usa. Cada modelo pode ser colocado em qualquer mapa das mesas em que você é
            Mestre, pela aba Tokens do painel.
          </p>
          {sugeridas.isError && (
            <p className="aviso-erro" role="alert">
              {sugeridas.error.message}
            </p>
          )}
          <div className="estado-acoes">
            <button className="botao" type="button" disabled={sugeridas.isPending} onClick={() => sugeridas.mutate()}>
              {sugeridas.isPending ? 'Criando…' : 'Criar categorias sugeridas'}
            </button>
            <button className="botao botao-primario" type="button" onClick={() => setDialogo({ tipo: 'novo-modelo' })}>
              Novo modelo
            </button>
          </div>
          <p className="campo-dica">As sugeridas são NPCs, Vilões e Inimigos. Dá para renomear e excluir depois.</p>
        </div>
      )}

      {!carregando && !erro && !vazia && (
        <>
          <div className="abas abas-rolagem" role="tablist" aria-label="Categorias">
            {abas.map((aba) => (
              <button
                key={aba.id}
                className="aba"
                type="button"
                role="tab"
                aria-selected={filtro === aba.id}
                onClick={() => setFiltro(aba.id)}
              >
                {aba.nome} <span className="aba-contagem">{aba.quantidade}</span>
              </button>
            ))}
          </div>

          <div className="biblioteca-barra">
            <input
              className="biblioteca-busca"
              type="search"
              placeholder="Buscar pelo nome"
              aria-label="Buscar modelo pelo nome"
              value={busca}
              onChange={(evento) => setBusca(evento.target.value)}
            />
            {categoriaAberta && (
              <div className="pagina-topo-acoes">
                <button
                  className="botao botao-discreto"
                  type="button"
                  onClick={() => setDialogo({ tipo: 'renomear-categoria', categoria: categoriaAberta })}
                >
                  Renomear categoria
                </button>
                <button
                  className="botao botao-discreto botao-perigo"
                  type="button"
                  onClick={() => setDialogo({ tipo: 'excluir-categoria', categoria: categoriaAberta })}
                >
                  Excluir categoria
                </button>
              </div>
            )}
          </div>

          {visiveis.length === 0 ? (
            <p className="estado">
              {busca.trim() ? 'Nenhum modelo com esse nome aqui.' : 'Nenhum modelo nesta categoria ainda.'}
            </p>
          ) : (
            <ul className="personagens">
              {visiveis.map((modelo) => (
                <ModeloCard
                  key={modelo.id}
                  modelo={modelo}
                  categoria={filtro === 'todas' && modelo.categoriaId ? nomeDaCategoria.get(modelo.categoriaId) : undefined}
                  aoEditar={() => setDialogo({ tipo: 'editar-modelo', modelo })}
                  aoExcluir={() => setDialogo({ tipo: 'excluir-modelo', modelo })}
                />
              ))}
            </ul>
          )}
        </>
      )}

      {dialogo?.tipo === 'nova-categoria' && <CategoriaDialogo aoSalvar={(nova) => setFiltro(nova.id)} aoFechar={fechar} />}
      {dialogo?.tipo === 'renomear-categoria' && <CategoriaDialogo categoria={dialogo.categoria} aoFechar={fechar} />}
      {dialogo?.tipo === 'excluir-categoria' && (
        <ConfirmarDialogo
          titulo={`Excluir a categoria ${dialogo.categoria.nome}?`}
          rotuloConfirmar="Excluir categoria"
          rotuloEmAndamento="Excluindo…"
          emAndamento={removerCategoria.isPending}
          erro={removerCategoria.error?.message}
          aoConfirmar={() => removerCategoria.mutate(dialogo.categoria)}
          aoFechar={fechar}
        >
          <p>
            {dialogo.categoria.quantidadeDeModelos === 0
              ? 'A categoria está vazia.'
              : dialogo.categoria.quantidadeDeModelos === 1
                ? 'O modelo dela não é excluído: fica em "Sem categoria".'
                : `Os ${dialogo.categoria.quantidadeDeModelos} modelos dela não são excluídos: ficam em "Sem categoria".`}
          </p>
        </ConfirmarDialogo>
      )}
      {dialogo?.tipo === 'novo-modelo' && (
        <ModeloFormDialogo
          categorias={listaDeCategorias}
          categoriaInicial={categoriaAberta?.id ?? null}
          aoFechar={fechar}
        />
      )}
      {dialogo?.tipo === 'editar-modelo' && (
        <ModeloFormDialogo categorias={listaDeCategorias} modelo={dialogo.modelo} aoFechar={fechar} />
      )}
      {dialogo?.tipo === 'excluir-modelo' && (
        <ConfirmarDialogo
          titulo={`Excluir ${dialogo.modelo.nome}?`}
          rotuloConfirmar="Excluir modelo"
          rotuloEmAndamento="Excluindo…"
          emAndamento={removerModelo.isPending}
          erro={removerModelo.error?.message}
          aoConfirmar={() => removerModelo.mutate(dialogo.modelo)}
          aoFechar={fechar}
        >
          <p>O modelo sai da biblioteca. Os tokens que já estão nos mapas continuam lá.</p>
        </ConfirmarDialogo>
      )}
    </main>
  )
}
