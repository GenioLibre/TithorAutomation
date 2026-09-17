# Organización de frmPrincipal

El formulario sigue siendo una única clase `partial` de Windows Forms. Esta reorganización mueve métodos completos, sin cambiar su implementación, sus nombres ni los eventos del diseñador.

| Archivo | Responsabilidad |
| --- | --- |
| frmPrincipal.cs | Campos compartidos, constructor, navegación entre paneles y cierre del formulario. |
| frmPrincipal.Colores.cs | Análisis de colores, tabla CMYK, aplicación de cambios y vista previa. Incluye ColorDetectado. |
| frmPrincipal.Catalogo.cs | Productos, archivos master, análisis, sincronización y filtros del catálogo. |
| frmPrincipal.Produccion.cs | Lectura y aprobación de pedidos, planificación y copia de moldes. |
| frmPrincipal.Corel.cs | Conexión con CorelDRAW y actualización de su estado. |
| frmPrincipal.Designer.cs | Controles y enlaces de eventos generados por el diseñador; sin cambios. |

Los campos permanecen en el archivo principal para conservar su orden de inicialización. Las partes se registran en el proyecto con `DependentUpon` para agruparlas bajo el formulario en Visual Studio. Esta separación facilita navegar por el código; todavía no desacopla la lógica de la interfaz.

## Validación

- Comparación con Roslyn de los 115 miembros originales: mismos tokens, sin miembros perdidos o duplicados.
- Orden de campos idéntico y cada parte incluida una sola vez en el proyecto.
- Compilación Debug/x64 con MSBuild de 64 bits y CorelDRAW 27 instalado.
- No se ejecutaron operaciones contra documentos de CorelDRAW ni la base de datos del usuario.

Para compilar desde un entorno de desarrollo con MSBuild de 64 bits, .NET Framework 4.8 y las referencias COM de CorelDRAW 27 disponibles:

```powershell
msbuild TithorAutomation.slnx /restore /p:RestorePackagesConfig=true /p:Configuration=Debug /p:Platform=x64
```

## Prueba manual pendiente

Usar una cuenta de Windows de pruebas o una copia de seguridad de la base SQLite y copias de los documentos CDR.

1. Abrir el formulario en el diseñador de Visual Studio y comprobar los tres paneles.
2. Iniciar la aplicación y comprobar el estado de conexión con CorelDRAW.
3. Analizar colores, previsualizar, restaurar y aplicar un cambio sobre un CDR de prueba; comprobar Deshacer.
4. Seleccionar un producto y un master de prueba, analizar, sincronizar y filtrar el catálogo.
5. Cargar un Excel de fundas, analizarlo y aprobarlo; copiar moldes a un documento de prueba y deshacer la copia.
6. Cerrar el formulario con una vista previa activa y comprobar el aviso/restauración.

## Reversión

La propuesta se entrega en una rama independiente. Mientras no se integre, `master` conserva el código original. Si se integra y es necesario volver atrás, revertir el commit de reorganización (o el commit de squash/merge correspondiente). Git revierte código; no restaura datos SQLite ni documentos CDR modificados durante pruebas.
