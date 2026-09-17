using System;
using System.Collections.Generic;

namespace TithorAutomation.Modelos
    {
    public class MoldeProduccion
        {
        public string CodigoProducto { get; set; }

        public string Diseno { get; set; }

        public string Talla { get; set; }

        public string Pieza { get; set; }

        public string CodigoMolde { get; set; }

        public int FilaExcel { get; set; }

        public int NumeroUnidad { get; set; }

        public string NombreDestino { get; set; }

        public Dictionary<string, string> Campos { get; private set; }

        public MoldeProduccion()
            {
            CodigoProducto = string.Empty;
            Diseno = string.Empty;
            Talla = string.Empty;
            Pieza = string.Empty;
            CodigoMolde = string.Empty;
            NombreDestino = string.Empty;

            Campos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

        public string ObtenerCampo(string nombre)
            {
            if (string.IsNullOrWhiteSpace(nombre))
                return string.Empty;

            string valor;

            if (Campos.TryGetValue(nombre.Trim(), out valor))
                return valor ?? string.Empty;

            return string.Empty;
            }
        }
    }