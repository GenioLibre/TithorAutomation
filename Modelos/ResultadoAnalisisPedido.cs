using System;
using System.Collections.Generic;

namespace TithorAutomation.Modelos
    {
    public class ResultadoAnalisisPedido
        {
        public int ProductoId { get; set; }

        public string CodigoProducto { get; set; }

        public string RutaExcel { get; set; }

        public string NombreHoja { get; set; }

        public List<LineaPedido> Lineas { get; private set; }

        public List<string> AdvertenciasGenerales { get; private set; }

        public ResultadoAnalisisPedido()
            {
            CodigoProducto = string.Empty;
            RutaExcel = string.Empty;
            NombreHoja = string.Empty;

            Lineas = new List<LineaPedido>();
            AdvertenciasGenerales = new List<string>();
            }

        public int TotalFilas
            {
            get
                {
                return Lineas.Count;
                }
            }

        public int TotalFilasProcesables
            {
            get
                {
                int total = 0;

                foreach (LineaPedido linea in Lineas)
                    {
                    if (linea.Procesable)
                        total++;
                    }

                return total;
                }
            }

        public int TotalFilasOmitidas
            {
            get
                {
                return TotalFilas - TotalFilasProcesables;
                }
            }

        public int TotalUnidades
            {
            get
                {
                int total = 0;

                foreach (LineaPedido linea in Lineas)
                    {
                    if (linea.Procesable)
                        total += linea.Cantidad;
                    }

                return total;
                }
            }

        public bool PuedeAprobar
            {
            get
                {
                return TotalFilasProcesables > 0;
                }
            }

        public List<string> ObtenerDisenos()
            {
            List<string> disenos = new List<string>();

            HashSet<string> encontrados =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase
                );

            foreach (LineaPedido linea in Lineas)
                {
                if (!linea.Procesable ||
                    string.IsNullOrWhiteSpace(linea.Diseno))
                    {
                    continue;
                    }

                if (encontrados.Add(linea.Diseno))
                    disenos.Add(linea.Diseno);
                }

            return disenos;
            }

        public List<string> ObtenerTallas()
            {
            List<string> tallas = new List<string>();

            HashSet<string> encontradas =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase
                );

            foreach (LineaPedido linea in Lineas)
                {
                if (!linea.Procesable ||
                    string.IsNullOrWhiteSpace(linea.Talla))
                    {
                    continue;
                    }

                if (encontradas.Add(linea.Talla))
                    tallas.Add(linea.Talla);
                }

            return tallas;
            }
        }
    }