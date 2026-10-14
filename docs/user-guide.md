# FlyyOptimizer

App de escritorio en español para Windows 11 x64. Analiza el equipo, muestra evidencia de oportunidades de rendimiento, reúne drivers oficiales y aplica ajustes reversibles para Windows y Fortnite.

La interfaz usa superficies claras, tipografía Segoe UI Variable y transiciones nativas al navegar y usar controles. El movimiento respeta la preferencia de animaciones de Windows y el modo de alto contraste.

## Abrir la app

En la distribución portable, extrae el ZIP y abre `FlyyOptimizer.exe`. Solo verás el ejecutable con el logo de Flyy y una carpeta `Archivos` con los recursos, instrucciones y licencias. Conserva ambos al mover o compartir la app.

```text
FlyyOptimizer-win-x64/
├── FlyyOptimizer.exe
└── Archivos/
```

El ejecutable incluye .NET y las bibliotecas necesarias; el usuario no necesita instalar SDK, WinUtil, Optimizer ni utilidades aparte. Las bibliotecas nativas se extraen automáticamente en la caché temporal de .NET al arrancar, sin llenar la carpeta portable. La publicación usa [el formato de archivo único de Microsoft](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview) y conserva el arranque directo y la elevación desde el mismo ejecutable.

Desde el proyecto:

```powershell
.\scripts\build.ps1
.\scripts\run.ps1
```

Para compilar necesitas el SDK .NET 10. El script también reconoce el SDK aislado de Codex en `%USERPROFILE%\.codex\runtimes\dotnet-10-sdk`. El SDK se puede descargar desde [Microsoft](https://dotnet.microsoft.com/download/dotnet/10.0).

## Uso

1. Pulsa **Analizar equipo**. Sigue las etapas y los recursos en vivo mientras se identifica si es portátil o escritorio, el fabricante, el modelo comercial y el SKU. Las lecturas son reales y puedes cancelar.
2. Revisa **Hardware** y **Mejoras**. Además de recomendaciones con evidencia, las herramientas permiten seleccionar un plan de energía existente, gestionar entradas de inicio del usuario y reducir animaciones. Los cambios elegidos guardan su estado anterior para deshacerlos.
3. En **Drivers**, pulsa **Buscar actualizaciones**. Si Windows Update ofrece drivers, aparece **Descargar** y una lista desplegable; si no, aparece un mensaje sin esos controles. Descargar guarda los archivos en Windows Update, sin instalarlos; puede abrir Flyy con permisos de administrador. Para Lenovo, el análisis consulta también el catálogo oficial del modelo/SKU. La fecha de un driver no demuestra que esté desactualizado.
4. Usa la vista previa de temporales para elegir archivos a eliminar y comprobar espacio recuperado/archivos omitidos. Los archivos eliminados no se pueden deshacer. Las guías de Windows siguen disponibles y su historial registra únicamente que se abrieron.
5. Pulsa **Optimizar PC** en Mejoras o Gaming y confirma **Sí**. Flyy detecta Fortnite y aplica automáticamente las preferencias disponibles. Al completar muestra **PC listo**; un fallo o cancelación conserva su indicación y los respaldos. En Mejoras puedes desplegar **Restaurar optimización**. En Gaming puedes elegir los controles de rendimiento y pulsar Aplicar selección.
6. Usa **Historial** para volver a un diagnóstico guardado y exporta un informe JSON o Markdown.

## Rendimiento y Fortnite

**Optimizar PC** aplica los controles disponibles: Modo Juego, grabación Xbox, carga de apps Store/Edge, efectos del escritorio, GPU de Fortnite y calidad competitiva del juego. Con corriente usa el modo Mejor rendimiento de Windows en equipos compatibles con el plan Equilibrado; si no está disponible, puede usar un plan Alto rendimiento ya existente. La energía puede aumentar consumo, ruido y temperatura. Se comprueban los valores aplicados y se guardan los originales para restaurar.

En **Gaming**, personaliza los controles y pulsa **Aplicar selección**. Los ajustes no disponibles explican su requisito. No se incluyen preferencias de publicidad, telemetría ni búsqueda web en este perfil de rendimiento. La app no ofrece captura, importación de CSV ni comparación de métricas y ya no distribuye PresentMon.

La detección de Fortnite usa manifiestos Epic y rutas acotadas. Solo edita claves gráficas existentes del usuario con el juego cerrado; preserva resolución, límite de FPS, renderizador, controles y archivos de anti-cheat. No promete una mejora de FPS sin medirla. [Alcance, referencias y verificación](performance-focused.md).

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

## Actualizar FlyyOptimizer

Desde 1.1.0, al abrir la app se comprueba la última release estable del repositorio oficial. Si existe una versión superior, el portable se descarga automáticamente. La barra lateral muestra el progreso y, cuando el archivo está verificado, ofrece **Reiniciar y actualizar**. Pulsa el botón cuando hayan terminado el análisis y las demás operaciones. La app no reinicia por su cuenta.

Si la consulta o descarga falla, la app sigue disponible y puedes reintentar desde la barra lateral. Si está al día, muestra que no hay actualizaciones disponibles. Las actualizaciones no necesitan una cuenta de GitHub ni tokens.

Para aplicar el paquete usa una sesión normal, sin permisos de administrador, y una carpeta portable donde tengas permiso de escritura. Los informes y respaldos de ajustes no se reemplazan. Un fallo durante el reemplazo intenta restaurar la instalación anterior y muestra el problema; no hay recuperación automática ante un corte eléctrico durante el reemplazo.

**Si tienes 1.0**, descarga y extrae 1.1.0 manualmente una vez desde [las releases oficiales](https://github.com/flyybnjj/flyyoptimizer/releases). Conserva **FlyyOptimizer.exe** y **Archivos** juntos. Las próximas versiones podrán llegar dentro de la app.
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

Después de compilar, `./scripts/verify-portable.ps1` comprueba la estructura de dos elementos, las licencias y el recurso fijado; compara todos los archivos del ZIP con la publicación y ejecuta el ZIP extraído desde una ruta con espacios, sin búsqueda del SDK y con un directorio de trabajo diferente. Verifica el análisis, la interfaz y los contratos de elevación sin aplicar optimizaciones, descargar drivers ni abrir UAC.

## Referencias

Consulta [la investigación de GitHub, Reddit y Microsoft](research/2026-10-13-referencias.md), [las herramientas integradas](native-tools.md), [el colector incluido](bundled-gaming.md), [el tema y movimiento](apple-theme-motion.md) y [la verificación de esta versión](self-contained-verification.md). Los proyectos usados como inspiración no se ejecutan ni importan como presets.

Las dependencias y avisos están en [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md). Esta primera versión requiere pruebas adicionales en otros modelos de hardware antes de una distribución amplia.
