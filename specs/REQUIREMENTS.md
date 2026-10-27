# Requisitos — Mendikat TrailLab

Versión: 0.1 (Incremento 0) · Estado: borrador pendiente de aprobación

Convenciones:
- Identificadores estables `ÁREA-NN`. No se reutilizan ni renumeran; si un requisito se retira se marca *Retirado*.
- **Inc** = incremento previsto (ver [ROADMAP.md](ROADMAP.md)). **Ver** = versión del requisito.
- Los criterios de aceptación (CA) son comprobables; los escenarios detallados están en [ACCEPTANCE.md](ACCEPTANCE.md).
- Trazabilidad: cada prueba automatizada lleva el atributo `[Trait("Req", "<Id>")]`.

---

## APP — Ventana principal y documentos

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| APP-01 | 1 | 1 | La ventana principal muestra título «Mendikat TrailLab», menú superior clásico, área de documentos y barra de estado. | Al arrancar se ven los cuatro elementos; el título incluye el documento activo y `*` si tiene cambios. |
| APP-02 | 2 | 1 | Menú **Archivo**: Nuevo GPX, Abrir GPX… (sólo GPX desde 2026-10-26), Guardar, Guardar como…, Cerrar documento, Salir. | Guardar/Guardar como/Cerrar están deshabilitados sin documento activo; Guardar está deshabilitado si el documento activo no tiene cambios. |
| APP-03 | 1 | 1 | Varios documentos abiertos simultáneamente, en pestañas dentro de la ventana principal (ADR-0002). Cada documento conserva su estado, ruta y formato. | Abrir un segundo archivo no cierra ni modifica el primero; cambiar de pestaña restaura su vista. |
| APP-04 | 1 | 1 | Cerrar un documento con cambios (o salir) pregunta Guardar / No guardar / Cancelar. | Cancelar conserva el documento y sus cambios; No guardar descarta; Guardar guarda y cierra (si falla, no cierra). |
| APP-05 | 1 | 1 | Los errores de archivo y cartografía se muestran con mensajes comprensibles en español sin bloquear ni cerrar la aplicación. | Abrir un archivo corrupto muestra un mensaje con el motivo; la aplicación sigue operativa. |
| APP-06 | 1 | 2 | Controles visibles: zoom +/−, ajustar a datos, selector de cartografía y botón de perfil de elevación. | Los controles están en una barra de herramientas; el de perfil sólo se habilita con un track seleccionado. |
| APP-07 | 1 | 1 | Un mismo archivo no se abre dos veces: si ya está abierto, se activa su pestaña. | Abrir dos veces la misma ruta activa el documento existente. |
| APP-08 | 1 | — | **Archivo › Nuevo GPX** (Ctrl+N) abre un documento GPX sin archivo, «Sin título N.gpx», con un track vacío «Track 1» seleccionado. **Control + clic** en el mapa añade un punto (sin elevación ni tiempo, marcado como añadido) justo tras el punto seleccionado o, si no lo hay, al final del track, ruta o waypoints seleccionado; sin capa seleccionada crea un track. El punto nuevo queda seleccionado. Guardar un documento nuevo pide la ruta como «Guardar como». | El documento nuevo no tiene cambios (cerrarlo no pregunta); añadir un punto marca `*` y se deshace con Ctrl+Z; cancelar la ruta al guardar (o al cerrar eligiendo Guardar) conserva el documento; el GPX escrito valida contra el XSD 1.1. |
| APP-09 | 1 | — | Menú **Ayuda**: «Ayuda de Mendikat TrailLab» (F1) abre una guía HTML/CSS incluida con la aplicación (`Help/ayuda.html`, funciona sin conexión) en el navegador; «¡Dona ahora!» abre el botón de donación de PayPal de Mendikat (`hosted_button_id` 4BNLU9ACSHTFN, el mismo que la web); «Web de Mendikat» abre https://www.mendikat.net; «Buscar actualizaciones…» (menú y botón de «Acerca de») abre de momento la misma web, hasta tener la dirección definitiva (`AboutViewModel.UpdatesUrl`); «Acerca de…» muestra nombre, versión (de `Directory.Build.props`, ahora 0.0.3), autor Javier Urrutia, correo javi@mendikat.net y la web https://www.mendikat.net, con enlaces. | F1 abre la guía; el donativo abre PayPal; Acerca de muestra «Versión 0.0.3»; la guía contiene el formulario de PayPal y sus enlaces internos funcionan. |
| APP-10 | 1 | — | Soltar archivos sobre cualquier parte de la ventana los abre, varios a la vez y en orden: GPX, KML, KMZ y TCX como documentos (CONV-01/02), `.map`, `.imp`, `.asc` y `.hgt` como cartografía de usuario (LMAP-01). Mientras se arrastra, el cursor indica si hay algún archivo que se pueda abrir; carpetas y archivos de otros tipos se omiten y se cuentan en la barra de estado. No se acepta mientras hay otra carga en curso. | Soltar un GPX, un `.asc` y un KML → dos documentos y un MDT; soltar una foto y una carpeta → «2 archivos soltados no se han abierto». |

## MAP — Cartografía

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| MAP-01 | 1 | 2 | Mapa interactivo (arrastrar, rueda para zoom) con tracks, puntos y waypoints. | Los datos se dibujan sobre los mosaicos; arrastrar y hacer zoom no bloquea la UI. |
| MAP-02 | 1 | 2 | Proveedores definidos en un registro central (`MapProviderCatalog`), no en la UI. Lista inicial y estado en ADR-0004. | Ninguna URL de mosaicos aparece fuera del catálogo (comprobado por prueba que analiza el catálogo y revisión de código). |
| MAP-03 | 1 | 2 | Cartografía predeterminada: «Mapa IGN (España)». | Primer arranque (sin configuración) muestra IGN. |
| MAP-04 | 1 | 2 | La cartografía elegida se recuerda entre sesiones en la configuración de usuario (`%APPDATA%\Mendikat\TrailLab\settings.json`). | Elegir OpenTopoMap, reiniciar → OpenTopoMap. Si la guardada ya no es válida → IGN. |
| MAP-05 | 2 | — | *Retirado (2026-10-24)*: se eliminó Thunderforest, único proveedor con API key (ADR-0004). | — |
| MAP-06 | 1 | 2 | Cada proveedor muestra su atribución obligatoria sobre el mapa. | La atribución cambia al cambiar de proveedor. |
| MAP-07 | 1 | 2 | Errores de red, mosaicos ausentes y tiempos de espera no congelan la UI; se indica el problema en la barra de estado. | Sin red, el mapa sigue respondiendo y muestra «No se pudo cargar la cartografía»; los datos GPX se siguen dibujando. |
| MAP-08 | 1 | 2 | Las geometrías se transforman WGS84 → Web Mercator (EPSG:3857) sólo para dibujar. Mediciones nunca en Web Mercator. | Prueba: punto conocido proyectado a EPSG:3857 con tolerancia 1 cm; revisión de que distancias usan geodesia. |
| MAP-09 | 1 | 2 | Al abrir un archivo el mapa se ajusta a la extensión de sus datos sin borrar capas de otros documentos. | — ver ACC-MAP-02. |
| MAP-10 | 1 | 2 | Las peticiones de mosaicos envían un `User-Agent` identificativo de la aplicación (exigido por la política de OSM). | Prueba unitaria del cliente HTTP con doble de red. |

## COORD — Barra de estado y coordenadas

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| COORD-01 | 1 | 2 | Al mover el puntero sobre el mapa se muestran coordenadas UTM WGS84 con zona y banda: `30T 555555 4750000` (Este Norte, metros, enteros). | Valores de referencia de ACC-COORD-01 con tolerancia ±1 m. |
| COORD-02 | 1 | 2 | Se muestran coordenadas geográficas WGS84 en DMS con hemisferio, latitud primero: `43º 22' 23,11'' N 1º 45' 10,00'' W` (decisión D5). | Segundos con 2 decimales y coma decimal; hemisferio N/S y E/W según signo. |
| COORD-03 | 1 | 2 | Zona UTM calculada automáticamente incluyendo excepciones de Noruega (32V) y Svalbard (31X/33X/35X/37X). | Pruebas en 60° N 5° E → 32V; 78° N 10° E → 33X. |
| COORD-04 | 1 | 2 | Fuera de cobertura UTM (lat > 84° N o < 80° S) se muestra «UPS» con sus coordenadas, o «Fuera de UTM» si UPS no aplica. | Prueba en 85° N. |
| COORD-05 | 1 | 2 | Conversiones con GeographicLib.NET (Karney) sobre WGS84 (ADR-0003). Ninguna etiqueta «WGS84» sobre valores de otro datum. | Prueba con valores de referencia de GeoConvert. |
| COORD-06 | 1 | 2 | La actualización de coordenadas es fluida (limitada a la frecuencia de refresco) y no bloquea el mapa. | El cálculo por evento de ratón < 1 ms (prueba de rendimiento orientativa). |
| COORD-07 | 1 | — | La barra de estado muestra el datum de las coordenadas del archivo activo (WGS84 en GPX, KML y TCX; WGS84 sin archivos, para el mapa). Las coordenadas mostradas están siempre en ese datum (COORD-05). | «WGS84» visible con y sin documentos. |
| COORD-08 | 1 | — | Al detener el puntero (~0,25 s) se muestra la altitud bajo él: la del punto del archivo si hay uno visible con elevación bajo el puntero («Alt. 1.234 m (archivo)»); si no, la del terreno según el MDT del IGN 5 m («Alt. 846 m (MDT IGN 5 m)»), sólo España y con conexión. Sin dato, fuera del mapa o sin conexión: «—», sin errores ni bloqueos; tras un fallo del servicio no se vuelve a consultar durante 1 min. Las teselas del MDT descargadas se reutilizan (caché en memoria de la sesión, compartida con *Ajustar altitudes*). La información sobre herramientas indica el origen y su referencia vertical (DEM-05). | Punto del archivo → su elevación sin consultar el MDT; fuera de puntos → MDT; mover el puntero cancela la consulta pendiente; sin red → «—». |

## GPX — Lectura y escritura GPX

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| GPX-01 | 1 | 2 | Abrir GPX 1.1 y 1.0 con filtro `*.gpx`: tracks, segmentos, puntos, rutas y waypoints. | Fixtures válidos se cargan con los recuentos esperados. |
| GPX-02 | 1 | 2 | Detectar y rechazar con mensaje claro: archivo vacío, XML inválido, raíz distinta de `gpx`, lat/lon ausentes o fuera de rango. | Cada fixture inválido produce un error tipificado y un mensaje en español; no se abre documento parcial salvo aviso explícito. |
| GPX-03 | 1 | 4 | Preservar al guardar: metadatos, campos estándar de puntos (`ele`, `time`, `name`, `cmt`, `desc`, `sym`, `type`, `fix`, `sat`, `hdop`…), atributos y elementos desconocidos y bloques `extensions` (como XML literal). | Prueba ida y vuelta: abrir → guardar → reabrir conserva datos; los XML de extensiones son equivalentes (comparación canónica). |
| GPX-04 | 1 | 4 | Guardar siempre GPX 1.1 válido. Si el origen era GPX 1.0 se avisa antes de guardar de que se convertirá a 1.1. | Validación contra XSD de GPX 1.1 en pruebas. |
| GPX-05 | 1 | 2 | Elevación y tiempo son opcionales; su ausencia se representa como «sin dato», nunca como 0. | Fixture sin `ele` → `Elevation == null`. |
| GPX-06 | 1 | 2 | Los `time` se leen como UTC (`DateTimeOffset`) preservando el instante; si el texto original no puede reproducirse exactamente se conserva el instante. | Prueba con `Z`, con offset y sin zona (se avisa y se trata como UTC). |

## WPT — Archivos de waypoints

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| WPT-01 | 1 | 2 | *Retirado (2026-10-26)*: sólo GPX. Antes: Lector/escritor de **OziExplorer Waypoint File v1.1** (ADR-0006), diseñado tras una interfaz `IWaypointFileFormat` con detección por contenido para añadir dialectos. | Fixture OziExplorer se carga con nombre, lat, lon, alt, fecha, descripción. |
| WPT-06 | 2 | 2 | *Retirado (2026-10-26)*: sólo GPX. Antes: Lector/escritor de **CompeGPS/TwoNav WPT** (líneas `G`, `U`, `W`, `w`), datum WGS 84, sólo coordenadas geográficas (`U 1`); `U 0` (UTM) se rechaza con mensaje claro. Altitud en metros. Líneas de tipo desconocido se conservan. | Fixture CompeGPS geográfico se carga; fixture UTM → error comprensible. |
| WPT-07 | 1 | 2 | *Retirado (2026-10-26)*: sólo GPX. Antes: La detección de dialecto es por contenido, no por extensión; un `.wpt` no reconocido produce un error que indica que el dialecto no está soportado. | Fixture desconocido → error claro. |
| WPT-02 | 1 | 2 | *Retirado (2026-10-26)*: sólo GPX. Antes: Sólo datum «WGS 84» en v1; otro datum → error claro indicando el datum encontrado (no se convierte en silencio). | Fixture con «European 1950» → error comprensible. |
| WPT-03 | 1 | 2 | *Retirado (2026-10-26)*: sólo GPX. Antes: Altitud OziExplorer en pies se convierte a metros para el modelo; `-777` = sin altitud. Al guardar se reconvierte; si el valor no se editó, se escribe el texto original. | Ida y vuelta conserva el texto original de altitud no editada. |
| WPT-04 | 1 | 4 | *Retirado (2026-10-26)*: sólo GPX. Antes: Los campos no editables (símbolo, colores, formato, proximidad…) se conservan textualmente al guardar. | Ida y vuelta byte a byte de líneas no modificadas (salvo fin de línea normalizado CRLF). |
| WPT-05 | 2 | 2 | *Retirado (2026-10-26)*: sólo GPX. Antes: Codificación detectada (UTF-8 válido con no-ASCII → UTF-8; si no, Windows-1252) y conservada. OziExplorer: el carácter 209 no se convierte al leer (es «Ñ» en Windows-1252); al escribir textos editados, las comas se sustituyen por el carácter 209 (ADR-0006). | Fixture con «Ñ» se lee como «Ñ»; fixtures ANSI y UTF-8 se leen correctamente. |

## LAY — Capas y selección

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| LAY-01 | 2 | 3 | Panel de capas en la pestaña de cada documento (ADR-0002) con sus tracks, rutas y waypoints: nombre, nº de puntos y longitud, casilla de visibilidad y selección. | Ocultar un track lo quita del mapa sin modificar el documento (visibilidad no es un cambio). |
| LAY-02 | 1 | 2 | Símbolos diferenciables: línea de track, punto de track, punto seleccionado, waypoint y punto añadido. | Leyenda/estilos definidos en un único lugar (`MapStyles`). |
| LAY-03 | 1 | 3 | Seleccionar un track y sus puntos desde la lista o desde el mapa (clic con tolerancia en píxeles). | Clic sobre un punto lo selecciona en la lista; selección en lista resalta en mapa. |
| LAY-04 | 1 | 3 | Panel de detalle del punto seleccionado: lat, lon, elevación, fecha/hora (local y UTC), nombre y otros campos del formato. | Campos ausentes se muestran como «—». |

## EDIT — Edición de puntos

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| EDIT-01 | 1 | 3 | **Supr** (o menú contextual «Eliminar») elimina el elemento seleccionado: punto(s) seleccionados; o el track completo si la selección es el track. | Eliminar un punto reduce el recuento en 1 y redibuja. |
| EDIT-02 | 1 | 3 | Confirmación sólo al eliminar un track completo o > 50 puntos a la vez. | Eliminar 1 punto no pide confirmación; eliminar un track sí. |
| EDIT-03 | 1 | 3 | **Insert** (o menú contextual «Insertar punto») inserta un punto en el punto medio geodésico entre el punto seleccionado y el siguiente del mismo segmento; si es el último, entre el anterior y él. Elevación interpolada linealmente si ambos la tienen; tiempo interpolado si ambos lo tienen; si no, sin dato. Marcado como «añadido». | Prueba del punto medio con tolerancia 1 cm; orden respetado. |
| EDIT-04 | 2 | 3 | Mover un punto seleccionado arrastrándolo en el mapa (sólo el punto ya seleccionado; el arrastre empieza tras 4 px para evitar movimientos accidentales) o editando lat/lon en el panel de detalle (validados). | Lat fuera de [−90, 90] o lon fuera de [−180, 180] se rechaza con mensaje; mover se deshace con Ctrl+Z. |
| EDIT-05 | 1 | 3 | Deshacer/Rehacer (Ctrl+Z / Ctrl+Y) por documento para todas las operaciones de edición, incluidas herramientas (simplificación, tiempos, DEM). | Eliminar → Deshacer restaura el punto en la misma posición con todos sus campos. |
| EDIT-06 | 1 | 3 | Con selección vacía, Supr/Insert muestran un aviso breve en la barra de estado y no hacen nada. | — |
| EDIT-07 | 1 | 3 | Toda modificación actualiza dibujo, lista, estadísticas y perfil de forma coherente (modelo único observable). | Prueba de que el evento de cambio del documento se emite una vez por operación. |

## PROF — Perfil de elevación

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| PROF-01 | 1 | 5 | Ventana independiente de perfil asociada a un track: distancia acumulada (km) frente a elevación (m). | Ejes con unidades; título con nombre del track. |
| PROF-02 | 1 | 5 | Distancia acumulada geodésica horizontal (Karney, WGS84) ignorando la elevación; entre segmentos distintos no se suma el hueco por defecto (opción documentada). | Prueba con distancias de referencia (±1 mm). |
| PROF-03 | 1 | 5 | Elevaciones ausentes producen huecos en la curva y un aviso «N puntos sin elevación». | Track sin elevaciones → mensaje y gráfico vacío sin error. |
| PROF-04 | 1 | 5 | Mover el puntero sobre el perfil resalta el punto en el mapa; hacer clic lo selecciona en la lista. Seleccionar en mapa/lista mueve el cursor del perfil. | ACC-PROF-02. |
| PROF-05 | 1 | 5 | Tracks con 0, 1 o puntos duplicados no generan errores. | Pruebas unitarias de casos degenerados. |
| PROF-06 | 1 | 5 | El perfil muestra el desnivel positivo y negativo acumulados, calculados como en mendikat.net: perfil suavizado con una media móvil gaussiana de 100 m (ADR-0008). Los segmentos se miden por separado, las elevaciones ausentes se omiten y en un circuito (inicio y fin a menos de 100 m) el negativo se iguala al positivo. | Subida de 100 m entre tramos llanos → +100 m; oscilaciones de 3 m cada 10 m → sólo queda la diferencia neta; mismos valores que `GaussianElevationAnalyzer.php`. |
| PROF-07 | 1 | 5 | El perfil muestra el tiempo total, en movimiento y detenido. Un tramo entre puntos consecutivos de un segmento cuenta como detenido si la velocidad horizontal es menor de 1 km/h; la pausa entre segmentos también. Sin horas: «los puntos no tienen hora». | Parada de 10 min en el mismo punto → 10 min detenido; pausa de 30 min entre segmentos → 30 min detenido. |
| PROF-08 | 1 | — | El perfil muestra la velocidad media (distancia en movimiento ÷ tiempo en movimiento, con el mismo umbral de 1 km/h que PROF-07) y la velocidad máxima: la mayor media de cualquier tramo de al menos 30 s dentro de un segmento, para que los saltos del GPS no la fijen (ADR-0008). En km/h con un decimal; sin horas no se muestran. | 100 m en 60 s, parada de 10 min y 100 m en 60 s → media 6,0 km/h; 2 min a 1 m/s y 1 min a 3 m/s → máxima 10,8 km/h; un salto de 45 m en un punto no la fija. |

## DEM — Ajuste de altitud

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| DEM-01 | 1 | 7 | Servicio de elevación `IElevationSource` independiente de UI y de GPX. Fuente(s) según D4/ADR-0007. | Pruebas con doble de la fuente. |
| DEM-02 | 1 | 7 | Vista previa: nº de puntos que cambiarán, nº sin dato DEM, diferencia mín/máx/media. | — |
| DEM-03 | 1 | 7 | Aplicar requiere confirmación; se puede cancelar durante la descarga; la operación se puede deshacer. | Cancelar no modifica el documento. |
| DEM-04 | 2 | 7 | Puntos sin dato DEM conservan su elevación original. Con el MDT del IGN, «sin dato» incluye las celdas a 0,000 (mar y fuera de España: el servicio no tiene valor NODATA) y la zona fuera de cobertura. | Un punto en el mar o en Francia no cambia; nunca pasa a 0 m. |
| DEM-05 | 1 | 7 | Sin corrección geoidal implícita; el datum vertical de la fuente se muestra en la vista previa. | Texto del datum visible. |
| DEM-06 | 1 | 7 | Los puntos ajustados se marcan en memoria con el origen de su elevación (original / DEM / editada manualmente) mientras dure la sesión. | El panel de detalle muestra el origen. |

## SIMP — Simplificación

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| SIMP-01 | 1 | 6 | Douglas–Peucker con tolerancia en metros, aplicado por segmento sobre una proyección Transverse Mercator local centrada en el segmento (ADR-0008). | Recta muestreada con ruido < tolerancia → sólo extremos. |
| SIMP-02 | 1 | 6 | Se conservan siempre el primer y último punto de cada segmento; segmentos ≤ 2 puntos no cambian; los segmentos no se fusionan. | Pruebas multi-segmento. |
| SIMP-03 | 1 | 6 | Los puntos conservados mantienen todos sus campos (tiempo, elevación, extensiones) sin cambios. | Comparación de puntos supervivientes. |
| SIMP-04 | 1 | 6 | Vista previa con puntos originales, resultantes y tolerancia antes de aplicar; aplicar se puede deshacer. | — |
| SIMP-05 | 1 | 6 | Puntos repetidos y geometrías degeneradas (todos iguales, colineales) se manejan sin error; implementación iterativa (sin recursión profunda) para tracks muy largos. | Prueba con 1 000 000 de puntos sin desbordamiento de pila. |

## TIME — Regeneración de tiempos

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| TIME-01 | 1 | 6 | Entradas: fecha y hora inicial, zona horaria (por defecto la del sistema), velocidad y unidad (km/h, m/s, min/km). | Validación: velocidad > 0. |
| TIME-02 | 1 | 6 | Modo «por distancia» (predeterminado): `t_i = t_0 + d_i / v`, con `d_i` distancia geodésica acumulada. | Prueba: 1 000 m a 4 km/h → +15 min. |
| TIME-03 | 1 | 6 | Modo «intervalo uniforme»: `t_i = t_0 + i·Δt` con Δt configurable, documentado como distinto del anterior. | — |
| TIME-04 | 1 | 6 | Segmentos: el tiempo continúa entre segmentos; el hueco entre segmentos se recorre a la misma velocidad (opción: sin hueco). Sin pausas en v1. | Prueba multi-segmento. |
| TIME-05 | 1 | 6 | Los tiempos se escriben en UTC ISO 8601 (`yyyy-MM-ddTHH:mm:ssZ`, con fracción si procede); conversión local→UTC una sola vez y visible en la vista previa. | Prueba en cambio de horario de verano. |
| TIME-06 | 1 | 6 | Sólo afecta al track seleccionado; nunca a waypoints ni a otros documentos. Vista previa y cancelar; deshacer. | — |

## REV — Invertir track

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| REV-01 | 1 | — | *Herramientas › Invertir track* invierte el sentido del track o ruta seleccionado: el orden de los segmentos y el de los puntos de cada segmento. Los segmentos no se fusionan y los puntos conservan todos sus campos. | Dos segmentos → orden inverso de segmentos y puntos; track vacío o de un punto sin error. |
| REV-02 | 1 | — | Las horas se reflejan, `t' = t_primera + t_última − t`, para que el resultado siga en orden cronológico con la misma hora de inicio y fin y los mismos intervalos entre puntos. Los puntos sin hora siguen sin hora. | 0, 60, 180, 600, 660 s → 0, 60, 480, 600, 660 s. |
| REV-03 | 1 | — | Sin diálogo; un único paso que se deshace; sólo afecta a la capa seleccionada (nunca a waypoints). | Deshacer restaura orden y horas. |

## LMAP — Cartografía local georreferenciada (incremento 9, hecho 2026-10-27 con el alcance de D7)

Mapas ráster locales para ver debajo de los tracks. Ninguno se declara soportado si sólo se detecta: hay que leerlo,
georreferenciarlo y dibujarlo en su sitio. Alcance: decisiones D7 y D10 (PROJECT.md); lectores propios, sin
dependencias nuevas (`Mapping/Local`).

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| LMAP-01 | 2 | 9 | *Archivo › Abrir cartografía…*, separado de *Abrir archivo*, con filtros por formato soportado (sólo los que realmente se leen: `.map`, `.imp`, `.asc`, `.hgt`; varios a la vez). Si se eligen con *Abrir archivo…*, también se abren como cartografía (no como documento). El mapa se dibuja sobre la cartografía en línea y debajo de los tracks, sin sustituirla. Los mapas y MDT abiertos se listan en el desplegable «Cartografías Usuario (n)» de la barra de herramientas (D10): casilla para mostrar u ocultar (en un MDT, para usarlo o no), ajustar el mapa a su extensión y quitar. No son documentos: no se guardan ni se recuerdan entre sesiones (D10). Abrir GPX no cambia. | Abrir un mapa local con un GPX abierto: el track se sigue viendo encima; quitar el mapa no afecta a los documentos. |
| LMAP-02 | 1 | 9 | Detección del formato por contenido y validación antes de cargar. Cualquier fallo (formato no soportado, archivo auxiliar ausente, georreferenciación inválida, dependencia opcional no instalada, error de lectura) muestra un mensaje en español que nombra el archivo y la causa, no añade nada al mapa y la aplicación sigue operativa (APP-05). | Archivo inexistente, extensión incorrecta, archivo vacío o truncado → mensaje claro y ningún mapa añadido. |
| LMAP-03 | 2 | 9 | **OziExplorer `.map`**: se interpretan cabecera, ruta de la imagen, datum, proyección y puntos de control. La imagen se busca en la ruta indicada (absoluta o relativa a la carpeta del `.map`) y, si no está, con el mismo nombre en la carpeta del `.map`. Imagen ausente → error que nombra el archivo que falta. Calibración incompleta o incoherente (sin puntos suficientes, valores fuera de rango) → error. La imagen debe estar en un formato que decodifica Windows (JPG, PNG, BMP, GIF, TIFF); OZF2/OZFX3 (formato propietario) y ECW no se admiten y se dice explícitamente. | `.map` válido con imagen; `.map` con imagen ausente; `.map` truncado; `.map` que apunta a un `.ozf2`. |
| LMAP-04 | 2 | 9 | Los datums y proyecciones de OziExplorer se transforman correctamente a WGS84. Se admite una lista documentada: datums WGS 84, ETRS89 (igual a WGS84 a la precisión de un mapa) y European 1950 (ED50, con la transformación de Helmert de 7 parámetros por huso 29, 30 y 31 de mendikat.net, `lib/GIS/HelmertTransformation.php`); proyecciones Latitud/Longitud, UTM y Transverse Mercator; un datum o proyección fuera de la lista da error en vez de colocar el mapa mal. Los nombres de datum de OziExplorer se interpretan igual que en mendikat.net (`lib/GPS/Oziexplorer/Parsers/DatumParser.php`). | Los puntos de control de un `.map` en ED50/UTM caen en su posición WGS84 de referencia (los de mendikat.net: longitud a 1e-7°, latitud a 1e-5° por D12). |
| LMAP-05 | 2 | — | *Aplazado (D7, 2026-10-27)*: **GeoTIFF (`.tif`, `.tiff`)** de visualización: se leen georreferenciación (GeoKeys, tiepoints, escala o matriz), CRS y metadatos; coordenadas geográficas y proyectadas; se reproyecta para dibujarlo en Web Mercator (MAP-08). Un TIFF sin georreferenciación utilizable se rechaza con un mensaje propio. | GeoTIFF en EPSG:25830 y en EPSG:4326 colocados en su sitio; TIFF sin georreferenciación → «no está georreferenciado». |
| LMAP-06 | 2 | 9 | **CompeGPS/TwoNav `.imp`**: se interpreta la calibración y se resuelve la imagen a la que remite (el `.imp` no contiene la imagen). La imagen debe estar en un formato que decodifica Windows (JPG, PNG, BMP, GIF, TIFF); ECW u otros → mensaje explícito (LMAP-08 aplazado). | `.imp` válido con su imagen; `.imp` con imagen ausente → error que nombra el archivo. |
| LMAP-07 | 2 | — | *Aplazado (D7, 2026-10-27)*: **TwoNav `.rmap`**: sólo se admite si existe una biblioteca fiable con licencia compatible (D9). No se escribe un decodificador parcial. Si no se admite, el selector no lo ofrece como soportado, abrirlo da un mensaje explícito y la limitación figura en README. | Según D9: muestra legítima colocada en su sitio, o mensaje de «formato no soportado». |
| LMAP-08 | 2 | — | *Aplazado (D7, 2026-10-27)*: **ECW (`.ecw`)**: sólo si el controlador está disponible en el equipo (dependencia nativa x64 y licencia, D8). Se comprueba al abrir; si falta, se informa de qué falta sin bloquear la aplicación. Con controlador, lectura parcial por regiones. | ECW con controlador → en su sitio; sin controlador → mensaje con la dependencia que falta. |
| LMAP-09 | 2 | 9 | Rendimiento y recursos: una imagen de más de 50 megapíxeles se decodifica a tamaño reducido (los JPEG directamente a ese tamaño), de modo que nunca ocupa más de unos 200 MB; el mapa se dibuja en teselas de 256 píxeles que se reproyectan bajo demanda en segundo plano (rejilla de 17 × 17 puntos exactos por tesela, interpolada); lectura fuera del hilo de UI (NFR-01) y carga cancelable (NFR-02); tamaños de MDT validados antes de reservar memoria (máx. 100 millones de valores). Acercar, alejar y desplazar no bloquean el visor. Con D7 no hay lectura por ventanas ni pirámides (exigiría GDAL o lectores propios de TIFF por bloques). | Imagen de 400 × 200 con límite de 20 000 píxeles → decodificada a 200 × 100; MDT demasiado grande → error sin reservar memoria. |
| LMAP-10 | 1 | 9 | Arquitectura: detección, lectura, georreferenciación, dibujo y diagnóstico separados, con una interfaz común de mapa ráster; añadir un formato no obliga a cambiar el motor cartográfico. Sin dependencias innecesarias ni decodificadores propios si existe una biblioteca fiable (NFR-04). | Prueba de arquitectura / revisión: el mapa no conoce los formatos concretos. |
| LMAP-11 | 1 | 9 | Superposición de cartografías de usuario: cada mapa tiene un deslizador de opacidad (0–100 %, con su porcentaje) para ver lo que hay debajo; el orden de la lista es el orden de dibujo (la de arriba, encima) y se cambia arrastrando el asa ≡ o con Ctrl+↑/↓; una cartografía recién abierta queda arriba. La cartografía en línea es siempre el fondo y los tracks quedan siempre encima, sea cual sea el orden. Los MDT no tienen opacidad (no se dibujan). | Reordenar cambia el orden de las capas en el mapa; cambiar de cartografía en línea la deja al fondo; opacidad 0,4 → capa al 40 %. |

## LDEM — Modelos digitales de elevaciones locales (incremento 9, hecho 2026-10-27 con el alcance de D7)

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| LDEM-01 | 2 | 9 | Los DEM se distinguen de los mapas de visualización (no se dibujan) y se abren como fuente de elevación que implementa `IElevationSource` (DEM-01): *Ajustar altitudes* los ofrece después de las resoluciones del IGN, y la altitud bajo el puntero (COORD-08) los consulta antes que el MDT del IGN, sin conexión. | Un DEM abierto responde a consultas de elevación por coordenada WGS84; desmarcado, no se usa. |
| LDEM-02 | 1 | 9 | **SRTM HGT (`.hgt`)**: dimensiones por tamaño de archivo (1201² o 3601² muestras), enteros de 16 bits con signo big-endian, −32768 = sin dato, límites a partir del nombre (`N42W002`), muestras en los nodos (incluidos los bordes). Tamaño no válido o nombre sin coordenadas → error. | Valores conocidos en nodos y entre nodos; archivo truncado → error; huecos −32768 → sin dato. |
| LDEM-03 | 2 | — | *Aplazado (D7, 2026-10-27)*: **GeoTIFF de elevaciones**: se detecta un ráster de una banda numérica; se respetan tipo de dato, georreferenciación, CRS y NoData. | DEM GeoTIFF con CRS proyectado y NoData: valores de referencia y celdas NoData sin dato. |
| LDEM-04 | 1 | 9 | **ESRI ASCII Grid (`.asc`)**: cabecera `ncols`, `nrows`, `xllcorner`/`xllcenter`, `yllcorner`/`yllcenter`, `cellsize`, `NODATA_value` (opcional); matriz con el número exacto de valores. CRS del `.prj` con el mismo nombre (sin distinguir mayúsculas ni espacios alrededor del nombre, p. ej. «MDT05-0112-H30-LIDAR .PRJ»; WKT de ESRI u OGC, formato clásico de ArcInfo «Projection UTM / Zone / Datum», o `EPSG:n`): geográficas, UTM o Transverse Mercator en metros sobre WGS84, ETRS89, REGCAN95 o ED50. Los nombres de MDT del IGN («MDT05-0112-H30-LIDAR», «PNOA_MDT05_ETRS89_HU30_…», «…_REGCAN95_HU28_…») indican huso y datum y mandan sobre un `.prj` que diga otro huso (hay `.prj` del IGN con el huso equivocado), avisándolo en los detalles. Sin nombre del IGN ni `.prj`, el MDT se rechaza con un mensaje que nombra el `.prj` que falta (D10). El carácter Ctrl+Z del final (fin de archivo de MS-DOS) se ignora. | Misma rejilla con esquina y con centro da los mismos valores en los mismos puntos; matriz incompleta, con valores de más o no numéricos → error; sin `.prj` → error. |
| LDEM-05 | 1 | 9 | Unidades y referencia vertical se conservan y se muestran cuando el archivo las documenta; si no, se indican como desconocidas. Nunca se supone altura ortométrica o elipsoidal ni se aplica corrección (DEM-05). | Texto de la referencia vertical o «desconocida». |
| LDEM-06 | 1 | 9 | Valores ausentes, archivos corruptos y puntos fuera del DEM dan «sin dato», nunca 0 m; el punto conserva su elevación (DEM-04). | Consulta fuera de la cobertura → sin dato. |
| LDEM-07 | 2 | 9 | Diseño ampliable a mosaicos contiguos y conjuntos grandes. De momento cada archivo es un MDT completo en memoria; con varios abiertos se consulta el primero que tiene dato en el punto, así que mosaicos contiguos funcionan abriéndolos todos. Carga bajo demanda por mosaico: pendiente. | Revisión de la interfaz. |

### Pruebas comunes a LMAP y LDEM

Archivos de referencia pequeños generados para las pruebas (sin red). No basta con abrir sin excepciones: se
comprueban la posición geográfica de los puntos de control y, en los DEM, los valores de elevación. Casos mínimos:
`.map` válido, con imagen ausente y corrupto; GeoTIFF georreferenciado y TIFF sin georreferenciar; RMAP e IMP si
hay muestras legítimas; ECW con y sin controlador; HGT válido, corrupto y con huecos; DEM GeoTIFF con CRS y NoData;
ASC con esquina y con centro; archivos inexistentes, extensiones incorrectas y errores de lectura.

## CONV — Importar KML, KMZ y TCX y convertir a GPX (incremento 10, hecho 2026-10-26)

Formatos populares de Google Earth (KML, KMZ) y Garmin (TCX) que se **leen** y se convierten a GPX; nunca se
escriben (la aplicación sigue guardando sólo GPX). mendikat.net ya importa KML/KMZ (`lib/GPS/KML`, `lib/GPS/KMZ`,
`lib/GPS/File.php`): se sigue su modelo (cada `LineString` → un track, cada `Point` → un waypoint, el KMZ se lee de
`doc.kml`), pero no sus defectos aparentes, que hay que confirmar y corregir también en el PHP (D11): latitud y
longitud intercambiadas en `LineStringParser`, puntos repetidos por cada hijo del nodo, elevación ausente convertida
en 0 m, sólo el primer `LineString` de un `MultiGeometry`, `desciption` mal escrito y nombre del waypoint perdido.

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| CONV-01 | 2 | 10 | *Archivo › Abrir archivo…* (Ctrl+O) con filtros «Todos los admitidos», GPX, KML, KMZ y TCX; el formato se detecta por el contenido (raíz `gpx`, `kml`, `TrainingCenterDatabase`; ZIP con KML para KMZ), no sólo por la extensión. Abrir GPX no cambia. | Un `.kml` renombrado a `.xml` se reconoce; un archivo de otro tipo da un mensaje claro (APP-05). |
| CONV-02 | 1 | 10 | Un KML, KMZ o TCX se abre como documento GPX **nuevo y sin guardar** con el nombre del origen y extensión `.gpx` (como *Archivo › Nuevo*, APP-08): el original nunca se modifica y *Guardar* pide dónde guardar el `.gpx` (excepción explícita a SAVE-01). | Abrir `ruta.kml` → pestaña «ruta.gpx» sin archivo; Guardar pide ruta con `ruta.gpx` propuesto. |
| CONV-03 | 2 | 10 | **KML** (2.2 y extensiones `gx:` de Google Earth): `Placemark/Point` → waypoint con nombre y descripción; `LineString` → track de un segmento; `MultiGeometry` con varios `LineString` → un track con un segmento por línea; `gx:Track` (con `when` y `gx:coord`) → track con horas; `gx:MultiTrack` → un segmento por `gx:Track`. Se recorren todas las carpetas (`Folder`, `Document`). Coordenadas en orden lon,lat[,alt], con o sin espacios junto a las comas (mendikat.net los escribe); un `Point` usa su primera tupla, como Google Earth; sin altitud → sin elevación (nunca 0 m). Polígonos, superposiciones, modelos y estilos se ignoran con un aviso que dice cuántos. | Archivo de referencia con un punto, una línea, un `MultiGeometry` de dos líneas y un `gx:Track`: posiciones, nombres, elevaciones y horas esperados; polígono → aviso. |
| CONV-04 | 2 | 10 | **KMZ**: ZIP cuyo documento principal es `doc.kml` (como mendikat.net) o, si no existe, el primer `.kml` de la raíz; el resto (imágenes, iconos) se ignora. Se validan tamaño descomprimido (máx. 256 MB), número de entradas (máx. 10 000) y relación de compresión (máx. 200:1 en entradas de más de 1 MB) antes de extraer, y se vuelve a limitar mientras se descomprime (protección frente a ZIP malicioso) y nada se escribe en disco. | KMZ válido igual que su KML; ZIP sin KML o KML corrupto dentro → error; ZIP con relación de compresión desproporcionada → error sin agotar memoria. |
| CONV-05 | 2 | 10 | **TCX** (Garmin Training Center v2): cada `Activity` → un track (tipo = `Sport`, nombre = `Id`/fecha) con un segmento por `Lap` (sus `Track` unidos); cada `Trackpoint` con `Position` → punto con hora, `AltitudeMeters` y, como extensión Garmin `TrackPointExtension` v1 de GPX, frecuencia cardiaca y cadencia. Los `Trackpoint` sin posición (interior, pausas) se omiten con un aviso que dice cuántos; los puntos con distancia, velocidad o potencia (sin equivalente en GPX) se cuentan en otro aviso. Cada `Course` → track, y sus `CoursePoint` → waypoints. | TCX de referencia con dos vueltas y puntos sin posición: dos segmentos, horas, alturas, pulso y cadencia correctos y aviso con el recuento; el GPX resultante valida contra el XSD (GPX-04). |
| CONV-06 | 2 | 10 | *Archivo › Convertir a GPX…*: elige uno o varios archivos KML, KMZ o TCX y una carpeta de destino (por defecto la del origen), y escribe un `.gpx` por archivo con escritura segura (SAVE-03). Qué hacer si el GPX ya existe se elige en el diálogo: guardarlo con otro nombre («nombre (2).gpx», predeterminado), reemplazarlo u omitir ese archivo. Un origen que ya es GPX no se convierte. Al terminar, informe por archivo: tracks, puntos y waypoints convertidos, avisos y errores. Un archivo con error no detiene los demás. En segundo plano, con progreso y cancelable (NFR-01, NFR-02). | Lote de tres archivos con uno corrupto → dos `.gpx` válidos y el error del tercero en el informe; cancelar a mitad no deja archivos a medias. |
| CONV-07 | 1 | 10 | La conversión es la misma por las dos vías (abrir y herramienta) y no pierde nada en silencio: todo lo que no tiene equivalente en GPX se cuenta en los avisos de carga (`LoadWarning`). | Abrir y guardar da el mismo GPX que la herramienta. |

## WPE — Edición de waypoints

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| WPE-01 | 2 | 3 | Lista de los waypoints (`wpt`) del GPX y su representación en el mapa (triángulo con su nombre al acercarse), con selección sincronizada. | Un GPX sólo con waypoints muestra todos en el mapa y en la lista. |
| WPE-02 | 2 | 3 | Editar nombre, comentario, descripción, lat, lon, elevación y fecha. | Todos los campos se ofrecen (GPX los admite todos). |
| WPE-03 | 2 | 3 | Validación: lat ∈ [−90, 90], lon ∈ [−180, 180], elevación ∈ [−500, 9000] m o vacía, hora UTC `AAAA-MM-DD HH:MM:SS` o vacía; números con coma o punto decimal, sin separador de miles. No se aplica longitud máxima de nombre (no hay un límite fiable documentado; nunca se trunca). | Valores inválidos no se aplican y se informa. |
| WPE-04 | 2 | 4 | Guardar / Guardar como en GPX; aviso si el archivo cambiará (GPX 1.0 → 1.1). | — |

## SAVE — Guardado y seguridad de datos

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| SAVE-01 | 1 | 4 | Guardar escribe en el mismo formato y extensión del origen. No hay cambio silencioso de formato. | — |
| SAVE-02 | 1 | 4 | Guardar como permite elegir ruta y nombre; el diálogo confirma la sobrescritura. Se fuerza la extensión del formato. | — |
| SAVE-03 | 1 | 4 | Escritura segura: archivo temporal en la misma carpeta → releer y validar → `File.Replace` con copia `.bak` temporal → eliminar copia al tener éxito. Si falla, el original queda intacto. | Prueba con fallo simulado durante la escritura. |
| SAVE-04 | 1 | 1 | Indicador de cambios sin guardar por documento (`*` en pestaña y título). | Editar marca; guardar desmarca; deshacer hasta el estado guardado desmarca. |
| SAVE-05 | 1 | 1 | El documento en memoria es independiente del archivo; nada se escribe automáticamente. | Revisión: sólo los comandos Guardar/Guardar como escriben. |
| SAVE-06 | 1 | 1 | Modelo de datos independiente de la UI (proyecto `Domain` sin referencias a WPF). | Prueba de arquitectura: el ensamblado Domain no referencia `PresentationFramework`. |

## NFR — Requisitos no funcionales

| Id | Ver | Inc | Requisito | Criterios de aceptación |
|----|-----|-----|-----------|-------------------------|
| NFR-01 | 1 | 1 | Lectura, parseo y procesamiento pesado fuera del hilo de UI (`Task.Run` para CPU, async para E/S). | Abrir un GPX de 50 MB mantiene la UI receptiva y muestra progreso. |
| NFR-02 | 1 | 8 | Operaciones largas cancelables con progreso (carga grande, DEM, simplificación grande). | — |
| NFR-03 | 1 | 1 | Excepciones capturadas en límites claros (comandos de UI, lectores, cliente HTTP); sin `catch` vacíos. Manejador global que registra y muestra error sin cerrar. | Revisión de código / analizador. |
| NFR-04 | 1 | 1 | Dependencias NuGet mínimas, justificadas y registradas con licencia en README (ADR-0009). | — |
| NFR-05 | 1 | 1 | Funciona sin internet para abrir/editar/guardar. | ACC-NFR-01. |
| NFR-06 | 1 | 1 | Logs técnicos en `%LOCALAPPDATA%\Mendikat\TrailLab\logs` con rotación; sin rutas completas de usuario (sólo nombre de archivo) ni contenido de los archivos. | — |
| NFR-07 | 1 | 1 | Interfaz en español; código, identificadores y comentarios en inglés; textos de UI en recursos (`.resx`) para futura localización. | — |
| NFR-08 | 1 | 8 | Accesibilidad básica: navegación por teclado de menús y listas, nombres accesibles (AutomationProperties) en controles. | — |
| NFR-09 | 1 | 2 | Rendimiento: un track de 100 000 puntos se dibuja y se desplaza de forma fluida (simplificación de dibujo por nivel de zoom permitida, sin alterar datos). | — |
