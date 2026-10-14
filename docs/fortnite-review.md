# Revisión del perfil Fortnite

La revisión independiente inicial inspeccionó perfil, snapshot WinUtil, preferencia GPU, navegación interna y lifecycle de WPF. Confirmó validación de selección antes de mutaciones, destinos WinUtil acotados, conservación de tokens GPU y originales, restauración con conflictos y refresh después de cancelar. Su probe detectó que un archivo de solo lectura se ofrecía para editar.

También se identificó durante el desarrollo que el reemplazo POSIX de un destino mantenido abierto sin compartir Delete falla con error 32 en NTFS. El cierre fue realizado por el agente principal: se mantuvo el handle exclusivo para escribir la configuración y se conservó el reemplazo atómico para el journal protegido por lease. La documentación distingue esa escritura de una actualización atómica del archivo.

Verificaciones de las correcciones:

- La detección no ofrece configuración de solo lectura; el perfil muestra su motivo real. Ambas regresiones se observaron fallar antes de corregirse.
- Los casos de edición y restauración que fallaban por error 32 pasan con escritura exclusiva, flush y comprobación de bytes.
- Dos regresiones de interrupción de aplicación/restauración reconocen el mismo archivo y restauran el respaldo original.
- El probe propio de revisión `artifacts/fortnite-review/Review.csproj` confirma que una sustitución del archivo por otra identidad, aun con contenido parecido a una escritura parcial, se conserva y no permite restauración automática. El mismo probe confirma el bloqueo de solo lectura. Salida 0.
- Dos pruebas retienen el lease nativo y pasan a batería antes de liberarlo. Fallaban aplicando potencia/GPU; ahora ambas conservan los originales porque el estado AC se comprueba después del lease y antes de escribir.
- Las comprobaciones WPF de fixtures incluyen navegación real a Inicio, selección manual, opciones no disponibles, error de detección, resultados mixtos, progreso, cancelación parcial y espera de cierre hasta actualizar los respaldos.
- La revisión visual del paquete encontró AppData virtualizado por Windows: se reprodujo la comparación incorrecta con un archivo propio y se corrigió validando contra el parent físico abierto. La nueva regresión y el smoke real confirman que el perfil carga.

La revisión independiente final se interrumpió por el límite de uso de los agentes; no se presenta como una aprobación independiente completa. El agente principal terminó las correcciones, la inspección y las pruebas citadas. Ninguna verificación modificó ajustes reales del PC, abrió UAC o editó la configuración real del juego. Los resultados completos de suite y paquete se documentan en `fortnite-verification.md`.
