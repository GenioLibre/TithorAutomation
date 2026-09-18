using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TithorAutomation.Modelos;

namespace TithorAutomation.Servicios
    {
    public class AnalizadorMasterCorel
        {
        public List<Molde> Analizar(VGCore.Application corelApp, string rutaArchivo)
            {
            if (corelApp == null)
                throw new InvalidOperationException("No existe conexión con CorelDRAW.");

            if (string.IsNullOrWhiteSpace(rutaArchivo) || !File.Exists(rutaArchivo))
                throw new FileNotFoundException("No se encontró el archivo Master.", rutaArchivo);

            List<Molde> moldes = new List<Molde>();
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

                for (int numeroPagina = 1; numeroPagina <= documento.Pages.Count; numeroPagina++)
                    {
                    AnalizarPagina(documento.Pages[numeroPagina], numeroPagina, moldes);
                    }

                MarcarCodigosDuplicados(moldes);
                return moldes;
                }
            finally
                {
                if (cerrarAlFinal && documento != null)
                    {
                    try
                        {
                        documento.Close();
                        }
                    catch
                        {
                        }
                    }
                }
            }
        private void AnalizarPagina(VGCore.Page pagina, int numeroPagina, List<Molde> moldes)
            {
            for (int numeroCapa = 1; numeroCapa <= pagina.Layers.Count; numeroCapa++)
                {
                AnalizarCapa(pagina.Layers[numeroCapa], numeroPagina, moldes);
                }
            }
        private void AnalizarCapa(VGCore.Layer capa, int numeroPagina, List<Molde> moldes)
            {
            for (int indice = 1; indice <= capa.Shapes.Count; indice++)
                {
                VGCore.Shape objetoSuperior = capa.Shapes[indice];
                string nombreGrupo = ObtenerNombreSeguro(objetoSuperior);

                if (!EsGrupoMolde(objetoSuperior, nombreGrupo))
                    continue;

                AnalizarGrupoMolde(
                    objetoSuperior,
                    nombreGrupo,
                    numeroPagina,
                    capa.Name,
                    moldes
                );
                }
            }
        private bool EsGrupoMolde(VGCore.Shape objeto, string nombre)
            {
            if (objeto == null)
                return false;

            string codigo = NormalizarCodigo(nombre);

            if (!codigo.StartsWith("molde_", StringComparison.OrdinalIgnoreCase))
                return false;

            try
                {
                return objeto.Shapes != null;
                }
            catch
                {
                return false;
                }
            }
        private void AnalizarGrupoMolde(VGCore.Shape grupo, string nombreGrupo, int numeroPagina, string nombreCapa, List<Molde> moldes)
            {
            string codigoGrupo = NormalizarCodigo(nombreGrupo);
            string talla = ObtenerUltimoSegmento(codigoGrupo).ToUpperInvariant();
            int cantidadElementos = 0;

            try
                {
                cantidadElementos = grupo.Shapes.Count;
                }
            catch
                {
                cantidadElementos = 0;
                }

            if (cantidadElementos == 0)
                {
                moldes.Add(CrearMoldeInvalido(
                    CrearCodigoEstructural(nombreCapa, codigoGrupo, "sin_elementos"),
                    nombreGrupo,
                    talla,
                    numeroPagina,
                    nombreCapa,
                    0,
                    "El grupo \"" + nombreGrupo + "\" no contiene elementos."
                ));

                return;
                }

            for (int indice = 1; indice <= cantidadElementos; indice++)
                {
                moldes.Add(CrearMoldeDesdeElemento(
                    grupo.Shapes[indice],
                    codigoGrupo,
                    nombreGrupo,
                    talla,
                    numeroPagina,
                    nombreCapa,
                    indice
                ));
                }
            }
        private Molde CrearMoldeDesdeElemento(VGCore.Shape objeto, string codigoGrupo, string nombreGrupo, string talla, int numeroPagina, string nombreCapa, int indiceObjeto)
            {
            string nombreElemento = ObtenerNombreSeguro(objeto);
            string codigoElemento = NormalizarCodigo(nombreElemento);

            if (string.IsNullOrWhiteSpace(codigoElemento))
                {
                string codigoTemporal =
                    CrearCodigoEstructural(
                        nombreCapa,
                        codigoGrupo,
                        CrearCodigoTemporal(numeroPagina, nombreCapa, indiceObjeto)
                    );

                return CrearMoldeInvalido(
                    codigoTemporal,
                    nombreElemento,
                    talla,
                    numeroPagina,
                    nombreCapa,
                    indiceObjeto,
                    "El elemento " + indiceObjeto + " del grupo \"" + nombreGrupo + "\" no tiene nombre."
                );
                }

            string codigoPieza = ObtenerCodigoPieza(codigoElemento, talla);

            return new Molde
                {
                Codigo = CrearCodigoEstructural(nombreCapa, codigoGrupo, codigoElemento),
                NombreObjeto = nombreElemento,
                Pieza = ConvertirCodigoATexto(codigoPieza),
                Talla = talla,
                Corte = string.Empty,
                Manga = string.Empty,
                Cuello = string.Empty,
                Pagina = numeroPagina,
                Capa = nombreCapa,
                Estado = "Nuevo",
                FechaAnalisis = DateTime.Now,
                IndiceObjeto = indiceObjeto,
                Observacion =
                    "Elemento válido del grupo " +
                    nombreGrupo +
                    " en la capa " +
                    nombreCapa +
                    "."
                };
            }
        private Molde CrearMoldeInvalido(string codigo, string nombreObjeto, string talla, int pagina, string capa, int indice, string observacion)
            {
            return new Molde
                {
                Codigo = codigo,
                NombreObjeto = nombreObjeto,
                Pieza = string.Empty,
                Talla = talla,
                Corte = string.Empty,
                Manga = string.Empty,
                Cuello = string.Empty,
                Pagina = pagina,
                Capa = capa,
                Estado = "Inválido",
                FechaAnalisis = DateTime.Now,
                IndiceObjeto = indice,
                Observacion = observacion
                };
            }
        private string CrearCodigoEstructural(string nombreCapa, string codigoGrupo, string codigoElemento)
            {
            string codigoCapa = NormalizarCodigo(nombreCapa);

            if (string.IsNullOrWhiteSpace(codigoCapa))
                codigoCapa = "sin_capa";

            return codigoCapa + "__" + codigoGrupo + "__" + codigoElemento;
            }
        private string ObtenerCodigoPieza(string codigoElemento, string talla)
            {
            if (string.IsNullOrWhiteSpace(codigoElemento))
                return string.Empty;

            string tallaNormalizada = NormalizarCodigo(talla);

            if (!string.IsNullOrWhiteSpace(tallaNormalizada))
                {
                string marcador = "_" + tallaNormalizada + "_";
                int posicion = codigoElemento.IndexOf(marcador, StringComparison.OrdinalIgnoreCase);

                if (posicion >= 0)
                    {
                    string resultado = codigoElemento.Substring(posicion + marcador.Length);

                    if (!string.IsNullOrWhiteSpace(resultado))
                        return resultado;
                    }
                }

            return codigoElemento;
            }
        private string ObtenerUltimoSegmento(string codigoGrupo)
            {
            if (string.IsNullOrWhiteSpace(codigoGrupo))
                return string.Empty;

            string[] partes = codigoGrupo.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);

            return partes.Length == 0
                ? string.Empty
                : partes[partes.Length - 1];
            }
        private string ConvertirCodigoATexto(string codigo)
            {
            if (string.IsNullOrWhiteSpace(codigo))
                return string.Empty;

            string texto = codigo.Replace('_', ' ').Trim();

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(texto);
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
        private void MarcarCodigosDuplicados(List<Molde> moldes)
            {
            Dictionary<string, List<Molde>> agrupados =
                new Dictionary<string, List<Molde>>(StringComparer.OrdinalIgnoreCase);

            foreach (Molde molde in moldes)
                {
                if (string.IsNullOrWhiteSpace(molde.Codigo))
                    continue;

                if (!agrupados.ContainsKey(molde.Codigo))
                    agrupados.Add(molde.Codigo, new List<Molde>());

                agrupados[molde.Codigo].Add(molde);
                }

            foreach (KeyValuePair<string, List<Molde>> grupo in agrupados)
                {
                if (grupo.Value.Count <= 1)
                    continue;

                foreach (Molde molde in grupo.Value)
                    {
                    molde.Estado = "Duplicado";
                    molde.Observacion =
                        "Existen varios elementos con el código \"" +
                        molde.Codigo +
                        "\".";
                    }
                }
            }
        private VGCore.Document BuscarDocumentoAbierto(VGCore.Application corelApp, string rutaArchivo)
            {
            string rutaBuscada = Path.GetFullPath(rutaArchivo);

            for (int i = 1; i <= corelApp.Documents.Count; i++)
                {
                VGCore.Document documento = corelApp.Documents[i];

                try
                    {
                    string rutaDocumento = documento.FullFileName;

                    if (string.IsNullOrWhiteSpace(rutaDocumento))
                        continue;

                    if (string.Equals(
                        Path.GetFullPath(rutaDocumento),
                        rutaBuscada,
                        StringComparison.OrdinalIgnoreCase))
                        {
                        return documento;
                        }
                    }
                catch
                    {
                    }
                }

            return null;
            }
        public static string NormalizarCodigo(string texto)
            {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            string normalizado =
                texto.Trim()
                     .ToLowerInvariant()
                     .Normalize(NormalizationForm.FormD);

            StringBuilder resultado = new StringBuilder();
            bool ultimoFueSeparador = false;

            foreach (char caracter in normalizado)
                {
                UnicodeCategory categoria = CharUnicodeInfo.GetUnicodeCategory(caracter);

                if (categoria == UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsLetterOrDigit(caracter))
                    {
                    resultado.Append(caracter);
                    ultimoFueSeparador = false;
                    }
                else if (!ultimoFueSeparador && resultado.Length > 0)
                    {
                    resultado.Append('_');
                    ultimoFueSeparador = true;
                    }
                }

            return resultado.ToString().Trim('_');
            }
        private string CrearCodigoTemporal(int pagina, string capa, int indice)
            {
            string codigoCapa = NormalizarCodigo(capa);

            if (string.IsNullOrWhiteSpace(codigoCapa))
                codigoCapa = "sin_capa";

            return "sin_nombre_p" + pagina + "_" + codigoCapa + "_" + indice;
            }
        }
    }
