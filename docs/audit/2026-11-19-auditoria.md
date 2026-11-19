# Auditoría de Summit — 2026-11-19

Seis auditores independientes (errores, seguridad, rendimiento de CPU y memoria,
rendimiento de GPU y latencia, producto, mantenimiento) revisaron el código.
Cada hallazgo pasó por dos verificadores: uno intentó refutarlo y otro juzgó su
impacto real. De 39 hallazgos, 34 se confirmaron. Además se midió la app real.

## Mediciones (Release, GTX 1070, monitor 1 a 75 Hz, monitor 2 a 59 Hz)

| FPS elegido | Cuadros mostrados | CPU de Summit | GPU de Summit | GPU de Windows (DWM) |
|---|---|---|---|---|
| 15 | 15 | 0,7 % de un núcleo | 0,2 % | 1,2 % |
| 30 | 30 | 2,2 % | 0,9 % | 3,8 % |
| 60 | 60 | 3,2 % | 1,7 % | 4,6 % |
| 120 | 60 | 3,0 % | 1,7 % | 6,4 % |

- La mayor parte del costo en la placa de video lo pone Windows al capturar, no
  el dibujo de Summit. Bajar los FPS es lo que más ahorra.
- Memoria: unos 110 MB. Arranque: la ventana aparece en 0,3–1,6 s.
- Sin vulnerabilidades conocidas en los paquetes; están al día.

## Aplicado

**Errores**
- Minimizada, Summit seguía capturando y procesando 38–66 cuadros por segundo.
  Ahora se detiene (medido: 0 cuadros y 0,4 % de un núcleo) y retoma al volver.
- Si la placa de video se reiniciaba y la primera recuperación fallaba, la vista
  quedaba congelada hasta tocar un control. Ahora Summit revisa una vez por
  segundo cuando no llegan cuadros y se recupera sola, espaciando los intentos.
- Un monitor que dejaba de responder durante la recuperación hacía descartar la
  placa de video y recrearla en cada repintado. Ahora se conserva.
- Errores en el repintado o al cambiar de tamaño podían mostrar el diálogo de
  "excepción no controlada" de .NET. Ahora reinician la imagen en silencio.
- Al minimizar Summit desde otro programa, cortar la captura dentro del aviso de
  minimizado fallaba (error de COM) y abría ese diálogo. Encontrado en las
  pruebas de esta auditoría; la pausa ahora se hace después del aviso.
- Mostrar el mismo monitor donde está Summit creaba un espejo infinito que
  forzaba a dibujar sin parar. Ahora la ventana se excluye de la captura y
  funciona como lupa de lo que hay detrás (en otro monitor sigue visible para
  capturas y transmisiones).
- Un fallo al reconectar un monitor abría un diálogo, a veces en medio del uso.
  Ahora muestra "Sin señal" y reintenta; el diálogo queda solo para elecciones
  hechas a mano.
- El limitador de FPS usaba la hora de llegada de cada cuadro, que tiene
  variaciones. Ahora usa la marca de tiempo del propio cuadro; tests nuevos
  simulan monitores de 59,94 / 60 / 75 / 120 / 144 Hz.
- Una segunda Summit duplicaba el consumo y pisaba la configuración. Ahora
  abrirla de nuevo trae la ventana existente.
- La rueda del mouse sobre una lista cerrada cambiaba el monitor o los FPS sin
  querer. Ya no; además el foco inicial queda en el zoom.
- La barra de controles no escalaba en pantallas al 125–200 %. Ahora escala.

**Seguridad**
- Las librerías nativas se buscaban primero en la carpeta de Summit (riesgo si
  se ejecuta desde Descargas con un DLL plantado). Ahora solo desde System32.
- Los shaders ya no se compilan al arrancar: vienen compilados
  (`tools/build-shaders.ps1`). Se quitó la dependencia `Vortice.D3DCompiler` y
  la carga de `d3dcompiler_47.dll`.
- La configuración se guarda de forma atómica (archivo temporal y reemplazo), y
  un archivo de configuración enorme ya no puede impedir el arranque.

**Compilación y documentación**
- Dos advertencias CA1416 corregidas; ahora usar una API más nueva que el
  Windows mínimo sin protegerla es un error de compilación.
- `global.json` fija el SDK 10 (había un SDK 11 instalado que se usaba solo).
- README: cómo instalar con `dotnet publish`, requisitos de ejecución.
- Código y paquetes sin uso eliminados; plan y spec actualizados.

## Aplicado después, a pedido

- **Imagen nítida con zoom alto**: cada píxel se dibuja como un bloque definido
  y solo el borde se suaviza un píxel de pantalla. Costo casi nulo.

## Propuestas para decidir (cambian cómo se ve o se usa)

- **Zoom con la rueda del mouse sobre la imagen** (±0,5 por paso).
- **Nombre del monitor**: marcar "(esta ventana)" en la lista.
- **Texto de ayuda** al pasar el mouse por el contador de fps.
- **Topar los FPS al ritmo del monitor donde está Summit**: en tu equipo 120 y
  Máx muestran lo mismo que 60, porque el monitor 2 va a 59 Hz.

## No aplicado (poco valor por ahora)

- Ajustar el recolector de memoria de .NET para bajar ~10 MB: el ahorro no está
  comprobado en esta máquina.
- Identificar los monitores por algo más estable que `\\.\DISPLAYn`: si Windows
  los renumera, en el peor caso Summit muestra su propio monitor, que ahora
  funciona como lupa en vez de espejo infinito.

## Descartado tras verificar

- El borde amarillo de captura no necesita permiso extra en apps de escritorio.
- No hay bloqueo al cerrar la captura mientras llega un cuadro.
- La cola de cuadros de la placa no agrega demora visible en este uso.
- Prioridad de GPU: no se puede subir sin permisos de administrador.
- Monitores HDR, firma del ejecutable, ARM64, archivo de bloqueo de paquetes:
  correctos o sin impacto para uso personal.
