# Polaris GPS: licence and ECW exception

Copyright © 2026 Mendikat (Javier Urrutia) · https://www.mendikat.net

Polaris GPS is free software: you can redistribute it and/or modify it under the terms of the GNU General Public
License as published by the Free Software Foundation, version 3 (see [LICENSE](LICENSE)).

Polaris GPS is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied
warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

## Additional permission under GNU GPL version 3 section 7

If you modify Polaris GPS, or any covered work, by linking or combining it with the ERDAS ECW/JP2 SDK of Hexagon
(Intergraph Corporation), or a modified version of that library, containing parts covered by the terms of the ERDAS
ECW/JP2 SDK licence, the licensors of Polaris GPS grant you additional permission to convey the resulting work.
Corresponding Source for a non-source form of such a combination shall not include the source code for the parts of
the ERDAS ECW/JP2 SDK used, as well as that of the covered work.

The ERDAS ECW/JP2 SDK (`gdal_ECW_JP2ECW.dll`, `NCSEcw.dll`) is not covered by the GPL: it is distributed under its
own licence (`gdal\ECW5License.rtf` in the installation), which the application asks the user to accept before
opening ECW files.

## Third-party components

Polaris GPS uses components under their own licences, compatible with the GPL version 3: Mapsui, SkiaSharp,
GeographicLib, Microsoft.Extensions (MIT); GDAL, PROJ, GEOS and their libraries (MIT and similar; their licences are in
the `gdal` folder of the installation); NetTopologySuite (BSD-3-Clause). The installer is built with the WiX Toolset
(MS-RL). The map tiles, elevation models and routes downloaded at run time belong to their providers and follow their
terms (IGN Spain CC BY 4.0, OpenStreetMap ODbL…).
