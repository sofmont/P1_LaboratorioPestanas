# Programa 1 — Laboratorio por pestañas con encadenamiento separado

Tarea 6.2 — Implementación y evaluación empírica de búsquedas (Windows Forms, .NET 8, C#).

- Interfaz · arquitectura: Pestañas · capas Core/Experiments/UI
- Tabla hash: División k mod m + encadenamiento separado
- Generador · semilla por defecto: SplitMix64 · 20261103

## Ejecución

1. Windows 10/11 con Visual Studio 2022 (17.8+) y «Desarrollo de escritorio de .NET», o el SDK de .NET 8.
2. Abrir `P1_LaboratorioPestanas.csproj` y pulsar F5, o ejecutar `dotnet run` en esta carpeta.
3. Pestaña «Experimento»: N = 1000, 10000, 100000 y 1000 consultas.

Sin dependencias NuGet ni archivos del diseñador. Con la misma semilla, tamaños y consultas se obtienen exactamente los mismos datos y conteos.
