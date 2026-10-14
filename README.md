# FlyyOptimizer

App de escritorio en español para Windows 11 x64. Analiza el equipo, muestra evidencia de oportunidades de rendimiento, reúne información de drivers y permite comparar sesiones gaming.

## Abrir la app

En la distribución portable, extrae toda la carpeta y abre `FlyyOptimizer.exe`. La distribución self-contained incluye .NET; no requiere instalar el SDK. Conserva los DLL y archivos de licencia junto al ejecutable.

Desde el proyecto:

```powershell
.\scripts\build.ps1
.\scripts\run.ps1
```

Para compilar necesitas el SDK .NET 10. El script también reconoce el SDK aislado de Codex en `%USERPROFILE%\.codex\runtimes\dotnet-10-sdk`. El SDK se puede descargar desde [Microsoft](https://dotnet.microsoft.com/download/dotnet/10.0).

## Uso

1. Pulsa **Analizar equipo**. Sigue las etapas y los recursos en vivo mientras se identifica si es portátil o escritorio, el fabricante, el modelo comercial y el SKU. Las lecturas son reales y puedes cancelar.
2. Revisa **Hardware** y **Mejoras**. Cada recomendación explica sus lecturas y ofrece un siguiente paso cuando corresponde.
3. En **Drivers**, consulta las versiones instaladas y las ofertas de Windows Update. Para Lenovo, el análisis consulta también el catálogo oficial del modelo/SKU. Puedes filtrar sus paquetes y abrir su ficha o descarga oficial. La fecha de un driver no demuestra que esté desactualizado.
4. Las guías abren Inicio, Almacenamiento, Energía, Windows Update o soporte oficial. Los cambios se eligen en esas herramientas. El historial registra la apertura de la guía, no supone que aplicaste un ajuste.
5. En **Gaming**, importa un CSV de PresentMon o selecciona su ejecutable de consola oficial y el proceso de la lista para capturarlo. También puedes escribir el PID manualmente. Compara sesiones del mismo juego y escenario.
6. Usa **Historial** para volver a un diagnóstico guardado y exporta un informe JSON o Markdown.

## Gaming y PresentMon

Descarga la consola desde [las releases oficiales de PresentMon](https://github.com/GameTechDev/PresentMon/releases). La app no distribuye ni instala PresentMon automáticamente. La captura utiliza la CLI 2.3.1 o posterior con métricas v1 y puede requerir permisos del grupo Performance Log Users o administrador para ETW. Los errores se muestran en la app.

El CSV debe corresponder a un proceso y una cadena de presentación. Mezclar varias cadenas o juegos distorsiona los tiempos, por lo que el importador rechaza mezclas identificables. Admite cabeceras compatibles de PresentMon y omite filas de tiempo inválido, indicando su número.

- FPS medios: `1000 / frame time medio en ms`.
- Percentiles P95 y P99: rango más próximo sobre los tiempos ordenados.
- 1% low: `1000 / media del 1% de frames más lentos`, redondeando hacia arriba el número de frames.
- Los tiempos por frame no son latencia de entrada.

Una comparación solo es útil si repites el escenario y las condiciones. Una reducción de uso de CPU no demuestra por sí sola mayor rendimiento.

Los CSV de captura se conservan en `%LocalAppData%\FlyyOptimizer\Captures`. Puedes importarlos nuevamente o copiarlos para analizarlos en otra herramienta.

## Qué puede comprobar

CPU, GPU, RAM, unidades y espacio libre, procesos por memoria, aplicaciones de inicio, redes, plan energético, drivers instalados y códigos de incidencia. Las temperaturas y otras lecturas dependen del hardware y de los permisos de sus proveedores.

El umbral orientativo de temperatura es 90 °C en tres lecturas. No es el límite térmico universal ni confirma throttling; revisa las especificaciones del modelo. CPU y memoria se señalan por carga media sostenida en varias muestras; una observación corta no determina la causa de todos los problemas.

El análisis usa WMI y sensores opcionales de [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor). Las lecturas GPU usan las API accesibles de AMD/NVIDIA. Los sensores de CPU que requieren un controlador de bajo nivel están deshabilitados; se puede obtener temperatura de CPU cuando ya está expuesta por un proveedor WMI de LibreHardwareMonitor u OpenHardwareMonitor en ejecución. La app mantiene el resto de los datos y explica qué no estuvo disponible. No instala ni activa un controlador de sensores.

La búsqueda de drivers respeta las fuentes y políticas de Windows Update. No encontrar ofertas no descarta otra versión compatible del fabricante. La instalación se realiza mediante Windows Update o el proveedor oficial.

## Identificación y soporte oficial del equipo

La clasificación usa el tipo de sistema y su chassis. Una batería de UPS conectada a un escritorio no lo convierte en portátil. Si Windows no aporta evidencia suficiente, se muestra tipo desconocido.

El catálogo Lenovo se resuelve con modelo o SKU y se verifica contra el nombre del producto oficial. Sus paquetes son los publicados para ese modelo: pueden incluir variantes de componentes o paquetes opcionales y no equivalen a actualizaciones confirmadas para cada dispositivo instalado. Revisa el sistema operativo y la ficha antes de instalar.

**Buscar por serie** es una acción explícita. Solo entonces se lee la serie de BIOS y se consulta al fabricante oficial. La interfaz la enmascara; no se guarda en el diagnóstico, historial ni exportaciones. La consulta normal por modelo no envía la serie. Las series vacías o genéricas de BIOS se descartan.

Otros fabricantes ofrecen su portal oficial cuando no hay un servicio de catálogo verificado. No se presenta una búsqueda genérica como resultado específico del modelo. Los drivers genéricos de Microsoft dirigen al Catálogo Microsoft Update; soporte Surface corresponde solo a un equipo Surface identificado.

## Datos locales

Los diagnósticos y el registro de guías se guardan en `%LocalAppData%\FlyyOptimizer\reports`. No se suben a un servicio. Los informes contienen hardware, modelo/SKU, nombres de procesos y aplicaciones de inicio: revísalos antes de compartirlos. Las rutas de perfil en comandos de inicio se anonimizan. No se consultan hostname ni nombre de usuario. La serie se usa únicamente en la búsqueda explícita de soporte descrita arriba.

Un informe dañado no oculta los otros informes válidos. La app conserva un registro de guías dañado y muestra el error en lugar de sobrescribirlo silenciosamente.

Varias instancias pueden escribir y refrescar las guías compartidas sin sobrescribir los registros de otra instancia.

## Verificación desde código

```powershell
dotnet test FlyyOptimizer.sln
dotnet build FlyyOptimizer.sln -c Release
dotnet run --project src/FlyyOptimizer.App -- --scan "$env:TEMP\flyy-scan.json"
dotnet run --project src/FlyyOptimizer.App -- --smoke-test "$env:TEMP\flyy-smoke"
```

Estos comandos no aplican optimizaciones al equipo. Los modos `--render <png>` y `--smoke-test <directorio>` permiten revisar el render real de WPF. El modo de prueba usa un historial aislado dentro del directorio de salida.

## Referencias

Consulta [la investigación de GitHub, Reddit y Microsoft](docs/research/2026-10-13-referencias.md), [el diseño](docs/superpowers/specs/2026-10-13-flyyoptimizer-design.md) y [el plan](docs/superpowers/plans/2026-10-13-flyyoptimizer.md). Los proyectos usados como inspiración no se ejecutan ni importan como presets.

Las dependencias y avisos están en [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Esta primera versión requiere pruebas adicionales en otros modelos de hardware antes de una distribución amplia.
