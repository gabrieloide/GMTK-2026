# Paleta de color — GMTK 2026

Paleta extraída de los 127 materiales de `Assets/Materials/`, deduplicada y simplificada
por distancia perceptual **CIEDE2000** (ΔE en espacio CIE-Lab, D65).

## Archivos

| Archivo | Contenido | Uso |
|---|---|---|
| `GMTK2026-24.gpl` | 24 colores, la paleta de trabajo | Aseprite / GIMP / Krita |
| `GMTK2026-24.pal` | los mismos 24, formato JASC-PAL | Aseprite, Paint Shop Pro |
| `GMTK2026-24.png` | tira de 24×1 px | Aseprite: *Sprite → Save palette from sprite* |
| `GMTK2026-full-58.gpl` | los 58 colores únicos del proyecto | referencia / no perder nada |
| `GMTK2026-full-58.png` | tira de 58×1 px | idem |

### Importar en Aseprite

Paleta → menú de opciones (☰) → **Load Palette…** → elegir `GMTK2026-24.gpl`.
Para dejarla como paleta por defecto: **Save as Default Palette** después de cargarla.

## De 127 materiales a 24 colores

1. **127 materiales → 58 colores únicos.** `Materials/pallete/` y `Materials/pallete/metal/`
   son la misma paleta duplicada (38 pares con el mismo hex, distinto acabado metálico),
   más 8 duplicados internos y 9 materiales sin tintar (blanco puro).
2. **58 → 24.** Se fusionaron los colores separados por ΔE2000 < ~10, es decir, los que el
   ojo apenas distingue lado a lado, y se descartaron entradas sin uso en `SampleScene`
   que caían dentro de una rampa ya cubierta.

Los 24 finales tienen una separación mínima de **ΔE2000 = 9.17** (entre `#CE9248` y
`#F3A833`), así que no queda ningún par ambiguo.

### Fusiones más evidentes

| Se fusionaron | ΔE2000 | Superviviente |
|---|---|---|
| `#8E8E8E` + `#8C8C8C` | 0.69 | `#8E8E8E` (prácticamente invisible) |
| `#FDD63F` + `#FFD940` + `#FFD91A` | 0.7 – 2.4 | `#FDD63F` |
| `#3859B3` + `#015BC0` | 3.99 | `#015BC0` |
| `#FFFFFF` + `#E9E9E9` + `#F6E8E0` | 4.5 – 7.7 | `#FFFFFF` |
| `#905847` + `#94493A` | 5.15 | `#94493A` |
| `#3AD66C` + `#5AB552` | 8.98 | `#5AB552` |
| `#62A477` + `#4E9380` | 9.60 | `#4E9380` |

## La paleta

### Neutros
| Hex | Nombre |
|---|---|
| `#10121C` | Negro azulado — outlines y sombras |
| `#4D3533` | Marrón oscuro |
| `#78727D` | Gris violáceo — asfalto |
| `#BEBEBE` | Gris claro |
| `#FFFFFF` | Blanco |

### Rojos
`#6B2643` vino oscuro · `#AC2847` rojo vino · `#EC273F` rojo vivo

### Tierra y naranjas
`#94493A` terracota · `#DE5D3A` naranja teja

### Amarillos
`#CE9248` ocre · `#F3A833` ámbar · `#FDD63F` amarillo

### Verdes
`#26854C` bosque · `#5AB552` hierba · `#4E9380` verde azulado · `#D3EED3` verde pálido

### Azules
`#182E46` azul noche · `#015BC0` azul fuerte · `#3388DE` azul medio · `#36C5F4` celeste

### Morados y rosas
`#9A4D76` morado · `#C878AF` lila · `#FFA2AC` rosa

## Nota sobre los colores de gameplay

Estos **no** están en los 24 y conviene que sigan fuera, para que no se confundan con la
ciudad:

| Color | Material | ΔE2000 al vecino más cercano de la paleta |
|---|---|---|
| `#26E64C` | `OrderPickupMarker` | 6.1 de `#3AD66C`, 9.4 de `#9DE64E` |
| `#F24026` | `OrderDropoffMarker` | 5.1 de `#DE5D3A` ⚠️ |
| `#FFD91A` | `DeliveryArrow` | 2.1 de `#FFD940` ⚠️ |

⚠️ El rojo del *dropoff* está a ΔE 5.1 del naranja teja de los edificios (`#DE5D3A`, el
2º color más usado de la escena) y la flecha amarilla queda muy cerca de los amarillos de
fachada. Si en juego cuesta distinguir los marcadores, ahí está el motivo: conviene
alejarlos en tono o saturación, no solo en brillo.

## Nota técnica

Unity guarda estos materiales en **espacio gamma** (los valores del `.mat` son exactamente
k/255, p. ej. `0.82745105` = 211/255), así que el hex de estos archivos es el mismo que
muestra el color picker del inspector. El proyecto renderiza en Linear color space, pero
eso solo afecta la conversión en GPU, no el valor serializado.
