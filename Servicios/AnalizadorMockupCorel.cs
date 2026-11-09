using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TithorAutomation.Modelos;

namespace TithorAutomation.Servicios
{
    public sealed class AnalizadorMockupCorel
    {
        public List<Mockup> Analizar(VGCore.Application corelApp, string rutaArchivo)
        {
            if (corelApp == null) throw new InvalidOperationException("No existe conexión con CorelDRAW.");
            if (string.IsNullOrWhiteSpace(rutaArchivo) || !File.Exists(rutaArchivo))
                throw new FileNotFoundException("No se encontró el Master de mockups.", rutaArchivo);
            VGCore.Document documento = null;
            bool cerrar = false;
            try
            {
                for (int i = 1; i <= corelApp.Documents.Count; i++)
                {
                    var abierto = corelApp.Documents[i];
                    if (string.Equals(abierto.FullFileName, Path.GetFullPath(rutaArchivo), StringComparison.OrdinalIgnoreCase))
                    { documento = abierto; break; }
                }
                if (documento == null) { documento = corelApp.OpenDocument(rutaArchivo); cerrar = true; }
                if (documento.Dirty)
                    throw new InvalidOperationException("Guarde los cambios del master en CorelDRAW y vuelva a analizar. La sincronización utiliza el archivo guardado.");
                var resultado = new List<Mockup>();
                for (int i = 1; i <= documento.Pages.Count; i++)
                {
                    VGCore.Page pagina = documento.Pages[i];
                    string codigo = CodigoPagina(pagina.Name);
                    if (codigo == null) continue;
                    var mockup = new Mockup { Codigo = codigo, NombrePagina = pagina.Name, NombreGrupo = pagina.Name,
                        Producto = codigo == "fundas" ? "Fundas" : "Camisetas deportivas", Estado = "Nuevo", FechaAnalisis = DateTime.Now };
                    try
                    {
                        if (mockup.EsCamiseta)
                        {
                            var grupos = EditorMockupCorel.BuscarVariantesPagina(pagina);
                            if (grupos.Count == 0) throw new InvalidOperationException("La página no contiene grupos como clasico_varon_redondo o clasico_varon_espalda.");
                            var variantes = grupos.Select(x => VarianteMockupCamiseta.DesdeCodigo(EditorMockupCorel.Normalizar(x.Item2.Name))).ToList();
                            if (variantes.GroupBy(x => new { x.Tipo, x.Corte, x.Cuello }).Any(x => x.Count() > 1))
                                throw new InvalidOperationException("Hay variantes duplicadas para el mismo tipo, corte y cuello.");
                            foreach (var variante in variantes.Where(x => !x.EsEspalda))
                                if (!variantes.Any(x => x.EsEspalda && x.Tipo == variante.Tipo && x.Corte == variante.Corte))
                                    throw new InvalidOperationException("Falta la espalda de " + variante.Tipo + " " + variante.Corte + ".");
                            if (!variantes.Any(x => x.Tipo == "clasico" && x.Corte == "varon" && x.Cuello == "redondo") ||
                                !variantes.Any(x => x.Tipo == "clasico" && x.Corte == "varon" && x.EsEspalda))
                                throw new InvalidOperationException("La vista genérica requiere clasico_varon_redondo y clasico_varon_espalda.");
                            var piezas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var grupo in grupos)
                            {
                                try { foreach (var campo in EditorMockupCorel.DetectarCamposCamiseta(grupo.Item2.Shapes)) piezas.Add(campo.Nombre); }
                                catch (Exception ex) { throw new InvalidOperationException(grupo.Item2.Name + ": " + ex.Message, ex); }
                            }
                            mockup.Variantes = string.Join("\n", variantes.Select(x => x.Codigo));
                            mockup.Piezas = string.Join(", ", piezas.OrderBy(x => x));
                            mockup.Observacion = variantes.Count + " variantes registradas en la página.";
                        }
                        else
                        {
                            var contenedores = EditorMockupCorel.BuscarContenedoresFundas(pagina);
                            if (contenedores.Count != 1) throw new InvalidOperationException("La página Fundas debe contener una sola capa o grupo Diseño / Mockup Fundas.");
                            var contenedor = contenedores[0];
                            var objetos = contenedor.Item2 == null ? contenedor.Item1.Shapes : contenedor.Item2.Shapes;
                            string nombre = contenedor.Item2 == null ? contenedor.Item1.Name : contenedor.Item2.Name;
                            var campos = EditorMockupCorel.DetectarCamposFundas(objetos, nombre);
                            mockup.Piezas = string.Join(", ", campos.Select(x => x.Nombre));
                            mockup.Observacion = "Página Fundas válida.";
                        }
                    }
                    catch (Exception ex) { mockup.Estado = "Inválido"; mockup.Observacion = ex.Message; }
                    resultado.Add(mockup);
                }
                foreach (var repetidos in resultado.GroupBy(x => x.Codigo).Where(x => x.Count() > 1))
                    foreach (var mockup in repetidos) { mockup.Estado = "Duplicado"; mockup.Observacion = "Existe más de una página para " + mockup.Producto + "."; }
                if (resultado.Count == 0) throw new InvalidOperationException("Nombre las páginas Fundas y Camisetas deportivas (también se acepta Camisetas).");
                return resultado;
            }
            finally { if (cerrar && documento != null) { try { documento.Close(); } catch { } } }
        }

        public static string CodigoPagina(string nombre)
        {
            string codigo = EditorMockupCorel.Normalizar(nombre);
            if (codigo == "fundas") return "fundas";
            if (codigo == "camisetas" || codigo == "camisetas_deportivas") return "camisetas_deportivas";
            return null;
        }
    }
}
