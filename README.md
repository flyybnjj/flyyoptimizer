# FlyyOptimizer

App de escritorio en español para Windows 11 x64. Analiza el equipo, muestra evidencia de oportunidades de rendimiento, reúne información de drivers y permite comparar sesiones gaming.

La interfaz usa superficies claras, tipografía Segoe UI Variable y transiciones nativas al navegar y usar controles. El movimiento respeta la preferencia de animaciones de Windows y el modo de alto contraste.

## Abrir la app

En la distribución portable, extrae toda la carpeta y abre `FlyyOptimizer.exe`. El paquete incluye .NET y la consola de medición PresentMon; el usuario no necesita instalar SDK, WinUtil, Optimizer ni utilidades de captura aparte. Conserva los DLL, la carpeta `tools` y las licencias junto al ejecutable.

Desde el proyecto:

```powershell
.\scripts\build.ps1
.\scripts\run.ps1
```

Para compilar necesitas el SDK .NET 10. El script también reconoce el SDK aislado de Codex en `%USERPROFILE%\.codex\runtimes\dotnet-10-sdk`. El SDK se puede descargar desde [Microsoft](https://dotnet.microsoft.com/download/dotnet/10.0).

## Uso

1. Pulsa **Analizar equipo**. Sigue las etapas y los recursos en vivo mientras se identifica si es portátil o escritorio, el fabricante, el modelo comercial y el SKU. Las lecturas son reales y puedes cancelar.
2. Revisa **Hardware** y **Mejoras**. Además de recomendaciones con evidencia, las herramientas permiten seleccionar un plan de energía existente, gestionar entradas de inicio del usuario y reducir animaciones. Los cambios elegidos guardan su estado anterior para deshacerlos.
3. En **Drivers**, consulta las versiones instaladas y las ofertas de Windows Update. Para Lenovo, el análisis consulta también el catálogo oficial del modelo/SKU. Puedes filtrar sus paquetes y abrir su ficha o descarga oficial. La fecha de un driver no demuestra que esté desactualizado.
4. Usa la vista previa de temporales para elegir archivos a eliminar y comprobar espacio recuperado/archivos omitidos. Los archivos eliminados no se pueden deshacer. Las guías de Windows siguen disponibles y su historial registra únicamente que se abrieron.
5. En **Gaming**, pulsa **Detectar Fortnite** y revisa los ajustes seleccionados. **Optimizar PC** aplica los pasos elegidos y muestra sus resultados y respaldos dentro de la app. También puedes configurar Modo Juego/capturas por separado y medir una sesión con el colector incluido, importar CSV o escribir el PID. Compara sesiones del mismo juego y escenario.
6. Usa **Historial** para volver a un diagnóstico guardado y exporta un informe JSON o Markdown.

## Gaming y PresentMon

**Optimizar PC para Fortnite** combina una selección oficial integrada de Chris Titus WinUtil (Modo Juego) con ajustes propios de Flyy: capturas Xbox, animaciones opcionales, plan Alto rendimiento existente en corriente, preferencia GPU para el ejecutable detectado y calidad gráfica baja del juego. El usuario elige qué aplicar; el perfil no inicia limpieza ni retira aplicaciones de inicio automáticamente. Los accesos a inicio, energía, temporales y Modo Juego abren sus controles dentro de Flyy.

La detección lee manifiestos locales Epic y verifica el ejecutable, con una búsqueda acotada de carpetas comunes y selección manual. Para editar `GameUserSettings.ini`, cierra Fortnite y asegúrate de que el archivo ya exista y permita escritura. El perfil conserva resolución, límite de FPS, renderizador y controles, y no vuelve el archivo de solo lectura. **Restaurar ajuste** usa el respaldo original y comprueba cambios externos antes de restaurarlo. Modo Performance se explica en la guía incluida y se configura en Fortnite/Launcher. El perfil no anuncia FPS adicionales sin una medición comparable.

Los cambios proceden de recursos incluidos y APIs locales; no se descarga ni ejecuta un script remoto de WinUtil. Fuente y alcance en [la investigación Fortnite](docs/research/2026-10-14-fortnite-profile.md), [la detección y restauración del juego](docs/fortnite-game.md) y [la verificación de esta versión](docs/fortnite-verification.md).

La distribución incluye la consola x64 oficial PresentMon 2.6.0, cuya procedencia es [la release oficial](https://github.com/GameTechDev/PresentMon/releases/tag/v2.6.0). FlyyOptimizer valida el binario incluido y lo resuelve automáticamente. La captura funciona sin descargar utilidades en el primer uso. ETW puede necesitar permisos del grupo Performance Log Users o administrador de Windows. Si Windows rechaza la captura por permisos y la app no está elevada, aparece **Abrir Flyy como administrador**: el usuario puede abrir otra ventana de Flyy con el diálogo de permisos de Windows, conservando la ventana y sesión actuales.

Si se pierde o altera el binario incluido, extrae nuevamente el paquete completo de FlyyOptimizer. No se sustituye automáticamente por otro ejecutable ni se pide instalar una aplicación externa.

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

Las herramientas de optimización guardan respaldos locales para restaurar ajustes y entradas de inicio. Esos respaldos conservan los valores y comandos originales necesarios para deshacer. Un cambio externo posterior puede impedir la restauración automática para no sobrescribirlo. El análisis y la apertura de la app no aplican ajustes ni limpian archivos; cada acción se inicia desde su control correspondiente.

## Verificación desde código

```powershell
dotnet test FlyyOptimizer.sln
dotnet build FlyyOptimizer.sln -c Release
dotnet run --project src/FlyyOptimizer.App -- --scan "$env:TEMP\flyy-scan.json"
dotnet run --project src/FlyyOptimizer.App -- --smoke-test "$env:TEMP\flyy-smoke"
```

Estos comandos no aplican optimizaciones al equipo. Los modos `--render <png>` y `--smoke-test <directorio>` permiten revisar el render real de WPF. El modo de prueba usa un historial aislado dentro del directorio de salida.

## Referencias

Consulta [la investigación de GitHub, Reddit y Microsoft](docs/research/2026-10-13-referencias.md), [las herramientas integradas](docs/native-tools.md), [el colector incluido](docs/bundled-gaming.md), [el tema y movimiento](docs/apple-theme-motion.md) y [la verificación de esta versión](docs/self-contained-verification.md). Los proyectos usados como inspiración no se ejecutan ni importan como presets.

Las dependencias y avisos están en [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Esta primera versión requiere pruebas adicionales en otros modelos de hardware antes de una distribución amplia.
