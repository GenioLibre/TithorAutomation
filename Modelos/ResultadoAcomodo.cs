using System.Collections.Generic;

namespace TithorAutomation.Modelos
    {
    public class ResultadoAcomodo
        {
        public ResultadoAcomodo()
            {
            Piezas = new List<PiezaAcomodable>();
            }

        public List<PiezaAcomodable> Piezas { get; set; }

        public double AnchoMaterial { get; set; }
        public double Separacion { get; set; }
        public double AltoEstimado { get; set; }

        public int TotalPiezas
            {
            get
                {
                return Piezas.Count;
                }
            }

        public int TotalProcesables
            {
            get
                {
                return Piezas.FindAll(x => x.Procesable).Count;
                }
            }

        public int TotalNoProcesables
            {
            get
                {
                return Piezas.FindAll(x => !x.Procesable).Count;
                }
            }
        }
    }