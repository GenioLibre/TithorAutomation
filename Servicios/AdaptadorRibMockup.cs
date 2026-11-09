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

        private sealed class ExtremoRib
        {
            public int Primero, Ultimo;
            public double Largo, Puntuacion;
            public HashSet<int> Indices = new HashSet<int>();
        }

        private static TramoRibMockup LeerContorno(Shape destino)
        {
            Curve curva = destino.Type == cdrShapeType.cdrCurveShape ? destino.Curve : destino.DisplayCurve;
            if (curva == null) throw ErrorContorno(destino, "CorelDRAW no devolvió una curva.");
            if (curva.SubPaths.Count != 1)
                throw ErrorContorno(destino, "Tiene " + curva.SubPaths.Count + " subtrayectos; se necesita una sola cinta, no un anillo compuesto.");
            if (!curva.SubPaths[1].Closed)
                throw ErrorContorno(destino, "CorelDRAW informa que el subtrayecto está abierto. El relleno visible no confirma el cierre de los nodos.");
            if (curva.Segments.Count < 4)
                throw ErrorContorno(destino, "La curva está cerrada, pero tiene menos de cuatro segmentos para identificar las cuatro esquinas de la cinta.");
            var segmentos = Enumerable.Range(1, curva.Segments.Count).Select(i => curva.Segments[i]).ToList();
            int n = segmentos.Count;
            double ancho = destino.SizeWidth, centro = destino.CenterX;
            double total = segmentos.Sum(s => s.Length);
            var giros = Enumerable.Range(0, n).Select(i => Giro(segmentos[(i + n - 1) % n], segmentos[i])).ToArray();
            var izquierdos = BuscarExtremos(segmentos, giros, centro, ancho, true);
            var derechos = BuscarExtremos(segmentos, giros, centro, ancho, false);
            TramoRibMockup mejor = null;
            double puntuacion = double.NegativeInfinity;
            foreach (var izquierda in izquierdos)
            foreach (var derecha in derechos)
            {
                if (izquierda.Indices.Overlaps(derecha.Indices)) continue;
                var camino = new List<int>();
                int i = (izquierda.Ultimo + 1) % n;
                while (i != derecha.Primero && camino.Count < n)
                {
                    camino.Add(i);
                    i = (i + 1) % n;
                }
                if (camino.Count == n || camino.Any(k => izquierda.Indices.Contains(k) || derecha.Indices.Contains(k))) continue;
                var otro = Enumerable.Range(0, n).Where(k => !izquierda.Indices.Contains(k) &&
                    !derecha.Indices.Contains(k) && !camino.Contains(k)).ToList();
                double largoA = camino.Sum(k => segmentos[k].Length);
                double largoB = total - izquierda.Largo - derecha.Largo - largoA;
                double alto = (izquierda.Largo + derecha.Largo) / 2;
                if (alto <= 0 || largoA <= alto * 2 || largoB <= alto * 2 || otro.Count == 0) continue;
                double score = izquierda.Puntuacion + derecha.Puntuacion;
                if (score <= puntuacion) continue;
                Func<List<int>, double> altura = ids => ids.Sum(k =>
                    segmentos[k].GetPointAt(0.5, cdrSegmentOffsetType.cdrParamSegmentOffset).y * segmentos[k].Length) /
                    ids.Sum(k => segmentos[k].Length);
                bool superior = altura(camino) >= altura(otro);
                Node inicioI = segmentos[izquierda.Primero].StartNode, finI = segmentos[izquierda.Ultimo].EndNode;
                Node inicioD = segmentos[derecha.Primero].StartNode, finD = segmentos[derecha.Ultimo].EndNode;
                if (new[] { inicioI.Index, finI.Index, inicioD.Index, finD.Index }.Distinct().Count() != 4) continue;
                mejor = new TramoRibMockup { Destino = destino, Contorno = curva.GetCopy(), Alto = alto,
                    Largo = (largoA + largoB) / 2,
                    SuperiorIzquierdo = superior ? finI.Index : inicioI.Index,
                    InferiorIzquierdo = superior ? inicioI.Index : finI.Index,
                    SuperiorDerecho = superior ? inicioD.Index : finD.Index,
                    InferiorDerecho = superior ? finD.Index : inicioD.Index };
                puntuacion = score;
            }
            if (mejor == null)
                throw ErrorContorno(destino, "La curva SÍ está cerrada (" + curva.Nodes.Count + " nodos, " + n +
                    " segmentos), pero no se pudieron separar los bordes interior/exterior y los extremos. Candidatos: " +
                    izquierdos.Count + " izquierdos y " + derechos.Count + " derechos.");
            return mejor;
        }

        private static List<ExtremoRib> BuscarExtremos(List<Segment> segmentos, double[] giros,
            double centro, double ancho, bool izquierda)
        {
            int n = segmentos.Count;
            var candidatos = new List<ExtremoRib>();
            // Cachear las lecturas COM antes de explorar las combinaciones.
            var longitudes = segmentos.Select(s => s.Length).ToArray();
            var centrosX = segmentos.Select(s => s.GetPointAt(0.5, cdrSegmentOffsetType.cdrParamSegmentOffset).x).ToArray();
            for (int inicio = 0; inicio < n; inicio++)
            {
                double largo = 0, interiores = 0, xAcumulado = 0;
                var indices = new HashSet<int>();
                for (int paso = 0; paso < n - 2; paso++)
                {
                    int actual = (inicio + paso) % n;
                    double x = centrosX[actual];
                    if (izquierda ? x >= centro - ancho * 0.2 : x <= centro + ancho * 0.2) break;
                    largo += longitudes[actual];
                    if (largo > ancho * 0.4) break;
                    if (paso > 0) interiores += giros[actual];
                    xAcumulado += x * longitudes[actual];
                    indices.Add(actual);
                    if (largo <= 0) continue;
                    // Admitir cierres curvos y subdivididos. Favorecer las esquinas
                    // en los extremos del cierre, sin exigir un ángulo mínimo ni
                    // que el segmento sea casi recto. Los giros interiores aportan
                    // menos peso para reconocer un cierre curvo subdividido.
                    double score = giros[inicio] + giros[(actual + 1) % n] + interiores * 0.5 +
                        Math.Abs(xAcumulado / largo - centro) / ancho * 0.1;
                    candidatos.Add(new ExtremoRib { Primero = inicio, Ultimo = actual, Largo = largo,
                        Puntuacion = score, Indices = new HashSet<int>(indices) });
                }
            }
            return candidatos.OrderByDescending(c => c.Puntuacion).Take(16).ToList();
        }

        private static double Giro(Segment anterior, Segment siguiente)
        {
            VGCore.Point a = anterior.GetPointAt(0.99, cdrSegmentOffsetType.cdrParamSegmentOffset);
            VGCore.Point b = anterior.GetPointAt(1, cdrSegmentOffsetType.cdrParamSegmentOffset);
            VGCore.Point c = siguiente.GetPointAt(0, cdrSegmentOffsetType.cdrParamSegmentOffset);
            VGCore.Point d = siguiente.GetPointAt(0.01, cdrSegmentOffsetType.cdrParamSegmentOffset);
            double ax = b.x - a.x, ay = b.y - a.y, bx = d.x - c.x, by = d.y - c.y;
            double norma = Math.Sqrt((ax * ax + ay * ay) * (bx * bx + by * by));
            return norma > 0 ? Math.Acos(Math.Max(-1, Math.Min(1, (ax * bx + ay * by) / norma))) : 0;
        }

        private static Exception ErrorContorno(Shape destino, string detalle)
        {
            return new InvalidOperationException("No se pudo preparar el rib '" + destino.Name + "'. " + detalle);
        }

        // CornerIndices puede cruzar COM como SAFEARRAYs anidados, incluidos
        // Int32[*] con límite inferior distinto de cero. Mantener su estructura
        // al devolverlo a Corel; nunca convertir un array completo a Int32.
        internal static object MapearEsquinas(object valor, int cantidadNodos, Func<int, int> mapear)
        {
            object registros = valor;
            while (registros is Array && ((Array)registros).Length == 1)
                registros = ((Array)registros).Cast<object>().First();
            Array matriz = registros as Array;
            if (matriz == null || matriz.Length != 4)
                throw new InvalidOperationException("Formato de esquinas no reconocido: " + DescribirMatriz(valor));
            var filas = matriz.Cast<object>().Select(x =>
            {
                var hoja = new List<int>();
                LeerEnteros(x, hoja);
                return hoja;
            }).ToList();
            int ancho = filas[0].Count;
            if (ancho == 0 || filas.Any(f => f.Count != ancho))
                throw new InvalidOperationException("Esquinas con estructura desigual: " + DescribirMatriz(valor));
            var columnas = new List<int>();
            var resultados = new Dictionary<int, int[]>();
            for (int columna = 0; columna < ancho; columna++)
            {
                int[] indices = filas.Select(f => f[columna]).ToArray();
                if (indices.Any(i => i < 1 || i > cantidadNodos) || indices.Distinct().Count() != 4) continue;
                int[] destino = indices.Select(mapear).ToArray();
                if (destino.Distinct().Count() != 4) continue;
                columnas.Add(columna);
                resultados.Add(columna, destino);
            }
            if (columnas.Count != 1)
                throw new InvalidOperationException("No se pudo identificar una única columna de nodos en las esquinas: " + DescribirMatriz(valor));
            int elegida = columnas[0], posicion = 0;
            // Los demás valores de cada registro (si existen) permanecen intactos.
            return TransformarMatriz(valor, original =>
            {
                int fila = posicion / ancho, columna = posicion++ % ancho;
                return columna == elegida
                    ? Convert.ChangeType(resultados[elegida][fila], original.GetType(), System.Globalization.CultureInfo.InvariantCulture)
                    : original;
            });
        }

        private static void LeerEnteros(object valor, List<int> salida)
        {
            Array matriz = valor as Array;
            if (matriz != null)
            {
                foreach (object elemento in matriz) LeerEnteros(elemento, salida);
                return;
            }
            if (!(valor is int) && !(valor is short) && !(valor is long) && !(valor is byte))
                throw new InvalidOperationException("Tipo no numérico en CornerIndices: " + (valor == null ? "null" : valor.GetType().FullName));
            salida.Add(Convert.ToInt32(valor));
        }

        private static object TransformarMatriz(object valor, Func<object, object> transformar)
        {
            Array original = valor as Array;
            if (original == null) return transformar(valor);
            Array copia = (Array)original.Clone();
            int[] indices = new int[original.Rank];
            Action<int> recorrer = null;
            recorrer = dimension =>
            {
                for (int i = original.GetLowerBound(dimension); i <= original.GetUpperBound(dimension); i++)
                {
                    indices[dimension] = i;
                    if (dimension + 1 < original.Rank) recorrer(dimension + 1);
                    else copia.SetValue(TransformarMatriz(original.GetValue(indices), transformar), indices);
                }
            };
            recorrer(0);
            return copia;
        }

        private static string DescribirMatriz(object valor)
        {
            Array matriz = valor as Array;
            if (matriz == null) return Convert.ToString(valor, System.Globalization.CultureInfo.InvariantCulture);
            return matriz.GetType().Name + "[" + string.Join(",", matriz.Cast<object>().Select(DescribirMatriz)) + "]";
        }

        internal static void NormalizarOrientacion(Shape copia)
        {
            // Modificar únicamente la copia: el documento fuente conserva su
            // rotación, contenido y estado de guardado.
            double angulo = copia.RotationAngle % 360;
            if (angulo > 180) angulo -= 360;
            if (angulo < -180) angulo += 360;
            if (Math.Abs(angulo) > 0.000001)
                copia.RotateEx(-angulo, copia.CenterX, copia.CenterY);
            // También admitir un rectángulo dibujado verticalmente con ángulo 0.
            if (copia.SizeHeight > copia.SizeWidth)
                copia.RotateEx(90, copia.CenterX, copia.CenterY);
            if (copia.SizeWidth <= 0 || copia.SizeHeight <= 0 ||
                double.IsNaN(copia.SizeWidth) || double.IsNaN(copia.SizeHeight) ||
                double.IsInfinity(copia.SizeWidth) || double.IsInfinity(copia.SizeHeight))
                throw new InvalidOperationException("El diseño rib no tiene dimensiones válidas después de orientar la copia.");
        }

        internal static Shape Deformar(Shape copia, TramoRibMockup tramo, Layer capa, Action<string> informar)
        {
            informar("Ajustando el rib por alto y recortando el tramo del contorno");
            NormalizarOrientacion(copia);
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
            informar("Leyendo las esquinas de la envolvente de CorelDRAW");
            Curve neutra = efecto.Envelope.Container;
            object indicesCorel = (object)efecto.Envelope.CornerIndices;
            Func<int, int> mapearNodo = indice =>
            {
                Node nodo = neutra.Nodes[indice];
                bool izquierda = nodo.PositionX < tramo.Largo / 2;
                bool superior = nodo.PositionY > tramo.Alto / 2;
                return izquierda
                    ? (superior ? tramo.SuperiorIzquierdo : tramo.InferiorIzquierdo)
                    : (superior ? tramo.SuperiorDerecho : tramo.InferiorDerecho);
            };
            object esquinas = MapearEsquinas(indicesCorel, neutra.Nodes.Count, mapearNodo);
            informar("Aplicando la envolvente curva al rib");
            if (!efecto.Envelope.CopyFromCurve(tramo.Contorno,
                cdrEnvelopeMode.cdrEnvelopeOriginal, false, cdrEnvelopeCopyMode.cdrEnvelopeUseOriginal, esquinas))
                throw new InvalidOperationException("No se pudo adaptar la envolvente del rib al cuello.");
            return imagen;
        }
    }
}
