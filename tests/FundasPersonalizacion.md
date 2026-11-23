# Personalización de fundas por unidad

Validación de ejecución pendiente en Windows/CorelDRAW 2026.

El Excel conserva Diseño, Talla y Cantidad. Nombre/Nombres y Número/Números son columnas opcionales (se ignoran acentos y mayúsculas al leer encabezados).

- Las listas se separan por comas, conservan el orden y eliminan solo espacios exteriores.
- Cada lista puede tener hasta Cantidad valores. Las unidades restantes reciben una cadena vacía; nunca se repite el último valor.
- Se permiten solo nombres, solo números o ambas columnas vacías.
- Si ambas listas tienen datos, deben tener la misma longitud para conservar los pares.
- Rechazar valores vacíos entre comas y comas iniciales/finales; no compactar ni desplazar posiciones.
- Conservar números como texto: 07 sigue siendo 07.
- Una lista inválida detiene el análisis con la fila del Excel. El planificador también rechaza listas inválidas para no llegar a copiar ni escalar.
- El plan de producción guarda nombre y numero individuales por NumeroUnidad. Todas las piezas de esa unidad reciben esos mismos datos a través del reemplazo existente de Escalar, tanto individual como por lote.
- No se modifica el flujo de camisetas ni se agregan controles en runtime.

Ejemplos del Excel proporcionado:

| Cantidad | Nombre | Números | Resultado |
| --- | --- | --- | --- |
| 9 | vacío | 1,2,3,4,5,6,7,8,9 | Un número por funda; todos los nombres vacíos. |
| 2 | Test | vacío | Unidad 1: Test; unidad 2: nombre vacío; números vacíos. |
| 3 | Ana,Luis | 07,08 | Unidad 1: Ana/07; unidad 2: Luis/08; unidad 3: ambos vacíos. |

Para comprobar en Corel, volver a analizar el Excel, analizar el documento de producción y aplicar un diseño con varios textos llamados nombre y numero, incluidos textos dentro de PowerClips. Verificar que todos los de una unidad coincidan y que una unidad sin valor no conserve el texto de la plantilla. Probar también una lista con más valores que Cantidad y dos listas de distinta longitud: debe impedir el análisis antes de modificar Corel.

Las pruebas de lector y planificador están en PedidoTests.PersonalizacionFundasDesdeExcel, dentro del ejecutable de pruebas existente.
