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
| 6 | Procesamiento | 3, 4 | SIMP-01..05, TIME-01..06 | Douglas–Peucker iterativo; regeneración de tiempos; diálogos con vista previa; deshacer. **Estado: terminado, pendiente de aprobación** (ver TRACEABILITY.md). |
| 7 | DEM (IGN WCS) | 3 | DEM-01..06 | Fuente(s) DEM según ADR-0007; vista previa; cancelación; pruebas con doble de red. |
| 8 | Robustez | 1–7 | NFR-02, NFR-08 + regresión | Archivos grandes, cancelación, errores de red, accesibilidad, documentación final, lista de errores conocidos. |

## Dependencias críticas

```
0 ──► 1 ──► 2 ──► 3 ──► 4 ──► 6
                   │           ▲
                   ├──► 5      │
                   └──► 7
D1 (instalar SDK .NET 10) ─► 1
```
