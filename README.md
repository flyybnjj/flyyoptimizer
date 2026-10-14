# FlyyOptimizer

**Analiza tu PC, encuentra drivers oficiales y aplica ajustes de rendimiento para Windows y Fortnite desde una sola app.**

Aplicación de escritorio en español para **Windows 11 x64**, desarrollada en **C# y WPF sobre .NET 10**. La distribución portable incluye el runtime: los usuarios no necesitan instalar .NET, Optimizer ni WinUtil por separado.

## Descargar

**[Descargar FlyyOptimizer 1.0.1 portable para Windows x64](https://github.com/flyybnjj/flyyoptimizer/releases/download/v1.0.1/FlyyOptimizer-win-x64.zip)**

Extrae todo el ZIP y abre **FlyyOptimizer.exe**. Conserva la carpeta **Archivos** junto al ejecutable. También puedes consultar [las releases](https://github.com/flyybnjj/flyyoptimizer/releases).

## Funciones

| Área | Qué ofrece |
| --- | --- |
| Diagnóstico | CPU, GPU, RAM, almacenamiento, procesos, inicio, red y drivers instalados. |
| Identificación | Distingue portátil y escritorio e identifica fabricante, modelo y SKU. |
| Drivers | Busca ofertas de Windows Update y permite descargarlas dentro de la app. Consulta el catálogo oficial del modelo en Lenovo. |
| Optimizar PC | Aplica los ajustes disponibles tras confirmar, verifica los cambios y guarda respaldos. |
| Gaming | Permite elegir ajustes de Windows y detectar la instalación de Fortnite. |
| Restauración | Recupera ajustes respaldados cuando su estado actual permite restaurarlos. |
| Historial | Conserva diagnósticos locales y exporta informes JSON o Markdown. |

## Cómo usarlo

1. Extrae todo el ZIP portable y abre **FlyyOptimizer.exe**.
2. Pulsa **Analizar equipo** para identificar el hardware y las oportunidades de mejora.
3. En **Drivers**, pulsa **Buscar actualizaciones**. Si hay ofertas, aparecen **Descargar** y la lista desplegable; si no, verás que no hay actualizaciones disponibles.
4. Pulsa **Optimizar PC** y confirma **Sí, optimizar**. La app aplica las opciones disponibles y muestra el resultado.
5. Para elegir ajustes concretos, abre **Gaming** y pulsa **Aplicar selección**. Puedes restaurar los ajustes respaldados desde **Mejoras**.

La descarga de drivers guarda los paquetes en la caché de Windows Update; la instalación se realiza mediante Windows Update o el proveedor oficial. Algunas acciones requieren permisos de administrador y abren la propia app con esos permisos.

## Portable ordenado

```text
FlyyOptimizer-win-x64/
├── FlyyOptimizer.exe
└── Archivos/
    ├── README.md
    ├── THIRD-PARTY-NOTICES.md
    ├── licenses/
    └── tools/
```

El ejecutable integra .NET y las bibliotecas de la aplicación. **Archivos** reúne los recursos y las licencias. Conserva ambos al mover o compartir la app.

## Ajustes de rendimiento

Según la compatibilidad del equipo, FlyyOptimizer permite:

- Activar Modo Juego y reducir la grabación y la actividad de Xbox Game Bar.
- Reducir aplicaciones y procesos en segundo plano de Store y Edge.
- Reducir animaciones, transparencias y otros efectos del escritorio.
- Usar **Mejor rendimiento** con corriente, o un plan **Alto rendimiento** ya existente cuando corresponda.
- Asignar la GPU de alto rendimiento a Fortnite en equipos compatibles.
- Aplicar calidad competitiva, desactivar VSync y desenfoque de movimiento en la configuración existente de Fortnite.

Los cambios se comprueban y guardan su estado anterior. Fortnite debe estar cerrado para editar su configuración; se preservan resolución, límite de FPS, renderizador y controles. No se modifican archivos de anti-cheat. El efecto de cada ajuste depende del hardware y de la carga del equipo; no se promete una cantidad de FPS.

Desde **1.0.1**, los ajustes de registro restringidos por Windows, como las políticas de Edge, se comprueban antes de seleccionarlos. Si faltan permisos, aparecen desactivados en Gaming con su explicación; los demás ajustes disponibles se pueden aplicar. [Corrección y verificación](docs/edge-permissions-fix.md).

## Compilar desde el código

Requisitos de desarrollo: **Windows 11 x64**, **Git** y **SDK .NET 10**.

```powershell
git clone https://github.com/flyybnjj/flyyoptimizer.git
cd flyyoptimizer
.\scripts\build.ps1
.\scripts\run.ps1
```

El script ejecuta las pruebas, compila y genera:

```text
artifacts/FlyyOptimizer-win-x64/
artifacts/FlyyOptimizer-win-x64.zip
```

El SDK se necesita para desarrollar y compilar; el runtime viene integrado en la distribución para usuarios. Los archivos de `artifacts/` quedan fuera del repositorio.

## Verificar

```powershell
dotnet test FlyyOptimizer.sln -c Release
.\scripts\verify-portable.ps1
```

La verificación portable compara los archivos del ZIP con la publicación y ejecuta la copia extraída desde una ruta con espacios, sin buscar el SDK. Comprueba el análisis, la interfaz y los contratos de elevación sin aplicar optimizaciones ni descargar drivers.

## Datos y compatibilidad

Los diagnósticos y respaldos se guardan localmente en `%LocalAppData%\FlyyOptimizer`. La consulta por modelo no envía la serie del equipo; la búsqueda por serie es una acción explícita y la serie no se guarda en los informes.

Las temperaturas dependen de los sensores accesibles. El análisis no instala controladores de sensores. La consulta de Windows Update respeta las políticas del sistema; el catálogo Lenovo muestra paquetes del modelo y no determina por sí solo que un driver instalado esté desactualizado. Para otros fabricantes se ofrece su portal oficial cuando no hay un catálogo integrado verificado.

La limpieza de archivos temporales no se puede deshacer. Los ajustes de energía pueden aumentar consumo, ruido y temperatura. Esta versión necesita validación en más modelos de hardware.

## Documentación y referencias

- [Guía detallada de uso](docs/user-guide.md)
- [Controles de rendimiento y Fortnite](docs/performance-focused.md)
- [Herramientas nativas y restauración](docs/native-tools.md)
- [Interfaz y animaciones](docs/apple-theme-motion.md)
- [Investigación de GitHub, Reddit y Microsoft](docs/research/2026-10-13-referencias.md)
- [Dependencias, atribuciones y licencias de terceros](THIRD-PARTY-NOTICES.md)

La selección de ajustes toma como referencia **Optimizer**, **Chris Titus Tech WinUtil** y la documentación de Microsoft. FlyyOptimizer implementa sus propios controles nativos; la selección fijada de WinUtil y sus atribuciones se incluyen en el proyecto.
