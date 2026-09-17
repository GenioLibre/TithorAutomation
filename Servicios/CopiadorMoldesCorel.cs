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

            VGCore.Document documentoMaster = null;
            VGCore.Layer capaDestino = null;

            bool masterAbiertoPorServicio = false;
            bool grupoComandosAbierto = false;

            int totalCopiado = 0;
            string etapa = "Preparando la copia";

            List<VGCore.Shape> gruposCopiados = new List<VGCore.Shape>();

            try
                {
                etapa = "Buscando el archivo Master abierto";

                documentoMaster = BuscarDocumentoAbierto(corelApp, rutaMaster);

                if (documentoMaster == null)
                    {
                    etapa = "Abriendo el archivo Master";

                    documentoMaster = corelApp.OpenDocument(rutaMaster, 0);
                    masterAbiertoPorServicio = true;
                    }

                if (documentoMaster == null)
                    throw new InvalidOperationException("CorelDRAW no pudo abrir el archivo Master.");

                if (EsMismoDocumento(documentoDestino, documentoMaster))
                    throw new InvalidOperationException("El documento destino no puede ser el mismo archivo Master.");

                etapa = "Leyendo los moldes del Master";

                documentoMaster.Activate();

                Dictionary<string, VGCore.Shape> indiceMoldes = CrearIndiceMoldes(documentoMaster);

                if (indiceMoldes.Count == 0)
                    throw new InvalidOperationException("No se encontraron grupos con códigos 'molde_' dentro del Master.");

                etapa = "Validando los moldes solicitados";

                ValidarMoldesSolicitados(plan, indiceMoldes);

                etapa = "Creando la capa de producción";

                documentoDestino.Activate();

                capaDestino = ObtenerOCrearCapa(documentoDestino, NombreCapaProduccion);

                if (capaDestino == null)
                    throw new InvalidOperationException($"No se pudo crear la capa '{NombreCapaProduccion}'.");

                capaDestino.Activate();

                etapa = "Iniciando el grupo de comandos";

                documentoDestino.BeginCommandGroup("Tithor - Copiar moldes");
                grupoComandosAbierto = true;

                for (int i = 0; i < plan.Moldes.Count; i++)
                    {
                    MoldeProduccion solicitud = plan.Moldes[i];

                    etapa = $"Copiando el molde '{solicitud.CodigoMolde}'";

                    if (!indiceMoldes.TryGetValue(solicitud.CodigoMolde, out VGCore.Shape moldeOrigen))
                        throw new InvalidOperationException($"No se encontró el molde '{solicitud.CodigoMolde}' dentro del Master.");

                    documentoMaster.Activate();
                    moldeOrigen.Copy();

                    documentoDestino.Activate();
                    capaDestino.Activate();

                    VGCore.Shape copia = capaDestino.Paste();

                    if (copia == null)
                        throw new InvalidOperationException($"CorelDRAW no pudo pegar el molde '{solicitud.CodigoMolde}'.");

                    if (!string.IsNullOrWhiteSpace(solicitud.NombreDestino))
                        copia.Name = solicitud.NombreDestino;

                    gruposCopiados.Add(copia);

                    totalCopiado++;

                    progreso?.Invoke(totalCopiado, plan.Moldes.Count);
                    }

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
                if (grupoComandosAbierto)
                    {
                    try
                        {
                        documentoDestino.Activate();
                        documentoDestino.EndCommandGroup();

                        grupoComandosAbierto = false;

                        documentoDestino.Undo();
                        }
                    catch
                        {
                        grupoComandosAbierto = false;
                        }
                    }

                string codigo = $"0x{ex.HResult:X8}";

                throw new InvalidOperationException(
                    $"Error durante la etapa: {etapa}.\n" +
                    $"Código original: {codigo}\n\n" +
                    ex.Message,
                    ex
                );
                }
            finally
                {
                if (grupoComandosAbierto)
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

                if (masterAbiertoPorServicio && documentoMaster != null)
                    {
                    try
                        {
                        documentoMaster.Close();
                        }
                    catch
                        {
                        }
                    }

                try
                    {
                    documentoDestino.Activate();
                    }
                catch
                    {
                    }
                }
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

            if (string.IsNullOrWhiteSpace(nombre))
                return;

            nombre = nombre.Trim();

            if (!nombre.StartsWith("molde_", StringComparison.OrdinalIgnoreCase))
                return;

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