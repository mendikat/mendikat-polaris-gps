# ADR-0005 — Modelo GPX y preservación de datos

- Estado: **Aceptada**
- Fecha: 2026-10-24

## Decisión

- Lector/escritor propio con `System.Xml` (`XmlReader` para leer en streaming y `XmlWriter` para escribir). Sin dependencia externa: GPX es pequeño y estable.
- Lectura de **GPX 1.1** y **1.0**; escritura siempre **GPX 1.1** (aviso al guardar un origen 1.0, GPX-04).
- Modelo en `Domain` sin dependencias de UI ni XML:
  `GpxDocument → Metadata, Waypoints, Routes, Tracks → Segments → TrackPoint`.
- Cada elemento guarda:
  - campos estándar tipados (`Elevation: double?`, `Time: DateTimeOffset?`, `Name`, `Comment`, `Description`, `Symbol`, …);
  - **elementos/atributos desconocidos y `<extensions>`** como fragmentos XML literales (`string`/`XElement` serializado) en orden, reemitidos tal cual al guardar.
- Valores ausentes = `null`, nunca 0.
- Los `time` se leen a `DateTimeOffset` UTC. Si un archivo trae hora sin zona, se trata como UTC y se informa en el resumen de carga.
- Validación en la carga: XML bien formado, raíz `gpx`, lat ∈ [−90, 90], lon ∈ [−180, 180], números en cultura invariante.
- Prefijos/namespaces de extensiones se conservan declarándolos en la raíz al guardar.

## Consecuencias

- La equivalencia de ida y vuelta se prueba a nivel de datos y de XML canónico de extensiones, no byte a byte (formato, espacios y orden de atributos pueden variar).
- Prueba de validación contra el XSD oficial de GPX 1.1 (incluido en el proyecto de pruebas).

## Escritura (incremento 4, 2026-10-25)

- `GpxWriter` escribe GPX 1.1 en UTF-8 sin BOM, indentado, CRLF. Los elementos se ordenan según la secuencia
  del esquema (`wptType`, `trkType`, `rteType`, `metadataType`); los hijos de `extensions` se reúnen en un único
  elemento; los elementos de otros espacios de nombres que aparecían fuera de `extensions` se escriben al final.
- Números con el formato más corto que vuelve a leerse igual (`"R"`): no se redondean las coordenadas originales.
  Horas en UTC (`…Z`), con fracciones de segundo si las había.
- Las declaraciones de espacio de nombres ya hechas en la raíz se omiten en los fragmentos; las demás se
  conservan para mantener los prefijos (`custom:info`).
- **GPX 1.0 → 1.1**: los campos con equivalente pasan al espacio de nombres 1.1; `url`/`urlname` → `link`;
  cabecera raíz (`name`, `desc`, `author`, `email`, `url`, `time`, `keywords`, `bounds`) → `metadata`
  (`email` se divide en `id`/`domain`); lo que no tiene equivalente (`course`, `speed`…) se guarda en
  `extensions` con su espacio de nombres 1.0. Se pide confirmación una vez por documento (GPX-04).
- Las salidas de las pruebas se validan contra el XSD oficial de GPX 1.1 (copiado en `tests/…/Fixtures/schema`).
