# Mendikat TrailLab — Visión y alcance

Versión del documento: 0.1 (Incremento 0) · Fecha: 2026-10-24 · Estado: borrador pendiente de aprobación

## 1. Visión

Mendikat TrailLab es una aplicación de escritorio Windows para abrir, visualizar, inspeccionar,
editar y guardar archivos GPX (tracks, rutas y waypoints) sobre cartografía online, priorizando
la **exactitud de los datos GPS** y la **preservación de la información original**.

## 2. Objetivos

| Id | Objetivo |
|----|----------|
| G1 | Trabajar con varios tracks y documentos de waypoints a la vez, cada uno con su estado y archivo de origen. |
| G2 | Representar con exactitud tracks y waypoints sobre cartografía (IGN España por defecto). |
| G3 | Mostrar coordenadas UTM y geográficas WGS84 correctas bajo el puntero. |
| G4 | Editar puntos y waypoints de forma segura (deshacer, confirmaciones, sin escrituras automáticas). |
| G5 | Ofrecer herramientas de procesamiento: perfil de elevación, simplificación, regeneración de tiempos y ajuste DEM. |
| G6 | Guardar sin perder información no modificada (campos estándar y extensiones desconocidas). |

## 3. Usuarios

- **Montañero / ciclista / corredor** que prepara o revisa rutas, corrige puntos erróneos y
  ajusta altitudes o tiempos antes de cargarlas en un GPS.
- **Editor de contenidos de rutas** (p. ej. clubes) que limpia tracks y waypoints antes de publicarlos.

Se asume un usuario de Windows con conocimientos básicos de GPS, no necesariamente técnico.

## 4. Alcance (versión 1)

- Apertura, visualización, edición y guardado de GPX 1.1 (y lectura de GPX 1.0, ver ADR-0005).
- Los waypoints se trabajan dentro del GPX. Los archivos WPT no se admiten (retirados el 2026-10-26, ADR-0006).
- Mapa interactivo con los proveedores listados en ADR-0004.
- Barra de estado con coordenadas UTM y WGS84 (DMS).
- Panel de capas, lista de puntos, edición de puntos (Supr / Insert), deshacer.
- Perfil de elevación sincronizado con mapa y lista.
- Simplificación Douglas–Peucker, regeneración de tiempos, ajuste de altitud por DEM.

## 5. Fuera de alcance (versión 1)

- Edición colaborativa, cuentas, sincronización en la nube, backend propio, telemetría.
- Aplicación móvil o web.
- Enrutamiento automático, cálculo de rutas, navegación en tiempo real.
- Descarga masiva de mapas o caché offline de cartografía (más allá de una caché HTTP en memoria/temporal de sesión).
- Procesamiento por lotes, conversión masiva de formatos, estadísticas avanzadas.
- Otros formatos (KML, FIT, TCX…) — se podrán especificar más adelante.

## 6. Restricciones

- C# / .NET 10 LTS, WPF, Windows 10/11 x64 (ADR-0001, ADR-0002).
- Código e identificadores en inglés; interfaz en español (coma decimal, cultura `es-ES`).
- Unidades métricas. Nunca alterar datos originales de forma silenciosa.
- Sin dependencias, servicios ni cuentas innecesarias; sin secretos en el repositorio.
- Abrir/editar/guardar funciona sin conexión; sólo cartografía y DEM online requieren red.

## 7. Glosario

| Término | Definición |
|---------|------------|
| Documento | Contenido cargado en memoria desde un archivo GPX, con su ruta de origen, formato y estado de cambios. Independiente del archivo en disco. |
| Track / segmento / punto | `trk` / `trkseg` / `trkpt` de GPX. Un track tiene ≥0 segmentos; un segmento, ≥0 puntos ordenados. |
| Ruta | `rte` de GPX: lista ordenada de `rtept`. |
| Waypoint | Punto con nombre independiente (`wpt` en GPX). |
| WGS84 | Datum/elipsoide EPSG:4326 usado por GPX. Latitud/longitud en grados decimales. |
| UTM | Proyección Transverse Mercator por husos de 6°. **Zona** = número de huso (1–60). **Banda** = letra de latitud (C–X, sin I ni O), cada 8° (X: 12°). No son lo mismo. |
| UPS | Universal Polar Stereographic, para latitudes fuera de UTM (> 84° N, < 80° S). |
| Web Mercator | Proyección EPSG:3857 de los mosaicos de mapa. Sólo se usa para dibujar; nunca para medir. |
| DEM / MDT | Modelo digital de elevaciones / del terreno. |
| Datum vertical | Referencia de las altitudes (p. ej. geoide EGM08-REDNAP, EGM96, elipsoide). |
| Distancia geodésica | Distancia sobre el elipsoide WGS84 calculada con el algoritmo de Karney (GeographicLib). |

## 8. Decisiones abiertas (requieren al responsable del producto)

| Id | Decisión | Estado (2026-10-24) | Documento |
|----|----------|---------------------|-----------|
| D1 | Instalar el **SDK de .NET 10** (sólo hay runtime .NET 9). | Resuelta: el responsable instala el SDK oficial. **Bloquea el Incremento 1 hasta que esté instalado.** | ADR-0001 |
| D2 | **Dialecto WPT** inicial. | Resuelta: OziExplorer v1.1 **y** CompeGPS/TwoNav. Retirada el 2026-10-26: sólo GPX. | ADR-0006 |
| D3 | **Thunderforest**: ¿API key propia? | Resuelta: Thunderforest se elimina del catálogo. | ADR-0004 |
| D4 | **Fuente DEM**. | Resuelta: IGN MDT WCS, sólo España. | ADR-0007 |
| D5 | **Formato DMS**: el ejemplo del documento base mezcla hemisferios. | Resuelta: latitud primero, «43º 22' 23,11'' N 1º 45' 10,00'' W». | ADR-0003 |
| D6 | **Control de versiones**. | Resuelta: repositorio git inicializado. | — |
| D7 | **Biblioteca para cartografía y DEM locales** (LMAP, LDEM): GDAL para .NET (formatos, CRS y pirámides resueltos, pero dependencias nativas x64 de gran tamaño) frente a bibliotecas gestionadas más pequeñas (lector TIFF + transformaciones de coordenadas) y lectores propios sólo para formatos de texto simples (`.map`, `.imp`, `.asc`, `.hgt`). | Abierta (2026-10-26): evaluar compatibilidad con .NET 10, tamaño de distribución y licencias de cada controlador antes del incremento 9. | ADR-0010 (por redactar) |
| D8 | **ECW**: requiere el SDK propietario de Hexagon (ERDAS ECW/JP2), con condiciones de licencia y redistribución propias. | Abierta: verificar licencia; opción por defecto, usarlo sólo si ya está instalado en el equipo, sin incluirlo en el instalador. | ADR-0010 |
| D9 | **RMAP de TwoNav**: formato propietario; hay que comprobar si existe una biblioteca fiable con licencia compatible. | Abierta: si no la hay, no se admite y se documenta (LMAP-07). | ADR-0010 |
| D10 | **Presentación y CRS por defecto**: dónde se listan los mapas locales (panel Capas o selector de cartografía), si se recuerdan entre sesiones, y qué CRS suponer en un `.asc` sin `.prj` (preguntar al usuario o rechazarlo). | Abierta. | ADR-0010 |
| D11 | **Lector KML de mendikat.net**: posibles defectos (latitud/longitud intercambiadas en `LineStringParser`, puntos duplicados, elevación ausente → 0 m, sólo el primer `LineString` de un `MultiGeometry`, nombre del waypoint perdido). La regla de «mismos resultados que mendikat.net» no debe copiar defectos. | Abierta (2026-10-26): confirmar con archivos reales y corregir también el PHP; TrailLab implementa el comportamiento correcto (CONV-03). | — |
