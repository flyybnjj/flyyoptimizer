# Interacción y catálogo OEM en WPF

Se conserva la navegación, la paleta y los controles de Windows. El análisis muestra etapas y porcentaje de pasos realmente completados, con la última muestra de CPU, memoria y GPU y trazas de muestras reales. Un dato ausente conserva su hueco. Al finalizar se muestran promedios de la observación y la identidad del equipo: portátil/escritorio/desconocido, nombre comercial, modelo y SKU. Cancelar conserva la observación anterior y no guarda muestras parciales.

Drivers separa tres fuentes: catálogo oficial del modelo, ofertas actuales de Windows Update e inventario instalado. Una búsqueda de modelo se ejecuta después de un análisis nuevo; cargar historial limpia el catálogo y las ofertas anteriores. La búsqueda por serie requiere un análisis actual y un clic explícito junto a su explicación. Solo se muestra la serie enmascarada; no entra al snapshot ni al registro de guías. El catálogo permite filtrar nombre/categoría/versión/sistema, seleccionar un paquete, revisar sus datos y abrir una descarga o ficha oficial. Si la fuente falla, conserva su mensaje, el portal oficial y el reintento.

Gaming permite seleccionar un proceso observado por memoria, actualizar la lista de procesos o escribir el PID manualmente. Cambiar el PID elimina una selección anterior. La validación de PID, duración y ejecutable se muestra junto al formulario; un PID terminado falla antes de iniciar PresentMon. Importar o capturar una sesión A nueva limpia la comparación B anterior. El parser recibe el token de cancelación. Las guías registran únicamente su apertura, sin afirmar que se aplicó un ajuste. El cierre sigue esperando la limpieza mediante OperationLifetime.

Abrir historial limpia los candidatos de captura, la selección y el PID del formulario. Los procesos archivados siguen visibles como inventario histórico en Hardware, pero no entran al selector: Windows puede reutilizar un PID para otro proceso. Un análisis actual vuelve a poblar la lista; «Actualizar procesos» y la entrada manual siguen disponibles.

## Evidencia

- Build de App con SDK 10.0.402: cero errores y cero advertencias.
- `--smoke-test artifacts/interactive-ui-v2-final`: exit 0; 23 comprobaciones deterministas del view model. Las fixtures viven en un almacén separado y los lanzamientos externos se sustituyen por un receptor local; no se aplican ajustes ni se instala software.
- Análisis real: 10 muestras, 225 drivers, equipo Lenovo LOQ 15IAX9 / 83GS identificado como portátil, 28 paquetes recibidos del catálogo oficial sin consulta por serie.
- Dos lotes visuales como máximo: inicial y confirmación. Se renderizaron las seis vistas en 1240×820 y 1000×680, además de estado vacío, análisis en curso, detalle de paquete y formulario de captura desplazado. La corrección compactó el texto OEM y distinguió última muestra de promedio. Un fallo inicial de enlace WPF se corrigió estableciendo OneWay en ProgressBar.Value.
- Artefactos: `artifacts/interactive-ui-v2-final/status.json`, `interaction-checks.json` y PNG de cada vista. La ficha añadió después la categoría y un nombre de acción que distingue descarga de página oficial; esa corrección se verificó por compilación sin una tercera ronda visual.

Las comprobaciones incluyen navegación, bloqueo de operaciones simultáneas, cancelación de análisis, GPU ausente, consulta automática sin serie, acción explícita de serie, filtros, selección/manual de PID, errores contextuales, enlace de paquete sin instalación, ofertas separadas, limpieza al cargar otra observación, catálogo sin red con portal y reintento, todas las guías de Windows y cierre. No se abrieron ajustes reales ni se inició una captura de juego. La apertura de ventanas externas y el menú desplegable nativo no se verificaron visualmente mediante los renders WPF.

## Regresión de PID histórico

El runner `--interaction-test <directorio>` ejecuta las mismas fixtures del smoke sin abrir una ventana, consultar red ni renderizar vistas. Se añadió un historial con un nombre antiguo y el mismo PID que la selección actual: antes de la corrección falló porque el selector reutilizaba ese PID. Después pasó junto con la preservación del inventario histórico y de la entrada manual/actualización en vivo. La ejecución enfocada quedó en `artifacts/interaction-history-regression-final/interaction-checks.json` (26 comprobaciones). Build de App: cero errores y cero advertencias. No se añadió una ronda visual ni se publicaron binarios.
