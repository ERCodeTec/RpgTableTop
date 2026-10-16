import { memo, useEffect, useMemo } from 'react'
import { BoxGeometry, Color, EdgesGeometry } from 'three'
import { useImagemCarregada } from '../../shared/imagens/imagens'
import { useEditorStore } from './editorStore'
import { alturaDesenhada, grausParaRad } from './geometria'
import { useMapaStore } from './mapaStore'
import { A_VISTA, EXPLORADA, estadoDaArea, NUNCA_VISTA } from './nevoa'
import { texturaDaImagem, useTexturaPronta } from './texturas'

/**
 * Caixa com as coordenadas de textura em células: um ladrilho da imagem por célula, em vez de a
 * imagem esticada pela parede inteira. No topo, o ladrilho acompanha a rotação da estrutura.
 * Nas laterais, um ladrilho mais alto que largo (`proporcao` = altura / largura da imagem) cobre
 * essa quantidade de células na vertical: um de 64x128 cobre a parede de altura 2 inteira.
 * `umLadrilhoNaAltura` (a porta) estica um ladrilho só pela altura das laterais: repetido, o
 * desenho de uma porta viraria duas portas empilhadas.
 */
function criarCaixa(
  largura: number,
  altura: number,
  profundidade: number,
  rotacaoGraus: number,
  umLadrilhoNaAltura: boolean,
  proporcao: number,
): BoxGeometry {
  const caixa = new BoxGeometry(largura, altura, profundidade)
  const uv = caixa.attributes.uv
  const vertical = umLadrilhoNaAltura ? 1 : altura / proporcao
  // Faces na ordem do BoxGeometry (+x, -x, +y, -y, +z, -z), com o tamanho de cada uma em células.
  const tamanhos = [
    [profundidade, vertical],
    [profundidade, vertical],
    [largura, profundidade],
    [largura, profundidade],
    [largura, vertical],
    [largura, vertical],
  ]
  const angulo = grausParaRad(rotacaoGraus)
  const [cosseno, seno] = [Math.cos(angulo), Math.sin(angulo)]
  for (let face = 0; face < 6; face++) {
    for (let vertice = 0; vertice < 4; vertice++) {
      const indice = face * 4 + vertice
      const u = uv.getX(indice) * tamanhos[face][0]
      const v = uv.getY(indice) * tamanhos[face][1]
      if (face === 2) uv.setXY(indice, u * cosseno - v * seno, u * seno + v * cosseno)
      else uv.setXY(indice, u, v)
    }
  }
  return caixa
}

/** No que já foi explorado e não está à vista, a estrutura aparece nesta fração da cor. */
const LUZ_DO_EXPLORADO = 0.35
const TEXTURA_NO_EXPLORADO = new Color('#ffffff').multiplyScalar(LUZ_DO_EXPLORADO)

/**
 * Uma estrutura do cenário: caixa do tamanho da área ocupada, com a cor ou a textura escolhida.
 * Com a névoa, o jogador não vê o que nunca foi visto e vê escurecido o que só foi explorado.
 */
export const EstruturaNoMapa = memo(function EstruturaNoMapa({ id, sombras }: { id: string; sombras: boolean }) {
  const estrutura = useMapaStore((estado) => estado.estruturas[id])
  const selecionada = useEditorStore((estado) => estado.modo === 'editar' && estado.selecionadaId === id)
  const texturaCarregada = useImagemCarregada(estrutura?.texturaImagemId)
  const texturaPronta = useTexturaPronta(estrutura?.texturaPadrao)
  const naNevoa = useMapaStore((estado) =>
    estrutura && estado.nevoa && !estado.nevoa.translucida ? estadoDaArea(estado.nevoa, estrutura) : A_VISTA,
  )

  const largura = estrutura?.largura ?? 1
  const profundidade = estrutura?.profundidade ?? 1
  const altura = estrutura ? alturaDesenhada(estrutura) : 1
  const rotacao = estrutura?.rotacaoGraus ?? 0
  const ehPorta = estrutura?.tipo === 'Porta'
  const imagem = (estrutura?.texturaImagemId ? texturaCarregada : (texturaPronta?.image as HTMLImageElement | undefined)) ?? null
  const proporcao = imagem && imagem.width > 0 ? imagem.height / imagem.width : 1

  const cor = estrutura?.cor
  const corNoExplorado = useMemo(() => (cor ? new Color(cor).multiplyScalar(LUZ_DO_EXPLORADO) : null), [cor])

  const caixa = useMemo(
    () => criarCaixa(largura, altura, profundidade, rotacao, ehPorta, proporcao),
    [largura, altura, profundidade, rotacao, ehPorta, proporcao],
  )
  const contorno = useMemo(() => new EdgesGeometry(caixa), [caixa])
  useEffect(
    () => () => {
      caixa.dispose()
      contorno.dispose()
    },
    [caixa, contorno],
  )

  if (!estrutura || naNevoa === NUNCA_VISTA) return null
  const explorada = naNevoa === EXPLORADA

  const aberta = estrutura.tipo === 'Porta' && estrutura.portaAberta
  const chao = estrutura.tipo === 'Chao'
  const textura = estrutura.texturaImagemId && texturaCarregada ? texturaDaImagem(estrutura.texturaImagemId, true) : texturaPronta

  return (
    <group position={[estrutura.x + largura / 2, altura / 2, estrutura.y + profundidade / 2]}>
      <mesh geometry={caixa} castShadow={sombras && !aberta && !chao} receiveShadow={sombras}>
        <meshLambertMaterial
          // Trocar entre opaco e translúcido, ou entre cor e textura, exige um material novo.
          key={`${aberta}|${textura ? 'textura' : 'cor'}`}
          // Com textura, a cor branca deixa a imagem como ela é.
          color={textura ? (explorada ? TEXTURA_NO_EXPLORADO : '#ffffff') : explorada ? corNoExplorado! : estrutura.cor}
          map={textura}
          transparent={aberta}
          opacity={aberta ? 0.45 : 1}
          depthWrite={!aberta}
        />
      </mesh>
      {selecionada && (
        <lineSegments geometry={contorno} renderOrder={20}>
          <lineBasicMaterial color="#e2b23c" depthTest={false} />
        </lineSegments>
      )}
    </group>
  )
})
