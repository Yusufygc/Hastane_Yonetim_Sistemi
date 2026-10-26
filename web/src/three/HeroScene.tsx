import { Canvas, useFrame } from '@react-three/fiber'
import { useMemo, useRef, type RefObject } from 'react'
import { Color, InstancedMesh, Object3D, type Group } from 'three'

export const SECTION_COUNT = 4
const STEPS = 56
// Bölüm başına renk: ilk ekranda turkuaz, kaydırdıkça mavi → mor → yeşil (onay).
const PALETTE = ['#14b8a6', '#3b82f6', '#8b5cf6', '#22c55e'].map((c) => new Color(c))

/** İki sarmal şerit ve aralarındaki bağlar: kaydırma ile döner, renk değiştirir, kameraya yaklaşır. */
function Helix({ progress }: { progress: RefObject<number> }) {
  const group = useRef<Group>(null)
  const strandA = useRef<InstancedMesh>(null)
  const strandB = useRef<InstancedMesh>(null)
  const rungs = useRef<InstancedMesh>(null)
  const smooth = useRef(0)
  const tmp = useMemo(() => new Object3D(), [])
  const tint = useMemo(() => new Color(), [])

  useFrame((state, delta) => {
    // Sayfa kaydırma ilerlemesi (0..1) yumuşatılır: ani kaydırmada sahne sıçramaz.
    smooth.current += (progress.current - smooth.current) * Math.min(1, delta * 6)
    const offset = smooth.current
    const g = group.current
    if (!g) return

    g.rotation.y += delta * 0.15
    g.rotation.z = offset * Math.PI * 0.6
    // Metin sol/sağ dönüşümlü: sarmal metnin karşı yanında durur (dar ekranda ortada kalır).
    const amplitude = state.size.width > 720 ? 1.9 : 0
    g.position.x = Math.cos(offset * Math.PI * (SECTION_COUNT - 1)) * amplitude
    g.scale.setScalar(1 + offset * 0.45)
    state.camera.position.z = 9 - offset * 2.5

    const t = state.clock.elapsedTime
    for (let i = 0; i < STEPS; i++) {
      const p = i / (STEPS - 1)
      const angle = p * Math.PI * 4 + t * 0.4
      const y = (p - 0.5) * 7
      const radius = 1.1 + Math.sin(t * 0.6 + p * 6) * 0.08
      const ax = Math.cos(angle) * radius
      const az = Math.sin(angle) * radius

      tmp.rotation.set(0, 0, 0)
      tmp.position.set(ax, y, az)
      tmp.scale.setScalar(0.11)
      tmp.updateMatrix()
      strandA.current?.setMatrixAt(i, tmp.matrix)

      tmp.position.set(-ax, y, -az)
      tmp.updateMatrix()
      strandB.current?.setMatrixAt(i, tmp.matrix)

      tmp.position.set(0, y, 0)
      tmp.rotation.set(0, -angle, Math.PI / 2)
      tmp.scale.set(0.03, radius * 2, 0.03)
      tmp.updateMatrix()
      rungs.current?.setMatrixAt(i, tmp.matrix)
    }
    for (const mesh of [strandA.current, strandB.current, rungs.current]) {
      if (mesh) mesh.instanceMatrix.needsUpdate = true
    }

    const section = Math.min(offset * (SECTION_COUNT - 1), PALETTE.length - 1.001)
    const from = Math.floor(section)
    tint.copy(PALETTE[from]).lerp(PALETTE[from + 1], section - from)
    for (const mesh of [strandA.current, strandB.current]) {
      ;(mesh?.material as { color: Color } | undefined)?.color.copy(tint)
    }
  })

  return (
    <group ref={group}>
      <instancedMesh ref={strandA} args={[undefined, undefined, STEPS]}>
        <sphereGeometry args={[1, 20, 20]} />
        <meshStandardMaterial roughness={0.3} metalness={0.2} />
      </instancedMesh>
      <instancedMesh ref={strandB} args={[undefined, undefined, STEPS]}>
        <sphereGeometry args={[1, 20, 20]} />
        <meshStandardMaterial roughness={0.3} metalness={0.2} />
      </instancedMesh>
      <instancedMesh ref={rungs} args={[undefined, undefined, STEPS]}>
        <cylinderGeometry args={[1, 1, 1, 8]} />
        <meshStandardMaterial color="#94a3b8" roughness={0.6} transparent opacity={0.55} />
      </instancedMesh>
    </group>
  )
}

/** 3B sahne; üstteki sayfa kaydırması `progress` ile (0..1) sarmalı döndürür. Metinler normal HTML'dir (Landing). */
export default function HeroScene({ progress }: { progress: RefObject<number> }) {
  return (
    <Canvas aria-hidden="true" camera={{ position: [0, 0, 9], fov: 45 }} dpr={[1, 1.75]} gl={{ antialias: true, powerPreference: 'high-performance' }}>
      <ambientLight intensity={0.7} />
      <directionalLight position={[4, 6, 5]} intensity={1.6} />
      <pointLight position={[-5, -2, 3]} intensity={12} color="#38bdf8" />
      <Helix progress={progress} />
    </Canvas>
  )
}
