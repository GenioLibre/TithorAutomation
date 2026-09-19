# Comprobaciones de regresión

Requisitos: Windows, Visual Studio con MSBuild de .NET Framework, targeting pack de .NET Framework 4.8, referencias COM de CorelDRAW 27 y los paquetes de `packages.config` restaurados en `packages/`.

Desde una consola de desarrollador **x64** de Visual Studio, en la raíz del repositorio:

```powershell
MSBuild tests/TithorAutomation.Tests.csproj /t:Build /p:Configuration=Release /p:Platform=x64
./tests/bin/Release/TithorAutomation.Tests.exe
```

El ejecutable devuelve 0 cuando las pruebas pasan y 1 ante cualquier fallo. No inicia CorelDRAW ni accede a SQLite; genera un Excel temporal y lo elimina al terminar. El proyecto referencia la aplicación real, no una copia de su lógica.

Cobertura: nombres de moldes, cobertura proporcional, dimensiones inválidas, cantidades numéricas y de texto en tres culturas, formatos de Excel, fórmulas, rechazo de nombres duplicados, piezas faltantes/ inválidas, personalización y separación de variantes al reutilizar selecciones del catálogo. Resultado verificado: **115 comprobaciones**.

## Rendimiento de planificación

Después de compilar Release, desde la misma consola:

```powershell
./tests/MedirRendimiento.ps1
```

El script requiere Git y el commit base `2a07e4a` disponible localmente. Compila los planificadores originales con otro nombre y los compara con los actuales. Se calienta cada implementación y se toma la mediana de tres ejecuciones. Verifica también que las solicitudes generadas conserven los mismos nombres, piezas, cantidades, diseños, campos y orden.

Resultados locales orientativos, 1.000 filas por pedido:

| Planificador | Moldes en catálogo | Antes | Después |
| --- | ---: | ---: | ---: |
| Fundas | 1.212 | 124,77 ms | 2,00 ms |
| Camisetas | 1.204 | 721,49 ms | 4,46 ms |

Es una carga sintética con variantes repetidas. No mide lectura de Excel, operaciones COM, impresión ni el tiempo total de producción. Las ganancias dependen del tamaño del catálogo y de cuántas variantes se repitan.

## Integración manual pendiente en CorelDRAW

- Copiar un pedido y repetir en el mismo documento: debe rechazarse sin crear capas ni objetos. Repetir en otro documento vacío debe funcionar.
- Abrir un documento antiguo con nombres de producción duplicados: Escalar debe mostrar el conflicto, sin elegir silenciosamente una copia.
- Analizar acomodo en la página 1 y activar la página 2: debe rechazar la operación sin redimensionar ninguna página. También probar después de reordenar o eliminar páginas.
- En el depurador, provocar un fallo de extracción justo después de duplicar la primera plantilla PowerClip: debe ejecutarse la reversión y desaparecer la copia temporal.
- Comprobar escalado y Ctrl+Z con diseños reales. Verificar que `EventsEnabled` y `Optimization` recuperen sus valores anteriores después de éxito y error.

Estas comprobaciones manuales no se ejecutaron durante la validación automática.
