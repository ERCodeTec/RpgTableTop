import { useFrame, useThree } from '@react-three/fiber'
import { useEffect, useLayoutEffect, useMemo, useRef, type ReactNode } from 'react'
import type { OrthographicCamera } from 'three'
import { grausParaRad } from './geometria'
import { useMapaStore } from './mapaStore'
import { Rig, RigContext } from './rig'
import { useVisaoStore } from './visaoStore'

const semAnimacao = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches

/**
 * Liga a câmera da cena às preferências de visão do usuário (inclinação e giro, com transição
 * suave) e ao mapa na tela (enquadramento e limites), e entrega o Rig ao restante da cena.
 */
export function CameraDoMapa({ children }: { children: ReactNode }) {
  const camera = useThree((estado) => estado.camera) as OrthographicCamera
  const gl = useThree((estado) => estado.gl)
  const invalidate = useThree((estado) => estado.invalidate)
  const tamanho = useThree((estado) => estado.size)
  const mapaId = useMapaStore((estado) => estado.mapa?.id)
  const largura = useMapaStore((estado) => estado.mapa?.largura ?? 1)
  const altura = useMapaStore((estado) => estado.mapa?.altura ?? 1)
  const enquadramentos = useMapaStore((estado) => estado.enquadramentos)
  const inclinacao = useVisaoStore((estado) => estado.inclinacao)
  const rotacao = useVisaoStore((estado) => estado.rotacao)

  const rig = useMemo(() => new Rig(camera, gl.domElement, invalidate), [camera, gl, invalidate])
  rig.largura = largura
  rig.altura = altura
  rig.tela = tamanho

  // Enquadra ao abrir, ao trocar de mapa, ao mudar as dimensões e quando o usuário pede.
  useLayoutEffect(() => {
    const visao = useVisaoStore.getState()
    rig.girarPara(grausParaRad(visao.inclinacao), grausParaRad(visao.rotacao), false)
    rig.enquadrar(grausParaRad(visao.inclinacao), grausParaRad(visao.rotacao))
  }, [rig, mapaId, largura, altura, enquadramentos])

  const jaMontou = useRef(false)
  useEffect(() => {
    if (!jaMontou.current) {
      jaMontou.current = true
      return
    }
    rig.girarPara(grausParaRad(inclinacao), grausParaRad(rotacao), !semAnimacao())
  }, [rig, inclinacao, rotacao])

  // Ao redimensionar a janela, só os limites mudam; o usuário mantém o enquadramento que escolheu.
  useEffect(() => rig.ajustarAosLimites(), [rig, tamanho.width, tamanho.height])

  useFrame(() => {
    rig.avancar(performance.now())
  })

  return <RigContext.Provider value={rig}>{children}</RigContext.Provider>
}
