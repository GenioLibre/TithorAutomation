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
   ambos juegos de mangas. La regla 196.652 mm / 294.088° afecta únicamente a
   manga derecha del frente clásico varón V/redondo, no a la espalda.
7. Aplicar otro diseño y exportar con Guardar todo. La vista genérica de configuración
   no se debe sobrescribir con los diseños aplicados.
8. Cambiar a Fundas, cargar y aplicar: conserva su flujo y ajuste anteriores.
9. Con el master sin guardar, Analizar debe pedir guardarlo; no debe guardarlo por sí solo.
10. Abrir `frmOpcionesMockupCamiseta` en el diseñador de Visual Studio: todos sus
    controles deben verse y poder editarse. Compilar Release x64 y ejecutar las
    pruebas del proyecto tests antes de probar los pasos de CorelDRAW.

Validado fuera de Windows: sintaxis C#, XML del proyecto, migración SQLite
repetible y persistencia de las variantes. Los pasos visuales y COM requieren CorelDRAW.
