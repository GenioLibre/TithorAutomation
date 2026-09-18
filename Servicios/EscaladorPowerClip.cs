using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using VGCore;
using TithorAutomation.Modelos;

namespace TithorAutomation.Servicios
{
    public sealed class PiezaEscalable
    {
        public string Pieza { get; set; }
        public string Talla { get; set; }
        public Shape Contenedor { get; set; }
        public string Estado { get; set; }
        public string Clave { get; set; }
        public bool TieneContenido { get; set; }
        public string Diseno { get; set; }
        public string NombreGrupo { get; set; }
        public int FilaExcel { get; set; }
    }

    public sealed class EscaladorPowerClip
    {
        public static bool InterpretarNombre(string nombre, out string pieza, out string talla)
        {
            pieza = talla = null;
            string codigo = AnalizadorMasterCorel.NormalizarCodigo(nombre ?? "");
            Match match = Regex.Match(codigo, @"^funda_(s|m|l)_(frente|espalda|lateral_derecho|lateral_izquierdo)$");
            if (!match.Success) return false;
            talla = match.Groups[1].Value.ToUpperInvariant();
            switch (match.Groups[2].Value)
            {
                case "frente": pieza = "Frente"; break;
                case "espalda": pieza = "Espalda"; break;
                case "lateral_derecho": pieza = "Lateral derecho"; break;
                default: pieza = "Lateral izquierdo"; break;
            }
            return true;
        }

        public static void CalcularTamanoCobertura(double anchoDiseno, double altoDiseno, double anchoDestino, double altoDestino, out double anchoFinal, out double altoFinal)
            {
            if (!Positivo(anchoDiseno) || !Positivo(altoDiseno) || !Positivo(anchoDestino) || !Positivo(altoDestino))
                throw new InvalidOperationException("El diseño y el molde deben tener dimensiones mayores que cero.");

            double escalaPorAlto = altoDestino / altoDiseno;

            anchoFinal = anchoDiseno * escalaPorAlto;
            altoFinal = altoDestino;

            if (anchoFinal < anchoDestino)
                {
                double escalaPorAncho = anchoDestino / anchoDiseno;

                anchoFinal = anchoDestino;
                altoFinal = altoDiseno * escalaPorAncho;
                }

            if (!Positivo(anchoFinal) || !Positivo(altoFinal))
                throw new InvalidOperationException("Las dimensiones resultantes no son válidas.");
            }

        private static bool Positivo(double valor) => valor > 0 && !double.IsNaN(valor) && !double.IsInfinity(valor);

        public static string TallaDelGrupo(string nombre)
        {
            string codigo = AnalizadorMasterCorel.NormalizarCodigo(nombre ?? "");
            // Compatibilidad con documentos copiados antes de conservar los nombres originales.
            Match match = Regex.Match(codigo, @"^(?:prod_.+_)?molde_(s|m|l)$");
            return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
        }

        public static bool InterpretarPiezaEnGrupo(string nombre, string tallaGrupo, out string pieza, out string talla)
        {
            if (InterpretarNombre(nombre, out pieza, out talla))
                return tallaGrupo == null || talla == tallaGrupo;
            if (tallaGrupo == null) return false;
            string codigo = AnalizadorMasterCorel.NormalizarCodigo(nombre ?? "");
            if (codigo == "lado_derecho") codigo = "lateral_derecho";
            if (codigo == "lado_izquierdo") codigo = "lateral_izquierdo";
            return InterpretarNombre("funda_" + tallaGrupo.ToLowerInvariant() + "_" + codigo, out pieza, out talla);
        }

        public static bool MismoDocumento(Document a, Document b)
        {
            if (a == null || b == null) return false;
            IntPtr pa = IntPtr.Zero, pb = IntPtr.Zero;
            try
            {
                pa = Marshal.GetIUnknownForObject(a);
                pb = Marshal.GetIUnknownForObject(b);
                return pa == pb;
            }
            finally
            {
                if (pa != IntPtr.Zero) Marshal.Release(pa);
                if (pb != IntPtr.Zero) Marshal.Release(pb);
            }
        }

        public List<PiezaEscalable> Analizar(Document documento)
        {
            var resultado = new List<PiezaEscalable>();
            for (int p = 1; p <= documento.Pages.Count; p++)
            {
                Page pagina = documento.Pages[p];
                for (int l = 1; l <= pagina.Layers.Count; l++)
                {
                    Layer capa = pagina.Layers[l];
                    // El copiador crea esta capa. Se excluyen masters y diseños de otras capas.
                    if (!string.Equals(capa.Name, "TITHOR_PRODUCCION", StringComparison.OrdinalIgnoreCase)) continue;
                    Recorrer(capa.Shapes, resultado, p, !capa.Editable || !capa.Visible);
                }
            }
            return resultado;
        }

        private void Recorrer(Shapes objetos, List<PiezaEscalable> resultado, int pagina, bool bloqueado, string tallaGrupo = null)
        {
            for (int i = 1; i <= objetos.Count; i++)
            {
                Shape objeto = objetos[i];
                bool noEditable = bloqueado || objeto.Locked;
                string pieza, talla;
                string tallaPropia = objeto.Type == cdrShapeType.cdrGroupShape ? TallaDelGrupo(objeto.Name) : null;
                if (tallaPropia != null)
                {
                    Recorrer(objeto.Shapes, resultado, pagina, noEditable, tallaPropia);
                    continue;
                }
                if (InterpretarPiezaEnGrupo(objeto.Name, tallaGrupo, out pieza, out talla))
                {
                    var entrada = new PiezaEscalable { Pieza = pieza, Talla = talla };
                    Shape contenedor = ResolverContenedor(objeto);
                    entrada.Contenedor = contenedor;
                    entrada.Clave = pagina + ":" + objeto.StaticID;
                    entrada.Estado = contenedor == null ? "Sin contenedor único" :
                        noEditable || RutaBloqueada(objeto, contenedor, false) ? "Bloqueado u oculto" :
                        !Positivo(contenedor.SizeHeight) || !Positivo(contenedor.SizeWidth) ? "Dimensiones inválidas" : "Listo";
                    entrada.TieneContenido = contenedor != null && contenedor.PowerClip != null && contenedor.PowerClip.Shapes.Count > 0;
                    resultado.Add(entrada);
                    continue;
                }
                // No se recorren los diseños dentro de los PowerClips.
                if (objeto.Type == cdrShapeType.cdrGroupShape && objeto.PowerClip == null)
                    Recorrer(objeto.Shapes, resultado, pagina, noEditable, tallaGrupo);
            }
        }

        public List<PiezaEscalable> AnalizarPedido(Document documento, PlanProduccion plan)
        {
            if (documento == null) throw new ArgumentNullException(nameof(documento));
            if (plan == null || plan.Moldes == null || plan.Moldes.Count == 0)
                throw new InvalidOperationException("No existe un pedido copiado para iniciar el escalado guiado.");

            Dictionary<string, Shape> grupos = ObtenerGruposProduccion(documento);
            List<PiezaEscalable> resultado = new List<PiezaEscalable>();

            foreach (MoldeProduccion solicitud in plan.Moldes)
            {
                List<string> nombresPiezas = ObtenerPiezasSolicitud(solicitud);
                Shape grupo = null;

                if (!string.IsNullOrWhiteSpace(solicitud.NombreDestino))
                    grupos.TryGetValue(solicitud.NombreDestino, out grupo);

                foreach (string nombrePieza in nombresPiezas)
                    resultado.Add(CrearPiezaPedido(grupo, solicitud, nombrePieza));
            }

            return resultado;
        }

        private Dictionary<string, Shape> ObtenerGruposProduccion(Document documento)
        {
            Dictionary<string, Shape> grupos = new Dictionary<string, Shape>(StringComparer.OrdinalIgnoreCase);

            for (int p = 1; p <= documento.Pages.Count; p++)
            {
                Page pagina = documento.Pages[p];

                for (int l = 1; l <= pagina.Layers.Count; l++)
                {
                    Layer capa = pagina.Layers[l];

                    if (!string.Equals(capa.Name, "TITHOR_PRODUCCION", StringComparison.OrdinalIgnoreCase))
                        continue;

                    for (int i = 1; i <= capa.Shapes.Count; i++)
                    {
                        Shape objeto = capa.Shapes[i];
                        string nombre = ObtenerNombreSeguro(objeto);

                        if (!string.IsNullOrWhiteSpace(nombre) && !grupos.ContainsKey(nombre))
                            grupos.Add(nombre, objeto);
                    }
                }
            }

            return grupos;
        }

        private List<string> ObtenerPiezasSolicitud(MoldeProduccion solicitud)
        {
            List<string> piezas = new List<string>();

            if (solicitud.PiezasIncluidas != null && solicitud.PiezasIncluidas.Count > 0)
            {
                piezas.AddRange(solicitud.PiezasIncluidas);
                return piezas;
            }

            if ((solicitud.CodigoProducto ?? string.Empty).IndexOf("FUNDA", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string talla = (solicitud.Talla ?? string.Empty).Trim().ToLowerInvariant();
                piezas.Add("funda_" + talla + "_frente");
                piezas.Add("funda_" + talla + "_espalda");
                piezas.Add("funda_" + talla + "_lateral_izquierdo");
                piezas.Add("funda_" + talla + "_lateral_derecho");
            }

            return piezas;
        }

        private PiezaEscalable CrearPiezaPedido(Shape grupo, MoldeProduccion solicitud, string nombrePieza)
        {
            PiezaEscalable entrada = new PiezaEscalable
            {
                Diseno = solicitud.Diseno ?? string.Empty,
                NombreGrupo = solicitud.NombreDestino ?? string.Empty,
                FilaExcel = solicitud.FilaExcel,
                Talla = (solicitud.Talla ?? string.Empty).ToUpperInvariant(),
                Pieza = NombrePiezaVisible(nombrePieza)
            };

            if (grupo == null)
            {
                entrada.Clave = entrada.NombreGrupo + ":" + AnalizadorMasterCorel.NormalizarCodigo(nombrePieza);
                entrada.Estado = "Grupo no encontrado";
                return entrada;
            }

            Shape objeto = BuscarObjetoPorNombre(grupo, nombrePieza);

            if (objeto == null)
            {
                entrada.Clave = grupo.StaticID + ":" + AnalizadorMasterCorel.NormalizarCodigo(nombrePieza);
                entrada.Estado = "Pieza no encontrada";
                return entrada;
            }

            Shape contenedor = ResolverContenedor(objeto);
            entrada.Contenedor = contenedor;
            entrada.Clave = grupo.StaticID + ":" + objeto.StaticID;
            entrada.Estado = contenedor == null ? "Sin contenedor único" :
                grupo.Locked || objeto.Locked || RutaBloqueada(objeto, contenedor, false) ? "Bloqueado u oculto" :
                !Positivo(contenedor.SizeHeight) || !Positivo(contenedor.SizeWidth) ? "Dimensiones inválidas" : "Listo";
            entrada.TieneContenido = contenedor != null && contenedor.PowerClip != null && contenedor.PowerClip.Shapes.Count > 0;
            return entrada;
        }

        private Shape BuscarObjetoPorNombre(Shape raiz, string nombreBuscado)
        {
            string buscado = AnalizadorMasterCorel.NormalizarCodigo(nombreBuscado);
            if (AnalizadorMasterCorel.NormalizarCodigo(ObtenerNombreSeguro(raiz)) == buscado)
                return raiz;

            if (raiz.Type != cdrShapeType.cdrGroupShape || raiz.PowerClip != null)
                return null;

            for (int i = 1; i <= raiz.Shapes.Count; i++)
            {
                Shape encontrado = BuscarObjetoPorNombre(raiz.Shapes[i], nombreBuscado);
                if (encontrado != null) return encontrado;
            }

            return null;
        }

        private string NombrePiezaVisible(string nombre)
        {
            string codigo = AnalizadorMasterCorel.NormalizarCodigo(nombre);

            if (codigo.StartsWith("funda_") && codigo.EndsWith("_frente")) return "Frente";
            if (codigo.StartsWith("funda_") && codigo.EndsWith("_espalda")) return "Espalda";
            if (codigo.EndsWith("lateral_izquierdo")) return "Lateral izquierdo";
            if (codigo.EndsWith("lateral_derecho")) return "Lateral derecho";
            if (codigo.StartsWith("frente")) return "Frente";
            if (codigo == "espalda") return "Espalda";
            if (codigo.Contains("manga") && codigo.EndsWith("izquierda")) return "Manga izquierda";
            if (codigo.Contains("manga") && codigo.EndsWith("derecha")) return "Manga derecha";
            if ((codigo.Contains("short") || codigo.Contains("pierna")) && (codigo.EndsWith("izquierdo") || codigo.EndsWith("izquierda"))) return "Short izquierdo";
            if ((codigo.Contains("short") || codigo.Contains("pierna")) && (codigo.EndsWith("derecho") || codigo.EndsWith("derecha"))) return "Short derecho";

            return nombre.Replace('_', ' ');
        }

        private string ObtenerNombreSeguro(Shape objeto)
        {
            try { return (objeto.Name ?? string.Empty).Trim(); }
            catch { return string.Empty; }
        }

        private Shape ResolverContenedor(Shape objeto)
        {
            if (objeto.PowerClip != null) return objeto;
            if (objeto.Type == cdrShapeType.cdrGroupShape)
            {
                var contenedores = new List<Shape>();
                BuscarPowerClips(objeto.Shapes, contenedores);
                return contenedores.Count == 1 ? contenedores[0] : null;
            }
            if (objeto.Type == cdrShapeType.cdrRectangleShape || objeto.Type == cdrShapeType.cdrEllipseShape)
                return objeto;
            if (objeto.Type == cdrShapeType.cdrCurveShape && objeto.Curve.Closed) return objeto;
            return null;
        }

        private bool RutaBloqueada(Shape raiz, Shape destino, bool bloqueado)
        {
            bloqueado = bloqueado || raiz.Locked;
            if (raiz.StaticID == destino.StaticID) return bloqueado;
            if (raiz.Type == cdrShapeType.cdrGroupShape)
                for (int i = 1; i <= raiz.Shapes.Count; i++)
                    if (RutaBloqueada(raiz.Shapes[i], destino, bloqueado)) return true;
            return false;
        }

        private void BuscarPowerClips(Shapes objetos, List<Shape> encontrados)
        {
            for (int i = 1; i <= objetos.Count; i++)
            {
                Shape objeto = objetos[i];
                if (objeto.PowerClip != null) encontrados.Add(objeto);
                else if (objeto.Type == cdrShapeType.cdrGroupShape) BuscarPowerClips(objeto.Shapes, encontrados);
            }
        }

        private bool Contiene(Shape raiz, Shape buscado)
        {
            if (raiz.StaticID == buscado.StaticID) return true;
            Shapes hijos = raiz.PowerClip != null ? raiz.PowerClip.Shapes :
                raiz.Type == cdrShapeType.cdrGroupShape ? raiz.Shapes : null;
            if (hijos == null) return false;
            for (int i = 1; i <= hijos.Count; i++) if (Contiene(hijos[i], buscado)) return true;
            return false;
        }

        public void Validar(Shape diseno, IList<PiezaEscalable> destinos, bool reemplazar)
        {
            if (diseno == null || diseno.Type != cdrShapeType.cdrGroupShape)
                throw new InvalidOperationException("Seleccione un único diseño agrupado en CorelDRAW.");
            if (destinos.Count == 0) throw new InvalidOperationException("No hay piezas para esa selección.");
            foreach (PiezaEscalable destino in destinos)
            {
                if (destino.Estado != "Listo")
                    throw new InvalidOperationException(destino.Pieza + " " + destino.Talla + ": " + destino.Estado + ".");
                Shape contenedor = destino.Contenedor;
                if (Contiene(diseno, contenedor) || Contiene(contenedor, diseno))
                    throw new InvalidOperationException("El diseño debe estar separado de los moldes de destino.");
                if (destino.TieneContenido && !reemplazar)
                    throw new InvalidOperationException("Hay PowerClips con contenido. Active Reemplazar contenido existente si desea sustituirlo.");
                double anchoFinal;
                double altoFinal;
                CalcularTamanoCobertura(diseno.SizeWidth, diseno.SizeHeight, contenedor.SizeWidth, contenedor.SizeHeight, out anchoFinal, out altoFinal);
                }
            if (destinos.Select(x => x.Contenedor.StaticID).Distinct().Count() != destinos.Count)
                throw new InvalidOperationException("Se detectaron contenedores duplicados. Revise los grupos del documento.");
        }

        public int Aplicar(Document documento, Shape diseno, IList<PiezaEscalable> destinos, bool reemplazar)
            {
            Validar(diseno, destinos, reemplazar);

            double anchoDiseno = diseno.SizeWidth;
            double altoDiseno = diseno.SizeHeight;

            bool abierto = false;
            bool huboCambios = false;

            try
                {
                documento.BeginCommandGroup("Tithor - Escalar diseños en PowerClip");
                abierto = true;

                foreach (PiezaEscalable destino in destinos)
                    {
                    Shape contenedor = destino.Contenedor;

                    double anchoContenedor = contenedor.SizeWidth;
                    double altoContenedor = contenedor.SizeHeight;
                    double centroX = contenedor.CenterX;
                    double centroY = contenedor.CenterY;

                    double anchoFinal;
                    double altoFinal;

                    CalcularTamanoCobertura(anchoDiseno, altoDiseno, anchoContenedor, altoContenedor, out anchoFinal, out altoFinal);

                    Shape copia = diseno.CopyToLayer(contenedor.Layer);

                    huboCambios = true;

                    copia.SetSize(anchoFinal, altoFinal);
                    copia.CenterX = centroX;
                    copia.CenterY = centroY;
                    copia.Name = "TITHOR_DISENO_" + destino.Pieza.Replace(' ', '_');

                    if (reemplazar && contenedor.PowerClip != null)
                        {
                        Shapes anteriores = contenedor.PowerClip.Shapes;

                        for (int i = anteriores.Count; i >= 1; i--)
                            anteriores[i].Delete();
                        }

                    copia.AddToPowerClip(contenedor, cdrTriState.cdrTrue);

                    copia.SetSize(anchoFinal, altoFinal);
                    copia.CenterX = centroX;
                    copia.CenterY = centroY;
                    }

                documento.EndCommandGroup();
                abierto = false;

                return destinos.Count;
                }
            catch (Exception error)
                {
                try
                    {
                    if (abierto)
                        {
                        documento.EndCommandGroup();
                        abierto = false;
                        }

                    if (huboCambios)
                        documento.Undo();
                    }
                catch (Exception errorReversion)
                    {
                    throw new InvalidOperationException(
                        "Falló la aplicación y no se pudo confirmar la reversión. Revise el documento antes de guardar. " +
                        errorReversion.Message,
                        error
                    );
                    }

                throw new InvalidOperationException(
                    "No se completó la aplicación. Los cambios realizados se deshicieron. " +
                    error.Message,
                    error
                );
                }
            }
        }
}
