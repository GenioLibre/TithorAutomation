# Finalizar: recortar mapas de bits

Probar en Windows/CorelDRAW 2026 con una copia del pedido. Finalizar pide confirmación y procesa TITHOR_PRODUCCION de la página activa.

- Bitmap grande dentro de PowerClip curvo: eliminar excedente fuera del rectángulo envolvente; el PowerClip conserva el recorte curvo.
- Bitmap con recorte previo: conservar su máscara intersectándola con el nuevo límite.
- Bitmap rotado, agrupado y PowerClip anidado: conservar posición, escala y orden visual; usar intersección de límites ancestrales.
- Bitmap pequeño o parcialmente visible: no ampliar ni mover; bitmap fuera de PowerClip no se recorta.
- Textos y vectores: conservar apariencia, convertir textos y desagrupar como antes.
- Ctrl+Z debe revertir el proceso completo. Un error debe revertir cambios y generar log.

Se utiliza Bitmap.CropEnvelope y Bitmap.Crop, sin volver a rasterizar ni fijar resolución. Pendiente validar compilación Windows y comportamiento COM real antes de usar en pedidos finales.
