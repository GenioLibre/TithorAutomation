using VGCore;

namespace TithorAutomation.Modelos
    {
    public class PiezaAcomodable
        {
        public int Orden { get; set; }
        public int StaticId { get; set; }
        public int GrupoOrigenStaticId { get; set; }

        public string Nombre { get; set; }
        public string Pieza { get; set; }
        public string Talla { get; set; }
        public string Grupo { get; set; }
        public string Estado { get; set; }

        public double AnchoOriginal { get; set; }
        public double AltoOriginal { get; set; }

        public double AnchoAcomodado { get; set; }
        public double AltoAcomodado { get; set; }

        public double XDestino { get; set; }
        public double YDestino { get; set; }

        public bool Procesable { get; set; }
        public bool Rotada { get; set; }

        public Shape Forma { get; set; }
        public Shape GrupoOrigen { get; set; }
        }
    }