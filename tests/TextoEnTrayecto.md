# Comprobación manual: variables en texto sobre trayecto

Requiere CorelDRAW y un pedido con dos unidades de nombres y números diferentes. Ejecutar sobre un documento de prueba, sin modificar el master.

1. En el PowerClip de diseño, crear una curva llamada `nombre` y ajustar a ella un texto editable sin nombre. Aplicar el diseño a las dos unidades.
   - Cada copia debe mostrar el nombre correspondiente del Excel.
   - El original debe conservar su contenido; no deben aparecer textos adicionales.
2. Repetir con una curva llamada `numero`. Verificar el número correspondiente.
3. Añadir dos textos a la misma curva `nombre`: ambos deben actualizarse.
4. Dejar la curva sin nombre y nombrar el texto `nombre`: debe seguir funcionando.
5. Nombrar la curva `nombre` y su texto `numero`: prevalece la variable de la curva.
6. Probar dentro de grupos anidados y PowerClips, tanto individualmente como por lote.
7. Usar un trayecto con desplazamiento, distancia y orientación personalizados. Verificar que se conserven respecto al diseño escalado y que el texto siga vinculado a su curva.
8. Reaplicar con Reemplazar contenido activado: debe quedar un único diseño aplicado por destino.
9. Probar nombres y números vacíos: el texto variable debe quedar vacío.
10. Verificar regresión: textos rectos `nombre` y `numero` siguen reemplazándose y centrando como antes; textos sobre trayecto sin nombre variable conservan su contenido.

Esta comprobación requiere ejecución en CorelDRAW; no forma parte de las pruebas automáticas sin COM.
