# ADR-0008 — Algoritmos de procesamiento (distancia, simplificación, tiempos, edición)

- Estado: **Aceptada** (se revisará al implementar los incrementos 5–6)
- Fecha: 2026-10-24

## Distancia acumulada (PROF-02, TIME-02)

- Distancia **horizontal geodésica** entre puntos consecutivos con `Geodesic.WGS84.Inverse` (Karney, GeographicLib). No se incluye la componente vertical (criterio habitual en GPS y comparable con otras herramientas). Puntos duplicados → distancia 0.
- Entre segmentos (`trkseg`) **no se suma** el hueco por defecto en el perfil. En la regeneración de tiempos sí se recorre (configurable, TIME-04).

## Simplificación (SIMP-*)

- **Douglas–Peucker iterativo** (pila explícita, sin recursión) por segmento.
- Distancia perpendicular calculada en una **proyección Transverse Mercator local** (`TransverseMercator` de GeographicLib, meridiano central = longitud media del segmento, factor de escala 1). Error de escala < 0,1 % para extensiones < ~250 km a cada lado del meridiano; segmentos más anchos se tratan con meridiano por sub-tramo (documentado en el código).
- Extremos de cada segmento siempre conservados; segmentos ≤ 2 puntos intactos; puntos conservados sin modificar (tiempo, elevación, extensiones). Se avisa de que los puntos eliminados pierden sus extensiones.
- Tolerancia en metros (por defecto 5 m), vista previa del recuento resultante.

## Inserción de puntos (EDIT-03)

- Punto medio geodésico (`Inverse` + `Direct` a la mitad de la distancia). Elevación y tiempo interpolados linealmente sólo si existen en ambos vecinos.

## Regeneración de tiempos (TIME-*)

- Modo por distancia: `t_i = t_0 + d_i / v`. Modo uniforme: `t_i = t_0 + i·Δt`.
- Hora inicial introducida como hora local + zona IANA/Windows explícita → convertida a UTC una sola vez (`TimeZoneInfo`), con aviso si la hora es ambigua o inexistente por cambio de horario.
- Escritura en UTC ISO 8601 `Z`. Precisión de segundos (fracción opcional si v alta).

## Deshacer

- Patrón comando por documento (`IEditCommand { Do; Undo }`) con pilas deshacer/rehacer en `TrailDocument` (incremento 3).
  Comandos: `RemovePointsCommand`, `InsertPointCommand`, `RemoveItemCommand<T>`, `EditPointCommand` y
  `CompositeCommand` (borrar puntos de varios segmentos es un único paso). Los comandos guardan referencias a los
  objetos originales, así que deshacer restaura exactamente los mismos puntos con todos sus campos.
- Cada estado del contenido tiene un identificador; el documento está modificado si el estado actual no es el
  guardado, de modo que deshacer hasta el estado guardado quita el indicador `*` (SAVE-04).
- Las herramientas de los incrementos 6 y 7 podrán guardar una instantánea de los segmentos afectados (se revisará
  la memoria con tracks enormes en el incremento 8).
