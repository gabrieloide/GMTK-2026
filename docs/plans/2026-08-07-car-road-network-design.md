# Red de calles para carros de tráfico

## Problema

Los carros de tráfico (`CarObstacle`) hoy solo avanzan en línea recta desde un spawn point
hasta que expira su `lifetime`. Se ven artificiales: no giran, no recorren el mapa, y no
usan las calles existentes de forma creíble.

## Objetivo

Que los carros entren por un spawn (oculto dentro de una entrada/túnel), recorran el mapa
girando por las calles con naturalidad, y eventualmente salgan por cualquier spawn (no
necesariamente el mismo por el que entraron). Las calles son de doble sentido: dos carros
pueden cruzarse en la misma calle sin superponerse, cada uno en su carril.

Se necesita además una herramienta visual en el editor para dibujar esa red de calles
(nodos + conexiones), sin tener que armar listas a mano en el Inspector.

## Modelo de datos

**`RoadNetwork`** — un componente en un único GameObject de la escena (ej. "RoadNetwork").
Es dueño de todos los nodos (sus hijos), dibuja los gizmos, y expone:
- `GetRandomSpawnNode()`
- `GetRandomNeighbor(RoadNode current, RoadNode cameFrom)` — vecino al azar, evitando
  volver por donde vino si hay otra opción.
- `ShortestPathToNearestSpawn(RoadNode from)` — Dijkstra simple sobre el grafo (pesos =
  distancia euclidiana entre nodos), usado cuando un carro decide irse.
- `laneOffset` (float, default ~1.2) — separación entre los dos carriles de una calle.

**`RoadNode`** — componente en cada GameObject-nodo, hijo de `RoadNetwork`.
- `isSpawnPoint: bool`
- `connections: List<RoadNode>` — conexión simétrica (conectar A↔B agrega B a la lista de
  A y A a la lista de B).
- `Connect(RoadNode other)` / `Disconnect(RoadNode other)`.

**Carril por sentido**: cuando un carro viaja de A a B, su punto objetivo no es la línea
A–B sino un punto desplazado perpendicularmente hacia su derecha (`Vector3.Cross(Vector3.up,
dirAB).normalized * laneOffset`). Un carro que viaja de B a A usa el desplazamiento opuesto
(su propia derecha respecto a `dirBA`). Resultado: dos carriles reales sin cruce, como una
calle de dos manos.

## Comportamiento del carro (`CarObstacle`)

- Guarda `fromNode` / `toNode` (arista actual) y avanza hacia el punto de destino con
  carril desplazado.
- Gira suavemente: rota su `forward` hacia la dirección del siguiente punto
  (`Quaternion.RotateTowards`, no salto instantáneo) y avanza por su propio forward, igual
  que hace un auto real entrando a una curva.
- Al llegar a un nodo (radio pequeño):
  - Si le quedan "saltos" de paseo, elige un vecino al azar (evitando el nodo del que
    viene si hay alternativa).
  - Si se le acabaron los saltos, pide a `RoadNetwork` el camino más corto al spawn más
    cercano y lo sigue nodo por nodo hasta llegar — ahí se destruye.
- `wanderHops` inicial: aleatorio entre un mínimo y máximo configurable (ej. 4–10).

## `CarSpawner`

Deja de usar su propio array `spawnPoints`. En su lugar referencia el `RoadNetwork` y:
- Elige un nodo con `isSpawnPoint = true` al azar para instanciar el carro.
- El carro nace orientado hacia el primer vecino que va a tomar (no hacia la rotación del
  nodo).
- Pasa la referencia del `RoadNetwork` y el nodo inicial a `CarObstacle.Init(...)`.

## Herramienta de editor (`RoadNetworkEditor`)

- **Gizmos siempre visibles** (`OnDrawGizmos` en `RoadNetwork`, sin selección necesaria):
  esfera naranja en nodos spawn, esfera cian en nodos normales; cada conexión dibujada
  como dos líneas finas desplazadas (un carril por sentido) con una flecha marcando el
  sentido de cada una.
- **Modo edición** (activo cuando `RoadNetwork` está seleccionado en la jerarquía, vía
  `OnSceneGUI` en el editor custom):
  - `Ctrl+Click` sobre el suelo → raycast y crea un nodo nuevo ahí, como hijo de
    `RoadNetwork`.
  - `Click` sobre un nodo → lo selecciona (se resalta en amarillo). `Click` sobre un
    segundo nodo → conecta/desconecta esa calle entre ambos.
  - `Shift+Click` sobre un nodo → alterna su flag `isSpawnPoint` (cian ↔ naranja).
  - Mover un nodo usa el gizmo normal de mover de Unity (son GameObjects comunes).
  - Botón en el Inspector para eliminar el nodo seleccionado (limpia sus conexiones).

## Fuera de alcance (por ahora)

- Detección de colisión/carril entre carros de tráfico entre sí (ya existe
  `CarObstacle`/`Rigidbody` kinemático; no se agregan reglas de prioridad en
  intersecciones).
- Pathfinding influenciado por tráfico/densidad — el camino al spawn más cercano usa solo
  distancia geométrica.
