# Finalizar: contenido de PowerClips

Validación manual pendiente en Windows/CorelDRAW 2026. Trabajar con una copia del pedido.

## Convertir todo a mapa de bits activado

- Crear en TITHOR_PRODUCCION varios PowerClips con textos, bitmaps y grupos; incluir texto en trayecto y efectos con objetos asociados.
- Finalizar y confirmar. Cada PowerClip debe conservar su contorno y contener una única imagen llamada TITHOR_CONTENIDO_FINAL, en CMYK a 100 ppp.
- Comprobar posición, tamaño, orden y apariencia; incluir imágenes mayores que el marco y PowerClips anidados.
- Repetir con el caso que registraba «El objeto de referencia ya no existe» en AplanarContenidoFinal/Delete.
- Verificar que los objetos de otras capas y páginas permanezcan intactos.
- Ctrl+Z debe restaurar el contenido y la agrupación originales.
- Ante un error, comprobar la reversión; el log debe identificar el PowerClip, la etapa y el HRESULT. No debe mostrarse éxito ni limpiarse el formulario.
- Si Corel elimina un objeto asociado durante la limpieza, aceptar su ausencia solo si la imagen nueva continúa dentro del PowerClip. No ocultar otros fallos COM.

El aplanado rasteriza una copia visible del marco sin contorno. Tras insertar la imagen, vuelve a consultar la colección de hijos antes de cada borrado y comprueba que la imagen esté presente. No reutiliza referencias a hijos capturadas antes de rasterizar. El resultado debe ser un PowerClip con un único bitmap.

## Opción desactivada

Comprobar regresión del recorrido existente de textos a curvas, recorte individual de bitmaps, conversión CMYK a 100 ppp y separación de grupos. Esta corrección no cambia ese recorrido.
