using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using VGCore;

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

        public static double AnchoProporcional(double ancho, double alto, double alturaDestino)
        {
            if (!Positivo(ancho) || !Positivo(alto) || !Positivo(alturaDestino))
                throw new InvalidOperationException("El diseño y el molde deben tener dimensiones mayores que cero.");
            double resultado = ancho * (alturaDestino / alto);
            if (!Positivo(resultado)) throw new InvalidOperationException("Las dimensiones resultantes no son válidas.");
            return resultado;
        }

        private static bool Positivo(double valor) => valor > 0 && !double.IsNaN(valor) && !double.IsInfinity(valor);

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

        private void Recorrer(Shapes objetos, List<PiezaEscalable> resultado, int pagina, bool bloqueado)
        {
            for (int i = 1; i <= objetos.Count; i++)
            {
                Shape objeto = objetos[i];
                bool noEditable = bloqueado || objeto.Locked;
                string pieza, talla;
                if (InterpretarNombre(objeto.Name, out pieza, out talla))
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
                    Recorrer(objeto.Shapes, resultado, pagina, noEditable);
            }
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
                AnchoProporcional(diseno.SizeWidth, diseno.SizeHeight, contenedor.SizeHeight);
            }
            if (destinos.Select(x => x.Contenedor.StaticID).Distinct().Count() != destinos.Count)
                throw new InvalidOperationException("Se detectaron contenedores duplicados. Revise los grupos del documento.");
        }

        public int Aplicar(Document documento, Shape diseno, IList<PiezaEscalable> destinos, bool reemplazar)
        {
            Validar(diseno, destinos, reemplazar);
            double ancho = diseno.SizeWidth, alto = diseno.SizeHeight;
            bool abierto = false, huboCambios = false;
            try
            {
                documento.BeginCommandGroup("Tithor - Escalar diseños en PowerClip");
                abierto = true;
                foreach (PiezaEscalable destino in destinos)
                {
                    Shape contenedor = destino.Contenedor;
                    double altura = contenedor.SizeHeight;
                    double centroX = contenedor.CenterX, centroY = contenedor.CenterY;
                    Shape copia = diseno.CopyToLayer(contenedor.Layer);
                    huboCambios = true;
                    copia.SetSize(AnchoProporcional(ancho, alto, altura), altura);
                    copia.CenterX = centroX;
                    copia.CenterY = centroY;
                    copia.Name = "TITHOR_DISENO_" + destino.Pieza.Replace(' ', '_');
                    if (reemplazar && contenedor.PowerClip != null)
                    {
                        Shapes anteriores = contenedor.PowerClip.Shapes;
                        for (int i = anteriores.Count; i >= 1; i--) anteriores[i].Delete();
                    }
                    copia.AddToPowerClip(contenedor, cdrTriState.cdrTrue);
                    // Reafirma el tamaño después de insertar, independientemente del ajuste automático de CorelDRAW.
                    copia.SetSize(AnchoProporcional(ancho, alto, altura), altura);
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
                    if (abierto) { documento.EndCommandGroup(); abierto = false; }
                    if (huboCambios) documento.Undo();
                }
                catch (Exception errorReversion)
                {
                    throw new InvalidOperationException("Falló la aplicación y no se pudo confirmar la reversión. Revise el documento antes de guardar. " + errorReversion.Message, error);
                }
                throw new InvalidOperationException("No se completó la aplicación. Los cambios realizados se deshicieron. " + error.Message, error);
            }
        }
    }
}
