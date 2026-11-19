import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useId, useRef, useState, type FormEvent } from 'react'
import { ApiError } from '../../shared/api/http'
import { Campo } from '../../shared/ui/Campo'
import { Dialogo } from '../../shared/ui/Dialogo'
import {
  atualizarMapa,
  chaveDasPastas,
  chaveDosMapas,
  COR_FUNDO_PADRAO,
  criarMapa,
  definirPastaDoMapa,
  DIMENSAO_MAXIMA,
  DIMENSAO_MINIMA,
  type DadosDoMapa,
  listarPastas,
  type Mapa,
} from './mapaApi'
import { useMapaStore } from './mapaStore'

interface MapaFormDialogoProps {
  mesaId: string
  /** Mapa em edição; ausente ao criar um novo. */
  mapa?: Mapa
  aoFechar: () => void
  aoSalvar?: (mapa: Mapa) => void
}

export function MapaFormDialogo({ mesaId, mapa, aoFechar, aoSalvar }: MapaFormDialogoProps) {
  const queryClient = useQueryClient()
  const idCor = useId()
  const idPasta = useId()
  const [nome, setNome] = useState(mapa?.nome ?? '')
  const [largura, setLargura] = useState(String(mapa?.largura ?? 20))
  const [altura, setAltura] = useState(String(mapa?.altura ?? 15))
  const [corFundo, setCorFundo] = useState(mapa?.corFundo ?? COR_FUNDO_PADRAO)
  const [pastaId, setPastaId] = useState(mapa?.pastaId ?? '')
  const pastas = useQuery({ queryKey: chaveDasPastas(mesaId), queryFn: () => listarPastas(mesaId) })
  // Se o mapa foi criado e só a ida para a pasta falhou, tentar de novo não cria outro mapa.
  const criadoRef = useRef<Mapa | null>(null)

  const salvar = useMutation({
    mutationFn: async (dados: DadosDoMapa) => {
      const alvo = mapa ?? criadoRef.current
      const salvo = alvo ? await atualizarMapa(alvo.id, dados) : await criarMapa(mesaId, dados)
      if (!mapa) criadoRef.current = salvo
      const destino = pastaId || null
      return destino === (salvo.pastaId ?? null) ? salvo : definirPastaDoMapa(salvo.id, destino)
    },
    onSuccess: async (salvo) => {
      useMapaStore.getState().mapaAtualizado(salvo)
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: chaveDosMapas(mesaId) }),
        queryClient.invalidateQueries({ queryKey: chaveDasPastas(mesaId) }),
      ])
      aoFechar()
      aoSalvar?.(salvo)
    },
  })

  const erro = salvar.error instanceof ApiError ? salvar.error : null
  const erroGeral = salvar.error && (!erro || Object.keys(erro.erros).length === 0)
  const dicaDeDimensao = `De ${DIMENSAO_MINIMA} a ${DIMENSAO_MAXIMA} células`

  function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    // Campos numéricos vazios viram 0 e a API devolve a mensagem de faixa válida.
    salvar.mutate({ nome, largura: Number(largura) || 0, altura: Number(altura) || 0, corFundo })
  }

  return (
    <Dialogo titulo={mapa ? `Editar ${mapa.nome}` : 'Novo mapa'} aoFechar={aoFechar}>
      <form className="form-pilha" onSubmit={aoEnviar} noValidate>
        {erroGeral && (
          <p className="aviso-erro" role="alert">
            {salvar.error.message || 'Não foi possível salvar. Tente de novo.'}
          </p>
        )}
        <Campo
          rotulo="Nome do mapa"
          value={nome}
          maxLength={80}
          onChange={(evento) => setNome(evento.target.value)}
          erros={erro?.erros.nome}
          autoFocus
          required
        />
        <div className="form-linha">
          <Campo
            rotulo="Largura"
            type="number"
            inputMode="numeric"
            min={DIMENSAO_MINIMA}
            max={DIMENSAO_MAXIMA}
            value={largura}
            onChange={(evento) => setLargura(evento.target.value)}
            erros={erro?.erros.largura}
            dica={dicaDeDimensao}
            required
          />
          <Campo
            rotulo="Altura"
            type="number"
            inputMode="numeric"
            min={DIMENSAO_MINIMA}
            max={DIMENSAO_MAXIMA}
            value={altura}
            onChange={(evento) => setAltura(evento.target.value)}
            erros={erro?.erros.altura}
            dica={dicaDeDimensao}
            required
          />
          <div className="campo campo-cor">
            <label htmlFor={idCor}>Cor de fundo</label>
            <input
              id={idCor}
              type="color"
              value={corFundo}
              onChange={(evento) => setCorFundo(evento.target.value.toUpperCase())}
            />
            {erro?.erros.corFundo && <p className="campo-erro">{erro.erros.corFundo[0]}</p>}
          </div>
        </div>
        {(pastas.data?.length ?? 0) > 0 && (
          <div className="campo">
            <label htmlFor={idPasta}>Pasta</label>
            <select id={idPasta} value={pastaId} onChange={(evento) => setPastaId(evento.target.value)}>
              <option value="">Sem pasta</option>
              {pastas.data!.map((pasta) => (
                <option key={pasta.id} value={pasta.id}>
                  {pasta.nome}
                </option>
              ))}
            </select>
            {erro?.erros.pastaId && <p className="campo-erro">{erro.erros.pastaId[0]}</p>}
          </div>
        )}
        <div className="dialogo-acoes">
          <button className="botao" type="button" onClick={aoFechar}>
            Cancelar
          </button>
          <button className="botao botao-primario" type="submit" disabled={salvar.isPending}>
            {salvar.isPending ? 'Salvando…' : mapa ? 'Salvar alterações' : 'Criar mapa'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
