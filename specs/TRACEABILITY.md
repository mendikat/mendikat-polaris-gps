# Trazabilidad requisito → código → prueba

Las pruebas llevan `[Trait("Req", "<Id>")]` (y `[Trait("Acc", "<Id>")]` para escenarios de aceptación).
Filtrar, p. ej.: `dotnet exec <Tests.dll> -trait "Req=APP-04"`.

Estado: ✅ cubierto y probado · 🟡 parcial (se completa en el incremento indicado) · 🖐 comprobación manual · ⏳ especificado, sin implementar

## Incremento 1 — Esqueleto ejecutable (2026-10-24)

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| APP-01 | ✅ 🖐 | `App/MainWindow.xaml`, `MainViewModel.Title` | `MainViewModelTests.Title_and_tab_header_show_an_asterisk_for_unsaved_changes`; arranque comprobado (ventana con título y menú) |
| APP-02 | ✅ | `MainWindow.xaml` (menú Archivo), `MainViewModel` (CanExecute) | `Without_documents_only_open_and_exit_are_enabled`, `Save_is_enabled_only_when_the_active_document_has_changes` |
| APP-03 | ✅ | `MainViewModel.Documents/ActiveDocument`, `TabControl` | `Opening_a_second_file_keeps_the_first_one`, `Closing_the_active_document_activates_a_neighbour` |
| APP-04 | ✅ | `MainViewModel.CloseDocumentAsync/ConfirmCloseAllAsync`, `MainWindow.OnClosing` | 6 pruebas `APP-04` en `MainViewModelTests` |
| APP-05 | ✅ (completado en Inc 2 y 7; revisado 2026-10-26) | `DocumentLoadException` (todos los tipos con mensaje en `UserMessages.ForLoadError`), estado de cartografía (MAP-07), errores del MDT en su diálogo (DEM-03), `App.OnDispatcherUnhandledException` | `Load_error_shows_a_spanish_message_and_adds_no_document`, `Load_errors_have_spanish_messages`, `Zero_byte_file_is_reported_as_empty`, `A_service_failure_is_shown_in_the_dialog_and_changes_nothing` |
| APP-07 | ✅ | `TrailDocument.IsSameFile`, `MainViewModel.OpenPathAsync` | `Opening_an_already_open_file_activates_it_instead_of_duplicating`, `IsSameFile_compares_full_paths_case_insensitively` |
| SAVE-04 | 🟡 (Inc 3: volver a limpio al deshacer) | `TrailDocument.IsDirty`, `DocumentViewModel.Header` | `TrailDocumentTests`, `Save_as_saves_to_the_chosen_path_and_clears_the_indicator` |
| SAVE-05 | ✅ | Sólo `MainViewModel.SaveAsync` llama a `IDocumentStore.SaveAsync` | Revisión de código |
| SAVE-06 | ✅ | Proyecto `Domain` (net10.0, sin WPF) | `ArchitectureTests.Domain_does_not_reference_any_ui_framework` |
| NFR-01 | 🟡 (Inc 2: parseo en segundo plano con progreso) | E/S asíncrona en `FileProbeDocumentStore` | — |
| NFR-03 | ✅ | Manejadores globales en `App.xaml.cs`; sin `catch` vacíos | Revisión de código |
| NFR-04 | ✅ | ADR-0009 (tabla de dependencias) | — |
| NFR-05 | ✅ | Sin acceso a red en el incremento 1 | — |
| NFR-06 | ✅ | `Logging/FileLoggerProvider`, `Logging/PathRedactor` | `LoggingTests` (3) |
| NFR-07 | ✅ | `Resources/Strings.resx`, cultura `es-ES` en `App.ApplySpanishCulture` | Mensajes en español verificados en `MainViewModelTests` |

## Incremento 2 — Carga y mapa (2026-10-24)

133 pruebas en total (29 al cerrar el incremento 1), todas en verde. Comprobación manual: la aplicación abre GPX y WPT reales
con el menú, dibuja tracks, rutas y waypoints sobre IGN y OpenTopoMap, recuerda la cartografía entre sesiones
y muestra las coordenadas en la barra de estado (capturas de pantalla del 2026-10-24).

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| GPX-01 | ✅ | `Gpx/GpxReader` | `GpxReaderTests` (1.1, 1.0, sin namespace), `SettingsAndStoreTests.File_store_loads_gpx…` |
| GPX-02 | ✅ | `GpxReader`, `TextProbe`, `DocumentLoadError` | `GpxReaderTests` (malformado con línea, vacío, fuera de rango, no GPX, ele inválida, DTD) |
| GPX-03 | 🟡 (Inc 4: escritura) | Lectura conserva campos y extensiones en `XmlFragment`, `GpxFormatState` | `Unknown_fields_and_extensions_are_kept_verbatim…` |
| GPX-05 | ✅ | `TrailPoint.Elevation/Time` anulables | `Missing_elevation_and_time_are_null_not_zero` |
| GPX-06 | ✅ | `GpxReader.ParseOptionalTime` | `Times_are_read_as_utc…`, `Times_without_zone…` |
| WPT-01 | ✅ | `Gpx/Wpt/OziExplorerFormat`, `IWaypointFileFormat` | `WaypointFileReaderTests` (Ozi) |
| WPT-02 | ✅ | `WptParsing.IsWgs84`, validación de coordenadas | `OziExplorer_other_datum…`, `…longitude_out_of_range…` |
| WPT-03 | ✅ (lectura) | `OziExplorerFormat.ParseAltitude` | `OziExplorer_altitude_in_feet…` |
| WPT-05 | ✅ (lectura) | `WaypointFileReader.Decode` | `Encoding_is_detected_from_content`, `…ansi_enye_is_kept…` |
| WPT-06 | ✅ (lectura) | `Gpx/Wpt/CompeGpsFormat` | `CompeGps_*`, rechazo de `U 0` |
| WPT-07 | ✅ | `WaypointFileReader` (detección por contenido) | `Unsupported_or_invalid_wpt_files_are_rejected` |
| MAP-01 | ✅ 🖐 | `App/Map/MapController`, `Mapping/Layers/TrailLayerFactory` | `Document_layers_contain_lines_points_and_waypoints`; captura |
| MAP-02 | ✅ | `Mapping/Providers/MapProviderCatalog` | `MapProviderCatalogTests` (incluye búsqueda de URLs fuera del catálogo), `ObservedTileSourceTests` (URLs) |
| MAP-03 | ✅ | `MapProviderCatalog.Default`, `MapSettings.Provider` | `Default_provider_is_ign_spain`, `First_run_uses_ign_spain` |
| MAP-04 | ✅ 🖐 | `Settings/JsonSettingsStore`, `MainViewModel.SelectedMapProvider` | `Settings_survive_a_restart_as_json`, `Chosen_map_is_saved_and_restored…`; arranque con OpenTopoMap guardado |
| MAP-05 | Retirado | — | — |
| MAP-06 | ✅ 🖐 | Enlace de atribución en `MainWindow.xaml` | `Every_provider_has_attribution…`; captura |
| MAP-07 | ✅ (🖐 sin red pendiente) | `ObservedTileSource`, `MapController.TileStatusChanged`, `MainViewModel.MapStatus` | `Http_errors…`, `Network_errors_and_timeouts…`, `Cancellation…`, `Tile_failures_show_a_status…` |
| MAP-08 | ✅ | `Mapping/Coordinates/WebMercator` (única conversión) | `Web_mercator_projection_matches_epsg_3857`, `…round_trip…`, `…wraps_longitude` |
| MAP-09 | ✅ 🖐 | `MainViewModel.OpenPathAsync` → `MapRequest.ZoomTo` | `Opening_a_file_asks_the_map_to_fit…`, `World_extent_covers_the_data_bounds`; captura con 2 documentos |
| MAP-10 | ✅ | `Mapping/Tiles/TileHttpClientFactory` | `Requests_send_an_identifying_user_agent…` |
| COORD-01..05 | ✅ 🖐 | `Mapping/Coordinates/CoordinateFormatter` | `CoordinateFormatterTests` (referencias externas, zonas especiales, UPS, bandas, DMS) |
| COORD-06 | ✅ | Cálculo directo en `MapPointerMoved` | `Formatting_takes_well_under_a_millisecond` |
| LAY-02 | ✅ 🖐 | `Mapping/Layers/MapStyles` | `Added_points_use_their_own_symbol`, `Each_document_gets_its_own_color` |
| APP-06 | 🟡 (perfil: Inc 3/5) | Barra de herramientas y menú Ver | `Zoom_to_data_is_enabled_only_with_points…` |
| NFR-09 | 🟡 (dibujo: comprobación manual en Inc 8) | Estilos compartidos, puntos sólo con zoom cercano | `Large_track_of_100000_points_is_read_quickly` |

## Incremento 3 — Inspección y edición (2026-10-24)

187 pruebas en total (133 al cerrar el incremento 2), todas en verde. Comprobación manual: con un GPX real de 532
puntos se ven el panel de capas, la lista de puntos, el detalle editable y el punto seleccionado resaltado en el mapa.
Durante la prueba manual se encontró y corrigió un fallo de Mapsui con listas de capas vacías que, desde el
incremento 2, hacía fallar el cierre de documentos (`MapController.RemoveLayers`).
Pendiente de comprobación manual por el responsable: Supr, Insert, arrastrar un punto, menú contextual y Ctrl+Z/Ctrl+Y.

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| LAY-01 | ✅ 🖐 | `LayerItemViewModel`, `DocumentPanel.xaml`, `MapController.SetVisibility`, capas por elemento en `TrailLayerFactory` | `Layers_list_tracks_routes_and_waypoints…`, `Hiding_a_layer…`, `Each_item_gets_its_own_layers…` |
| LAY-03 | ✅ 🖐 | `DocumentViewModel.SelectPoints/SetSelectedRows`, `PointHitTester`, `MainWindow` (clic y clic derecho) | `Selecting_a_point_from_the_map…`, `Hit_test_finds_the_nearest_visible_point…`, `Rows_number_points_by_segment…`, `Locate_finds…` |
| LAY-04 | ✅ 🖐 | `PointDetailViewModel` | `Detail_shows_values_and_missing_ones…` |
| EDIT-01 | ✅ | `MainViewModel.DeleteSelection`, `RemovePointsCommand`, `RemoveItemCommand<T>` | `Delete_removes_the_point_selects_the_next…` (ACC-EDIT-01), `…several_segments…`, `With_no_point_selected_delete_removes_the_whole_track…` |
| EDIT-02 | ✅ | `MainViewModel.ConfirmDeleteThreshold`, `IDialogService.Confirm` | `Deleting_more_than_50_points_asks…`, confirmación de track |
| EDIT-03 | ✅ | `PointEditing.CreateInsertion`, `Geodesy.Midpoint` | `Insert_*` (dominio y flujo), `Midpoint_is_halfway…` (ACC-EDIT-02) |
| EDIT-04 | ✅ (🖐 arrastre pendiente) | `MainViewModel.MovePoint`, arrastre en `MainWindow`, detalle | `Moving_a_point_can_be_undone`, `Point_detail_changes_apply_only_when_valid` |
| EDIT-05 | ✅ | `TrailDocument.Execute/Undo/Redo`, `IEditCommand` | `Undoing_back_to_the_saved_state…`, `A_new_edit_after_undo…`, `Undo_and_redo_commands_follow_the_history` |
| EDIT-06 | ✅ | Mensajes en barra de estado | `Without_a_usable_selection…` (ACC-EDIT-03) |
| EDIT-07 | ✅ | `TrailDocument.Changed` (uno por operación) → `DocumentViewModel` → `MapController.ShowDocument` | `Every_operation_raises_exactly_one_change_event`, `Edits_refresh_counts_layers_and_notify_the_map_once` |
| WPE-01 | ✅ 🖐 | Capa «Waypoints» con lista y mapa sincronizados | `Selecting_a_point_from_the_map…` |
| WPE-02 | ✅ | `PointDetailViewModel`, `PointFieldSupport` (sin comentario en WPT) | `Untouched_fields_keep_their_exact_original_values`, `Clearing_a_text_removes_the_value`, `Format_restrictions…` |
| WPE-03 | ✅ | `PointInput`, `PointDetailViewModel.TryBuild` | `PointInputTests` (números, rangos, horas), `Invalid_values_are_not_built…` |
| SAVE-04 | ✅ | Identificadores de estado en `TrailDocument` | `Undoing_back_to_the_saved_state_clears_the_unsaved_indicator` |

## Incremento 4 — Guardado (2026-10-25)

223 pruebas en total (187 al cerrar el incremento 3), todas en verde. Pendiente de comprobación manual por el
responsable: guardar y reabrir sus propios GPX y WPT.

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| GPX-03 | ✅ | `Gpx/GpxWriter` | `Round_trip_keeps_points_standard_fields_and_extensions` (ACC-GPX-01), `…metadata_root_attributes…`, `Coordinates_elevations_and_times_are_written_without_rounding`, `Edits_are_written…` |
| GPX-04 | ✅ | `GpxWriter` (conversión 1.0), `MainViewModel.ConfirmSaveIssues` | `Output_is_valid_gpx_11` (XSD oficial, 5 fixtures), `Gpx_10_is_upgraded…`, `The_gpx_10_upgrade_is_asked_only_once…` |
| GPX-05 | ✅ (escritura) | No se escriben valores ausentes | `Missing_values_are_not_invented` (ACC-GPX-02) |
| WPT-03 | ✅ | `WaypointLineWriter.OziExplorer` (pies, `-777`) | `Ozi_elevation_time_and_position_edits_use_the_format_units` |
| WPT-04 | ✅ | `WaypointLineWriter` (líneas sin editar intactas) | `Unchanged_ozi_file_is_written_byte_for_byte`, `Unchanged_compegps_ansi…`, `Editing_the_name_rewrites_only_that_field` (ACC-WPT-02) |
| WPT-05 | ✅ (escritura) | `WaypointFileWriter` (misma codificación, sin BOM añadido) | `Utf8_file_stays_utf8…` |
| WPE-04 | ✅ | `WaypointFileWriter.Check`, confirmación en `MainViewModel` | `Characters_the_encoding_cannot_store…`, `Compegps_waypoints_without_elevation…`, `Save_issues_are_confirmed…`, `Deleted_waypoints_are_not_written` |
| SAVE-01 | ✅ | `FileDocumentStore.SaveAsync`, `TrailFileWriter` | `Store_saves_in_the_same_format_and_the_file_reopens_identically` (GPX, Ozi, CompeGPS) |
| SAVE-02 | ✅ | `MainViewModel.SaveActiveAsAsync`, diálogo con `OverwritePrompt` | `Save_as_forces_the_extension…`, `Save_as_asks_before_overwriting…` |
| SAVE-03 | ✅ | `Gpx/SafeFileWriter`, `TrailFileWriter.Verify` | `Writing_creates_or_replaces…`, `A_failed_replace_keeps_the_original_intact…`, `Verification_rejects_output…`, `Save_failures_keep_the_document_modified…` |

## Incremento 5 — Perfil de elevación (2026-10-25)

241 pruebas en total (223 al cerrar el incremento 4), todas en verde. El gráfico se comprobó renderizándolo fuera
de pantalla (`RenderTargetBitmap`) con un track sintético con hueco de elevaciones y dos segmentos.
Pendiente de comprobación manual por el responsable: la ventana con sus tracks y la sincronización con el mapa.

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| PROF-01 | ✅ 🖐 | `Views/ProfileWindow`, `Charting/ProfileChart` (dibujo WPF propio), `ProfileViewModel` | `Profile_window_is_titled_with_the_track…`, `Axis_*`, `Profile_of_100000_points_builds_quickly` |
| PROF-02 | ✅ | `Domain/Profiles/ElevationProfile` (Karney, horizontal; hueco entre segmentos opcional) | `Distance_is_cumulative_geodesic_and_horizontal` (ACC-PROF-01), `The_gap_between_segments_is_not_counted_unless_asked` |
| PROF-03 | ✅ | `ElevationProfile.ElevationRuns`, aviso en la ventana | `Missing_elevations_are_counted_and_split_the_curve`, `A_track_without_elevations_has_no_curve`, `Edits_update_the_profile…` |
| PROF-04 | ✅ (🖐 pendiente) | `ProfileViewModel.Hover/SelectAt/Step`, `MapController.ShowHover` | `Hovering_the_profile_reports_the_point_and_clicking_selects_it`, `Selecting_a_point_in_the_list_or_map_moves_the_profile_cursor` (ACC-PROF-02), `Arrow_keys_step_through_the_points` |
| PROF-05 | ✅ | `ElevationProfile.Nearest` (búsqueda binaria, primer duplicado) | `Empty_single_and_duplicated_points_do_not_fail` |
| APP-06 | ✅ | Botón y menú «Perfil» habilitados con un track o ruta seleccionado | `Profile_button_is_enabled_only_for_tracks_and_routes` |
| EDIT-07 | ✅ (perfil) | El perfil se recalcula tras cada edición y se cierra si se elimina el track | `Edits_update_the_profile_and_deleting_the_track_closes_it` |

## Petición fuera de incremento — Archivo › Nuevo GPX (2026-10-25)

Petición del responsable: crear un GPX a mano. 256 pruebas en total (15 nuevas), todas en verde.
Pendiente de comprobación manual: ACC-APP-05 (Ctrl+clic en el mapa real).

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| APP-08 | ✅ 🖐 | `TrailDocument.CreateNew/IsNew`, `PointEditing.CreateAddition`, `AddItemCommand`, `MainViewModel.NewGpx/AddPointAt`, guardado de documentos nuevos en `MainViewModel.SaveActiveAsync`, Ctrl+clic en `MainWindow.OnMapTapped` | `NewDocumentTests` (10), `PointAdditionTests` (5) |

## Incremento 6 — Simplificación y regeneración de tiempos (2026-10-25)

276 pruebas en total (256 tras el incremento 5 y APP-08), todas en verde. Los diálogos se comprobaron renderizándolos
fuera de pantalla. Pendiente de comprobación manual por el responsable: usar ambas herramientas con sus tracks.

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| SIMP-01 | ✅ | `Domain/Processing/TrackSimplifier` (TM local, tolerancia en m) | `Nearly_straight_segments_keep_only_their_ends…` (ACC-SIMP-01), `Deviations_larger_than_the_tolerance_are_kept`, `Tolerance_must_be_positive` |
| SIMP-02 | ✅ | Por segmento, extremos siempre conservados | `Nearly_straight_segments…`, `Short_degenerate_and_duplicated_segments_are_handled` |
| SIMP-03 | ✅ | `RemovePointsCommand` (los conservados no se tocan) | `Kept_points_are_unchanged_and_the_simplification_can_be_undone` |
| SIMP-04 | ✅ 🖐 | `SimplifyViewModel`, `Views/SimplifyWindow`, `MainViewModel.Simplify` | `Simplify_shows_a_preview_and_applies_one_undoable_step`, `Cancelling_or_an_invalid_tolerance_changes_nothing` |
| SIMP-05 | ✅ | Douglas–Peucker iterativo | `One_million_points_do_not_overflow_the_stack`, `Short_degenerate_and_duplicated_segments_are_handled` |
| TIME-01 | ✅ 🖐 | `TimesViewModel`, `Views/TimesWindow`, `TimeRegenerator.ToMetersPerSecond` | `Speed_units_convert…`, `Times_dialog_starts_from_the_current_first_time_and_validates_input` |
| TIME-02 | ✅ | `TimeRegenerator.Plan` (modo por distancia) | `Times_follow_distance_and_speed_across_a_daylight_saving_change` (ACC-TIME-01) |
| TIME-03 | ✅ | Modo intervalo uniforme | `Uniform_mode_ignores_distance` |
| TIME-04 | ✅ | Hueco entre segmentos recorrido por defecto (opción) | `Segment_gaps_are_travelled_by_default_and_can_be_skipped` |
| TIME-05 | ✅ | Conversión local→UTC única; horas inexistentes/ambiguas | `Nonexistent_and_ambiguous_local_start_times_are_detected`, `A_start_time_that_does_not_exist_is_rejected` |
| TIME-06 | ✅ | `SetTimesCommand` sólo sobre el track/ruta elegido | `Regenerate_times_previews_and_changes_only_the_selected_track`, `Invalid_speed_gives_no_plan_and_undo_restores…`, `Tools_are_only_available_for_tracks_and_routes` |

## Incremento 7 — Ajuste de altitudes con el MDT del IGN (2026-10-25)

297 pruebas en total (276 al cerrar el incremento 6), todas en verde. Comprobaciones con el servicio real (fuera de
las pruebas automáticas): puntos de prueba, mar, Francia y fuera de cobertura; cimas del Aneto y el Mulhacén; y el
diálogo completo renderizado fuera de pantalla contra el servicio real. Esa comprobación encontró un fallo (enlace
bidireccional de la barra de progreso a una propiedad de sólo lectura que impedía abrir el diálogo), corregido y
cubierto por `WindowSmokeTests`. Pendiente de comprobación manual por el responsable: usar la herramienta con sus tracks.

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| DEM-01 | ✅ | `Domain/Elevation/IElevationSource`, `Mapping/Elevation/IgnWcsElevationSource`, `ArcGrid` | `IgnElevationTests` (multipart, NODATA, bilineal, URL, cuadrículas, errores), `Without_an_elevation_source…disabled` |
| DEM-02 | ✅ 🖐 | `ElevationAdjustment.Plan`, `DemViewModel.Preview`, `Views/DemWindow` | `Plan_counts_changes_equal_values_and_points_without_data`, `Preview_shows_counts…` (ACC-DEM-01) |
| DEM-03 | ✅ | Aplicar explícito, cancelación de la descarga, `SetElevationsCommand` deshacible | `Without_applying_nothing_changes`, `Cancelling_the_download_changes_nothing`, `Network_failures_and_cancellation_are_reported` |
| DEM-04 | ✅ | Ceros y `ExtentError` = sin dato; esos puntos no cambian | `Exact_zeros_mean_no_data…`, `Sea_and_outside_the_coverage_give_no_data`, `Applying_marks_dem_points_keeps_points_without_data…` |
| DEM-05 | ✅ | `IElevationSource.VerticalDatum` mostrado en el diálogo; sin corrección geoidal | `The_dialog_shows_the_vertical_datum…` |
| DEM-06 | ✅ | `TrailPoint.ElevationSource`, `PointDetailViewModel.ElevationOrigin` | `Editing_or_inserting_records_the_elevation_origin`, `Applying_marks_dem_points…` |

## Mejora del perfil (2026-10-25, a petición del responsable)

306 pruebas en total, todas en verde. Ventana del perfil comprobada renderizándola fuera de pantalla.

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| PROF-06 | ✅ | `Domain/Profiles/ProfileStatistics.ElevationGain`, `ProfileViewModel.Summary` | `Ascent_and_descent_add_real_climbs_and_ignore_small_noise`, `Without_a_filter_noise_would_inflate_the_gain`, `Segments_are_measured_separately…` |
| PROF-07 | ✅ | `ProfileStatistics.Times`, `ProfileViewModel.TimeSummary` | `Slow_intervals_count_as_stopped_time`, `The_pause_between_segments_is_stopped_time`, `Missing_or_single_times…`, `Durations_are_shown…` |
| PROF-01 | ✅ | Título del eje de elevación por encima del gráfico (margen superior 30 px) | Comprobación visual |

## Incremento 8 — Robustez (2026-10-26)

322 pruebas en total (307 al empezar), todas en verde; compila sin advertencias. Mediciones con un GPX sintético de
500 000 puntos (49 MB) antes → después: abrir 5,2 s sin progreso → 5,2 s con porcentaje y cancelable; aplicar o deshacer
una simplificación 11 s → milisegundos; vista previa de Simplificar ~440 ms por tecla en la interfaz → ~150 ms en segundo
plano. La versión publicada (`artifacts\publish\win-x64`) arranca y se cierra sin errores en el registro.
Fallo encontrado en el registro real (2026-10-25 12:13) y corregido: al salir sin cambios pendientes, `MainWindow.OnClosing`
volvía a llamar a `Close()` durante el cierre y saltaba un error.

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| NFR-02 | ✅ 🖐 | Abrir: `GpxReader.Read(stream, progress, token)`, `LoadProgress`/`ProgressStream`, `MainViewModel.Progress/CancelOperationCommand` (botón y Esc). Simplificar: `TrackSimplifier.Prepare`, `PreparedSimplification`, `SimplifyViewModel` en segundo plano. `RemovePointsCommand` lineal. DEM: ya cancelable (Inc 7) | `LoadProgressTests` (3), `Opening_can_be_cancelled_and_adds_no_document`, `Large_tracks_are_previewed_in_the_background…`, `LargeEditTests` (6) |
| NFR-08 | ✅ 🖐 | `AutomationProperties.Name` en todos los controles de entrada, listas, barras de progreso, mapa y botones de símbolo; «Guardar co_mo» deja de compartir la C con «_Cerrar archivo» | `AccessibilityTests.Every_input_control_has_an_accessible_name`, `Every_menu_item_has_an_access_key_unique_within_its_menu` |
| NFR-07 | ✅ | Textos de la barra de estado que estaban en el XAML pasan a `Strings.resx` | Revisión de código |
| NFR-01 | ✅ | Carga en segundo plano con progreso medible (se cierra el 🟡 del Inc 1) | `LoadProgressTests` |
| APP-04 | ✅ | `MainWindow.OnClosing`: si la respuesta se conoce al momento, se cierra o se cancela sin volver a llamar a `Close()` | `Main_window_without_changes_closes_at_the_first_attempt`, `Main_window_with_changes_stays_open_when_the_user_cancels` |
| NFR-09 | 🟡 🖐 | Sin cambios. Rehacer las capas tras una edición: ~0,2 s con 100 000 puntos, ~0,7–1 s con 500 000 | Pendiente: comprobación manual de la fluidez del mapa |

Pendiente de comprobación manual por el responsable: abrir un GPX muy grande y cancelar; Simplificar con ese track;
navegar con teclado (Alt, Tab, flechas) y con el Narrador de Windows; ACC-NFR-01 (sin red).

## Desnivel con filtro gaussiano (2026-10-26, a petición del responsable)

330 pruebas en total, todas en verde. Mismo resultado que `GaussianElevationAnalyzer.php` en 573 segmentos reales (ADR-0008).

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| PROF-06 | ✅ | `ProfileStatistics.ElevationGain/WindowPoints/Smooth/GainAndLoss` | `A_climb_between_flat_stretches_is_counted_in_full`, `Gps_noise_is_smoothed_out`, `The_window_is_100_m_whatever_the_point_spacing` (5), `Stops_do_not_shrink_the_mean_spacing`, `In_a_circuit_the_descent_equals_the_ascent`, `Gives_the_same_result_as_mendikat_net`, `Segments_are_measured_separately…` |

## Sólo GPX: se retira WPT (2026-10-26, a petición del responsable)

298 pruebas en total (330 antes; se eliminan las 33 de WPT y se añade 1), todas en verde. Se eliminan `Gpx/Wpt/` (lector,
escritor y dialectos), sus 8 fixtures y pruebas, el menú «Abrir WPT», `PointFieldSupport` (restricciones de campos de
OziExplorer/CompeGPS), `TrailPoint.FormatState` y los errores y avisos de guardado exclusivos de WPT. `DocumentFormat`
conserva sólo `Gpx`. Las filas WPT de las secciones anteriores quedan como historia. Comprobado con `waypoints.gpx`
(890 cimas de Burgos de mendikat.net): el mapa renderizado fuera de pantalla muestra los 890 triángulos y, al acercarse, sus nombres.

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| WPT-01..07 | Retirados | — | — |
| APP-02 | ✅ | Menú Archivo sin «Abrir WPT» | `Without_documents_only_open_and_exit_are_enabled` |
| WPE-01 | ✅ | Waypoints del GPX: `TrailLayerFactory` (triángulo + nombre), lista de puntos | `A_gpx_with_only_waypoints_shows_each_one_with_its_symbol_and_name` |
| WPE-02/03 | ✅ | `PointDetailViewModel`: todos los campos, sin restricciones de formato | `PointInputTests`, `Point_detail_changes_apply_only_when_valid` (ACC-WPE-02) |
| WPE-04 | ✅ | Sólo el aviso GPX 1.0 → 1.1 | `Save_issues_are_confirmed_and_cancelling_keeps_the_changes` |

## Perfil: fichas con iconos, velocidades e icono de ventana (2026-10-26, a petición del responsable)

302 pruebas en total, todas en verde. La cabecera del perfil pasa de dos líneas de texto a fichas (icono, valor y
etiqueta) en dos filas, «Recorrido» y «Tiempo»; la ventana muestra un icono de track en la barra de título. Comprobado
renderizando la ventana fuera de pantalla con tracks reales con y sin horas, ancha y estrecha.

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| PROF-08 | ✅ | `ProfileTimes.MovingDistance/AverageSpeed/MaxSpeed`, `ProfileStatistics.MaxSpeed`, fichas de `ProfileViewModel.TimeStats` | `Average_speed_counts_only_the_time_in_movement`, `Maximum_speed_is_the_fastest_stretch_of_at_least_30_seconds`, `A_gps_jump_does_not_set_the_maximum_speed`, `Without_30_seconds_of_data_there_is_no_maximum_speed` |
| PROF-02/06/07 | ✅ | `ProfileViewModel.RouteStats/TimeStats/NoTimesNote`, `ProfileStat`, plantilla de fichas en `ProfileWindow.xaml` | `Profile_window_is_titled_with_the_track_and_shows_a_summary` |

## Menú Ayuda (2026-10-26, a petición del responsable)

306 pruebas en total, todas en verde. Versión 0.0.1. Guía comprobada con una captura de Edge sin ventana y ventana
«Acerca de» renderizada fuera de pantalla.

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| APP-09 | ✅ | `MainViewModel.Help` (Help/Donate/About), `AboutViewModel`, `Views/AboutWindow`, `Help/ayuda.html`, `IDialogService.OpenExternal/ShowAbout` | `HelpMenuTests` (4), `Every_menu_item_has_an_access_key_unique_within_its_menu`, `Every_input_control_has_an_accessible_name` (incluye «Acerca de») |

## Géoportail SCAN 25 y enlace a la web (2026-10-26, a petición del responsable)

307 pruebas, todas en verde. Géoportail muestra el SCAN 25 del IGN francés (ADR-0004); el menú Ayuda y la cabecera de la guía enlazan a
https://www.mendikat.net (`Web_opens_mendikat_net`, `The_user_guide_has_the_donation_form_and_working_internal_links`).
Después: menú Ayuda reordenado (¡Dona ahora! antes que la web), nueva descripción en «Acerca de» y «Buscar actualizaciones»
en el menú y en «Acerca de», provisionalmente a https://www.mendikat.net (`Check_for_updates_opens_mendikat_net_for_now`).

## Invertir track (2026-10-26, a petición del responsable)

313 pruebas, todas en verde. Nueva entrada *Herramientas › Invertir track*, sin diálogo y deshacible.

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| REV-01 | ✅ | `Domain/Processing/TrackReverser`, `ReverseOrderCommand<T>` | `Segments_and_points_are_reversed_and_times_mirrored`, `Empty_and_single_point_tracks_are_handled` |
| REV-02 | ✅ | `TrackReverser` (horas reflejadas con `SetTimesCommand`) | `Segments_and_points_are_reversed_and_times_mirrored`, `Points_without_time_keep_having_none` |
| REV-03 | ✅ 🖐 | `MainViewModel.Reverse`, `ReverseCommand`, menú Herramientas | `Undo_restores_order_and_times`, `Reverse_applies_one_undoable_step_to_the_selected_track_only`, `Tools_are_only_available_for_tracks_and_routes` |

## Especificados, sin implementar (2026-10-26, a petición del responsable)

Requisitos nuevos aún sin código ni pruebas. Decisiones abiertas D7–D11 en PROJECT.md.

| Req | Estado | Incremento |
|-----|--------|------------|
| LMAP-05, 07, 08 | aplazado (D7) | GeoTIFF, RMAP y ECW |
| LDEM-03 | aplazado (D7) | DEM GeoTIFF |

## Incremento 10 — Importar KML, KMZ y TCX y convertir a GPX (2026-10-26, a petición del responsable)

353 pruebas, todas en verde (40 nuevas). Archivos de referencia propios en `tests/.../Fixtures/convert` (KML con
carpetas anidadas, `MultiGeometry`, `gx:Track`, `gx:MultiTrack`, polígono y superposición; TCX con dos vueltas, punto
sin posición, `TPX` y un `Course`); los KMZ se generan en la prueba (incluido uno de 50 MB de ceros comprimidos).

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| CONV-01 | ✅ | `Gpx/TrailFileReader` (detección por firma ZIP o raíz XML), `Strings.FilterOpen`, `DocumentLoadError.UnsupportedFormat` | `The_format_is_detected_from_the_content_not_the_extension`, `Other_xml_files_are_unsupported_and_named_by_their_root`, `Files_that_are_not_xml_are_reported_by_the_reader_of_their_extension`, `Gpx_files_are_read_as_before`, `An_unsupported_file_shows_a_clear_error_and_opens_nothing` |
| CONV-02 | ✅ | `FileDocumentStore.Read`, `TrailDocument.CreateImported/ImportedFrom/IsOpenedFrom/SuggestedSavePath`, `LoadWarningKind.ConvertedFrom` | `A_kml_opens_as_a_new_unsaved_gpx_named_after_it`, `Saving_a_converted_document_asks_where_and_never_touches_the_source`, `Opening_the_same_source_again_activates_the_converted_document` |
| CONV-03 | ✅ | `Gpx/KmlReader`, `ConvertedData` (metadatos con nombre y descripción del documento) | `Kml_points_become_waypoints…`, `Kml_lines_of_every_folder_become_tracks…`, `Google_earth_tracks_keep_their_times`, `Kml_conversion_reports_its_source_and_what_was_ignored`, `Invalid_kml_coordinates_are_rejected_with_their_line`, `Converted_kml_is_written_as_valid_gpx_with_its_metadata` |
| CONV-04 | ✅ | `Gpx/KmzReader` (límites de tamaño, entradas y compresión; copia limitada en memoria) | `Kmz_reads_doc_kml_like_its_kml_and_ignores_images`, `Kmz_without_doc_kml_uses_the_first_kml`, `Invalid_kmz_files_are_rejected` (3), `A_corrupt_kml_inside_a_kmz_is_reported_as_invalid_xml` |
| CONV-05 | ✅ | `Gpx/TcxReader` (pulso y cadencia en `gpxtpx:TrackPointExtension`) | `Tcx_activity_becomes_a_track_with_one_segment_per_lap`, `Tcx_heart_rate_and_cadence_go_to_the_garmin_trackpoint_extension`, `Tcx_reports_skipped_points_and_data_without_gpx_equivalent`, `Tcx_courses_become_tracks…`, `Converted_tcx_is_written_as_valid_gpx_and_keeps_heart_rate` (XSD) |
| CONV-06 | ✅ 🖐 | `ConvertViewModel`, `Views/ConvertWindow`, `MainViewModel.ConvertToGpx`, `Gpx/GpxConversion` (escritura verificada y `FreePath`), `IDialogService.AskConvertFilePaths/AskFolder` | `Convert_writes_one_valid_gpx_per_file_and_reports_errors_without_stopping`, `Existing_gpx_files_are_renamed_skipped_or_replaced` (3), `Gpx_can_go_to_another_folder…`, `Another_folder_must_be_chosen_before_converting`, `Stopping_leaves_only_complete_gpx_files`, `Files_can_be_added_and_removed_but_not_twice`, `The_tool_shows_the_dialog…`, `Free_paths_get_a_number`, `Every_input_control_has_an_accessible_name`, `Secondary_windows_can_be_created…` |
| CONV-07 | ✅ | Misma lectura (`TrailFileReader.ReadFile`) y escritura en las dos vías; avisos `IgnoredFeatures`, `PointsWithoutPosition`, `DataNotConverted` | `Convert_writes_one_valid_gpx_per_file…` (mismos bytes que abrir y guardar), `Kml_conversion_reports…`, `Tcx_reports_skipped_points…` |

Comprobación con datos reales: 303 KML de mendikat.net (uno de cada cien del almacén, más los dos que fallaban al probar
la aplicación). 301 se convierten y su GPX pasa la verificación (367 022 puntos); 2 tienen XML mal formado (`&deg;` en
la descripción) y se rechazan con «XML no válido, línea 3». La prueba encontró dos casos que se corrigieron: espacios
tras las comas en las coordenadas («-6.36669,  38.04557,1063») y puntos con una segunda tupla cortada (se usa la primera,
como Google Earth): `Kml_from_mendikat_net_with_spaces_after_commas_and_latin1_is_read`,
`A_kml_point_uses_its_first_tuple_like_google_earth`.

Pendiente (🖐): uso del diálogo con ratón y con KML/KMZ/TCX reales de Google Earth, Garmin Connect y Strava; D11
(confirmar y corregir el lector KML de mendikat.net).

## Datum y altitud bajo el puntero (2026-10-27, a petición del responsable)

360 pruebas, todas en verde. Barra de estado comprobada renderizando la ventana fuera de pantalla («Alt. 1.234 m
(archivo) │ WGS84 │ 30T 610926 4740868 │ 42º 48' 44,42'' N 1º 38' 35,56'' W»).

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| COORD-07 | ✅ | `TrailData.Datum`, `MainViewModel.PointerDatum`, barra de estado | `The_datum_of_the_file_is_shown_and_is_wgs84_without_files` |
| COORD-08 | ✅ 🖐 | `MainViewModel.Pointer` (espera, `PointAt` con el hit test del mapa, MDT IGN 5 m), caché de teselas de `IgnWcsElevationSource`, fuentes únicas por resolución en `App` | `Over_a_point_of_the_file_its_elevation_is_shown…`, `Elsewhere_the_terrain_elevation_of_the_dem_is_shown`, `Without_dem_data_or_outside_the_map_nothing_is_shown`, `Moving_the_pointer_cancels_the_pending_lookup`, `Without_connection_the_terrain_is_not_queried_again_for_a_while`, `Downloaded_tiles_are_reused` |

Pendiente (🖐): altitud del terreno con el servicio real del IGN moviendo el puntero por el mapa.

## Incremento 9 — Cartografía y MDT locales, alcance de D7 (2026-10-27, a petición del responsable)

416 pruebas, todas en verde (45 nuevas). Lectores propios en `Mapping/Local`, sin dependencias nuevas. Archivos de
referencia generados en cada prueba (imágenes PNG de colores conocidos, `.map`, `.imp`, `.hgt` y `.asc` con `.prj`).
Comprobaciones con datos reales: el HGT `N42W001` (SRTM 1″) da en cinco puntos del Pirineo valores a 0,5–3 m del MDT
del IGN de 25 m (21 m en una ladera fuerte); ventana con «Cartografías Usuario» renderizada fuera de pantalla.
Valores de referencia de ED50 calculados con el PHP de mendikat.net (`Geoposition::fromUtm(...)->toWGS84()`).

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| LMAP-01 | ✅ 🖐 | *Archivo › Abrir cartografía…*, `MainViewModel.LocalMaps`, `LocalMapItemViewModel`, desplegable en `MainWindow.xaml`, `MapController.AddLocalMap` (grupo de capas entre la base y los tracks) | `A_dem_is_listed_and_used_for_the_pointer_elevation`, `A_map_can_be_removed_and_is_not_opened_twice`, `Opening_cartography_does_not_touch_the_documents`, `Maps_and_dems_chosen_with_open_file_are_opened_as_cartography` |
| LMAP-02 | ✅ | `LocalMapLoader`, `LocalMapException`, `UserMessages.ForLocalMapError` | `Missing_empty_and_unknown_files_give_clear_errors` (3), `Truncated_or_incomplete_calibrations_are_rejected`, `A_failure_explains_why_and_adds_nothing` |
| LMAP-03 | ✅ | `OziMapReader`, `LocalFiles.ResolveImage`, `PixelTransform`, `GeoreferencedMap` | `Ozi_utm_calibration_points_land_where_they_say`, `Ozi_latitude_longitude_points_in_degrees_and_minutes`, `Without_points_the_wgs84_corners_are_used`, `An_image_path_from_another_computer_is_found_next_to_the_map`, `Missing_or_unsupported_images_are_reported_by_name` (3), `A_calibration_with_two_points_in_the_same_row_is_rejected` |
| LMAP-04 | ✅ | `Crs` (GeographicLib), `DatumShift` (Helmert de mendikat.net) | `Ed50_utm_gives_the_same_wgs84_as_mendikat_net` (5), `Wgs84_round_trips_through_every_datum` (3), `Ozi_ed50_map_is_placed_with_the_mendikat_net_transformation`, `Unsupported_datums_and_projections_are_rejected_instead_of_misplaced` (3), `Ed50_outside_zones_29_to_31_is_not_supported` |
| LMAP-06 | ✅ 🖐 | `CompeImpReader` | `Compegps_imp_utm_ed50_gives_the_same_placement_as_ozi`, `Compegps_imp_with_longitude_latitude_points`, `An_imp_without_bitmap_or_with_a_missing_image_is_rejected`. Pendiente: muestras `.imp` reales |
| LMAP-09 | 🟡 | `WpfRasterImageDecoder` (reducción a 50 Mpx), `LocalMapTileSource` (teselas bajo demanda en segundo plano), `PngEncoder` | `Large_images_are_decoded_at_a_reduced_size`, `Tiles_show_the_image_where_it_is_calibrated_and_nothing_outside`. Sin lectura por ventanas ni pirámides (D7) |
| LMAP-10 | ✅ | `LocalMapContent` (mapa o MDT), `IRasterImageDecoder`; el mapa sólo conoce `LocalMapTileSource` | Revisión de código |
| LDEM-01 | ✅ | `ElevationGrid : IElevationSource`, `MainViewModel.LocalDems`, `DemResolutionOption.Local`, altitud del puntero | `A_dem_answers_elevation_queries_like_any_elevation_source`, `An_unchecked_dem_is_not_used`, `Adjust_elevations_offers_the_local_dems_after_the_ign_ones` |
| LDEM-02 | ✅ | `HgtReader` | `Hgt_nodes_and_interpolation_from_the_file_name_corner`, `Hgt_voids_and_positions_outside_the_tile_have_no_data`, `Invalid_hgt_files_are_rejected` (2) |
| LDEM-04 | ✅ | `AscReader`, `PrjReader` | `Ascii_grid_with_corner_and_with_center_gives_the_same_values`, `Ascii_grid_without_prj_is_rejected_instead_of_guessed`, `Ascii_grids_with_wrong_values_are_rejected` (3), `Prj_files_are_recognised` (4), `Unsupported_systems_are_rejected` (2) |
| LDEM-05 | ✅ | `ElevationGrid.VerticalDatum` (EGM96 en HGT; desconocida en ASC) | `Ascii_grid_reports_its_system_and_an_unknown_vertical_reference` |
| LDEM-06 | ✅ | `ElevationGrid.ElevationAt` (sin dato si un nodo con peso no tiene valor) | `Hgt_voids_and_positions_outside_the_tile_have_no_data`, `Ascii_grid_with_corner_and_with_center_gives_the_same_values` |
| LDEM-07 | 🟡 | Varios MDT abiertos se consultan en orden | Sin carga por mosaicos bajo demanda |
| LMAP-05, 07, 08; LDEM-03 | aplazado (D7) | — | — |

### Cartografías Usuario: orden y opacidad (2026-10-27, a petición del responsable)

419 pruebas, todas en verde. El desplegable pasa a llamarse «Cartografías Usuario»; desplegable renderizado fuera de
pantalla con dos mapas (uno al 60 %) y un MDT.

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| LMAP-11 | ✅ 🖐 | `LocalMapItemViewModel.Opacity`, `MainViewModel.MoveLocalMap`/`LocalMapsReordered`, `MapController.OrderLocalMaps`/`SetLocalMapOpacity`, arrastre por el asa ≡ y Ctrl+↑/↓ en `MainWindow` | `Moving_reorders_the_list_and_announces_it`, `Opacity_is_clamped_shown_as_percent_and_announced`, `On_the_map_user_cartographies_stay_above_the_online_one_in_list_order`. Pendiente (🖐): arrastrar con el ratón en la aplicación |

### MDT del IGN reales: `.prj` de ArcInfo, nombres y Ctrl+Z (2026-10-27, a petición del responsable)

437 pruebas, todas en verde. Con la carpeta `MDT05-LIDAR` del responsable (355 `.asc` del IGN, 186 `.prj`) se vio que:
los `.prj` usan el formato clásico de ArcInfo, a veces se llaman «nombre .PRJ» (con un espacio), algunos dan un huso
equivocado (p. ej. `MDT05-0028-H29-LIDAR`: X desde 727 000, huso 29, y su `.prj` dice huso 30) y los archivos acaban
en Ctrl+Z. Ahora se cargan 15 de 15 de una muestra repartida, en 2–3 s y unos 90 MB cada uno, con altitudes a
0–0,5 m de las del MDT del IGN en línea (cuatro archivos, incluido uno de Canarias, dos puntos cada uno).

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| LDEM-04 | ✅ | `PrjReader.ParseArcInfo`, `IgnName`, `AscReader.CoordinateSystem`/`FindPrj`, Ctrl+Z en el lector, `MapDatum.Regcan95` | `Arcinfo_prj_files_are_recognised` (4), `Ign_terrain_model_names_state_their_system` (8), `An_ign_grid_ending_with_ctrl_z_and_a_prj_named_with_a_space_is_read`, `An_ign_name_wins_over_a_prj_with_another_zone_and_says_so`, `An_ign_grid_without_prj_uses_its_name`, `The_prj_is_found_whatever_its_case` (3) |

## Abrir soltando archivos sobre la ventana (2026-10-27, a petición del responsable)

442 pruebas, todas en verde.

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| APP-10 | ✅ 🖐 | `MainViewModel.OpenFilesAsync`/`CanOpenAny`/`DroppableExtensions`, `MainWindow.OnFileDragOver`/`OnFileDrop` (eventos `Preview`, sólo para archivos) | `Dropped_tracks_and_cartography_each_go_to_their_place`, `Other_files_and_folders_are_skipped_and_counted`, `The_drop_cursor_accepts_only_supported_files` (3). Pendiente (🖐): soltar desde el Explorador |
