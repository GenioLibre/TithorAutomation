using System;

namespace TithorAutomation.Modelos
    {
    public class Molde
        {
        public int Id { get; set; }

        public int MasterId { get; set; }

        public string Codigo { get; set; }

        public string NombreObjeto { get; set; }

        public string Pieza { get; set; }

        public string Talla { get; set; }

        public string Corte { get; set; }

        public string Manga { get; set; }

        public string Cuello { get; set; }

        public int Pagina { get; set; }

        public string Capa { get; set; }

        public string Estado { get; set; }

        public DateTime FechaAnalisis { get; set; }

        // Identificador temporal del objeto durante el análisis.
        // No se almacena en SQLite.
        public int IndiceObjeto { get; set; }

        // Mensaje de validación mostrado al usuario.
        // No se almacena en SQLite.
        public string Observacion { get; set; }

        public bool EsValido
            {
            get
                {
                return !string.IsNullOrWhiteSpace(Codigo) &&
                       !string.IsNullOrWhiteSpace(NombreObjeto) &&
                       Estado != "Inválido" &&
                       Estado != "Duplicado";
                }
            }
        }
    }