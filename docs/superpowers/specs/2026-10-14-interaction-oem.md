# Corrección de herramientas e identificación OEM

El usuario solicitó revisar todas las funciones por fallos reportados, mejorar la interacción y detectar portátil/escritorio para localizar drivers oficiales por modelo o serie, con Lenovo Toolkit como referencia. Autorizó revisar todas las herramientas sin aportar un caso específico.

Se conserva la interfaz WPF y la identidad visual existente. El análisis incorpora progreso por etapas y muestras en vivo reales; al finalizar muestra tipo de equipo, fabricante, nombre comercial, modelo y SKU. La clasificación usa chassis/PCSystemType; una batería UPS no basta para declarar portátil.

Los paquetes oficiales de modelo se presentan aparte de las ofertas de Windows Update. Un paquete de un catálogo OEM no prueba que sea una actualización del dispositivo instalado. Se muestran nombre, categoría, versión, fecha, sistema compatible y fuente. El botón abre una descarga oficial, sin ejecutar instaladores.

Lenovo se investiga mediante código de Lenovo Legion Toolkit y fuentes Lenovo oficiales. La búsqueda comienza por modelo/SKU. La búsqueda por serie es una acción explícita; la serie se lee transitoriamente, se muestra enmascarada y no se incluye en snapshots, historial ni exportaciones. Los servicios de fabricante solo reciben datos necesarios para la búsqueda autorizada.

Otros fabricantes ofrecen su portal oficial y explicación cuando no existe un catálogo programático verificado. No se inventa un resultado específico de modelo. No se ejecutan cambios del PC durante pruebas.

La revisión cubre scan/cancelación, Windows Update, todas las guías, fabricantes, filtros, importación/captura gaming, cierre, historial y exportación. Las correcciones se basan en fallos reproducibles; las funciones externas se distinguen de acciones realizadas por la propia app.

Contratos: MachineInfo añade ProductName, SystemSku, DeviceKind y DeviceTypeEvidence. ScanProgress transporta etapa, paso y muestra real. OemSupportResult incluye estado, fabricante, nombre del producto, URL oficial, paquetes, mensaje, coincidencia de modelo y serie enmascarada. No incluye la serie cruda.

Verificación: regresiones por fallo encontrado; clasificación laptop/desktop/UPS/desconocido; modelos OEM y placeholders; parser de catálogo con fixtures primarios; cancelación/red no disponible; exportación sin serie; una consulta real de modelo OEM; pruebas de flujo WPF y un lote de renders en los dos tamaños admitidos, con una ronda de corrección y confirmación como máximo.
