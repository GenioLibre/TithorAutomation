using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using TithorAutomation.Modelos;

namespace TithorAutomation.Servicios
    {
    public class LectorPedidoCamisetas : ILectorPedidoProducto
        {
        private readonly HashSet<string> prendasPermitidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
            "camiseta",
            "camiseta_short",
            "bividi"
            };
        private readonly HashSet<string> tallasPermitidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
            "2", "4", "6", "8", "10", "12", "14", "16",
            "xs", "s", "m", "l", "xl", "2xl", "3xl"
            };
        public bool PuedeLeer(string codigoProducto)
            {
            string codigo = (codigoProducto ?? string.Empty).Trim().ToUpperInvariant();

            return codigo == "CAMISETAS" || codigo.Contains("CAMISETA");
            }
        public ResultadoAnalisisPedido Analizar(int productoId, string codigoProducto, string rutaExcel)
            {
            ValidarArchivo(rutaExcel);

            ResultadoAnalisisPedido resultado = new ResultadoAnalisisPedido
                {
                ProductoId = productoId,
                CodigoProducto = codigoProducto,
                RutaExcel = Path.GetFullPath(rutaExcel)
                };

            using (XLWorkbook libro = new XLWorkbook(rutaExcel))
                {
                IXLWorksheet hoja = BuscarHojaPedido(libro);

                if (hoja == null)
                    {
                    throw new InvalidOperationException(
                        "No se encontró una hoja con las columnas Modelo, Nombre, Prenda, Talla Camiseta, Corte, Manga y Cuello."
                    );
                    }

                resultado.NombreHoja = hoja.Name;

                if (!string.Equals(hoja.Name, "Detalle de Camisetas", StringComparison.OrdinalIgnoreCase))
                    {
                    resultado.AdvertenciasGenerales.Add(
                        "La hoja se llama \"" +
                        hoja.Name +
                        "\". Se recomienda usar el nombre \"Detalle de Camisetas\"."
                    );
                    }

                Dictionary<string, int> columnas = ObtenerColumnas(hoja);
                IXLRow ultimaFilaUsada = hoja.LastRowUsed();

                if (ultimaFilaUsada == null || ultimaFilaUsada.RowNumber() < 2)
                    {
                    resultado.AdvertenciasGenerales.Add("El Excel no contiene filas de pedido.");
                    return resultado;
                    }

                for (int numeroFila = 2; numeroFila <= ultimaFilaUsada.RowNumber(); numeroFila++)
                    {
                    if (FilaVacia(hoja, numeroFila, columnas))
                        continue;

                    resultado.Lineas.Add(LeerLinea(hoja, numeroFila, columnas));
                    }
                }

            if (resultado.Lineas.Count == 0)
                resultado.AdvertenciasGenerales.Add("No se encontraron filas con información.");

            if (resultado.TotalFilasOmitidas > 0)
                {
                resultado.AdvertenciasGenerales.Add(
                    resultado.TotalFilasOmitidas +
                    " filas serán omitidas por contener datos incompletos o no compatibles."
                );
                }

            return resultado;
            }
        private void ValidarArchivo(string rutaExcel)
            {
            if (string.IsNullOrWhiteSpace(rutaExcel))
                throw new InvalidOperationException("No se seleccionó un archivo Excel.");

            if (!File.Exists(rutaExcel))
                throw new FileNotFoundException("No se encontró el archivo Excel.", rutaExcel);

            string extension = Path.GetExtension(rutaExcel);

            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase))
                {
                throw new InvalidOperationException("El archivo debe tener extensión .xlsx o .xlsm.");
                }
            }
        private IXLWorksheet BuscarHojaPedido(XLWorkbook libro)
            {
            foreach (IXLWorksheet hoja in libro.Worksheets)
                {
                Dictionary<string, int> columnas = ObtenerColumnas(hoja);

                if (columnas.ContainsKey("modelo") &&
                    columnas.ContainsKey("nombre") &&
                    columnas.ContainsKey("prenda") &&
                    columnas.ContainsKey("talla_camiseta") &&
                    columnas.ContainsKey("corte") &&
                    columnas.ContainsKey("manga") &&
                    columnas.ContainsKey("cuello"))
                    {
                    return hoja;
                    }
                }

            return null;
            }
        private Dictionary<string, int> ObtenerColumnas(IXLWorksheet hoja)
            {
            Dictionary<string, int> columnas =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (IXLCell celda in hoja.Row(1).CellsUsed())
                {
                string encabezado = Normalizar(celda.GetFormattedString());

                if (!string.IsNullOrWhiteSpace(encabezado) &&
                    !columnas.ContainsKey(encabezado))
                    {
                    columnas.Add(encabezado, celda.Address.ColumnNumber);
                    }
                }

            return columnas;
            }
        private bool FilaVacia(IXLWorksheet hoja, int numeroFila, Dictionary<string, int> columnas)
            {
            return string.IsNullOrWhiteSpace(ObtenerTexto(hoja, numeroFila, columnas, "modelo")) &&
                   string.IsNullOrWhiteSpace(ObtenerTexto(hoja, numeroFila, columnas, "nombre")) &&
                   string.IsNullOrWhiteSpace(ObtenerTexto(hoja, numeroFila, columnas, "prenda")) &&
                   string.IsNullOrWhiteSpace(ObtenerTexto(hoja, numeroFila, columnas, "talla_camiseta"));
            }
        private LineaPedido LeerLinea(IXLWorksheet hoja, int numeroFila, Dictionary<string, int> columnas)
            {
            string modelo = Normalizar(ObtenerTexto(hoja, numeroFila, columnas, "modelo"));
            string nombre = ObtenerTexto(hoja, numeroFila, columnas, "nombre");
            string prenda = Normalizar(ObtenerTexto(hoja, numeroFila, columnas, "prenda"));
            string numero = ObtenerTexto(hoja, numeroFila, columnas, "numero");
            string tallaCamiseta = NormalizarTalla(ObtenerTexto(hoja, numeroFila, columnas, "talla_camiseta"));
            string tallaShort = NormalizarTalla(ObtenerTexto(hoja, numeroFila, columnas, "talla_short"));
            string corte = Normalizar(ObtenerTexto(hoja, numeroFila, columnas, "corte"));
            string manga = Normalizar(ObtenerTexto(hoja, numeroFila, columnas, "manga"));
            string cuello = Normalizar(ObtenerTexto(hoja, numeroFila, columnas, "cuello"));

            LineaPedido linea = new LineaPedido
                {
                NumeroFila = numeroFila,
                Diseno = prenda,
                Talla = tallaCamiseta.ToUpperInvariant(),
                Cantidad = 1,
                Notas = CrearNotas(nombre, numero)
                };

            GuardarTodosLosCampos(hoja, numeroFila, columnas, linea);
            linea.AgregarCampo("modelo", modelo);
            linea.AgregarCampo("nombre", nombre);
            linea.AgregarCampo("prenda", prenda);
            linea.AgregarCampo("numero", numero);
            linea.AgregarCampo("talla_camiseta", tallaCamiseta);
            linea.AgregarCampo("talla_short", tallaShort);
            linea.AgregarCampo("corte", corte);
            linea.AgregarCampo("manga", manga);
            linea.AgregarCampo("cuello", cuello);
            linea.AgregarCampo("cantidad", "1");

            if (string.IsNullOrWhiteSpace(modelo))
                linea.MarcarNoProcesable("El modelo está vacío.");

            if (string.IsNullOrWhiteSpace(prenda))
                linea.MarcarNoProcesable("La prenda está vacía.");
            else if (!prendasPermitidas.Contains(prenda))
                linea.MarcarNoProcesable("La prenda \"" + prenda + "\" no está soportada.");

            if (string.IsNullOrWhiteSpace(tallaCamiseta))
                linea.MarcarNoProcesable("La talla de camiseta está vacía.");
            else if (!tallasPermitidas.Contains(tallaCamiseta))
                linea.MarcarNoProcesable("La talla de camiseta \"" + tallaCamiseta + "\" no está soportada.");

            if (string.IsNullOrWhiteSpace(corte))
                linea.MarcarNoProcesable("El corte está vacío.");

            if ((modelo == "clasico" || modelo == "raglan") && string.IsNullOrWhiteSpace(manga))
                linea.MarcarNoProcesable("La manga está vacía.");

            if ((modelo == "clasico" || modelo == "raglan") && string.IsNullOrWhiteSpace(cuello))
                linea.MarcarNoProcesable("El cuello está vacío.");

            if (prenda == "camiseta_short")
                {
                if (string.IsNullOrWhiteSpace(tallaShort))
                    linea.MarcarNoProcesable("La talla de short está vacía.");
                else if (!tallasPermitidas.Contains(tallaShort))
                    linea.MarcarNoProcesable("La talla de short \"" + tallaShort + "\" no está soportada.");
                }

            if (string.IsNullOrWhiteSpace(nombre))
                linea.AgregarAdvertencia("El nombre está vacío.");

            if (string.IsNullOrWhiteSpace(numero))
                linea.AgregarAdvertencia("El número está vacío.");

            return linea;
            }
        private string CrearNotas(string nombre, string numero)
            {
            if (string.IsNullOrWhiteSpace(nombre) && string.IsNullOrWhiteSpace(numero))
                return string.Empty;

            if (string.IsNullOrWhiteSpace(numero))
                return nombre;

            if (string.IsNullOrWhiteSpace(nombre))
                return "N° " + numero;

            return nombre + " | N° " + numero;
            }
        private string NormalizarTalla(string valor)
            {
            return Normalizar(valor).Replace("_", string.Empty);
            }
        private void GuardarTodosLosCampos(IXLWorksheet hoja, int numeroFila, Dictionary<string, int> columnas, LineaPedido linea)
            {
            foreach (KeyValuePair<string, int> columna in columnas)
                {
                string valor = hoja.Cell(numeroFila, columna.Value).GetFormattedString().Trim();
                linea.AgregarCampo(columna.Key, valor);
                }
            }
        private string ObtenerTexto(IXLWorksheet hoja, int numeroFila, Dictionary<string, int> columnas, string nombreColumna)
            {
            int numeroColumna;

            if (!columnas.TryGetValue(nombreColumna, out numeroColumna))
                return string.Empty;

            return hoja.Cell(numeroFila, numeroColumna).GetFormattedString().Trim();
            }
        private string Normalizar(string texto)
            {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            string normalizado =
                texto.Trim()
                     .ToLowerInvariant()
                     .Normalize(NormalizationForm.FormD);

            StringBuilder resultado = new StringBuilder();
            bool ultimoSeparador = false;

            foreach (char caracter in normalizado)
                {
                UnicodeCategory categoria = CharUnicodeInfo.GetUnicodeCategory(caracter);

                if (categoria == UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsLetterOrDigit(caracter))
                    {
                    resultado.Append(caracter);
                    ultimoSeparador = false;
                    }
                else if (!ultimoSeparador && resultado.Length > 0)
                    {
                    resultado.Append('_');
                    ultimoSeparador = true;
                    }
                }

            return resultado.ToString().Trim('_');
            }
        }
    }
