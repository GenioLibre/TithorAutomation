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

El aplanado rasteriza una copia visible del marco sin contorno. Comprueba una vez que la imagen esté insertada, toma el siguiente hijo actual en cada borrado y comprueba al final que solo quede esa imagen. Las búsquedas de objetos tras errores COM se mantienen, pero no se recorre toda la colección por cada borrado normal. No reutiliza referencias a hijos capturadas antes de rasterizar.

## Preparación y diagnóstico

- La confirmación debe aparecer antes de recorrer todos los objetos. Cancelar no modifica el documento.
- Preparar en un solo recorrido. Con el checkbox activo, no analizar hijos de los PowerClips que se convertirán completos. Incluir un PowerClip con muchos textos, grupos y bitmaps anidados para comprobarlo.
- Con el checkbox desactivado, conservar la conversión de textos y el recorte individual, incluidos límites de PowerClips anidados; grupos externos en orden de hijos a padres.
- No rechazar el pedido completo por una comprobación anticipada de bloqueo de cada hijo: si una operación de Corel falla, informar el objeto y la etapa.
- La barra muestra preparación indeterminada y después avance por operaciones terminadas. La etiqueta debe mostrar nombre/ID y etapa antes de llamadas largas: rasterizar, insertar, recortar, CMYK, 100 ppp, curvas o separar.
- Provocar un error en una operación posterior a la primera: conservar el porcentaje alcanzado y mostrar «Detenido en» con el paso fallido. El log debe incluir paso y contador; el documento se intenta revertir y no se muestra éxito.
- Probar contenido con cientos de hijos para comprobar que la limpieza normal no realiza una búsqueda completa por cada borrado.
- Confirmar que la etiqueta y barra existentes se reutilizan; no se agregan controles en runtime.

No hay mediciones de tiempo ni ejecución COM desde este entorno; validar la reducción de espera con el mismo pedido y la misma computadora.

## Opción desactivada

Comprobar regresión del recorrido existente de textos a curvas, recorte individual de bitmaps, conversión CMYK a 100 ppp y separación de grupos. Esta corrección no cambia ese recorrido.
