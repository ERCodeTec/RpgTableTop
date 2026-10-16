import { useMutation } from '@tanstack/react-query'
import { useId, useState, type FormEvent } from 'react'
import { ApiError } from '../../shared/api/http'
import { Campo } from '../../shared/ui/Campo'
import { Dialogo } from '../../shared/ui/Dialogo'
import {
  COR_TOKEN_PADRAO,
  criarToken,
  DESLOCAMENTO_MAXIMO,
  DESLOCAMENTO_MINIMO,
  DESLOCAMENTO_PADRAO,
  VISAO_MAXIMA,
  editarToken,
  NOME_DO_TIPO,
  type Token,
  type TipoToken,
} from './mapaApi'
import { CampoDeImagem, ICONE_INICIAL, SeletorDeIcone } from './CamposDoToken'
import { useMapaStore } from './mapaStore'

const TIPOS: TipoToken[] = ['Inimigo', 'Npc', 'Personagem']

interface TokenFormDialogoProps {
  mesaId: string
  mapaId: string
  /** Token em edição; ausente ao criar um novo. */
  token?: Token
  aoFechar: () => void
}

export function TokenFormDialogo({ mesaId, mapaId, token, aoFechar }: TokenFormDialogoProps) {
  const idTipo = useId()
  const idCor = useId()
  // No token de um jogador, tudo menos o HP atual vem do personagem dele.
  const deJogador = Boolean(token?.donoUsuarioId)
  const [tipo, setTipo] = useState<TipoToken>(token?.tipo ?? 'Inimigo')
  const [nome, setNome] = useState(token?.nome ?? '')
  const [hpAtual, setHpAtual] = useState(String(token?.hpAtual ?? 10))
  const [hpMaximo, setHpMaximo] = useState(String(token?.hpMaximo ?? 10))
  const [icone, setIcone] = useState(token?.icone ?? ICONE_INICIAL.Inimigo)
  const [corBorda, setCorBorda] = useState(token?.corBorda ?? COR_TOKEN_PADRAO)
  const [imagemId, setImagemId] = useState(token?.imagemId ?? null)
  const [deslocamento, setDeslocamento] = useState(String(token?.deslocamento ?? DESLOCAMENTO_PADRAO))
  const [visao, setVisao] = useState(String(token?.visao ?? 0))

  const salvar = useMutation({
    mutationFn: () => {
      const dados = {
        nome,
        hpAtual: Number(hpAtual) || 0,
        hpMaximo: Number(hpMaximo) || 0,
        icone,
        corBorda,
        imagemId,
        // O do token de um jogador vem do personagem; o servidor ignoraria.
        ...(deJogador ? {} : { deslocamento: Number(deslocamento) || 0, visao: Number(visao) || 0 }),
      }
      return token ? editarToken(token.id, dados) : criarToken(mapaId, { ...dados, tipo })
    },
    onSuccess: (salvo) => {
      // O evento do servidor traz o mesmo token; aplicar aqui só adianta a tela.
      useMapaStore.getState().aplicarToken(salvo)
      aoFechar()
    },
  })

  const erro = salvar.error instanceof ApiError ? salvar.error : null
  const erroGeral = salvar.error && (!erro || Object.keys(erro.erros).length === 0)

  const campoDeImagem = (
    <CampoDeImagem
      fonte={{ mesaId }}
      imagemId={imagemId}
      aoMudar={setImagemId}
      titulo="Imagem do token"
      erros={erro?.erros.imagemId}
    />
  )

  function aoTrocarTipo(novo: TipoToken) {
    setTipo(novo)
    setIcone(ICONE_INICIAL[novo])
  }

  function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    salvar.mutate()
  }

  return (
    <Dialogo titulo={token ? `Editar ${token.nome}` : 'Novo token'} aoFechar={aoFechar}>
      <form className="form-pilha" onSubmit={aoEnviar} noValidate>
        {erroGeral && (
          <p className="aviso-erro" role="alert">
            {salvar.error.message || 'Não foi possível salvar. Tente de novo.'}
          </p>
        )}

        {deJogador ? (
          <>
            <p className="campo-dica">
              Este é o token de um jogador. Nome, ícone, cor, HP máximo ({token!.hpMaximo}), deslocamento
              ({token!.deslocamento} células) e visão ({token!.visao} células) acompanham o personagem dele.
            </p>
            <Campo
              rotulo="HP atual"
              type="number"
              inputMode="numeric"
              min={0}
              max={token!.hpMaximo ?? undefined}
              value={hpAtual}
              onChange={(evento) => setHpAtual(evento.target.value)}
              erros={erro?.erros.hpAtual}
              autoFocus
              required
            />
            {campoDeImagem}
          </>
        ) : (
          <>
            {!token && (
              <div className="campo">
                <label htmlFor={idTipo}>Tipo</label>
                <select id={idTipo} value={tipo} onChange={(evento) => aoTrocarTipo(evento.target.value as TipoToken)}>
                  {TIPOS.map((opcao) => (
                    <option key={opcao} value={opcao}>
                      {NOME_DO_TIPO[opcao]}
                    </option>
                  ))}
                </select>
                {erro?.erros.tipo && <p className="campo-erro">{erro.erros.tipo[0]}</p>}
              </div>
            )}

            <Campo
              rotulo="Nome"
              value={nome}
              maxLength={60}
              onChange={(evento) => setNome(evento.target.value)}
              erros={erro?.erros.nome}
              autoFocus
              required
            />

            <SeletorDeIcone tipo={tipo} icone={icone} corBorda={corBorda} aoEscolher={setIcone} erros={erro?.erros.icone} />

            {campoDeImagem}

            <div className="form-linha">
              <Campo
                rotulo="HP atual"
                type="number"
                inputMode="numeric"
                min={0}
                max={999}
                value={hpAtual}
                onChange={(evento) => setHpAtual(evento.target.value)}
                erros={erro?.erros.hpAtual}
                required
              />
              <Campo
                rotulo="HP máximo"
                type="number"
                inputMode="numeric"
                min={1}
                max={999}
                value={hpMaximo}
                onChange={(evento) => setHpMaximo(evento.target.value)}
                erros={erro?.erros.hpMaximo}
                required
              />
              <Campo
                rotulo="Deslocamento"
                type="number"
                inputMode="numeric"
                min={DESLOCAMENTO_MINIMO}
                max={DESLOCAMENTO_MAXIMO}
                value={deslocamento}
                onChange={(evento) => setDeslocamento(evento.target.value)}
                erros={erro?.erros.deslocamento}
                dica="Células por rodada: 6 = 30 ft."
                required
              />
              <Campo
                rotulo="Visão de aliado"
                type="number"
                inputMode="numeric"
                min={0}
                max={VISAO_MAXIMA}
                value={visao}
                onChange={(evento) => setVisao(evento.target.value)}
                erros={erro?.erros.visao}
                dica="Com a névoa ligada, os jogadores enxergam também por este token, até estas células. 0 = não empresta visão."
              />
              <div className="campo campo-cor">
                <label htmlFor={idCor}>Cor da borda</label>
                <input
                  id={idCor}
                  type="color"
                  value={corBorda}
                  onChange={(evento) => setCorBorda(evento.target.value.toUpperCase())}
                />
                {erro?.erros.corBorda && <p className="campo-erro">{erro.erros.corBorda[0]}</p>}
              </div>
            </div>
          </>
        )}

        <div className="dialogo-acoes">
          <button className="botao" type="button" onClick={aoFechar}>
            Cancelar
          </button>
          <button className="botao botao-primario" type="submit" disabled={salvar.isPending}>
            {salvar.isPending ? 'Salvando…' : token ? 'Salvar alterações' : 'Criar token'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
