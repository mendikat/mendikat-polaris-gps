# ADR-0002 — Interfaz: WPF con documentos en pestañas (no MDI clásico)

- Estado: **Aceptada**
- Fecha: 2026-10-24

## Contexto

Se requiere aplicación de escritorio Windows con menú clásico, varios documentos abiertos e
interacción fluida con mapas. Se pide evaluar WPF y usar MDI real si la tecnología lo soporta bien.

## Opciones

| Opción | Pros | Contras |
|--------|------|---------|
| **WPF** | Maduro, MVVM, enlace de datos, dibujo vectorial acelerado, buen soporte en controles de mapa (Mapsui.Wpf). | Sin MDI nativo. |
| WinForms | MDI nativo (`IsMdiContainer`). | Dibujo y enlace de datos más limitados; MDI clásico es una UX anticuada (ventanas solapadas que se pierden). |
| WinUI 3 | Moderno. | Empaquetado/despliegue más complejo; ecosistema de mapas (sin Bing Maps) menos maduro; sin MDI. |
| Avalonia | Multiplataforma. | Dependencia de framework adicional no necesaria (sólo Windows). |

## Decisión

**WPF** con **documentos en pestañas** (`TabControl`) dentro de la ventana principal. WPF no tiene MDI
real; emularlo con ventanas internas flotantes añade complejidad sin beneficio. Las pestañas cumplen
la necesidad: varias vistas abiertas, alternar entre ellas y estado independiente.

Detalles:
- **Un único mapa compartido** muestra las capas de todos los documentos (el documento activo
  resaltado). Así se cumple «no reemplazar ni borrar capas de otros documentos» y se evita
  duplicar cachés de mosaicos. Las pestañas contienen la lista de elementos/puntos y el detalle
  del documento. *Alternativa descartada*: un mapa por pestaña (más memoria, no permite comparar tracks).
- Ventanas independientes (no pestañas) para el **perfil de elevación** (requisito PROF-01) y los diálogos de herramientas.
- Patrón **MVVM** sin framework externo (clases base propias `ObservableObject`/`RelayCommand`, ~100 líneas).
  Se reevaluará `CommunityToolkit.Mvvm` (MIT) sólo si el código repetitivo crece.
- No se incluye librería de docking (AvalonDock): no es necesaria para v1.

## Consecuencias

- Menú, barra de herramientas y barra de estado en la ventana principal; el «documento activo» define qué se guarda/cierra.
- La interfaz en español se pone en recursos `.resx`.
