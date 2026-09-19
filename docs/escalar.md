# Escalar diseños en PowerClip

1. Copiar los moldes desde **Moldes** al documento de producción.
2. Abrir **Escalar** y pulsar **Analizar Documento** con el pedido activo. La cola agrupa los destinos del pedido por diseño y pieza en las capas `TITHOR_PRODUCCION`.
3. Seleccionar en CorelDRAW un único diseño agrupado o una plantilla PowerClip con contenido, separado de los moldes.
4. Elegir la tarea de diseño y pieza que se desea aplicar.
5. Pulsar **Aplicar diseño**. Se crea una copia por destino pendiente, manteniendo la proporción y cubriendo el ancho y alto del molde. Los textos llamados `Nombre` y `Numero` se sustituyen con los valores de la fila correspondiente del Excel.

Los destinos se buscan por el nombre del grupo y los nombres de piezas del plan. Una pieza puede ser una curva cerrada, rectángulo, elipse o un grupo que contenga exactamente un PowerClip. Los nombres de producción duplicados se rechazan explícitamente. Para copiar nuevamente un pedido, use otro documento o deshaga la copia anterior; la comprobación se realiza antes de modificar el documento.

**Reemplazar contenido existente** está desmarcado inicialmente. Sin marcarlo, se procesan únicamente los destinos vacíos de la tarea. Al marcarlo se sustituye el contenido de todos sus destinos. El conjunto de cambios queda en un grupo para **Ctrl+Z**. Ante un error se intenta deshacer la operación, incluso si falla la extracción de la primera plantilla después de duplicarla; si CorelDRAW rechaza también la reversión se informa explícitamente.

Si cambia el documento activo o las piezas después del análisis, hay que revisar el análisis actualizado antes de aplicar. No se cambia la unidad del documento ni se guarda automáticamente. En Acomodar se vuelve a analizar la página activa al ejecutar la operación y se aplican sus piezas y medidas actuales, sin comparar identidades COM de páginas. Una página vacía, sin capa de producción o con piezas que no caben se rechaza antes de modificar el documento.

## Implementación

- `frmPrincipal.Escalar.cs`: flujo y cola de tareas; los controles están en `frmPrincipal.Designer.cs`.
- `Servicios/EscaladorPowerClip.cs`: detección, validación y aplicación.
- `tests/EscaladorPowerClipTests.cs`: pruebas de nombres y cálculo proporcional sin CorelDRAW ni SQLite.

El factor de cobertura es `max(anchoMolde / anchoOriginal, altoMolde / altoOriginal)`. Se reafirma el tamaño y centro después de insertar. En plantillas PowerClip se calcula el factor a partir del marco y se aplica al contenido extraído. Los estados anteriores de eventos y optimización se restauran al terminar.

## Pruebas manuales en una copia del documento

- Crear un pedido con 5 frentes S y 3 M del mismo diseño; comprobar que su tarea tenga ocho destinos, original intacto.
- Usar filas con nombres y números distintos; comprobar la personalización individual.
- Comprobar altura, proporción y centro con diseños anchos y estrechos; verificar Ctrl+Z.
- Probar moldes vacíos y con contenido; sin Reemplazar, solo deben procesarse los vacíos.
- Repetir reemplazando contenido y comprobar que no se acumulan diseños.
- Probar varias páginas, grupos anidados, contenedores bloqueados, selección sin agrupar, documento cambiado y piezas agregadas/eliminadas después del análisis.
- El fallo de una inserción debe revertir también las anteriores; comprobarlo con un documento de prueba.

Las pruebas automatizadas no sustituyen estas comprobaciones de integración con CorelDRAW. Consulte [ejecución y resultados de pruebas](../tests/README.md).
