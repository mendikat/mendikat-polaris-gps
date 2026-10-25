# Escenarios de aceptación — Mendikat TrailLab

Versión: 0.1 (Incremento 0)

Formato Given/When/Then. Cada escenario indica los requisitos que verifica y si se automatiza
(**[auto]**, prueba xUnit) o se comprueba manualmente (**[manual]**) mientras no haya infraestructura de pruebas de UI.

Fixtures: `tests/Mendikat.TrailLab.Tests/Fixtures/` (se crean en el incremento correspondiente).

---

## Documentos y ventana principal

**ACC-APP-01 — Varios documentos abiertos** (APP-03, MAP-09) [manual]
- Given la aplicación con `ruta-a.gpx` abierto
- When abro `ruta-b.gpx`
- Then hay dos pestañas, el mapa se ajusta a `ruta-b` y las capas de `ruta-a` siguen visibles e intactas.

**ACC-APP-02 — Cerrar con cambios y cancelar** (APP-04, SAVE-04) [manual + auto en ViewModel]
- Given un documento con un punto eliminado (pestaña con `*`)
- When elijo Archivo › Cerrar documento y pulso «Cancelar»
- Then el documento sigue abierto, conserva el cambio y el indicador `*`.

**ACC-APP-03 — Archivo corrupto** (APP-05, GPX-02) [auto + manual]
- Given un archivo `malformed.gpx` con XML truncado
- When lo abro
- Then se muestra «El archivo no es un XML válido (línea N)» y no se crea documento; la aplicación sigue operativa.

**ACC-APP-04 — Menú según estado** (APP-02) [auto en ViewModel]
- Given ningún documento abierto
- Then Guardar, Guardar como y Cerrar documento están deshabilitados; Abrir GPX/WPT y Salir habilitados.

**ACC-APP-05 — Crear un GPX a mano** (APP-08, SAVE-02, EDIT-05) [auto en ViewModel + manual]
- Given la aplicación abierta
- When elijo Archivo › Nuevo GPX y hago Ctrl+clic en tres lugares del mapa
- Then aparece la pestaña «Sin título 1.gpx *» con «Track 1» de 3 puntos unidos en el orden de los clics; Ctrl+Z quita el último;
  Guardar pide nombre y ruta, la pestaña pasa a mostrar el nombre elegido y el archivo se reabre con sus puntos.

## Cartografía

**ACC-MAP-01 — IGN por defecto y persistencia** (MAP-03, MAP-04) [auto (settings) + manual]
- Given no existe `settings.json`
- When arranco la aplicación
- Then la cartografía activa es «Mapa IGN (España)».
- When selecciono «OpenTopoMap», cierro y vuelvo a abrir
- Then la cartografía activa es «OpenTopoMap».

**ACC-MAP-02 — Ajuste a extensión** (MAP-09) [manual]
- Given un GPX cuyo bbox es (42,80; −1,70)–(42,90; −1,60)
- When lo abro
- Then el mapa muestra todo el bbox con margen y sin cambiar las capas existentes.

**ACC-MAP-03** — *Retirado* (MAP-05 retirado al eliminar Thunderforest).

**ACC-MAP-04 — Sin red** (MAP-07, NFR-05) [manual]
- Given el equipo sin conexión
- When abro un GPX y desplazo el mapa
- Then el track se dibuja sobre fondo vacío, la UI responde y la barra de estado indica que no se pudo cargar la cartografía.

## Coordenadas

**ACC-COORD-01 — Valores de referencia** (COORD-01, COORD-02, COORD-05) [auto]

GeoConvert es el propio GeographicLib que usa la aplicación, así que no sirve como referencia independiente.
Las referencias externas son el punto ecuador/Greenwich (Este 166 021,44 m, valor de manual) y el ejemplo de la
Torre CN del artículo «Universal Transverse Mercator» de Wikipedia. El resto comprueba zona, banda y formato.
Tolerancia UTM ±1 m (los metros se truncan, como en MGRS); DMS exacto a 0,01″.
Implementado en `tests/.../Mapping/CoordinateFormatterTests.cs` (incremento 2).

| Caso | Lat, Lon (WGS84) | Esperado |
|------|------------------|----------|
| Ecuador / meridiano | 0, 0 | `31N 166021 0` |
| Torre CN (Toronto) | 43°38′33,24″ N, 79°23′13,7″ W | `17T 630084 4833438` ±1 m |
| Pamplona | 42,8125 N, −1,6458 | Zona/banda `30T`; DMS `42º 48' 45,00'' N 1º 38' 44,88'' W` |
| Donostia | 43,3183 N, −1,9812 | `30T` |
| Huso 31 | 41,3851 N, 2,1734 E | `31T` |
| Canarias | 28,4636 N, −16,2518 | `28R` |
| Hemisferio sur | −33,8688, 151,2093 | `56H`, Norte entre 6 200 000 y 6 300 000 (falso norte 10 000 000); DMS `33º 52' 07,68'' S 151º 12' 33,48'' E` |

**ACC-COORD-02 — Zonas especiales** (COORD-03) [auto]
- Given lat 60 N, lon 5 E → Then zona `32V`.
- Given lat 78 N, lon 10 E → Then zona `33X`.
- Given lat 74 N, lon 8 E → Then zona `31X` (Svalbard: 0°–9° E).

**ACC-COORD-03 — Fuera de UTM** (COORD-04) [auto]
- Given lat 85 N, lon 0 → Then se muestra representación UPS (`UPS Z …`) y nunca una zona UTM.
- Letras UPS: `Y`/`Z` al norte (longitud oeste/este), `A`/`B` al sur.

**ACC-COORD-04 — Formato español** (COORD-02, NFR-07) [auto]
- Given segundos 23,114 → Then se muestra `23,11''` (coma decimal, 2 decimales, redondeo correcto incluyendo 59,999″ → incremento de minuto).

## GPX / WPT

**ACC-GPX-01 — Ida y vuelta** (GPX-03) [auto]
- Given `with-extensions.gpx` (Garmin TrackPointExtension, atributos desconocidos, metadatos)
- When lo cargo y lo guardo sin cambios en otro archivo y lo recargo
- Then todos los puntos, campos estándar y extensiones son equivalentes (comparación XML canónica de extensiones).

**ACC-GPX-02 — Datos ausentes** (GPX-05) [auto]
- Given `missing-data.gpx` con puntos sin `ele` ni `time`
- Then el modelo los representa como nulos y al guardar no se escriben `<ele>0</ele>` inventados.

**ACC-GPX-03 — Coordenadas fuera de rango** (GPX-02) [auto]
- Given un `trkpt lat="95"` → Then error «Latitud fuera de rango (95) en el punto N».

**ACC-WPT-01 — OziExplorer** (WPT-01, WPT-03, WPT-04) [auto]
- Given `ozi-sample.wpt` (WGS 84, 3 waypoints, uno con altitud −777)
- When lo cargo
- Then hay 3 waypoints, la altitud del tercero es nula y las demás están en metros (pies × 0,3048).
- When lo guardo sin cambios
- Then cada línea de waypoint es idéntica a la original.

**ACC-WPT-02 — Editar y guardar** (WPE-02, WPE-03, WPE-04) [auto + manual]
- Given un waypoint seleccionado
- When cambio su nombre y su latitud a 43,5 y guardo
- Then al reabrir el waypoint tiene el nuevo nombre y latitud; el resto de campos de la línea no cambia.
- When intento poner latitud 91 → Then se rechaza con mensaje y no se aplica.

## Edición

**ACC-EDIT-01 — Supr** (EDIT-01, EDIT-05) [auto en ViewModel/servicio]
- Given el punto 5 de 10 seleccionado
- When pulso Supr → Then quedan 9 puntos, se selecciona el que ocupaba la posición 6, el documento se marca modificado.
- When pulso Ctrl+Z → Then vuelven a ser 10 y el punto 5 es idéntico al original.

**ACC-EDIT-02 — Insert** (EDIT-03) [auto]
- Given el punto i seleccionado con siguiente i+1 en el mismo segmento
- When pulso Insert → Then se inserta en posición i+1 el punto medio geodésico, marcado como «añadido», con elevación/tiempo interpolados si ambos existen.
- Given el último punto del segmento seleccionado → Then se inserta entre el penúltimo y el último.

**ACC-EDIT-03 — Sin selección** (EDIT-06) [auto]
- Given nada seleccionado → When pulso Supr → Then aviso «Selecciona un punto o track» y el documento no cambia.

## Perfil

**ACC-PROF-01 — Distancia acumulada** (PROF-02) [auto]
- Given dos puntos (0,0) y (0,1) → Then distancia = 111 319,491 m ± 0,001 m (Karney WGS84).

**ACC-PROF-02 — Sincronización** (PROF-04, LAY-03) [manual]
- Given perfil abierto de un track
- When muevo el puntero a 3,2 km en el perfil → Then se resalta en el mapa el punto más cercano a 3,2 km.
- When hago clic en un punto del mapa → Then el cursor del perfil se coloca en su distancia y la lista lo selecciona.

## Herramientas

**ACC-SIMP-01 — Extremos por segmento** (SIMP-01, SIMP-02) [auto]
- Given un track con 2 segmentos de 100 puntos casi colineales (ruido 1 m) y tolerancia 5 m
- Then cada segmento conserva su primer y último punto y queda con 2 puntos; el track sigue teniendo 2 segmentos.

**ACC-TIME-01 — Coherencia velocidad/distancia** (TIME-02, TIME-05) [auto]
- Given un track de 3 puntos separados 500 m y 1 000 m, inicio 2026-03-29 01:30 Europe/Madrid, 6 km/h
- Then los tiempos son 00:30:00Z, 00:35:00Z y 00:45:00Z (UTC, cruzando cambio de horario sin error).

**ACC-DEM-01 — Vista previa y confirmación** (DEM-02, DEM-03, DEM-04) [auto con doble de fuente]
- Given 10 puntos, la fuente devuelve dato para 8
- Then la vista previa indica «8 se modificarán, 2 sin dato»; sin confirmar no hay cambios; tras aplicar, los 2 sin dato conservan su elevación original; Ctrl+Z restaura todas.

## No funcionales

**ACC-NFR-01 — Sin internet** (NFR-05) [manual]
- Given sin conexión → When abro, edito y guardo un GPX y un WPT → Then todo funciona; sólo el fondo cartográfico falta.

**ACC-NFR-02 — Archivo grande** (NFR-01, NFR-09) [manual + auto de rendimiento]
- Given un GPX de 100 000 puntos → When lo abro → Then la UI responde durante la carga (progreso visible) y el desplazamiento del mapa es fluido.
