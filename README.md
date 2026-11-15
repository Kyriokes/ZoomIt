# Summit

Summit muestra en vivo, dentro de su ventana, la pantalla completa de un monitor que elijas, con zoom.

## Controles

- **Monitor**: qué monitor mostrar.
- **Zoom**: de 1x a 8x. A 1x se ve el monitor entero; al acercar, la imagen llena la ventana.
- **Foco**: hacia dónde se acerca el zoom. Nueve puntos: centro, bordes y esquinas.
- **FPS**: 15, 30, 60, 120 o Máx (el ritmo del monitor). Menos FPS, menos consumo. Al lado se ve el ritmo real.

La configuración se guarda al cerrar en `%AppData%\Summit\settings.json`.

## Requisitos

- Windows 10 versión 2004 o posterior (Windows 11 recomendado).
- [.NET 10](https://dotnet.microsoft.com/download) para compilar y ejecutar.

Los programas en pantalla completa exclusiva pueden verse en negro; en modo ventana sin bordes se ven bien.

## Compilar y ejecutar

```
dotnet run --project src/Summit -c Release
```

Tests:

```
dotnet test
```
