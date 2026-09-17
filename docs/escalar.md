# Escalar diseños en PowerClip

1. Copiar los moldes desde **Moldes** al documento de producción.
2. Abrir **Escalar** y pulsar **Analizar Documento**. La tabla cuenta piezas por tipo, talla y estado en todas las páginas de la capa `TITHOR_PRODUCCION`.
3. Seleccionar en CorelDRAW un único diseño **agrupado**, separado de los moldes.
4. Elegir Frente, Espalda, Lateral derecho o Lateral izquierdo, y Todas las tallas o S/M/L.
5. Pulsar **Aplicar diseño** y revisar la cantidad de destinos. Se crea una copia por contenedor, con altura igual a la del molde, ancho proporcional y centrado. El original no se modifica. Un diseño estrecho puede dejar parte del ancho sin cubrir; uno ancho se recorta con el PowerClip.

Los nombres reconocidos son `funda_s_frente`, `funda_m_espalda`, `funda_l_lateral_derecho`, etc. Se recorren grupos anidados pero no el contenido de los PowerClips. Una pieza puede ser una curva cerrada, rectángulo, elipse o un grupo que contenga exactamente un PowerClip. Los grupos ambiguos y los objetos bloqueados/ocultos se informan en la tabla; no se omiten silenciosamente al aplicar.

**Reemplazar contenido existente** está desmarcado inicialmente. Si un destino ya tiene contenido, se exige marcarlo y confirmar antes de reemplazarlo. Repetir con esta opción sustituye el diseño anterior, sin apilar copias. El conjunto de cambios queda en un grupo para **Ctrl+Z**. Ante un error se intenta deshacer la operación; si CorelDRAW rechaza también la reversión se informa explícitamente.

Si cambia el documento activo o las piezas después del análisis, hay que revisar el análisis actualizado antes de aplicar. No se cambia la unidad del documento, no se guarda automáticamente y no se modifican variables con datos del Excel en esta versión.

## Implementación

- `frmPrincipal.Escalar.cs`: panel y flujo. El panel se crea al iniciar el formulario; no se edita dentro de `frmPrincipal.Designer.cs`.
- `Servicios/EscaladorPowerClip.cs`: detección, validación y aplicación.
- `tests/EscaladorPowerClipTests.cs`: pruebas de nombres y cálculo proporcional sin CorelDRAW ni SQLite.

El escalado utiliza `anchoOriginal × alturaMolde / alturaOriginal`. Se reafirma el tamaño y centro después de insertar para que el ajuste automático del PowerClip no determine la proporción final. API utilizada: [AddToPowerClip](https://community.coreldraw.com/sdk/api/draw/27/m/shape.addtopowerclip), [SetSize](https://community.coreldraw.com/sdk/api/draw/21.2/m/shape.setsize?lang=cpp) y [CopyToLayer](https://community.coreldraw.com/sdk/api/draw/19/m/shape.copytolayer?lang=cli).

## Pruebas manuales en una copia del documento

- Crear 5 frentes S y 3 M; comprobar conteos y aplicar a Frente / Todas: ocho copias, original intacto.
- Aplicar únicamente a M: las cinco S deben permanecer intactas.
- Comprobar altura, proporción y centro con diseños anchos y estrechos; verificar Ctrl+Z.
- Probar moldes vacíos y con contenido; cancelar la confirmación debe dejar el documento intacto.
- Repetir reemplazando contenido y comprobar que no se acumulan diseños.
- Probar varias páginas, grupos anidados, contenedores bloqueados, selección sin agrupar, documento cambiado y piezas agregadas/eliminadas después del análisis.
- El fallo de una inserción debe revertir también las anteriores; comprobarlo con un documento de prueba.

Las pruebas automatizadas no sustituyen estas comprobaciones de integración con CorelDRAW.
