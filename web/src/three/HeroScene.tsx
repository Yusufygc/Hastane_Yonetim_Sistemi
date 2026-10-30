import { Canvas, useFrame, type RootState } from '@react-three/fiber'
import { useEffect, useRef, useState, type RefObject } from 'react'
import {
  ACESFilmicToneMapping,
  AnimationClip,
  AnimationMixer,
  Box3,
  EquirectangularReflectionMapping,
  Group,
  LoopOnce,
  MathUtils,
  Mesh,
  MeshStandardMaterial,
  PMREMGenerator,
  SkinnedMesh,
  Vector3,
} from 'three'
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js'
import { MeshoptDecoder } from 'three/examples/jsm/libs/meshopt_decoder.module.js'
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js'
import { HDRLoader } from 'three/examples/jsm/loaders/HDRLoader.js'
import type { ResolvedTheme } from '../lib/theme'

export const SECTION_COUNT = 4

/** Stüdyo ışığı: Poly Haven "Ferndale Studio 03" (CC0), 1k. */
const STUDIO_HDRI_URL = '/hdr/studio.hdr'

/** Paylaşımlı GLTFLoader (Meshopt sıkıştırmalı GLB modellerini de çözer). */
const gltfLoader = new GLTFLoader()
gltfLoader.setMeshoptDecoder(MeshoptDecoder)

/** İndirilen GLB sahnelerini önbellekte tutar. */
const modelCache = new Map<string, Promise<Group>>()

/** Yüklenen modelin gömülü animasyon klipleri `userData` içinde taşınır. */
interface ModelData {
  animations?: AnimationClip[]
}

function loadNormalizedGltf(url: string, targetSize: number, keep?: string[]): Promise<Group> {
  const cacheKey = `${url}:${targetSize}:${(keep ?? []).join(',')}`
  const existing = modelCache.get(cacheKey)
  if (existing) return existing

  const promise = new Promise<Group>((resolve, reject) => {
    gltfLoader.load(
      url,
      (gltf) => {
        const scene = gltf.scene

        // Parça seti olarak gelen modelde yalnız adı `keep` listesindeki parçalar bırakılır.
        if (keep?.length) {
          const drop: Mesh[] = []
          scene.traverse((obj) => {
            if (obj instanceof Mesh && !keep.some((name) => obj.name.includes(name))) drop.push(obj)
          })
          drop.forEach((mesh) => mesh.removeFromParent())
        }

        scene.traverse((obj) => {
          if (obj instanceof Mesh) {
            obj.castShadow = true
            obj.receiveShadow = true
            // Kemikli modelde sınırlayıcı küre bağlama pozundan hesaplanır; animasyonda yanlışlıkla gizlenmesin.
            if (obj instanceof SkinnedMesh) obj.frustumCulled = false
            // Modelin kendi PBR malzemesi (renk, pürüzlülük, metalik doku) korunur; yalnız ortam yansıması ayarlanır.
            const materials = Array.isArray(obj.material) ? obj.material : [obj.material]
            materials.forEach((m: unknown) => {
              if (m instanceof MeshStandardMaterial) m.envMapIntensity = 1
            })
          }
        })

        // Modeli kendi sınırlayıcı kutusunun (BoundingBox) merkezine hizala ve hedef boyuta ölçekle
        scene.updateMatrixWorld(true)
        const box = new Box3().setFromObject(scene)
        const size = new Vector3()
        const center = new Vector3()
        box.getSize(size)
        box.getCenter(center)

        const maxDim = Math.max(size.x, size.y, size.z) || 1

        const wrapper = new Group()
        const pivot = new Group()
        scene.position.set(-center.x, -center.y, -center.z)
        pivot.add(scene)
        pivot.scale.setScalar(targetSize / maxDim)
        wrapper.add(pivot)
        wrapper.userData = { animations: gltf.animations } satisfies ModelData

        resolve(wrapper)
      },
      undefined,
      reject,
    )
  })

  modelCache.set(cacheKey, promise)
  return promise
}

function useGltfModel(url: string, targetSize: number, keep?: string[]): Group | null {
  const [model, setModel] = useState<Group | null>(null)
  // `keep` her render'da yeni dizi olabileceğinden bağımlılık olarak içeriği (metin) kullanılır.
  const keepKey = keep?.join(',') ?? ''

  useEffect(() => {
    let active = true
    loadNormalizedGltf(url, targetSize, keepKey ? keepKey.split(',') : undefined)
      .then((loaded) => {
        if (active) setModel(loaded)
      })
      .catch(() => {
        /* Yükleme hatasında sahne çökmez */
      })
    return () => {
      active = false
    }
  }, [url, targetSize, keepKey])

  return model
}

/** Her bölüm (0..3) için kaydırma konumuna (0..1) göre 0..1 arası yumuşak görünürlük ağırlığı. */
function stageWeight(offset: number, index: number): number {
  const center = index / (SECTION_COUNT - 1)
  const distance = Math.abs(offset - center) * (SECTION_COUNT - 1)
  return MathUtils.smoothstep(1 - Math.min(1, distance), 0, 1)
}

interface StageConfig {
  /** Bölüm sırası (0..SECTION_COUNT-1) */
  index: number
  url: string
  /** Modelin en uzun kenarının sahnedeki boyu */
  targetSize: number
  /** Parça seti modelinde bırakılacak parça adları (boşsa tüm model) */
  keep?: string[]
  /** Giriş sırasında kayma yönü: 1 aşağıdan, -1 yukarıdan gelir */
  enterFrom: 1 | -1
  /** Süzülme genliği ve sahne dönüşü */
  bob: number
  baseRotationY: number
  swayY: number
  /** Modelin kendi duruşu (yatay modelleri kameraya çevirmek için) */
  modelRotation?: [number, number, number]
  modelPosition?: [number, number, number]
  /** Sahnenin kameraya doğru eğimi */
  tiltX?: number
}

/**
 * Tek bir kahraman modeli kaydırma ilerlemesine göre sahneye getiren bölüm.
 * Modelde gömülü animasyon varsa son karede (poz) sabitlenir.
 */
function ModelStage({ offset, config }: { offset: RefObject<number>; config: StageConfig }) {
  const group = useRef<Group>(null)
  const mixer = useRef<AnimationMixer | null>(null)
  const model = useGltfModel(config.url, config.targetSize, config.keep)

  useEffect(() => {
    if (!model) return
    const clips = (model.userData as ModelData).animations ?? []
    if (!clips.length) return
    const m = new AnimationMixer(model)
    clips.forEach((clip) => {
      const action = m.clipAction(clip)
      action.setLoop(LoopOnce, 1)
      action.clampWhenFinished = true
      action.play()
    })
    mixer.current = m
    return () => {
      m.stopAllAction()
      m.uncacheRoot(model)
      mixer.current = null
    }
  }, [model])

  useFrame((state, delta) => {
    const g = group.current
    if (!g) return
    const w = stageWeight(offset.current, config.index)
    g.visible = w > 0.015
    if (!g.visible) return

    mixer.current?.update(delta)

    const t = state.clock.elapsedTime
    g.scale.setScalar(MathUtils.lerp(0.2, 1, w))
    g.position.y = (1 - w) * config.enterFrom * 1.1 + Math.sin(t * 1.4 + config.index) * config.bob
    g.rotation.x = config.tiltX ?? 0
    g.rotation.y = config.baseRotationY + Math.sin(t * 0.45 + config.index) * config.swayY + (1 - w) * 0.5 * config.enterFrom
  })

  return (
    <group ref={group}>
      <group position={config.modelPosition} rotation={config.modelRotation}>
        {model && <primitive object={model} />}
      </group>
    </group>
  )
}

/**
 * Bölümler (metinlerle sırası: `pages/Landing.tsx` `SECTIONS`):
 * 1 steteskop, 2 klinik odası, 3 mikroskop, 4 doktor.
 */
const STAGES: StageConfig[] = [
  { index: 0, url: '/models/v2/stethoscope.glb', targetSize: 3.0, enterFrom: -1, bob: 0.05, baseRotationY: -0.3, swayY: 0.25, modelRotation: [1.15, 0, 0] },
  { index: 1, url: '/models/v2/hospital_room.glb', keep: ['Bed', 'IvPole', 'Serum'], targetSize: 3.4, enterFrom: 1, bob: 0.03, baseRotationY: -0.6, swayY: 0.2 },
  { index: 2, url: '/models/v2/microscope.glb', targetSize: 3.0, enterFrom: -1, bob: 0.05, baseRotationY: -0.35, swayY: 0.25 },
  { index: 3, url: '/models/v2/doctor.glb', targetSize: 3.4, enterFrom: 1, bob: 0.03, baseRotationY: 0.2, swayY: 0.2 },
]

/**
 * Modellerin altında yalnız gölgeyi gösteren zemin; sahne 3B nesneleri ana sayfa zeminine bağlar.
 * Kaydırmaya göre yatay konumu ve kamera derinliği ayarlanır.
 */
function MedicalShowcase({ progress, theme }: { progress: RefObject<number>; theme: ResolvedTheme }) {
  const rootGroup = useRef<Group>(null)
  const smooth = useRef(0)
  const isLight = theme === 'light'

  useFrame((state, delta) => {
    state.scene.environmentIntensity = isLight ? 1.1 : 1.0
    smooth.current += (progress.current - smooth.current) * Math.min(1, delta * 6)
    const offset = smooth.current
    const root = rootGroup.current
    if (!root) return

    // Metin sol/sağ dönüşümlü olduğu için 3B sahne her zaman metnin karşı yanında durur (dar ekranda ortada)
    const amplitude = state.size.width > 720 ? 1.95 : 0
    root.position.x = Math.cos(offset * Math.PI * (SECTION_COUNT - 1)) * amplitude
    state.camera.position.z = 7.4 - offset * 0.35
  })

  return (
    <group ref={rootGroup}>
      {/* Gölge yakalayıcı zemin: yalnız modellerin gölgesini çizer, kendisi görünmez */}
      <mesh position={[0, -1.64, 0]} rotation={[-Math.PI / 2, 0, 0]} receiveShadow>
        <planeGeometry args={[14, 14]} />
        <shadowMaterial transparent opacity={isLight ? 0.22 : 0.4} />
      </mesh>

      {STAGES.map((config) => (
        <ModelStage key={config.url} offset={smooth} config={config} />
      ))}
    </group>
  )
}

/**
 * Gerçekçi ortam ışığı: önce yerleşik oda ortamı hemen uygulanır, stüdyo HDRI'ı inince onunla değiştirilir.
 * HDRI yüklenemezse oda ortamı kalır.
 */
function initStudioEnvironment({ gl, scene }: RootState) {
  const pmrem = new PMREMGenerator(gl)
  const fallback = pmrem.fromScene(new RoomEnvironment(), 0.04)
  scene.environment = fallback.texture

  new HDRLoader().load(
    STUDIO_HDRI_URL,
    (hdr) => {
      hdr.mapping = EquirectangularReflectionMapping
      const studio = pmrem.fromEquirectangular(hdr)
      scene.environment = studio.texture
      hdr.dispose()
      fallback.dispose()
      pmrem.dispose()
    },
    undefined,
    () => pmrem.dispose(),
  )
}

/** 3B sahne; sayfa kaydırması `progress` (0..1) ile gerçek GLB medikal modelleri sahneye getirir. */
export default function HeroScene({ progress, theme = 'dark' }: { progress: RefObject<number>; theme?: ResolvedTheme }) {
  const isLight = theme === 'light'
  return (
    <Canvas
      aria-hidden="true"
      camera={{ position: [0, 0.15, 7.4], fov: 42 }}
      dpr={[1, 1.75]}
      shadows
      onCreated={initStudioEnvironment}
      gl={{
        antialias: true,
        powerPreference: 'high-performance',
        alpha: true,
        toneMapping: ACESFilmicToneMapping,
        toneMappingExposure: isLight ? 1.05 : 1.0,
      }}
    >
      <ambientLight intensity={isLight ? 0.25 : 0.15} />
      {/* Ana ışık gölge düşürür; sahne x ekseninde kaydığı için gölge kutusu geniştir */}
      <directionalLight
        position={[4, 8, 5]}
        intensity={isLight ? 1.5 : 1.3}
        castShadow
        shadow-mapSize={[2048, 2048]}
        shadow-camera-left={-6}
        shadow-camera-right={6}
        shadow-camera-top={6}
        shadow-camera-bottom={-6}
        shadow-camera-near={0.5}
        shadow-camera-far={25}
        shadow-bias={-0.0004}
        shadow-normalBias={0.02}
      />
      <MedicalShowcase progress={progress} theme={theme} />
    </Canvas>
  )
}
