using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using TithorAutomation.Modelos;

namespace TithorAutomation.Servicios
    {
    public class LectorPedidoFundas : ILectorPedidoProducto
        {
        private readonly HashSet<string> tallasPermitidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "S",
            "M",
            "L"
        };

        public bool PuedeLeer(string codigoProducto)
            {
            string codigo = (codigoProducto ?? string.Empty).Trim().ToUpperInvariant();

            return codigo == "FUNDAS" || codigo.Contains("FUNDA");
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
                        "No se encontró una hoja con las columnas Diseño, Talla y Cantidad."
                    );
                    }

                resultado.NombreHoja = hoja.Name;

                if (!string.Equals(hoja.Name, "Detalle de Fundas", StringComparison.OrdinalIgnoreCase))
                    {
                    resultado.AdvertenciasGenerales.Add(
                        "La hoja se llama \"" + hoja.Name +
                        "\". Se recomienda renombrarla como \"Detalle de Fundas\"."
                    );
                    }

                Dictionary<string, int> columnas = ObtenerColumnas(hoja);
                IXLRow ultimaFilaUsada = hoja.LastRowUsed();

                if (ultimaFilaUsada == null || ultimaFilaUsada.RowNumber() < 2)
                    {
                    resultado.AdvertenciasGenerales.Add(
                        "El Excel no contiene filas de pedido."
                    );

                    return resultado;
                    }

                int ultimaFila = ultimaFilaUsada.RowNumber();

                for (int numeroFila = 2; numeroFila <= ultimaFila; numeroFila++)
                    {
                    if (FilaVacia(hoja, numeroFila, columnas))
                        continue;

                    LineaPedido linea = LeerLinea(hoja, numeroFila, columnas);
                    resultado.Lineas.Add(linea);
                    }
                }

            if (resultado.Lineas.Count == 0)
                {
                resultado.AdvertenciasGenerales.Add(
                    "No se encontraron filas con información."
                );
                }

            if (resultado.TotalFilasOmitidas > 0)
                {
                resultado.AdvertenciasGenerales.Add(
                    resultado.TotalFilasOmitidas +
                    " filas serán omitidas por contener datos no compatibles."
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
                throw new InvalidOperationException(
                    "El archivo debe tener extensión .xlsx o .xlsm."
                );
                }
            }

        private IXLWorksheet BuscarHojaPedido(XLWorkbook libro)
            {
            foreach (IXLWorksheet hoja in libro.Worksheets)
                {
                Dictionary<string, int> columnas = ObtenerColumnas(hoja);

                if (columnas.ContainsKey("diseno") &&
                    columnas.ContainsKey("talla") &&
                    columnas.ContainsKey("cantidad"))
                    {
                    return hoja;
                    }
                }

            return null;
            }

        private Dictionary<string, int> ObtenerColumnas(IXLWorksheet hoja)
            {
            Dictionary<string, int> columnas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (IXLCell celda in hoja.Row(1).CellsUsed())
                {
                string encabezado = NormalizarNombreCampo(celda.GetFormattedString());

                if (!string.IsNullOrWhiteSpace(encabezado) && !columnas.ContainsKey(encabezado))
                    columnas.Add(encabezado, celda.Address.ColumnNumber);
                }

            return columnas;
            }

        private bool FilaVacia(IXLWorksheet hoja, int numeroFila, Dictionary<string, int> columnas)
            {
            string diseno = ObtenerTexto(hoja, numeroFila, columnas, "diseno");
            string talla = ObtenerTexto(hoja, numeroFila, columnas, "talla");
            string cantidad = ObtenerTexto(hoja, numeroFila, columnas, "cantidad");
            string notas = ObtenerTexto(hoja, numeroFila, columnas, "notas");

            return string.IsNullOrWhiteSpace(diseno) &&
                   string.IsNullOrWhiteSpace(talla) &&
                   string.IsNullOrWhiteSpace(cantidad) &&
                   string.IsNullOrWhiteSpace(notas);
            }

        private LineaPedido LeerLinea(IXLWorksheet hoja, int numeroFila, Dictionary<string, int> columnas)
            {
            LineaPedido linea = new LineaPedido
                {
                NumeroFila = numeroFila,
                Diseno = ObtenerTexto(hoja, numeroFila, columnas, "diseno"),
                Talla = ObtenerTexto(hoja, numeroFila, columnas, "talla").ToUpperInvariant(),
                Notas = ObtenerTexto(hoja, numeroFila, columnas, "notas")
                };

            GuardarTodosLosCampos(hoja, numeroFila, columnas, linea);

            if (string.IsNullOrWhiteSpace(linea.Diseno))
                linea.MarcarNoProcesable("La columna Diseño está vacía.");

            if (string.IsNullOrWhiteSpace(linea.Talla))
                {
                linea.MarcarNoProcesable("La talla está vacía.");
                }
            else if (!tallasPermitidas.Contains(linea.Talla))
                {
                linea.MarcarNoProcesable(
                    "La talla \"" + linea.Talla + "\" no existe en el Master actual."
                );
                }

            IXLCell celdaCantidad = hoja.Cell(numeroFila, columnas["cantidad"]);
            int cantidad;

            if (!IntentarLeerCantidad(celdaCantidad, out cantidad))
                {
                linea.MarcarNoProcesable(
                    "La cantidad debe ser un número entero mayor que cero."
                );
                }
            else
                {
                linea.Cantidad = cantidad;
                linea.AgregarCampo("cantidad", cantidad.ToString(CultureInfo.InvariantCulture));
                }

            return linea;
            }

        private bool IntentarLeerCantidad(IXLCell celda, out int cantidad)
            {
            cantidad = 0;
            // Value evalúa también fórmulas. El formato visual nunca determina la cantidad.
            XLCellValue valor = celda.Value;
            if (valor.IsNumber)
                {
                double numero = valor.GetNumber();
                if (double.IsNaN(numero) || double.IsInfinity(numero) ||
                    numero <= 0 || numero > int.MaxValue || numero != Math.Truncate(numero))
                    return false;
                cantidad = (int)numero;
                return true;
                }
            // En texto solo se aceptan dígitos enteros, sin separadores ambiguos.
            return valor.IsText &&
                int.TryParse(valor.GetText().Trim(), NumberStyles.None,
                    CultureInfo.InvariantCulture, out cantidad) && cantidad > 0;
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

        private string NormalizarNombreCampo(string texto)
            {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            string normalizado = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
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
