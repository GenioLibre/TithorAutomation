using System.Collections.Generic;

namespace TithorAutomation.Modelos
    {
    public class PlanProduccion
        {
        public string CodigoProducto { get; set; }

        public List<MoldeProduccion> Moldes { get; private set; }

        public List<string> Advertencias { get; private set; }

        public PlanProduccion()
            {
            CodigoProducto = string.Empty;
            Moldes = new List<MoldeProduccion>();
            Advertencias = new List<string>();
            }

        public int TotalMoldes
            {
            get
                {
                return Moldes.Count;
                }
            }

        public bool EsValido
            {
            get
                {
                return Moldes.Count > 0 && Advertencias.Count == 0;
                }
            }
        }
    }