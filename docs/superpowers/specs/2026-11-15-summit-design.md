# Summit — diseño

## Qué es y para qué

Summit es una app de escritorio para Windows que muestra en vivo, dentro de su
ventana, la pantalla completa de un monitor elegido, con zoom. El nombre es un
juego de palabras con "zoom it".

Uso de origen: tenerla en un segundo monitor para ver ampliado lo que pasa en el
primero. La app es genérica: ni la interfaz ni los textos mencionan un uso
particular.

## Objetivos

- Elegir qué monitor mostrar.
- Zoom variable con una barrita (1x a 8x).
- Punto de foco: grilla de 9 puntos (centro, 4 esquinas, 4 bordes).
- Elegir FPS (15 / 30 / 60 / 120 / máximo del monitor) para regular el consumo.
- Ventana normal que arranca maximizada: queda la barra de título arriba y la
  barra de tareas abajo. No es pantalla completa.
- Lo más liviana posible: poca demora, poco procesador.

## Fuera de alcance

- Arrastrar la imagen con el mouse (descartado: molesto, y agrega peso).
- Pantalla completa, atajos globales, grabación, capturas, superposiciones.
- Cualquier mención a juegos.

## Lo que la haría fracasar

- Que se sienta pesada o con demora visible respecto del monitor original.
- Controles que estorben o se activen sin querer.

## Límites conocidos

- Juegos en pantalla completa exclusiva pueden verse en negro; en ventana sin
  bordes funciona.
- Requiere Windows 10 2004 o superior (Windows 11 para ocultar el borde amarillo
  de captura).

## Caminos considerados

1. **Captura de pantalla de Windows (Windows.Graphics.Capture) + Direct3D 11** —
   elegido. La imagen nunca sale de la placa de video; el zoom es un recorte al
   dibujar. Poca demora y poco procesador, y permite limitar los FPS.
2. **API de la Lupa de Windows** — descartado: no deja controlar los FPS.
3. **Captura por procesador (GDI/BitBlt)** — descartado: lento y pesado.

## Decisiones de construcción (del implementador)

- C# / .NET 10, WinForms para la ventana y la barra de controles.
- Vortice.Windows para Direct3D 11 / DXGI. Los shaders se compilan de antemano
  (`tools/build-shaders.ps1`), no en tiempo de ejecución.
- Barra de controles: una tira fina arriba de la imagen con monitor, zoom, foco
  y FPS.
- A 1x la imagen se muestra entera sin deformar (el resto queda en negro). Al
  acercar, primero llena esas franjas y después recorta hacia el foco.
- Al lado del selector de FPS se muestra el ritmo real medido.
- Se muestra el cursor del mouse dentro de la imagen.
- Si el monitor elegido es el mismo donde está Summit, la ventana se excluye de
  la captura (`SetWindowDisplayAffinity`): funciona como lupa de lo que hay
  detrás en vez de un espejo infinito. En otro monitor sigue siendo visible
  para capturas y transmisiones.
- La configuración se guarda al cerrar en
  `%AppData%\Summit\settings.json` y se restaura al abrir.
- Límite de FPS: se descartan los cuadros que llegan antes de tiempo; solo se
  dibuja cuando hay un cuadro nuevo o cambia un control. Donde Windows lo
  permite, además se le pide a la captura el doble del ritmo elegido (pedirle
  el ritmo exacto entregaba de menos: 60 pedidos daban 40).
- Si el monitor elegido se desconecta se muestra otro, pero se recuerda el
  elegido para volver a él cuando reaparezca.
- Minimizada, Summit detiene la captura; al restaurarla, la retoma.
- Si la placa de video se reinicia, Summit se recupera sola (revisa una vez por
  segundo cuando no llegan cuadros). Un monitor que falla al cambiar la
  configuración de pantalla no abre diálogos: se muestra "Sin señal" y se
  reintenta.
- Una sola instancia: abrirla de nuevo trae la ventana existente.
