# Roadmap — Mendikat TrailLab

Versión: 0.1 (Incremento 0)

Regla: un incremento no empieza hasta que el anterior tiene sus pruebas en verde (o excepciones
documentadas) y ha sido aprobado. Cada incremento termina con un informe: archivos modificados,
requisitos cubiertos, pruebas ejecutadas y resultados, limitaciones y siguiente paso.

**Definición de terminado (común)**: compila sin advertencias nuevas · pruebas de los requisitos del
incremento automatizadas y en verde · escenarios manuales comprobados y anotados · README/specs
actualizados · dependencias nuevas registradas con licencia.

| Inc | Nombre | Depende de | Requisitos | Definición de terminado específica |
|-----|--------|-----------|------------|-------------------------------------|
| 0 | Descubrimiento y especificaciones | — | — | PROJECT, REQUIREMENTS, ACCEPTANCE, ROADMAP, ADRs, README. Decisiones abiertas presentadas. **Estado: aprobado (2026-10-24).** |
| 1 | Esqueleto ejecutable | 0, D1 (SDK .NET 10) | APP-01..05, APP-07, SAVE-04..06, NFR-01, NFR-03..07 | Solución con App/Domain/Tests (Gpx y Mapping en Inc 2); ventana con menú, pestañas de documento, barra de estado; manejador global de errores; logs; prueba de arquitectura (Domain sin WPF). Settings se traslada al Inc 2 (MAP-04). **Estado: terminado (2026-10-24).** |
| 2 | Carga y mapa | 1 | Settings, MAP-01..04, MAP-06..10, COORD-01..06, GPX-01, -02, -05, -06, WPT-01..03, WPT-05..07, LAY-02, APP-06, NFR-09 | Lector GPX y WPT con fixtures; mapa Mapsui con catálogo de proveedores; IGN por defecto; barra de coordenadas con valores de referencia en verde. **Estado: terminado (2026-10-24).** |
| 3 | Inspección y selección | 2 | LAY-01, LAY-03, LAY-04, EDIT-01..07, WPE-01..03 | Panel de capas, lista de puntos, detalle; Supr/Insert/mover; pila deshacer/rehacer por documento. **Estado: terminado (2026-10-24).** |
| 4 | Guardado | 3 | GPX-03, GPX-04, WPT-04, WPE-04, SAVE-01..03 | Escritores GPX/WPT; pruebas ida y vuelta; escritura segura con fallo simulado; validación XSD. **Estado: terminado (2026-10-25).** |
| 5 | Perfil | 3 | PROF-01..05 | Ventana de perfil (dibujo WPF propio, sin librería de gráficos salvo justificación); sincronización con mapa/lista. **Estado: terminado (2026-10-25).** |
| 6 | Procesamiento | 3, 4 | SIMP-01..05, TIME-01..06 | Douglas–Peucker iterativo; regeneración de tiempos; diálogos con vista previa; deshacer. **Estado: terminado (2026-10-25).** |
| 7 | DEM (IGN WCS) | 3 | DEM-01..06 | Fuente(s) DEM según ADR-0007; vista previa; cancelación; pruebas con doble de red. **Estado: terminado (2026-10-25).** |
| 8 | Robustez | 1–7 | NFR-02, NFR-08 + regresión | Archivos grandes, cancelación, errores de red, accesibilidad, documentación final, lista de errores conocidos. **Estado: terminado, pendiente de aprobación** (ver TRACEABILITY.md). |
| 9 | Cartografía y DEM locales | 2, 7, D7–D10 | LMAP-01..04, 06, 09, 10; LDEM-01, 02, 04..07 (D7: sólo `.map`, `.imp`, `.asc`, `.hgt`; GeoTIFF, ECW y RMAP aplazados) | Investigación de bibliotecas y licencias (ADR-0010) antes de programar; formatos realmente soportados, dependencias y limitaciones documentados en README; pruebas con archivos de referencia que comprueban posición y elevaciones. **Estado: terminado (2026-10-27), pendiente de aprobación y de probar con mapas `.map`/`.imp` reales.** |
| 10 | Importar KML, KMZ y TCX y convertir a GPX | 4, D11 | CONV-01..07 | Archivos de referencia propios por formato (sin red); conversión idéntica al abrir y con la herramienta; GPX resultante válido contra el XSD; ZIP malicioso rechazado. **Estado: terminado (2026-10-26), pendiente de aprobación.** |

## Dependencias críticas

```
0 ──► 1 ──► 2 ──► 3 ──► 4 ──► 6
                   │           ▲
                   ├──► 5      │
                   └──► 7
D1 (instalar SDK .NET 10) ─► 1
```

## Después del plan (2026-10-26, a petición del responsable)

Hechas, con pruebas y documentadas en TRACEABILITY.md:

| Cambio | Requisitos / decisiones |
|--------|-------------------------|
| Desnivel con el filtro gaussiano de mendikat.net (`GaussianElevationAnalyzer.php`); idéntico al PHP en 573 segmentos reales | PROF-06, ADR-0008 |
| Sólo GPX: se retira WPT (lector, escritor, menú) | WPT-01..07 retirados, ADR-0006 retirada, WPE-01..04 |
| Perfil: fichas con iconos (recorrido / tiempo), velocidad media y máxima (30 s), icono de track en la ventana | PROF-07, PROF-08, ADR-0008 |
| Menú Ayuda: guía HTML (F1), ¡Dona ahora! (PayPal), web de Mendikat, Buscar actualizaciones, Acerca de (versión 0.0.1) | APP-09 |
| Géoportail muestra el SCAN 25 (clave pública `ign_scan_ws`) | ADR-0004 |
| Herramientas › Invertir track: puntos y segmentos en orden inverso, horas reflejadas, deshacible | REV-01..03 |
| Incremento 10: abrir KML, KMZ y TCX como GPX nuevo sin guardar y *Archivo › Convertir a GPX…* por lotes | CONV-01..07 |
| Barra de estado: datum del archivo y altitud bajo el puntero (punto del archivo o MDT del IGN) | COORD-07, COORD-08 |
| *Convertir a GPX…* pasa del menú Herramientas al menú Archivo (no necesita un track abierto) | CONV-06 |
| Incremento 9 (alcance D7): *Archivo › Abrir cartografía…* con mapas `.map`/`.imp` y MDT `.asc`/`.hgt`, desplegable «Cartografías Usuario» (orden por arrastre y opacidad), MDT locales en la barra de estado y en *Ajustar altitudes* | LMAP-01..04, 06, 09..11; LDEM-01, 02, 04..07; D10, D12 |
| Abrir archivos soltándolos sobre la ventana (tracks y cartografía) | APP-10 |
| Tarea `watch (Hot Reload)` y configuración de depuración en `.vscode/` | — |

Pendiente:

- D11: confirmar con archivos reales los defectos del lector KML de mendikat.net y corregirlos en el PHP (TrailLab ya lee correctamente).

- Incremento 9: probar con mapas `.map` e `.imp` reales (el formato `.imp` sólo se ha probado con archivos propios). Aplazados: GeoTIFF, ECW, RMAP (D7–D9) y carga de MDT por mosaicos bajo demanda (LDEM-07).
- D12: corregir en mendikat.net la iteración de latitud de `fromCartesianGeocentric`.

- Comprobaciones manuales de TRACEABILITY.md (NFR-09 fluidez con 100 000 puntos, NFR-02, NFR-08 con el Narrador, sin red, arrastre, perfil).
- Licencias antes de distribuir: SCAN 25 del IGN francés, Esri y OSM (ADR-0004, README).
- Dirección definitiva de «Buscar actualizaciones» (`AboutViewModel.UpdatesUrl`, hoy https://www.mendikat.net).
- Redibujado incremental del mapa para tracks de cientos de miles de puntos (limitación conocida).
