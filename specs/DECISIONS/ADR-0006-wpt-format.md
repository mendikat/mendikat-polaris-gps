# ADR-0006 — Dialecto WPT soportado inicialmente

- Estado: **Retirada** (2026-10-26): el responsable decide trabajar sólo con GPX; se elimina la lectura y escritura de WPT
  (código, pruebas y fixtures; quedan en el historial de git, commit 77ff3ee y anteriores). Los waypoints se editan dentro de
  un GPX. Antes: **Aceptada** (2026-10-24), se soportaban **OziExplorer v1.1 y CompeGPS/TwoNav** (opción C)
- Fecha: 2026-10-24

## Contexto

La extensión `.wpt` la usan varios formatos incompatibles. Los más comunes:

| Dialecto | Reconocimiento | Características |
|----------|---------------|-----------------|
| **OziExplorer Waypoint File v1.1** | Línea 1 `OziExplorer Waypoint File Version 1.1`; línea 2 = datum (`WGS 84`) | CSV de 24 campos por waypoint: número, nombre, lat, lon, fecha (TDateTime de Delphi: días desde 1899-12-30), símbolo, estado, formato de visualización, colores, descripción (las comas se sustituyen por el carácter 209), dirección del puntero, formato Garmin, proximidad, **altitud en pies** (`-777` = sin dato), fuente, tamaño de símbolo, etc. Codificación ANSI (Windows-1252). Muy extendido en España (OziExplorer, CompeGPS/TwoNav lo importa, GPSBabel `ozi`). |
| CompeGPS / TwoNav WPT | Líneas con prefijo de tipo: `G  WGS 84`, `U  1`, `W  nombre A 43.1N 1.2W …`, `w …` | Coordenadas en formato textual con hemisferio, UTM o geográficas según `U`; altitud en metros. |
| Otros | Formatos binarios o privados de fabricantes | Fuera de alcance. |

## Propuesta

**Opción A (recomendada)**: soportar **OziExplorer v1.1 con datum WGS 84** en v1.

- Lectura y escritura conservando **textualmente** los campos no editados (cada waypoint guarda su línea original troceada; al guardar sólo se reescriben los campos editados).
- Altitud: pies ↔ metros (× 0,3048) en el modelo; si no se editó, se reescribe el texto original para no introducir errores de redondeo.
- Fecha: TDateTime ↔ `DateTimeOffset` (OziExplorer no documenta zona; se trata como hora local declarada sin conversión — se mostrará «sin zona»).
- Otros datums → error explícito (no se convierte).
- Arquitectura: `IWaypointFileFormat { bool CanRead(header); Read; Write }` con detección por contenido, para añadir CompeGPS después sin tocar la UI.

**Opción B**: CompeGPS como dialecto inicial (si los archivos reales del usuario son de TwoNav/CompeGPS).

**Opción C**: ambos en v1 (más trabajo en el incremento 2).

## Decisión (2026-10-24)

Opción **C**: OziExplorer v1.1 y CompeGPS/TwoNav en el incremento 2, ambos tras `IWaypointFileFormat`
con detección por contenido (cabecera `OziExplorer Waypoint File` vs. líneas `G`/`U`/`W`).
Se conservan textualmente los campos no editados y las líneas de tipo desconocido.

## Detalles de implementación (incremento 2, revisados 2026-10-24)

Fuente de referencia: lectores de GPSBabel (`ozi.cc`, y `compegps.cc` de la versión 1.7.0, retirado en versiones
posteriores) y sus archivos de referencia. No se copian sus fixtures (licencia GPL); los fixtures son propios.

**Codificación** (ambos): si el contenido es UTF-8 válido con caracteres no ASCII (o BOM) se lee como UTF-8;
si no, como Windows-1252. Se recuerda para guardar con la misma codificación.

**OziExplorer**
- Campos por posición (0-based): 0 número, 1 nombre, 2 lat, 3 lon, 4 fecha TDateTime (días desde 1899-12-30),
  …, 10 descripción, …, 14 altitud en pies (`-777` = sin dato). Las líneas pueden tener menos campos.
- **Carácter 209**: la especificación lo usa como sustituto de la coma en textos, pero en Windows-1252 es «Ñ».
  Convertirlo al leer corrompería nombres como «PEÑA». **Decisión**: al leer no se convierte (se muestra tal
  cual). Al editar, los textos de OziExplorer **no admiten comas** (revisado en el incremento 3: sustituirlas por el
  carácter 209 haría que se leyeran después como «Ñ»).
- Fecha sin zona horaria: se interpreta como UTC y se informa con un aviso de carga.

**CompeGPS / TwoNav**
- Cabecera: `G <datum>` (sólo `WGS 84`), `U <sistema>`; otras líneas de cabecera (`N`, `L`, `M`…) se conservan.
- `W  nombre A <lat>ºN|S <lon>ºE|W <dd-MMM-yy[yy]> <HH:mm:ss> <alt m> <descripción…>`; el carácter de grado
  puede ser `º` en ANSI o UTF-8. La fecha `27-MAR-62` es un valor fijo que significa «sin fecha».
- Líneas `w` (información adicional del waypoint) y otras desconocidas se conservan asociadas al waypoint anterior.
- **Sólo `U 1` (latitud/longitud)**. `U 0` (UTM) se rechaza con un mensaje claro: no hay especificación fiable
  del formato de las coordenadas UTM y no se inventa (GPSBabel también lo rechazaba).
- La línea `L` (diferencia con UTC) no se aplica: las horas se tratan como UTC con aviso.

## Información solicitada

¿De qué programa/dispositivo proceden los `.wpt` que se usarán? Un archivo de ejemplo real (sin datos sensibles) permitiría fijar los fixtures.
