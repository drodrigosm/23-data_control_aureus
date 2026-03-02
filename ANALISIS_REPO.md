# Análisis rápido del repositorio `22-data_control_aureus`

Este documento resume qué hace el proyecto y para qué sirve.

## Qué es

`AureusControl` es una aplicación de escritorio WinUI 3 (Windows/.NET 8) orientada a **localizar y visualizar archivos de ejecución de bots** (CSV, LOG, configuración JSON y JSONL de entradas) dentro de una carpeta compartida con estructura por máquina.

## Para qué sirve

- Buscar por `bot_id` los archivos asociados en `Z:\02-DATA_RUNNING`.
- Mostrar en qué máquina se encontraron y las rutas de los archivos relevantes.
- Preparar visualización paginada de archivos grandes sin cargarlos completos en memoria.

## Flujo principal

1. Usuario escribe un `bot_id` y pulsa **Cargar**.
2. `BotFileLocatorService` recorre cada máquina y sus carpetas:
   - `logs` → `*.csv` y `*.log` con prefijo `<bot_id>_`
   - `config_running` → `*_stock_aureus_config.json`
   - `analyzer_logs` → `*_entries_*.jsonl`
3. Devuelve las rutas encontradas y la máquina.
4. La ventana principal imprime resultados en pantalla.

## Módulo de archivos grandes

Incluye infraestructura para abrir ficheros grandes por páginas:

- indexado de offsets por páginas de líneas,
- lectura por `Seek` + lote de líneas,
- caché LRU de páginas,
- parser CSV con delimitador automático,
- parser JSONL a diccionario plano.

Actualmente la UI principal no navega todavía a la página de visualización avanzada, pero la base técnica está implementada.
