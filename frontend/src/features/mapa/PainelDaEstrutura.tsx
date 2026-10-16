import { useEffect, useId, useRef, useState } from 'react'
import { Miniatura } from '../../shared/imagens/Miniatura'
import { alterarNoMapa, duplicarNoMapa, girarNoMapa, removerDoMapa } from './acoesDoEditor'
import {
  ALTURA_MAXIMA,
  ALTURA_MINIMA,
  NOME_DA_ESTRUTURA,
  type DadosDaEstrutura,
  type Estrutura,
  type TipoEstrutura,
} from './mapaApi'
import { SeletorDeTextura } from './SeletorDeTextura'
import { urlDaTexturaPronta, useCatalogoDeTexturas } from './texturasProntas'

const TIPOS: TipoEstrutura[] = ['Parede', 'Porta', 'Pilar', 'Obstaculo', 'Chao']
const ESPERA_MS = 400

/**
 * Um valor que o Mestre ajusta arrastando (cor, altura): a tela mostra na hora, mas a gravação
 * espera ele parar, para não gerar uma chamada e uma entrada no histórico a cada passo.
 */
function useAjusteAdiado<T>(valor: T, gravar: (novo: T) => void): [T, (novo: T) => void] {
  const [rascunho, setRascunho] = useState(valor)
  const temporizador = useRef<ReturnType<typeof setTimeout>>(undefined)
  const gravarRef = useRef(gravar)
  gravarRef.current = gravar

  useEffect(() => {
    setRascunho(valor)
    return () => clearTimeout(temporizador.current)
  }, [valor])

  return [
    rascunho,
    (novo) => {
      setRascunho(novo)
      clearTimeout(temporizador.current)
      temporizador.current = setTimeout(() => gravarRef.current(novo), ESPERA_MS)
    },
  ]
}

/** Propriedades da estrutura selecionada. Cada mudança é gravada na hora e entra no histórico. */
export function PainelDaEstrutura({ mesaId, estrutura }: { mesaId: string; estrutura: Estrutura }) {
  const idTipo = useId()
  const idCor = useId()
  const idAltura = useId()
  const [escolhendoTextura, setEscolhendoTextura] = useState(false)
  const alterar = (mudancas: Partial<DadosDaEstrutura>) => void alterarNoMapa(estrutura.id, mudancas)

  const [cor, setCor] = useAjusteAdiado(estrutura.cor, (nova) => alterar({ cor: nova }))
  const [altura, setAltura] = useAjusteAdiado(estrutura.altura, (nova) => alterar({ altura: nova }))
  const porta = estrutura.tipo === 'Porta'
  const chao = estrutura.tipo === 'Chao'
  const comTextura = estrutura.texturaImagemId !== null || estrutura.texturaPadrao !== null
  const catalogo = useCatalogoDeTexturas()
  const pronta = catalogo.data?.texturas.find((textura) => textura.chave === estrutura.texturaPadrao)

  return (
    <div className="painel-item painel-item-atual">
      <p className="painel-item-nome">
        <strong>{NOME_DA_ESTRUTURA[estrutura.tipo]}</strong>
        <span className="selo">{estrutura.bloqueia ? 'Bloqueia' : 'Não bloqueia'}</span>
      </p>
      <p className="painel-item-detalhe">
        {estrutura.largura} × {estrutura.profundidade} células, a partir de ({estrutura.x}, {estrutura.y})
      </p>

      <div className="campo">
        <label htmlFor={idTipo}>Tipo</label>
        <select id={idTipo} value={estrutura.tipo} onChange={(evento) => alterar({ tipo: evento.target.value as TipoEstrutura })}>
          {TIPOS.map((tipo) => (
            <option key={tipo} value={tipo}>
              {NOME_DA_ESTRUTURA[tipo]}
            </option>
          ))}
        </select>
      </div>

      {!chao && (
        <div className="campo">
          <label htmlFor={idAltura}>Altura: {altura.toLocaleString('pt-BR')} células</label>
          <input
            id={idAltura}
            type="range"
            min={ALTURA_MINIMA}
            max={ALTURA_MAXIMA}
            step={0.1}
            value={altura}
            onChange={(evento) => setAltura(Number(evento.target.value))}
          />
        </div>
      )}

      <div className="campo campo-cor">
        <label htmlFor={idCor}>Cor</label>
        <input id={idCor} type="color" value={cor} onChange={(evento) => setCor(evento.target.value.toUpperCase())} />
        {comTextura && <p className="campo-dica">Com textura, a cor não aparece.</p>}
      </div>

      <div className="campo">
        <span className="campo-rotulo">Textura</span>
        <div className="campo-imagem">
          {estrutura.texturaImagemId ? (
            <Miniatura imagemId={estrutura.texturaImagemId} alt="Textura atual" />
          ) : estrutura.texturaPadrao ? (
            <img
              className="miniatura miniatura-pixel"
              src={urlDaTexturaPronta(estrutura.texturaPadrao)}
              alt={`Textura atual: ${pronta?.nome ?? estrutura.texturaPadrao}`}
              title={pronta?.nome}
              draggable={false}
            />
          ) : (
            <span className="miniatura miniatura-vazia" aria-hidden="true" />
          )}
          <button className="botao botao-discreto" type="button" onClick={() => setEscolhendoTextura(true)}>
            {comTextura ? 'Trocar' : 'Escolher'}
          </button>
          {comTextura && (
            <button className="botao botao-discreto" type="button" onClick={() => alterar({ texturaImagemId: null, texturaPadrao: null })}>
              Remover
            </button>
          )}
        </div>
      </div>

      {porta ? (
        <label className="campo-caixa">
          <input type="checkbox" checked={estrutura.portaAberta} onChange={(evento) => alterar({ portaAberta: evento.target.checked })} />
          Porta aberta (deixa passar)
        </label>
      ) : (
        <label className="campo-caixa">
          <input
            type="checkbox"
            checked={estrutura.bloqueiaMovimento}
            onChange={(evento) => alterar({ bloqueiaMovimento: evento.target.checked })}
          />
          Bloqueia o movimento dos jogadores
        </label>
      )}

      <div className="painel-item-acoes">
        <button className="botao botao-discreto" type="button" onClick={() => void girarNoMapa(estrutura.id)} title="Atalho: R">
          Girar
        </button>
        <button className="botao botao-discreto" type="button" onClick={() => void duplicarNoMapa(estrutura.id)} title="Atalho: Ctrl+D">
          Duplicar
        </button>
        <button className="botao botao-discreto botao-perigo" type="button" onClick={() => void removerDoMapa(estrutura.id)} title="Atalho: Delete">
          Excluir
        </button>
      </div>

      {escolhendoTextura && (
        <SeletorDeTextura
          mesaId={mesaId}
          tipo={estrutura.tipo}
          aoEscolherPronta={(chave) => alterar({ texturaPadrao: chave, texturaImagemId: null })}
          aoEscolherImagem={(imagemId) => alterar({ texturaImagemId: imagemId, texturaPadrao: null })}
          aoFechar={() => setEscolhendoTextura(false)}
        />
      )}
    </div>
  )
}
