using System;

namespace TithorAutomation.Modelos
    {
    public class ArchivoMaster
        {
        public int Id { get; set; }

        public int ProductoId { get; set; }

        public string RutaArchivo { get; set; }

        public string NombreArchivo { get; set; }

        public string HashArchivo { get; set; }

        public long TamanoArchivo { get; set; }

        public DateTime? FechaModificacion { get; set; }

        public DateTime? FechaUltimoAnalisis { get; set; }

        public int CantidadMoldes { get; set; }

        public bool EsPrincipal { get; set; }

        public bool Activo { get; set; }

        public bool ExisteEnDisco
            {
            get
                {
                return !string.IsNullOrWhiteSpace(RutaArchivo) &&
                       System.IO.File.Exists(RutaArchivo);
                }
            }

        public string Estado
            {
            get
                {
                if (string.IsNullOrWhiteSpace(RutaArchivo))
                    return "No configurado";

                if (!ExisteEnDisco)
                    return "Archivo no encontrado";

                if (!FechaUltimoAnalisis.HasValue)
                    return "Pendiente de análisis";

                return "Sincronizado";
                }
            }
        }
    }