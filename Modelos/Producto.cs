using System;

namespace TithorAutomation.Modelos
{
    public class Producto
    {
        public int Id { get; set; }

        public string Nombre { get; set; }

        public string Codigo { get; set; }

        public string Descripcion { get; set; }

        public bool Activo { get; set; }

        public DateTime FechaCreacion { get; set; }

        public override string ToString()
        {
            return Nombre;
        }
    }
}