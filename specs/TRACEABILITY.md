# Trazabilidad requisito → código → prueba

Las pruebas llevan `[Trait("Req", "<Id>")]` (y `[Trait("Acc", "<Id>")]` para escenarios de aceptación).
Filtrar, p. ej.: `dotnet exec <Tests.dll> -trait "Req=APP-04"`.

Estado: ✅ cubierto y probado · 🟡 parcial (se completa en el incremento indicado) · 🖐 comprobación manual

## Incremento 1 — Esqueleto ejecutable (2026-10-24)

| Req | Estado | Código | Pruebas |
|-----|--------|--------|---------|
| APP-01 | ✅ 🖐 | `App/MainWindow.xaml`, `MainViewModel.Title` | `MainViewModelTests.Title_and_tab_header_show_an_asterisk_for_unsaved_changes`; arranque comprobado (ventana con título y menú) |
| APP-02 | ✅ | `MainWindow.xaml` (menú Archivo), `MainViewModel` (CanExecute) | `Without_documents_only_open_and_exit_are_enabled`, `Save_is_enabled_only_when_the_active_document_has_changes` |
| APP-03 | ✅ | `MainViewModel.Documents/ActiveDocument`, `TabControl` | `Opening_a_second_file_keeps_the_first_one`, `Closing_the_active_document_activates_a_neighbour` |
| APP-04 | ✅ | `MainViewModel.CloseDocumentAsync/ConfirmCloseAllAsync`, `MainWindow.OnClosing` | 6 pruebas `APP-04` en `MainViewModelTests` |
| APP-05 | 🟡 (Inc 2: errores de formato y cartografía) | `DocumentLoadException`, `UserMessages`, `App.OnDispatcherUnhandledException` | `Load_error_shows_a_spanish_message_and_adds_no_document`, `FileProbeDocumentStoreTests` |
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
