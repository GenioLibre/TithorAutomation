using System;
using System.Collections.Generic;
using System.Linq;

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
        public string Piezas { get; set; }
        public string NombrePagina { get; set; }
        public string Variantes { get; set; }
        public bool EsCamiseta { get { return Codigo == "camisetas_deportivas"; } }
        public List<VarianteMockupCamiseta> ObtenerVariantes()
            {
            return (Variantes ?? string.Empty).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(VarianteMockupCamiseta.DesdeCodigo).Where(x => x != null).ToList();
            }
        public override string ToString() { return Producto; }

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

    public sealed class OpcionesMockupCamiseta
        {
        public string Tipo { get; set; } = "clasico";
        public string Corte { get; set; } = "varon";
        public string Cuello { get; set; } = "redondo";
        }

    public sealed class VarianteMockupCamiseta
        {
        public string Codigo { get; private set; }
        public string Tipo { get; private set; }
        public string Corte { get; private set; }
        public string Cuello { get; private set; }
        public bool EsEspalda { get { return Cuello == "espalda"; } }
        public static VarianteMockupCamiseta DesdeCodigo(string codigo)
            {
            string normal = (codigo ?? string.Empty).Trim().ToLowerInvariant();
            string[] partes = normal.Split('_');
            if (partes.Length < 3 || (partes[0] != "clasico" && partes[0] != "ranglan") ||
                (partes[1] != "varon" && partes[1] != "mujer" && partes[1] != "dama")) return null;
            string cuello = string.Join("_", partes.Skip(2));
            if (cuello.StartsWith("frente_", StringComparison.Ordinal)) cuello = cuello.Substring(7);
            if (string.IsNullOrWhiteSpace(cuello)) return null;
            return new VarianteMockupCamiseta { Codigo = normal, Tipo = partes[0],
                Corte = partes[1] == "varon" ? "varon" : "mujer", Cuello = cuello };
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

