# Rib: escalado simple vigente

El rib de camisetas usa el marco completo de la referencia. Se escala proporcionalmente hasta cubrir ancho y alto del PowerClip destino, se centra en ambos ejes y se recorta el sobrante. Cada destino recibe una copia independiente. No se aplican envolventes, rasterización, reparto de tramos ni recorte previo de la mitad inferior.

Prueba Windows/Corel: cargar un mockup con rib_01 y rib_02; aplicar rib en modo individual y por lote. Verificar cobertura completa, centrado, proporciones y ausencia de errores de envolvente. Repetir con destino más ancho y más alto que la referencia. Las mangas, frente, espalda y Fundas conservan su comportamiento. El master original no debe guardarse.

Las secciones anteriores de MockupsPorPagina.md y el test de matrices corresponden al mecanismo de deformación anterior, que ya no se utiliza en este flujo.
