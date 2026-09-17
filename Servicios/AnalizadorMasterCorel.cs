using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TithorAutomation.Modelos;

namespace TithorAutomation.Servicios
    {
    public class AnalizadorMasterCorel
        {
        public List<Molde> Analizar(VGCore.Application corelApp,string rutaArchivo)
            {
            if (corelApp == null)
                {
                throw new InvalidOperationException(
                    "No existe conexión con CorelDRAW."
                );
                }

            if (string.IsNullOrWhiteSpace(rutaArchivo) ||
                !File.Exists(rutaArchivo))
                {
                throw new FileNotFoundException(
                    "No se encontró el archivo Master.",
                    rutaArchivo
                );
                }

            List<Molde> moldes = new List<Molde>();

            VGCore.Document documento = null;
            bool cerrarAlFinal = false;

            try
                {
                documento = BuscarDocumentoAbierto(
                    corelApp,
                    rutaArchivo
                );

                if (documento == null)
                    {
                    documento =
                        corelApp.OpenDocument(rutaArchivo);

                    cerrarAlFinal = true;
                    }

                for (int numeroPagina = 1;
                     numeroPagina <= documento.Pages.Count;
                     numeroPagina++)
                    {
                    VGCore.Page pagina =
                        documento.Pages[numeroPagina];

                    AnalizarPagina(
                        pagina,
                        numeroPagina,
                        moldes
                    );
                    }

                MarcarCodigosDuplicados(moldes);

                return moldes;
                }
            finally
                {
                // Solo cerramos el documento si el analizador lo abrió.
                // Como no realizamos cambios, CorelDRAW no debe guardarlo.
                if (cerrarAlFinal && documento != null)
                    {
                    try
                        {
                        documento.Close();
                        }
                    catch
                        {
                        // Evita ocultar el error principal del análisis.
                        }
                    }
                }
            }
        private void AnalizarPagina(VGCore.Page pagina,int numeroPagina,List<Molde> moldes)
            {
            for (int numeroCapa = 1;
                 numeroCapa <= pagina.Layers.Count;
                 numeroCapa++)
                {
                VGCore.Layer capa =
                    pagina.Layers[numeroCapa];

                AnalizarCapa(
                    capa,
                    numeroPagina,
                    moldes
                );
                }
            }
        private void AnalizarCapa(VGCore.Layer capa,int numeroPagina,List<Molde> moldes)
            {
            for (int indice = 1;
                 indice <= capa.Shapes.Count;
                 indice++)
                {
                VGCore.Shape objetoSuperior =
                    capa.Shapes[indice];

                string nombreSuperior =
                    ObtenerNombreSeguro(objetoSuperior);

                if (EsGrupoDeTalla(
                    objetoSuperior,
                    nombreSuperior))
                    {
                    AnalizarGrupoDeTalla(
                        objetoSuperior,
                        numeroPagina,
                        capa.Name,
                        moldes
                    );

                    continue;
                    }

                // También admite un molde nombrado directamente
                // en la capa, fuera de model_s/model_m/model_l.
                if (nombreSuperior.StartsWith(
                    "funda_",
                    StringComparison.OrdinalIgnoreCase))
                    {
                    Molde molde = CrearMoldeFunda(
                        objetoSuperior,
                        numeroPagina,
                        capa.Name,
                        indice,
                        null
                    );

                    moldes.Add(molde);
                    }
                }
            }
        private string ObtenerNombreSeguro(VGCore.Shape objeto)
            {
            try
                {
                return (objeto.Name ?? string.Empty).Trim();
                }
            catch
                {
                return string.Empty;
                }
            }
        private bool EsNombreDefinidoPorUsuario(string nombre)
            {
            if (string.IsNullOrWhiteSpace(nombre))
                return false;

            string texto =
                nombre.Trim().ToLowerInvariant();

            // Nombres automáticos que Corel puede mostrar.
            if (texto.StartsWith("group of ") ||
                texto.StartsWith("grupo de ") ||
                texto.StartsWith("curve ") ||
                texto.StartsWith("curva "))
                {
                return false;
                }

            return true;
            }
        private void MarcarCodigosDuplicados(List<Molde> moldes)
            {
            Dictionary<string, List<Molde>> agrupados =
                new Dictionary<string, List<Molde>>(
                    StringComparer.OrdinalIgnoreCase
                );

            foreach (Molde molde in moldes)
                {
                if (string.IsNullOrWhiteSpace(molde.Codigo))
                    continue;

                if (!agrupados.ContainsKey(molde.Codigo))
                    {
                    agrupados.Add(
                        molde.Codigo,
                        new List<Molde>()
                    );
                    }

                agrupados[molde.Codigo].Add(molde);
                }

            foreach (KeyValuePair<string, List<Molde>> grupo
                     in agrupados)
                {
                if (grupo.Value.Count <= 1)
                    continue;

                foreach (Molde molde in grupo.Value)
                    {
                    molde.Estado = "Duplicado";
                    molde.Observacion =
                        "Existen varios objetos con el código \"" +
                        molde.Codigo +
                        "\".";
                    }
                }
            }
        private VGCore.Document BuscarDocumentoAbierto(VGCore.Application corelApp, string rutaArchivo)
            {
            string rutaBuscada =
                Path.GetFullPath(rutaArchivo);

            for (int i = 1;
                 i <= corelApp.Documents.Count;
                 i++)
                {
                VGCore.Document documento =
                    corelApp.Documents[i];

                try
                    {
                    string rutaDocumento =
                        documento.FullFileName;

                    if (string.IsNullOrWhiteSpace(
                        rutaDocumento))
                        {
                        continue;
                        }

                    if (string.Equals(
                        Path.GetFullPath(rutaDocumento),
                        rutaBuscada,
                        StringComparison.OrdinalIgnoreCase))
                        {
                        return documento;
                        }
                    }
                catch
                    {
                    // Documento nuevo o sin ruta guardada.
                    }
                }

            return null;
            }
        public static string NormalizarCodigo(string texto)
            {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            string normalizado =
                texto.Trim()
                     .ToLowerInvariant()
                     .Normalize(
                         NormalizationForm.FormD
                     );

            StringBuilder resultado =
                new StringBuilder();

            bool ultimoFueSeparador = false;

            foreach (char caracter in normalizado)
                {
                UnicodeCategory categoria =
                    CharUnicodeInfo.GetUnicodeCategory(
                        caracter
                    );

                if (categoria ==
                    UnicodeCategory.NonSpacingMark)
                    {
                    continue;
                    }

                if (char.IsLetterOrDigit(caracter))
                    {
                    resultado.Append(caracter);
                    ultimoFueSeparador = false;
                    }
                else if (!ultimoFueSeparador &&
                         resultado.Length > 0)
                    {
                    resultado.Append('_');
                    ultimoFueSeparador = true;
                    }
                }

            return resultado
                .ToString()
                .Trim('_');
            }
        private string CrearCodigoTemporal(int pagina, string capa, int indice)
            {
            string codigoCapa =
                NormalizarCodigo(capa);

            if (string.IsNullOrWhiteSpace(codigoCapa))
                codigoCapa = "sin_capa";

            return "sin_nombre_p" +
                   pagina +
                   "_" +
                   codigoCapa +
                   "_" +
                   indice;
            }
        private bool EsGrupoDeTalla(VGCore.Shape objeto,string nombre)
            {
            if (objeto == null)
                return false;

            string codigo =
                NormalizarCodigo(nombre);

            return codigo == "molde_s" ||
                   codigo == "molde_m" ||
                   codigo == "molde_l";
            }
        private void AnalizarGrupoDeTalla(VGCore.Shape grupo, int numeroPagina, string nombreCapa, List<Molde> moldes)
            {
            string tallaGrupo =
                ObtenerTallaDelGrupo(
                    ObtenerNombreSeguro(grupo)
                );

            // Solo entra un nivel dentro del grupo.
            // No analiza recursivamente grupos interiores.
            for (int indice = 1;
                 indice <= grupo.Shapes.Count;
                 indice++)
                {
                VGCore.Shape objetoMolde =
                    grupo.Shapes[indice];

                Molde molde = CrearMoldeFunda(
                    objetoMolde,
                    numeroPagina,
                    nombreCapa,
                    indice,
                    tallaGrupo
                );

                moldes.Add(molde);
                }
            }
        private string ObtenerTallaDelGrupo(
            string nombreGrupo)
            {
            string codigo =
                NormalizarCodigo(nombreGrupo);

            if (codigo == "molde_s")
                return "S";

            if (codigo == "molde_m")
                return "M";

            if (codigo == "molde_l")
                return "L";

            return string.Empty;
            }
        private Molde CrearMoldeFunda(VGCore.Shape objeto, int numeroPagina, string nombreCapa, int indiceObjeto, string tallaGrupo)
            {
            string nombre =
                ObtenerNombreSeguro(objeto);

            string codigo =
                NormalizarCodigo(nombre);

            if (string.IsNullOrWhiteSpace(codigo))
                {
                codigo = CrearCodigoTemporal(
                    numeroPagina,
                    nombreCapa,
                    indiceObjeto
                );
                }

            Molde molde = new Molde
                {
                Codigo = codigo,
                NombreObjeto = nombre,
                Pieza = string.Empty,
                Talla = string.Empty,
                Corte = string.Empty,
                Manga = string.Empty,
                Cuello = string.Empty,
                Pagina = numeroPagina,
                Capa = nombreCapa,
                Estado = "Nuevo",
                FechaAnalisis = DateTime.Now,
                IndiceObjeto = indiceObjeto,
                Observacion = string.Empty
                };

            InterpretarNombreFunda(
                molde,
                tallaGrupo
            );

            return molde;
            }
        private void InterpretarNombreFunda(Molde molde,string tallaGrupo)
            {
            if (string.IsNullOrWhiteSpace(
                molde.NombreObjeto))
                {
                MarcarMoldeInvalido(
                    molde,
                    "El objeto no tiene nombre."
                );

                return;
                }

            string[] partes =
                molde.Codigo.Split(
                    new[] { '_' },
                    StringSplitOptions.RemoveEmptyEntries
                );

            if (partes.Length < 3 ||
                partes[0] != "funda")
                {
                MarcarMoldeInvalido(
                    molde,
                    "El nombre debe comenzar con funda_."
                );

                return;
                }

            string talla =
                partes[1].ToUpperInvariant();

            if (talla != "S" &&
                talla != "M" &&
                talla != "L")
                {
                MarcarMoldeInvalido(
                    molde,
                    "La talla debe ser S, M o L."
                );

                return;
                }

            string codigoPieza =
                string.Join(
                    "_",
                    partes,
                    2,
                    partes.Length - 2
                );

            string pieza =
                ObtenerNombrePieza(codigoPieza);

            if (string.IsNullOrWhiteSpace(pieza))
                {
                MarcarMoldeInvalido(
                    molde,
                    "La pieza \"" +
                    codigoPieza +
                    "\" no está reconocida."
                );

                return;
                }

            if (!string.IsNullOrWhiteSpace(tallaGrupo) &&
                !string.Equals(
                    talla,
                    tallaGrupo,
                    StringComparison.OrdinalIgnoreCase))
                {
                MarcarMoldeInvalido(
                    molde,
                    "La talla del objeto no coincide con el grupo " +
                    "molde_" +
                    tallaGrupo.ToLowerInvariant() +
                    "."
                );

                return;
                }

            molde.Talla = talla;
            molde.Pieza = pieza;
            molde.Estado = "Nuevo";
            molde.Observacion =
                "Molde válido encontrado en el Master.";
            }
        private string ObtenerNombrePieza( string codigoPieza)
            {
            switch (codigoPieza)
                {
                case "frente":
                    return "Frente";

                case "espalda":
                    return "Espalda";

                case "lateral_izquierdo":
                    return "Lateral izquierdo";

                case "lateral_derecho":
                    return "Lateral derecho";

                default:
                    return string.Empty;
                }
            }
        private void MarcarMoldeInvalido(Molde molde,string observacion)
            {
            molde.Estado = "Inválido";
            molde.Observacion = observacion;
            }
        }
    }