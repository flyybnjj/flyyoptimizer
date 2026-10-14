# Interaction and OEM Implementation Plan

> **For agentic workers:** Execute with systematic-debugging and test-driven-development. Independent Windows/OEM, audit and App tasks have separate file ownership.

**Goal:** Corregir todas las herramientas verificables y añadir análisis interactivo con identificación de equipo y soporte OEM específico.

**Architecture:** Core fija contratos; Windows obtiene identidad y catálogo; App muestra etapas, datos y acciones; revisión independiente valida herramientas.

**Tech Stack:** .NET 10/WPF; WMI; HTTP solo a servicios oficiales verificados.

**Spec:** docs/superpowers/specs/2026-10-14-interaction-oem.md.

## Global Constraints

- Mantener la interfaz nativa y datos reales.
- No enviar serie durante análisis básico ni guardarla en informes.
- Serie solo mediante consulta explícita y enmascarada en la UI.
- Catálogo OEM no equivale a ofertas de actualización del equipo instalado.
- Ningún instalador ni ajuste se ejecuta durante las verificaciones.
- Progreso de muestras basado en observación real, nunca datos simulados.

## Review Focus

- UPS en escritorio y chassis desconocido.
- SKU, modelo y serie con placeholders.
- Catalogo oficial vacío, cambiado, lento o sin red.
- Cierre/cancelación durante búsqueda y progreso entregado tarde.
- Exportaciones de un diagnóstico con identidad OEM.

## Tasks

- [x] Auditar todas las herramientas y reproducir errores; informe con evidencia y límites.
- [x] Clasificación e identidad WMI con pruebas RED→GREEN; agregar progreso estructurado sin romper API existente.
- [x] Investigar Lenovo Toolkit y servicios oficiales; implementar resolución por modelo/serie y catálogo con fixtures.
- [x] Refinar interacción: progreso, recursos vivos, equipo detectado, catálogo OEM, selección de proceso y feedback útil.
- [x] Corregir defectos de herramientas detectados, con regresiones.
- [x] Pruebas completas, consulta OEM real, revisión independiente, renders en lote y publicación portable.

## Cierre

214 pruebas correctas y una prueba de red opt-in omitida en la suite normal. Consulta modelo-only y smoke del ejecutable portable correctos; 26 comprobaciones de interacción, diez muestras, 225 dispositivos y 28 paquetes oficiales. Revisión independiente sin hallazgos materiales pendientes. Ver docs/verification.md y docs/review-v2.md para evidencia y límites.
