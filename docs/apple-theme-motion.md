# Tema claro y movimiento nativo

El usuario pidió cambiar la estética a Apple, mejorar las letras y añadir animaciones en todas las secciones, sin instalar componentes adicionales. La aplicación conserva WPF: superficies blancas, un lienzo gris claro, separación amplia, bordes finos y controles redondeados. La selección azul identifica el destino actual. El tema conserva los nombres de recursos existentes para que todas las vistas usen el mismo lenguaje visual.

## Tipografía y color

Se usan `Segoe UI Variable Text` para texto y `Segoe UI Variable Display` para títulos y cifras, con `Segoe UI` como alternativa instalada de Windows. No se incluyen fuentes SF de Apple ni se descargan fuentes. Las cifras usan alineación tabular donde ayuda a comparar valores; las cifras observadas no se interpolan ni se animan.

| Recurso | Color | Uso |
| --- | --- | --- |
| Ink | `#F5F5F7` | Lienzo |
| Panel | `#FFFFFF` | Superficies y controles |
| Raised | `#F0F1F4` | Encabezados y estados secundarios |
| Sidebar | `#ECEEF2` | Navegación |
| Chrome | `#F9F9FB` | Barra de estado |
| Line | `#D8DDE5` | Separadores |
| Text | `#1D1D1F` | Texto principal |
| Muted | `#60656F` | Texto secundario |
| Accent | `#0062CC` | Acción principal y selección |
| Selection | `#DCEBFC` | Fondo de selección |
| Warning | `#9A5700` | Avisos sobre fondo claro |
| MemoryAccent | `#5357B3` | Traza de memoria |
| GpuAccent | `#087C73` | Traza de GPU |

Las tarjetas tienen un radio de 14 px y relleno de 24 px. Botones, campos, menús y selecciones usan radios de 7–9 px. Los controles mantienen foco de teclado visible, estados deshabilitados y texto seleccionable. Las tablas conservan virtualización, filas de 42 px y separadores suaves. La plantilla de hallazgos comunica severidad mediante texto, sin la barra lateral decorativa anterior.

## Movimiento

La secuencia principal es la continuidad de navegación: al cambiar una vista, toda su superficie pasa de opacidad 0,68 a 1 en 160 ms. Esto incluye las tarjetas de la vista sin aplicar una entrada individual repetida. Los controles responden al puntero y al teclado: capas de selección/hover en 120 ms, presión de botones y navegación a escala 0,975 en 90 ms, y recuperación a tamaño normal en 140 ms. Los campos y menús mantienen un cambio de borde inmediato para explicar el foco.

`Motion.cs` proporciona estas propiedades adjuntas:

```xml
local:Motion.TransitionKey="{Binding SelectedPage}"
local:Motion.IsEnabled="True"
local:Motion.Reveal="{Binding IsSelected, RelativeSource={RelativeSource TemplatedParent}}"
```

El tema aplica automáticamente la respuesta a botones y navegación; `TransitionKey` se integra en el contenedor de páginas. `Reveal` se usa en capas decorativas, nunca oculta el contenido principal por defecto. `Motion.SettleMilliseconds` es 240, por encima de la animación más larga, para las capturas de verificación.

No hay animaciones periódicas en reposo, desenfoques, sombras animadas, movimiento de las gráficas ni contadores ficticios. Se usa `CubicEase` con salida suave y animaciones nativas de WPF; no hay dependencia web ni biblioteca externa. Cada interacción reemplaza el reloj anterior. Al completar se retira el reloj y se fija el estado final, conservando bindings. El callback comprueba la identidad del reloj para que un evento antiguo no retire una transición nueva. Deshabilitar o descargar un control presionado limpia los relojes y restaura escala 1.

Antes de crear cada animación se comprueba `SystemParameters.ClientAreaAnimation` y el modo de alto contraste. Cuando Windows reduce animaciones se aplica directamente el estado final. La comprobación de preferencia usa la misma configuración nativa que el control de animaciones de Mejoras.

## Verificación y entrega

La compilación Release de la aplicación terminó con cero errores y cero advertencias. Un probe WPF con ventana oculta y recursos reales verificó 16 contratos: plantillas, visibilidad inicial, respuesta a presión, navegación interrumpida, selección rápida, conservación de transforms/bindings y limpieza al deshabilitar/descargar. Resultado: `artifacts/apple-theme-motion/motion-checks.json`, `success: true`, salida 0. En este equipo Windows tenía las animaciones activadas; no se cambió su preferencia durante el probe. La rama de movimiento reducido se comprobó en el código, sin mutar los ajustes del equipo.

La verificación visual se entrega al dueño de la interfaz: una tanda conjunta de todas las vistas a 1240×820 y 1000×680, una sola corrección conjunta si corresponde y una segunda tanda de confirmación. No se inició una revisión visual independiente en este trabajo.

Referencia técnica: [animación y limpieza de relojes en WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/animation-tips-and-tricks).
