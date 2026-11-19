# Summit

Summit muestra en vivo, dentro de su ventana, la pantalla completa de un monitor que elijas, con zoom.

## Controles

- **Monitor**: qué monitor mostrar.
- **Zoom**: de 1x a 8x. A 1x se ve el monitor entero; al acercar, la imagen llena la ventana.
- **Foco**: hacia dónde se acerca el zoom. Nueve puntos: centro, bordes y esquinas.
- **FPS**: 15, 30, 60, 120 o Máx (el ritmo del monitor). Menos FPS, menos consumo. Al lado se ve cuántos cuadros nuevos llegan por segundo: si la imagen no cambia, no llegan cuadros.

Si elegís el mismo monitor donde está Summit, la ventana se deja afuera de la captura y funciona como lupa de lo que hay detrás. Mientras Summit está minimizado no captura nada. Solo se abre una ventana de Summit a la vez.

La configuración se guarda al cerrar en `%AppData%\Summit\settings.json`.

## Requisitos

- Windows 10 versión 2004 o posterior (Windows 11 recomendado).
- Para ejecutar: [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0).
- Para compilar: .NET SDK 10 (el repo fija la versión en `global.json`).

Los programas en pantalla completa exclusiva pueden verse en negro; en modo ventana sin bordes se ven bien.

## Instalar

```
dotnet publish src/Summit -c Release -o publish
```

Después abrí (o anclá a la barra de tareas) `publish\Summit.exe`.

## Compilar y ejecutar

```
dotnet run --project src/Summit -c Release
```

Los shaders vienen compilados en `src/Summit/Capture/MirrorShaders.cs`. Si cambiás `Mirror.hlsl`, regeneralos con `tools/build-shaders.ps1` (necesita el Windows SDK).

Tests:

```
dotnet test
```
