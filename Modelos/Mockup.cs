using System;

namespace TithorAutomation.Modelos
    {
    public sealed class Mockup
        {
        public int Id { get; set; }
        public int MasterId { get; set; }
        public string Codigo { get; set; }
        public string NombreGrupo { get; set; }
        public string Producto { get; set; }
        public string Estado { get; set; }
        public DateTime FechaAnalisis { get; set; }
        public string Observacion { get; set; }

        public bool EsValido
            {
            get
                {
                return !string.IsNullOrWhiteSpace(Codigo) &&
                       !string.IsNullOrWhiteSpace(NombreGrupo) &&
                       Estado != "Inválido" &&
                       Estado != "Duplicado";
                }
            }
        }

    public sealed class ArchivoMockupMaster
        {
        public int Id { get; set; }
        public string RutaArchivo { get; set; }
        public string NombreArchivo { get; set; }
        public string HashArchivo { get; set; }
        public long TamanoArchivo { get; set; }
        public DateTime FechaModificacionArchivo { get; set; }
        public DateTime? FechaUltimoAnalisis { get; set; }
        public int CantidadMockups { get; set; }
        }
    }
