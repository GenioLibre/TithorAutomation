using System;
using System.Collections.Generic;
using System.IO;
using TithorAutomation.Modelos;

namespace TithorAutomation.Servicios
    {
    public class CopiadorMoldesCorel
        {
        private const string NombreCapaProduccion = "TITHOR_PRODUCCION";
        private const int ColumnasGrid = 10;
        private const double SeparacionGridMilimetros = 200.0;
        public int Copiar(VGCore.Application corelApp, VGCore.Document documentoDestino, string rutaMaster, PlanProduccion plan, Action<int, int> progreso)
            {
            if (corelApp == null)
                throw new ArgumentNullException(nameof(corelApp));

            if (documentoDestino == null)
                throw new ArgumentNullException(nameof(documentoDestino));

            if (string.IsNullOrWhiteSpace(rutaMaster))
                throw new ArgumentException("La ruta del Master está vacía.", nameof(rutaMaster));

            if (!File.Exists(rutaMaster))
                throw new FileNotFoundException("No se encontró el archivo Master.", rutaMaster);

            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            if (plan.Moldes == null || plan.Moldes.Count == 0)
                throw new InvalidOperationException("El plan de producción no contiene moldes.");

            if (UsaSeleccionDePiezas(plan))
                return CopiarSeleccionDePiezas(corelApp, documentoDestino, rutaMaster, plan, progreso);

            VGCore.Document documentoMaster = null;
            VGCore.Layer capaDestino = null;

            VGCore.Layer capaTemporal = null;
            bool grupoComandosAbierto = false;
            bool comunicacionFallida = false;
            bool huboCambios = false;

            int totalCopiado = 0;
            string etapa = "Preparando la copia";

            List<VGCore.Shape> gruposCopiados = new List<VGCore.Shape>();
            var primerasCopias = new Dictionary<string, VGCore.Shape>(StringComparer.OrdinalIgnoreCase);

            try
                {
                etapa = "Validando el documento de destino";
                documentoMaster = BuscarDocumentoAbierto(corelApp, rutaMaster);
                if (documentoMaster != null && EsMismoDocumento(documentoDestino, documentoMaster))
                    throw new InvalidOperationException("El documento destino no puede ser el archivo Master.");
                if (documentoMaster != null && documentoMaster.Dirty)
                    throw new InvalidOperationException("El Master tiene cambios sin guardar. Guárdelo antes de copiar los moldes.");

                documentoDestino.Activate();
                VGCore.Page paginaDestino = documentoDestino.ActivePage;
                documentoDestino.BeginCommandGroup("Tithor - Copiar moldes");
                grupoComandosAbierto = true;

                etapa = "Creando la capa temporal";
                capaTemporal = paginaDestino.CreateLayer("TITHOR_TEMP_" + Guid.NewGuid().ToString("N"));
                huboCambios = true;

                etapa = "Creando la capa de producción";
                capaDestino = ObtenerOCrearCapa(documentoDestino, NombreCapaProduccion);
                if (!capaDestino.Editable || !capaDestino.Visible)
                    throw new InvalidOperationException("La capa TITHOR_PRODUCCION debe estar visible y desbloqueada.");

                etapa = "Importando el Master guardado al documento de destino";
                capaTemporal.Activate();
                var opciones = new VGCore.StructImportOptions();
                opciones.MaintainLayers = false;
                opciones.Mode = VGCore.cdrImportMode.cdrImportFull;
                var importador = capaTemporal.ImportEx(rutaMaster, VGCore.cdrFilter.cdrCDR, opciones);
                importador.Finish();

                etapa = "Buscando los moldes importados";
                var indiceMoldes = new Dictionary<string, VGCore.Shape>(StringComparer.OrdinalIgnoreCase);
                for (int n = 1; n <= capaTemporal.Shapes.Count; n++)
                    AgregarGrupoMoldeAlIndice(capaTemporal.Shapes[n], indiceMoldes);
                ValidarMoldesSolicitados(plan, indiceMoldes);
                paginaDestino.Activate();
                capaDestino.Activate();

                for (int i = 0; i < plan.Moldes.Count; i++)
                    {
                    MoldeProduccion solicitud = plan.Moldes[i];

                    etapa = $"Copiando el molde '{solicitud.CodigoMolde}'";

                    if (!indiceMoldes.TryGetValue(solicitud.CodigoMolde, out VGCore.Shape moldeOrigen))
                        throw new InvalidOperationException($"No se encontró el molde '{solicitud.CodigoMolde}' dentro del Master.");

                    string detalle = $"'{solicitud.CodigoMolde}', unidad {i + 1} de {plan.Moldes.Count}";
                    VGCore.Shape copia;
                    if (primerasCopias.TryGetValue(solicitud.CodigoMolde, out VGCore.Shape primeraCopia))
                        {
                        etapa = "Duplicando en el destino " + detalle;
                        documentoDestino.Activate();
                        copia = primeraCopia.Duplicate(0, 0);
                        }
                    else
                        {
                        etapa = "Activando el destino para " + detalle;
                        documentoDestino.Activate();
                        capaDestino.Activate();
                        etapa = "Copiando directamente a la capa " + detalle;
                        copia = moldeOrigen.CopyToLayer(capaDestino);
                        if (copia != null) primerasCopias.Add(solicitud.CodigoMolde, copia);
                        }

                    if (copia == null)
                        throw new InvalidOperationException($"CorelDRAW no devolvió la copia del molde '{solicitud.CodigoMolde}'.");

                    etapa = "Conservando el nombre de " + detalle;
                    // Cada unidad conserva el nombre del grupo del master, aunque se repita.
                    copia.Name = moldeOrigen.Name;

                    gruposCopiados.Add(copia);

                    totalCopiado++;

                    progreso?.Invoke(totalCopiado, plan.Moldes.Count);
                    }

                etapa = "Retirando los objetos temporales del Master";
                capaTemporal.Delete();
                capaTemporal = null;

                etapa = "Ordenando los moldes en formato grid";

                documentoDestino.Activate();
                capaDestino.Activate();

                OrdenarEnGrid(corelApp, documentoDestino, gruposCopiados);

                etapa = "Finalizando el grupo de comandos";

                documentoDestino.Activate();
                documentoDestino.EndCommandGroup();

                grupoComandosAbierto = false;

                documentoDestino.Activate();

                return totalCopiado;
                }
            catch (Exception ex)
                {
                string estadoReversion = "";
                comunicacionFallida = EsFalloDeComunicacion(ex);
                if (comunicacionFallida)
                    estadoReversion = "\nCorelDRAW notificó un fallo del servidor. No se pudo confirmar la reversión." +
                        "\nRevise o recupere el documento y reinicie CorelDRAW antes de volver a copiar.";
                if (grupoComandosAbierto && !comunicacionFallida)
                    {
                    try
                        {
                        documentoDestino.Activate();
                        documentoDestino.EndCommandGroup();

                        grupoComandosAbierto = false;

                        if (huboCambios) documentoDestino.Undo();
                        estadoReversion = huboCambios ? "\nLa operación se deshizo." : "\nNo se registraron cambios en el destino.";
                        }
                    catch (Exception errorReversion)
                        {
                        comunicacionFallida = EsFalloDeComunicacion(errorReversion);
                        estadoReversion = "\nNo se pudo confirmar la reversión: " + errorReversion.Message +
                            "\nRevise el documento antes de repetir la copia.";
                        }
                    }

                string codigo = $"0x{ex.HResult:X8}";

                throw new InvalidOperationException(
                    $"Error durante la etapa: {etapa}.\n" +
                    $"Código original: {codigo}\n\n" +
                    ex.Message + estadoReversion,
                    ex
                );
                }
            finally
                {
                if (grupoComandosAbierto && !comunicacionFallida)
                    {
                    try
                        {
                        documentoDestino.Activate();
                        documentoDestino.EndCommandGroup();
                        }
                    catch
                        {
                        }
                    }

                try
                    {
                    if (!comunicacionFallida) documentoDestino.Activate();
                    }
                catch
                    {
                    }
                }
            }

        private bool UsaSeleccionDePiezas(PlanProduccion plan)
            {
            foreach (MoldeProduccion solicitud in plan.Moldes)
                {
                if (!string.IsNullOrWhiteSpace(solicitud.CapaMaster) ||
                    (solicitud.PiezasIncluidas != null && solicitud.PiezasIncluidas.Count > 0))
                    return true;
                }

            return false;
            }

        private int CopiarSeleccionDePiezas(VGCore.Application corelApp, VGCore.Document documentoDestino, string rutaMaster, PlanProduccion plan, Action<int, int> progreso)
            {
            VGCore.Document documentoMaster = null;
            VGCore.Layer capaDestino = null;
            bool cerrarMaster = false;
            bool grupoComandosAbierto = false;
            bool comunicacionFallida = false;
            bool huboCambios = false;
            int totalCopiado = 0;
            string etapa = "Preparando la copia selectiva";
            List<VGCore.Shape> gruposCopiados = new List<VGCore.Shape>();
            Dictionary<string, VGCore.Shape> primerasCopias = new Dictionary<string, VGCore.Shape>(StringComparer.OrdinalIgnoreCase);

            try
                {
                etapa = "Abriendo el archivo Master";
                documentoMaster = BuscarDocumentoAbierto(corelApp, rutaMaster);

                if (documentoMaster == null)
                    {
                    documentoMaster = corelApp.OpenDocument(rutaMaster);
                    cerrarMaster = true;
                    }

                if (EsMismoDocumento(documentoDestino, documentoMaster))
                    throw new InvalidOperationException("El documento destino no puede ser el archivo Master.");

                if (documentoMaster.Dirty)
                    throw new InvalidOperationException("El Master tiene cambios sin guardar. Guárdelo antes de copiar los moldes.");

                etapa = "Indexando capas y grupos del Master";
                Dictionary<string, VGCore.Shape> indiceMoldes = CrearIndiceMoldesPorCapa(documentoMaster);
                ValidarSeleccionesSolicitadas(plan, indiceMoldes);

                documentoDestino.Activate();
                documentoDestino.BeginCommandGroup("Tithor - Copiar piezas de moldes");
                grupoComandosAbierto = true;

                capaDestino = ObtenerOCrearCapa(documentoDestino, NombreCapaProduccion);

                if (!capaDestino.Editable || !capaDestino.Visible)
                    throw new InvalidOperationException("La capa TITHOR_PRODUCCION debe estar visible y desbloqueada.");

                for (int i = 0; i < plan.Moldes.Count; i++)
                    {
                    MoldeProduccion solicitud = plan.Moldes[i];
                    string claveMolde = CrearClaveMolde(solicitud.CapaMaster, solicitud.CodigoMolde);
                    string claveCopia = CrearClaveCopia(solicitud);
                    VGCore.Shape moldeOrigen = indiceMoldes[claveMolde];
                    VGCore.Shape copia;

                    etapa = "Copiando " + solicitud.CapaMaster + " / " + solicitud.CodigoMolde;

                    if (primerasCopias.TryGetValue(claveCopia, out VGCore.Shape primeraCopia))
                        {
                        documentoDestino.Activate();
                        copia = primeraCopia.Duplicate(0, 0);
                        }
                    else
                        {
                        documentoMaster.Activate();
                        moldeOrigen.Copy();

                        documentoDestino.Activate();
                        capaDestino.Activate();
                        copia = capaDestino.Paste();

                        if (copia == null)
                            throw new InvalidOperationException("CorelDRAW no devolvió la copia del grupo solicitado.");

                        etapa = "Conservando únicamente las piezas solicitadas";
                        ConservarPiezasSolicitadas(copia, solicitud);

                        primerasCopias.Add(claveCopia, copia);
                        }

                    if (copia == null)
                        throw new InvalidOperationException("No se pudo crear la copia de " + solicitud.CodigoMolde + ".");

                    copia.Name = moldeOrigen.Name;
                    gruposCopiados.Add(copia);
                    huboCambios = true;
                    totalCopiado++;
                    progreso?.Invoke(totalCopiado, plan.Moldes.Count);
                    }

                etapa = "Ordenando los moldes en formato grid";
                documentoDestino.Activate();
                capaDestino.Activate();
                OrdenarEnGrid(corelApp, documentoDestino, gruposCopiados);

                etapa = "Finalizando el grupo de comandos";
                documentoDestino.EndCommandGroup();
                grupoComandosAbierto = false;
                documentoDestino.Activate();

                return totalCopiado;
                }
            catch (Exception ex)
                {
                string estadoReversion = string.Empty;
                comunicacionFallida = EsFalloDeComunicacion(ex);

                if (comunicacionFallida)
                    {
                    estadoReversion =
                        "\nCorelDRAW notificó un fallo del servidor. No se pudo confirmar la reversión." +
                        "\nRevise o recupere el documento y reinicie CorelDRAW antes de volver a copiar.";
                    }
                else if (grupoComandosAbierto)
                    {
                    try
                        {
                        documentoDestino.Activate();
                        documentoDestino.EndCommandGroup();
                        grupoComandosAbierto = false;

                        if (huboCambios)
                            documentoDestino.Undo();

                        estadoReversion = huboCambios
                            ? "\nLa operación se deshizo."
                            : "\nNo se registraron cambios en el destino.";
                        }
                    catch (Exception errorReversion)
                        {
                        estadoReversion =
                            "\nNo se pudo confirmar la reversión: " +
                            errorReversion.Message +
                            "\nRevise el documento antes de repetir la copia.";
                        }
                    }

                throw new InvalidOperationException(
                    "Error durante la etapa: " + etapa + ".\n" +
                    "Código original: 0x" + ex.HResult.ToString("X8") + "\n\n" +
                    ex.Message +
                    estadoReversion,
                    ex
                );
                }
            finally
                {
                if (grupoComandosAbierto && !comunicacionFallida)
                    {
                    try
                        {
                        documentoDestino.Activate();
                        documentoDestino.EndCommandGroup();
                        }
                    catch
                        {
                        }
                    }

                if (cerrarMaster && documentoMaster != null && !comunicacionFallida)
                    {
                    try
                        {
                        documentoMaster.Close();
                        }
                    catch
                        {
                        }
                    }

                if (!comunicacionFallida)
                    {
                    try
                        {
                        documentoDestino.Activate();
                        }
                    catch
                        {
                        }
                    }
                }
            }

        private Dictionary<string, VGCore.Shape> CrearIndiceMoldesPorCapa(VGCore.Document documentoMaster)
            {
            Dictionary<string, VGCore.Shape> indice = new Dictionary<string, VGCore.Shape>(StringComparer.OrdinalIgnoreCase);

            for (int paginaIndice = 1; paginaIndice <= documentoMaster.Pages.Count; paginaIndice++)
                {
                VGCore.Page pagina = documentoMaster.Pages[paginaIndice];

                for (int capaIndice = 1; capaIndice <= pagina.Layers.Count; capaIndice++)
                    {
                    VGCore.Layer capa = pagina.Layers[capaIndice];

                    for (int objetoIndice = 1; objetoIndice <= capa.Shapes.Count; objetoIndice++)
                        {
                        VGCore.Shape objeto = capa.Shapes[objetoIndice];

                        if (objeto.Type != VGCore.cdrShapeType.cdrGroupShape)
                            continue;

                        string nombre = ObtenerNombreSeguro(objeto);

                        if (!nombre.StartsWith("molde_", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string clave = CrearClaveMolde(capa.Name, nombre);

                        if (indice.ContainsKey(clave))
                            throw new InvalidOperationException("El grupo '" + nombre + "' está duplicado en la capa '" + capa.Name + "'.");

                        indice.Add(clave, objeto);
                        }
                    }
                }

            return indice;
            }

        private void ValidarSeleccionesSolicitadas(PlanProduccion plan, Dictionary<string, VGCore.Shape> indiceMoldes)
            {
            List<string> faltantes = new List<string>();

            foreach (MoldeProduccion solicitud in plan.Moldes)
                {
                string clave = CrearClaveMolde(solicitud.CapaMaster, solicitud.CodigoMolde);

                if (!indiceMoldes.ContainsKey(clave))
                    {
                    string descripcion = solicitud.CapaMaster + " / " + solicitud.CodigoMolde;

                    if (!faltantes.Contains(descripcion))
                        faltantes.Add(descripcion);
                    }
                }

            if (faltantes.Count > 0)
                {
                throw new InvalidOperationException(
                    "Los siguientes grupos no existen dentro del Master:\n• " +
                    string.Join("\n• ", faltantes)
                );
                }
            }

        private void ConservarPiezasSolicitadas(VGCore.Shape grupo, MoldeProduccion solicitud)
            {
            HashSet<string> requeridas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string pieza in solicitud.PiezasIncluidas)
                requeridas.Add(AnalizadorMasterCorel.NormalizarCodigo(pieza));

            if (requeridas.Count == 0)
                return;

            HashSet<string> encontradas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int indice = grupo.Shapes.Count; indice >= 1; indice--)
                {
                VGCore.Shape elemento = grupo.Shapes[indice];
                string nombre = AnalizadorMasterCorel.NormalizarCodigo(ObtenerNombreSeguro(elemento));

                if (requeridas.Contains(nombre))
                    encontradas.Add(nombre);
                else
                    elemento.Delete();
                }

            List<string> faltantes = new List<string>();

            foreach (string requerida in requeridas)
                {
                if (!encontradas.Contains(requerida))
                    faltantes.Add(requerida);
                }

            if (faltantes.Count > 0)
                {
                throw new InvalidOperationException(
                    "El grupo '" + solicitud.CodigoMolde + "' de la capa '" + solicitud.CapaMaster +
                    "' no contiene las piezas: " + string.Join(", ", faltantes) + "."
                );
                }
            }

        private string CrearClaveMolde(string capa, string grupo)
            {
            return AnalizadorMasterCorel.NormalizarCodigo(capa) + "|" + AnalizadorMasterCorel.NormalizarCodigo(grupo);
            }

        private string CrearClaveCopia(MoldeProduccion solicitud)
            {
            List<string> piezas = new List<string>();

            foreach (string pieza in solicitud.PiezasIncluidas)
                piezas.Add(AnalizadorMasterCorel.NormalizarCodigo(pieza));

            piezas.Sort(StringComparer.OrdinalIgnoreCase);

            return CrearClaveMolde(solicitud.CapaMaster, solicitud.CodigoMolde) + "|" + string.Join(",", piezas);
            }

        private string ObtenerNombreSeguro(VGCore.Shape objeto)
            {
            try
                {
                return (objeto.Name ?? string.Empty).Trim();
                }
            catch
                {
                return string.Empty;
                }
            }

        public static bool EsFalloDeComunicacion(Exception error)
            {
            for (Exception actual = error; actual != null; actual = actual.InnerException)
                {
                uint codigo = unchecked((uint)actual.HResult);
                if (codigo == 0x80010105 || codigo == 0x800706BE || codigo == 0x800706BA ||
                    codigo == 0x80010108 || codigo == 0x80010007)
                    return true;
                }
            return false;
            }

        private VGCore.Document BuscarDocumentoAbierto(VGCore.Application corelApp, string rutaMaster)
            {
            string rutaBuscada = NormalizarRuta(rutaMaster);

            for (int i = 1; i <= corelApp.Documents.Count; i++)
                {
                VGCore.Document documento = corelApp.Documents[i];
                string rutaDocumento = ObtenerRutaDocumento(documento);

                if (string.IsNullOrWhiteSpace(rutaDocumento))
                    continue;

                if (string.Equals(rutaBuscada, NormalizarRuta(rutaDocumento), StringComparison.OrdinalIgnoreCase))
                    return documento;
                }

            return null;
            }

        private bool EsMismoDocumento(VGCore.Document primerDocumento, VGCore.Document segundoDocumento)
            {
            if (primerDocumento == null || segundoDocumento == null)
                return false;

            string primeraRuta = ObtenerRutaDocumento(primerDocumento);
            string segundaRuta = ObtenerRutaDocumento(segundoDocumento);

            if (!string.IsNullOrWhiteSpace(primeraRuta) && !string.IsNullOrWhiteSpace(segundaRuta))
                {
                return string.Equals(
                    NormalizarRuta(primeraRuta),
                    NormalizarRuta(segundaRuta),
                    StringComparison.OrdinalIgnoreCase
                );
                }

            try
                {
                return string.Equals(
                    primerDocumento.Name,
                    segundoDocumento.Name,
                    StringComparison.OrdinalIgnoreCase
                );
                }
            catch
                {
                return false;
                }
            }

        private string ObtenerRutaDocumento(VGCore.Document documento)
            {
            if (documento == null)
                return string.Empty;

            try
                {
                return documento.FullFileName;
                }
            catch
                {
                return string.Empty;
                }
            }

        private string NormalizarRuta(string ruta)
            {
            if (string.IsNullOrWhiteSpace(ruta))
                return string.Empty;

            try
                {
                return Path.GetFullPath(ruta).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                );
                }
            catch
                {
                return ruta.Trim();
                }
            }

        private VGCore.Layer ObtenerOCrearCapa(VGCore.Document documento, string nombreCapa)
            {
            if (documento == null)
                throw new ArgumentNullException(nameof(documento));

            if (documento.Pages.Count == 0)
                throw new InvalidOperationException("El documento destino no contiene páginas.");

            VGCore.Page pagina = documento.ActivePage;

            if (pagina == null)
                {
                pagina = documento.Pages[1];
                pagina.Activate();
                }

            for (int i = 1; i <= pagina.Layers.Count; i++)
                {
                VGCore.Layer capa = pagina.Layers[i];

                if (string.Equals(capa.Name, nombreCapa, StringComparison.OrdinalIgnoreCase))
                    return capa;
                }

            return pagina.CreateLayer(nombreCapa);
            }

        private Dictionary<string, VGCore.Shape> CrearIndiceMoldes(VGCore.Document documentoMaster)
            {
            Dictionary<string, VGCore.Shape> indice =
                new Dictionary<string, VGCore.Shape>(StringComparer.OrdinalIgnoreCase);

            for (int paginaIndice = 1; paginaIndice <= documentoMaster.Pages.Count; paginaIndice++)
                {
                VGCore.Page pagina = documentoMaster.Pages[paginaIndice];

                for (int capaIndice = 1; capaIndice <= pagina.Layers.Count; capaIndice++)
                    {
                    VGCore.Layer capa = pagina.Layers[capaIndice];

                    for (int objetoIndice = 1; objetoIndice <= capa.Shapes.Count; objetoIndice++)
                        {
                        VGCore.Shape objeto = capa.Shapes[objetoIndice];

                        AgregarGrupoMoldeAlIndice(objeto, indice);
                        }
                    }
                }

            return indice;
            }
        private void AgregarGrupoMoldeAlIndice(VGCore.Shape objeto, Dictionary<string, VGCore.Shape> indice)
            {
            if (objeto == null)
                return;

            if (objeto.Type != VGCore.cdrShapeType.cdrGroupShape)
                return;

            string nombre;

            try
                {
                nombre = objeto.Name;
                }
            catch
                {
                return;
                }

            nombre = (nombre ?? "").Trim();

            if (!nombre.StartsWith("molde_", StringComparison.OrdinalIgnoreCase))
                {
                for (int i = 1; i <= objeto.Shapes.Count; i++)
                    AgregarGrupoMoldeAlIndice(objeto.Shapes[i], indice);
                return;
                }

            if (indice.ContainsKey(nombre))
                throw new InvalidOperationException($"El grupo de molde '{nombre}' está duplicado dentro del Master.");

            indice.Add(nombre, objeto);
            }



        private void ValidarMoldesSolicitados(PlanProduccion plan, Dictionary<string, VGCore.Shape> indiceMoldes)
            {
            List<string> faltantes = new List<string>();

            foreach (MoldeProduccion solicitud in plan.Moldes)
                {
                if (string.IsNullOrWhiteSpace(solicitud.CodigoMolde))
                    {
                    faltantes.Add("(código vacío)");
                    continue;
                    }

                if (!indiceMoldes.ContainsKey(solicitud.CodigoMolde) &&
                    !faltantes.Contains(solicitud.CodigoMolde))
                    {
                    faltantes.Add(solicitud.CodigoMolde);
                    }
                }

            if (faltantes.Count == 0)
                return;

            throw new InvalidOperationException(
                "Los siguientes moldes no existen dentro del Master:\n• " +
                string.Join("\n• ", faltantes)
            );
            }
        private void OrdenarEnGrid(VGCore.Application corelApp, VGCore.Document documentoDestino, List<VGCore.Shape> grupos)
            {
            if (grupos == null || grupos.Count == 0)
                return;

            double separacion = corelApp.ConvertUnits(SeparacionGridMilimetros, VGCore.cdrUnit.cdrMillimeter, documentoDestino.Unit);
            double anchoMaximo = 0;
            double altoMaximo = 0;
            double inicioX = grupos[0].LeftX;
            double inicioY = grupos[0].TopY;

            foreach (VGCore.Shape grupo in grupos)
                {
                if (grupo.SizeWidth > anchoMaximo)
                    anchoMaximo = grupo.SizeWidth;

                if (grupo.SizeHeight > altoMaximo)
                    altoMaximo = grupo.SizeHeight;

                if (grupo.LeftX < inicioX)
                    inicioX = grupo.LeftX;

                if (grupo.TopY > inicioY)
                    inicioY = grupo.TopY;
                }

            double anchoCelda = anchoMaximo + separacion;
            double altoCelda = altoMaximo + separacion;

            for (int i = 0; i < grupos.Count; i++)
                {
                VGCore.Shape grupo = grupos[i];

                int columna = i % ColumnasGrid;
                int fila = i / ColumnasGrid;

                double destinoX = inicioX + (columna * anchoCelda);
                double destinoY = inicioY - (fila * altoCelda);

                double movimientoX = destinoX - grupo.LeftX;
                double movimientoY = destinoY - grupo.TopY;

                grupo.Move(movimientoX, movimientoY);
                }
            }
        }
    }
