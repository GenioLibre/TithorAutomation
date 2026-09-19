using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TithorAutomation.Modelos;

namespace TithorAutomation.Servicios
    {
    public sealed class AnalizadorMockupCorel
        {
        public List<Mockup> Analizar(VGCore.Application corelApp, string rutaArchivo)
            {
            if (corelApp == null)
                throw new InvalidOperationException("No existe conexión con CorelDRAW.");

            if (string.IsNullOrWhiteSpace(rutaArchivo) || !File.Exists(rutaArchivo))
                throw new FileNotFoundException("No se encontró el archivo Master de mockups.", rutaArchivo);

            List<Mockup> resultado = new List<Mockup>();
            VGCore.Document documento = null;
            bool cerrarAlFinal = false;

            try
                {
                documento = BuscarDocumentoAbierto(corelApp, rutaArchivo);

                if (documento == null)
                    {
                    documento = corelApp.OpenDocument(rutaArchivo);
                    cerrarAlFinal = true;
                    }

                for (int pagina = 1; pagina <= documento.Pages.Count; pagina++)
                    AnalizarPagina(documento.Pages[pagina], resultado);

                MarcarDuplicados(resultado);
                return resultado;
                }
            finally
                {
                if (cerrarAlFinal && documento != null)
                    {
                    try { documento.Close(); }
                    catch { }
                    }
                }
            }

        private void AnalizarPagina(VGCore.Page pagina, List<Mockup> resultado)
            {
            for (int indiceCapa = 1; indiceCapa <= pagina.Layers.Count; indiceCapa++)
                {
                VGCore.Layer capa = pagina.Layers[indiceCapa];
                string nombreCapa = (capa.Name ?? string.Empty).Trim();

                if (EsNombreMockup(nombreCapa))
                    {
                    AgregarMockup(resultado, nombreCapa, capa.Shapes.Count, "Capa");
                    continue;
                    }

                for (int indice = 1; indice <= capa.Shapes.Count; indice++)
                    {
                    VGCore.Shape objeto = capa.Shapes[indice];
                    string nombre = ObtenerNombreSeguro(objeto);

                    if (EsNombreMockup(nombre))
                        AgregarMockup(resultado, nombre, CantidadElementos(objeto), "Grupo");
                    }
                }
            }

        private void AgregarMockup(List<Mockup> resultado, string nombre, int cantidadElementos, string origen)
            {
            string codigo = AnalizadorMasterCorel.NormalizarCodigo(nombre);
            string producto = ObtenerProducto(nombre);

            Mockup mockup = new Mockup
                {
                Codigo = codigo,
                NombreGrupo = nombre,
                Producto = producto,
                Estado = "Nuevo",
                FechaAnalisis = DateTime.Now,
                Observacion = origen + " válido."
                };

            if (cantidadElementos <= 0)
                {
                mockup.Estado = "Inválido";
                mockup.Observacion = "La " + origen.ToLowerInvariant() + " no contiene elementos.";
                }
            else if (string.IsNullOrWhiteSpace(producto))
                {
                mockup.Estado = "Inválido";
                mockup.Observacion = "Use el formato Mockup_NombreDelProducto.";
                }

            resultado.Add(mockup);
            }

        private bool EsNombreMockup(string nombre)
            {
            return AnalizadorMasterCorel.NormalizarCodigo(nombre)
                .StartsWith("mockup_", StringComparison.OrdinalIgnoreCase);
            }

        private string ObtenerProducto(string nombre)
            {
            string codigo = AnalizadorMasterCorel.NormalizarCodigo(nombre);

            if (!codigo.StartsWith("mockup_", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            string producto = codigo.Substring("mockup_".Length).Replace('_', ' ').Trim();

            return string.IsNullOrWhiteSpace(producto)
                ? string.Empty
                : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(producto);
            }

        private int CantidadElementos(VGCore.Shape objeto)
            {
            try
                {
                if (objeto.Shapes != null)
                    return objeto.Shapes.Count;
                }
            catch { }

            return 1;
            }

        private string ObtenerNombreSeguro(VGCore.Shape objeto)
            {
            try { return (objeto.Name ?? string.Empty).Trim(); }
            catch { return string.Empty; }
            }

        private void MarcarDuplicados(List<Mockup> mockups)
            {
            Dictionary<string, List<Mockup>> grupos = new Dictionary<string, List<Mockup>>(StringComparer.OrdinalIgnoreCase);

            foreach (Mockup mockup in mockups)
                {
                if (!grupos.ContainsKey(mockup.Codigo))
                    grupos.Add(mockup.Codigo, new List<Mockup>());

                grupos[mockup.Codigo].Add(mockup);
                }

            foreach (KeyValuePair<string, List<Mockup>> grupo in grupos)
                {
                if (grupo.Value.Count <= 1)
                    continue;

                foreach (Mockup mockup in grupo.Value)
                    {
                    mockup.Estado = "Duplicado";
                    mockup.Observacion = "Existe más de una capa o grupo con el nombre " + mockup.NombreGrupo + ".";
                    }
                }
            }

        private VGCore.Document BuscarDocumentoAbierto(VGCore.Application corelApp, string rutaArchivo)
            {
            string ruta = Path.GetFullPath(rutaArchivo);

            for (int i = 1; i <= corelApp.Documents.Count; i++)
                {
                try
                    {
                    string abierta = corelApp.Documents[i].FullFileName;

                    if (!string.IsNullOrWhiteSpace(abierta) &&
                        string.Equals(Path.GetFullPath(abierta), ruta, StringComparison.OrdinalIgnoreCase))
                        return corelApp.Documents[i];
                    }
                catch { }
                }

            return null;
            }
        }
    }
