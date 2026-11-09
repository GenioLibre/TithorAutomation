using System;
using System.Collections.Generic;
using System.Linq;
using VGCore;

namespace TithorAutomation.Servicios
{
    // Solo opera sobre copias de trabajo; el original y el master no se guardan.
    internal sealed class TramoRibMockup
    {
        public Shape Destino;
        public Curve Contorno;
        public double Alto, Largo, InicioNormalizado;
        public int SuperiorIzquierdo, InferiorIzquierdo, SuperiorDerecho, InferiorDerecho;
    }

    internal static class AdaptadorRibMockup
    {
        internal static bool EsNombre(string nombre)
        {
            if (nombre == "rib") return true;
            int numero;
            return nombre.StartsWith("rib_", StringComparison.Ordinal) &&
                int.TryParse(nombre.Substring(4), out numero) && numero > 0;
        }

        internal static List<TramoRibMockup> Preparar(IEnumerable<Shape> destinos, double proporcionOrigen)
        {
            var tramos = destinos.Select(LeerContorno).ToList();
            // Cada tramo conserva la escala dada por su grosor. El ancho restante
            // se recorta; nunca se comprime horizontalmente el rectángulo completo.
            double requerido = tramos.Sum(t => t.Largo / t.Alto);
            if (double.IsNaN(proporcionOrigen) || double.IsInfinity(proporcionOrigen) ||
                proporcionOrigen <= 0 || requerido > proporcionOrigen + 1e-6)
                throw new InvalidOperationException("El rib no alcanza para todos los tramos del cuello al ajustar por alto. Aumente el largo del diseño rib; no se estiró ni se modificó el mockup.");
            double inicio = Math.Max(0, (proporcionOrigen - requerido) / 2);
            foreach (var tramo in tramos)
            {
                tramo.InicioNormalizado = inicio;
                inicio += tramo.Largo / tramo.Alto;
            }
            return tramos;
        }

        private static TramoRibMockup LeerContorno(Shape destino)
        {
            Curve curva = destino.DisplayCurve;
            if (curva == null || curva.SubPaths.Count != 1 || !curva.SubPaths[1].Closed || curva.Segments.Count < 4)
                throw ErrorContorno(destino);
            // Una cinta cerrada tiene dos bordes largos y dos cierres cortos,
            // uno a cada extremo. Se admiten nodos intermedios en los bordes.
            var segmentos = Enumerable.Range(1, curva.Segments.Count).Select(i => curva.Segments[i]).ToList();
            double centro = destino.CenterX, ancho = destino.SizeWidth;
            Func<Segment, double> medioX = s => (s.StartNode.PositionX + s.EndNode.PositionX) / 2;
            Func<Segment, bool> cierre = s =>
            {
                double dx = s.EndNode.PositionX - s.StartNode.PositionX;
                double dy = s.EndNode.PositionY - s.StartNode.PositionY;
                double cuerda = Math.Sqrt(dx * dx + dy * dy);
                int anterior = (s.Index + segmentos.Count - 2) % segmentos.Count;
                int siguiente = s.Index % segmentos.Count;
                return cuerda > 0 && s.Length <= cuerda * 1.05 && s.Length < ancho * 0.3 &&
                    TieneEsquina(segmentos[anterior], s) && TieneEsquina(s, segmentos[siguiente]);
            };
            var izquierda = segmentos.Where(s => cierre(s) && medioX(s) < centro - ancho * 0.25).OrderBy(s => s.Length).FirstOrDefault();
            var derecha = segmentos.Where(s => cierre(s) && medioX(s) > centro + ancho * 0.25).OrderBy(s => s.Length).FirstOrDefault();
            if (izquierda == null || derecha == null) throw ErrorContorno(destino);
            int a = izquierda.Index, b = derecha.Index;
            double camino = 0, alturaCamino = 0;
            var indicesCamino = new HashSet<int>();
            int pasos = 0;
            for (int i = a % segmentos.Count + 1; i != b; i = i % segmentos.Count + 1)
            {
                if (++pasos > segmentos.Count) throw ErrorContorno(destino);
                Segment segmento = curva.Segments[i];
                camino += segmento.Length;
                alturaCamino += segmento.GetPointAt(0.5, cdrSegmentOffsetType.cdrParamSegmentOffset).y * segmento.Length;
                indicesCamino.Add(i);
            }
            double otro = segmentos.Sum(s => s.Length) - izquierda.Length - derecha.Length - camino;
            double alto = (izquierda.Length + derecha.Length) / 2;
            if (alto <= 0 || camino <= alto * 2 || otro <= alto * 2) throw ErrorContorno(destino);
            double alturaOtro = segmentos.Where(s => s.Index != a && s.Index != b && !indicesCamino.Contains(s.Index))
                .Sum(s => s.GetPointAt(0.5, cdrSegmentOffsetType.cdrParamSegmentOffset).y * s.Length);
            bool caminoSuperior = alturaCamino / camino >= alturaOtro / otro;
            Node si = caminoSuperior ? izquierda.EndNode : izquierda.StartNode;
            Node ii = caminoSuperior ? izquierda.StartNode : izquierda.EndNode;
            Node sd = caminoSuperior ? derecha.StartNode : derecha.EndNode;
            Node id = caminoSuperior ? derecha.EndNode : derecha.StartNode;
            // Rechazar esquinas ambiguas en lugar de generar una envolvente cruzada.
            if (new[] { si.Index, ii.Index, sd.Index, id.Index }.Distinct().Count() != 4)
                throw ErrorContorno(destino);
            return new TramoRibMockup { Destino = destino, Contorno = curva, Alto = alto,
                Largo = (camino + otro) / 2, SuperiorIzquierdo = si.Index, InferiorIzquierdo = ii.Index,
                SuperiorDerecho = sd.Index, InferiorDerecho = id.Index };
        }

        private static bool TieneEsquina(Segment anterior, Segment siguiente)
        {
            VGCore.Point a = anterior.GetPointAt(0.99, cdrSegmentOffsetType.cdrParamSegmentOffset);
            VGCore.Point b = anterior.GetPointAt(1, cdrSegmentOffsetType.cdrParamSegmentOffset);
            VGCore.Point c = siguiente.GetPointAt(0, cdrSegmentOffsetType.cdrParamSegmentOffset);
            VGCore.Point d = siguiente.GetPointAt(0.01, cdrSegmentOffsetType.cdrParamSegmentOffset);
            double ax = b.x - a.x, ay = b.y - a.y, bx = d.x - c.x, by = d.y - c.y;
            double norma = Math.Sqrt((ax * ax + ay * ay) * (bx * bx + by * by));
            // Evitar confundir un pequeño segmento de un borde subdividido con
            // el cierre lateral: sus dos extremos deben ser esquinas reales.
            return norma > 0 && (ax * bx + ay * by) / norma < Math.Cos(25 * Math.PI / 180);
        }

        private static Exception ErrorContorno(Shape destino)
        {
            return new InvalidOperationException("El rib '" + destino.Name + "' debe ser una cinta cerrada: dos bordes largos y un cierre recto corto en cada extremo izquierdo/derecho. Revise los nodos del contorno; no use un anillo con dos subtrayectos.");
        }

        internal static Shape Deformar(Shape copia, TramoRibMockup tramo, Layer capa, Action<string> informar)
        {
            informar("Ajustando el rib por alto y recortando el tramo del contorno");
            copia.RotateEx(-copia.RotationAngle, copia.CenterX, copia.CenterY);
            double ancho = copia.SizeWidth * tramo.Alto / copia.SizeHeight;
            copia.SetSize(ancho, tramo.Alto);
            // Crear el marco en coordenadas explícitas, sin depender del punto
            // de referencia seleccionado por el usuario en CorelDRAW.
            Shape marco = capa.CreateRectangle2(0, 0, tramo.Largo, tramo.Alto);
            marco.Fill.ApplyNoFill();
            marco.Outline.SetNoOutline();
            copia.CenterX = ancho / 2 - tramo.InicioNormalizado * tramo.Alto;
            copia.CenterY = tramo.Alto / 2;
            copia.AddToPowerClip(marco, cdrTriState.cdrFalse);
            // Rasterizar únicamente el recorte del mockup, no el diseño fuente.
            // Corel puede deformar el bitmap sin depender de grupos anidados.
            Curve rectangulo = marco.DisplayCurve.GetCopy();
            Shape imagen = marco.ConvertToBitmapEx(cdrImageType.cdrRGBColorImage, false, true, 300);
            imagen.SetSize(tramo.Largo, tramo.Alto);
            imagen.CenterX = tramo.Largo / 2;
            imagen.CenterY = tramo.Alto / 2;
            informar("Curvando el rib según los bordes del cuello");
            Effect efecto = imagen.CreateEnvelopeFromCurve(rectangulo, cdrEnvelopeMode.cdrEnvelopeOriginal,
                false, cdrEnvelopeCopyMode.cdrEnvelopeUseOriginal);
            // Leer el orden de esquinas de Corel, en vez de asumir el orden del
            // SAFEARRAY o la orientación/nodo inicial de la curva del master.
            Array originales = (Array)efecto.Envelope.CornerIndices;
            if (originales.Length != 4) throw new InvalidOperationException("CorelDRAW no devolvió las cuatro esquinas de la envolvente del rib.");
            Curve neutra = efecto.Envelope.Container;
            int[] esquinas = new int[4];
            int posicion = 0;
            foreach (object indice in originales)
            {
                Node nodo = neutra.Nodes[Convert.ToInt32(indice)];
                bool izquierda = nodo.PositionX < tramo.Largo / 2;
                bool superior = nodo.PositionY > tramo.Alto / 2;
                esquinas[posicion++] = izquierda
                    ? (superior ? tramo.SuperiorIzquierdo : tramo.InferiorIzquierdo)
                    : (superior ? tramo.SuperiorDerecho : tramo.InferiorDerecho);
            }
            if (esquinas.Distinct().Count() != 4 || !efecto.Envelope.CopyFromCurve(tramo.Contorno,
                cdrEnvelopeMode.cdrEnvelopeOriginal, false, cdrEnvelopeCopyMode.cdrEnvelopeUseOriginal, esquinas))
                throw new InvalidOperationException("No se pudo adaptar la envolvente del rib al cuello.");
            return imagen;
        }
    }
}
