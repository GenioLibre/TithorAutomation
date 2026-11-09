# Prueba en Windows / CorelDRAW

Preparación: guardar el master con página `Fundas` (su mockup habitual) y página
`Camisetas` o `Camisetas deportivas`. En esta última, incluir grupos
`clasico_varon_espalda`, `clasico_varon_redondo`, `clasico_varon_v` y las variantes
adicionales disponibles. Los destinos siguen siendo PowerClips nombrados
`frente`, `espalda`, `manga_corta_izquierda` y `manga_corta_derecha`.

1. Analizar y sincronizar. El catálogo y el selector deben contener dos entradas:
   Fundas y Camisetas deportivas. Deben guardarse solo dos vistas genéricas,
   la de camisetas con clásico/varón/redondo y su espalda.
2. Cerrar y abrir Tithor. El catálogo y las opciones deben conservarse.
3. Seleccionar Camisetas deportivas. Debe abrirse el formulario de opciones.
   Elegir clásico/varón/V y preparar: se cargan frente V y espalda y cada grupo
   queda centrado verticalmente en la página, conservando su posición horizontal.
4. En el formulario, elegir un corte/tipo sin variantes: Preparar queda
   deshabilitado. Añadir un cuello nuevo al master, guardar y sincronizar:
   debe aparecer sin editar el código. Dama acepta grupos con `mujer` o `dama`.
5. Cancelar el formulario: no abre documentos ni descarta resultados anteriores.
   Short, tipo de short y medias aparecen deshabilitados, pendientes de implementación.
6. En lote, una capa de origen con frente, espalda y ambas mangas reemplaza
   ambos juegos de mangas del clásico varón V/redondo: alto 196.652 mm y pivote
   10 mm debajo del centro superior. Frente: derecha +294.088°, izquierda
   −294.088°. Espalda: derecha −294.088°, izquierda +294.088°.
7. Aplicar otro diseño y exportar con Guardar todo. La vista genérica de configuración
   no se debe sobrescribir con los diseños aplicados.
8. Cambiar a Fundas, cargar y aplicar: conserva su flujo y ajuste anteriores.
9. Con el master sin guardar, Analizar debe pedir guardarlo; no debe guardarlo por sí solo.
10. Abrir `frmOpcionesMockupCamiseta` en el diseñador de Visual Studio: todos sus
    controles deben verse y poder editarse. Compilar Release x64 y ejecutar las
    pruebas del proyecto tests antes de probar los pasos de CorelDRAW.

Validado fuera de Windows: sintaxis C#, XML del proyecto, migración SQLite
repetible y persistencia de las variantes. Los pasos visuales y COM requieren CorelDRAW.

## Rib de camisetas

Preparar el diseño fuente como rectángulo horizontal llamado `rib` (también
admite PowerClip), junto a las otras piezas de la capa de diseño. En el master,
nombrar las cintas cerradas `rib` o `rib_01`, `rib_02`, etc. Se admite curva cerrada
sin contenido o PowerClip. Los cierres laterales deben ser segmentos rectos
cortos, sin subdividir; los bordes largos pueden tener nodos intermedios. No usar
anillos con dos subtrayectos. Las sombras/costuras estáticas se dejan fuera de
estos PowerClips y por encima: el contenido de cada destino se reemplaza.

1. Guardar el master, analizar/sincronizar y volver a cargar Camisetas. Debe
   aparecer una tarea rib con todos sus destinos. Los masters sin rib mantienen
   su flujo anterior y no exigen esta referencia.
2. Usar una tira de prueba con franjas de colores, números de izquierda a derecha
   y una marca en el borde superior. Aplicar por lote y después individualmente.
   Los tramos deben recibir recortes sucesivos, primero frente y luego espalda;
   dentro de cada vista, de izquierda a derecha. No deben repetirse todos los
   dibujos completos en cada destino ni quedar el borde superior abajo.
3. Comprobar curvas redondas y en V, con distintos nodos iniciales y direcciones
   del contorno. La envolvente debe seguir los bordes originales sin cruzarse.
4. El grosor se obtiene del promedio de ambos cierres laterales, NO de la caja
   envolvente del cuello. El largo se obtiene del promedio de ambos bordes
   curvos. El excedente del rectángulo se recorta simétricamente en sus extremos.
5. Usar un rib demasiado corto, uno rotado y una curva abierta: mostrar error
   antes de modificar destinos. No comprimir/estirar el ancho para cubrirlos.
6. En un error COM durante la envolvente, comprobar el mensaje de etapa/pieza,
   la recuperación con Undo y la posibilidad de volver a cargar/reintentar.
7. Confirmar que el original sigue vectorial/intacto: solo el recorte temporal
   del rib del mockup se rasteriza a 300 ppp para aplicar la envolvente.
8. Vista previa, Guardar todo y Copiar deben incluir toda la página de camisetas,
   incluyendo fondo y rib. Probar Fundas para verificar que no cambia su ajuste.

Pendiente de validación: compilación Windows y resultado visual/COM con el master
real. La geometría requiere una cinta con cierres izquierdo y derecho reconocibles;
si el master contiene otra topología, el mensaje pide corregir el contorno.
