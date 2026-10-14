# Third-party notices

FlyyOptimizer utiliza bibliotecas .NET y componentes publicados por sus respectivos autores. Se conservan los avisos y licencias del paquete publicado en la distribución. Las versiones concretas quedan fijadas en los proyectos y en los archivos de dependencias de NuGet.

## LibreHardwareMonitorLib

- Proyecto: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
- Paquete NuGet: LibreHardwareMonitorLib 0.9.6.
- Licencia principal: Mozilla Public License 2.0 (MPL-2.0).
- El proyecto contiene partes con licencias adicionales: consultar LICENSE y THIRD-PARTY-NOTICES.txt del repositorio y los avisos del paquete.
- Código fuente y licencia: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LICENSE
- No se modificó el código de esta biblioteca.

## System.Management y .NET

- System.Management: https://github.com/dotnet/runtime
- Licencia: MIT, con avisos de terceros de Microsoft.
- La distribución portable incluye runtime .NET y Windows Desktop de Microsoft. Sus archivos LICENSE y ThirdPartyNotices acompañan los componentes publicados.

## Dependencias transitivas de sensores

Los avisos originales y metadatos de los paquetes se incluyen en `licenses/`. El archivo `.deps.json` registra las dependencias de la distribución.

| Componente | Versión | Licencia | Fuente |
| --- | --- | --- | --- |
| BlackSharp.Core | 1.0.7 | MPL-2.0 | https://github.com/Blacktempel/BlackSharp |
| DiskInfoToolkit | 1.1.2 | MPL-2.0 | https://github.com/Blacktempel/DiskInfoToolkit |
| RAMSPDToolkit-NDD | 1.4.2 | MPL-2.0 | https://github.com/Blacktempel/RAMSPDToolkit |
| HidSharp | 2.6.4 | Apache-2.0 | LICENSE.txt incluido en licenses/HidSharp-2.6.4 |
| Mono.Posix.NETStandard | 1.0.0 | Avisos del paquete y Microsoft/Mono | https://www.nuget.org/packages/Mono.Posix.NETStandard/1.0.0 |
| System.Management | 10.0.13 | MIT | https://github.com/dotnet/dotnet |
| System.IO.Ports | 10.0.3 | MIT | https://github.com/dotnet/dotnet |
| System.CodeDom | 10.0.13 | MIT | https://github.com/dotnet/dotnet |
| System.IO.FileSystem.AccessControl | 5.0.0 | MIT | https://github.com/dotnet/runtime |

No se modificó el código de estas dependencias. Los metadatos `.nuspec` incluidos identifican también las revisiones de código fuente de los paquetes cuando el autor las proporciona.

## PresentMon (opcional, seleccionado por el usuario)

- Proyecto: https://github.com/GameTechDev/PresentMon
- Licencia principal: MIT; consultar también los componentes de terceros del proyecto.
- La app invoca un ejecutable instalado o seleccionado por el usuario y admite su CSV. No incluye un binario ni código de PresentMon.

## Referencias de diseño

UniGetUI (MIT) y Winutil (MIT) se consultaron como referencias. No se incluyó su código ni se ejecutaron sus scripts. Optimizer (GPL-3.0) fue una referencia histórica; no se incluyó su código.

## Herramientas de desarrollo

xUnit, xunit.runner.visualstudio y Microsoft.NET.Test.Sdk se usan para pruebas y no se incluyen en la aplicación publicada. Sus avisos están disponibles en los paquetes NuGet y repositorios oficiales.
