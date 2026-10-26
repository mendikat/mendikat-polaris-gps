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
- Las herramientas de los incrementos 6 y 7 guardan referencias a los puntos afectados, no copias. Revisado en el
  incremento 8 con 500 000 puntos: `RemovePointsCommand` recorre la lista una sola vez al hacer y al deshacer (antes
  borraba e insertaba punto a punto: 11 s por operación; ahora milisegundos).

## Implementación (incremento 6, 2026-10-25)

- `Domain/Processing/TrackSimplifier`: Transverse Mercator **exacta** de GeographicLib (`k0 = 1`) centrada en la longitud
  media del segmento (longitudes desenrolladas respecto al primer punto para segmentos que cruzan ±180°). La distancia
  usada es **punto–segmento** (no a la recta infinita), de modo que una vuelta cerrada (inicio = fin) no se colapsa.
  Douglas–Peucker iterativo con pila explícita (probado con 1 000 000 de puntos). El tramo «sub-meridiano» previsto para
  segmentos muy anchos no se ha implementado: la escala de la TM exacta a 250 km del meridiano es 1,0008 (error < 0,1 %).
- `Domain/Processing/TimeRegenerator`: la hora local se convierte a UTC una vez con `TimeZoneInfo`; una hora
  **inexistente** (salto de horario de verano) se rechaza; una hora **ambigua** se toma como hora estándar y se avisa
  en la vista previa. Tiempos redondeados al milisegundo. `SetTimesCommand` guarda las horas previas (incluida «sin hora»).
- Herramientas aplicables a tracks y rutas (no a waypoints), desde el menú «Herramientas», con diálogo modal de vista
  previa; nada cambia hasta pulsar «Aplicar» y el resultado es un único paso de deshacer.

## Estadísticas del perfil (2026-10-25, a petición del responsable)

- **Desnivel positivo/negativo** (PROF-06): histéresis de 5 m (`ProfileStatistics.ElevationHysteresis`). Sólo se
  suma un cambio cuando la elevación se ha alejado al menos 5 m del último nivel contado. Sin filtro, el ruido del GPS
  infla mucho el desnivel (oscilaciones de ±3 m sumarían 6 m por oscilación). Por segmento; elevaciones ausentes omitidas.
- **Tiempo** (PROF-07): total = último − primer punto con hora. En movimiento = suma de intervalos dentro de un segmento
  con velocidad horizontal ≥ 1 km/h (`ProfileStatistics.StoppedSpeed`). Detenido = total − en movimiento (incluye
  tramos lentos, pausas entre segmentos e intervalos con horas desordenadas).
- Ambos umbrales son constantes documentadas; si hiciera falta, pueden pasar a ser opciones del usuario.

## Revisión con tracks enormes (incremento 8, 2026-10-26)

Medido con un GPX sintético de 500 000 puntos (49 MB):

- La proyección Transverse Mercator era ~2/3 del tiempo de cada vista previa y no depende de la tolerancia.
  `TrackSimplifier.Prepare` proyecta una vez por diálogo y `PreparedSimplification.Plan` prueba cada tolerancia
  (~150 ms en lugar de ~440 ms). `TrackSimplifier.Plan` se mantiene como atajo.
- Por encima de 20 000 puntos, el diálogo calcula en segundo plano tras 300 ms sin teclear; una tolerancia nueva o
  cerrar el diálogo cancela el cálculo anterior (Douglas–Peucker comprueba la cancelación cada 4 096 pasos).
- Aplicar y deshacer: ver la revisión de `RemovePointsCommand` arriba.
