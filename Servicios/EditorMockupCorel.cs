using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VGCore;
using TithorAutomation.Modelos;

namespace TithorAutomation.Servicios
{
    public sealed class CampoMockup
    {
        public string Nombre { get; set; }
        public string Referencia { get; set; }
        public bool Invertido { get; set; }
        public List<Shape> Destinos { get; set; }
        public bool Aplicado { get; set; }
        public override string ToString() { return Nombre; }
    }

    public sealed class EditorMockupCorel : IDisposable
    {
        private Document documento;
        private ShapeRange composicion;
        private Layer capaTrabajo;
        private Page paginaTrabajo;
        private string codigoCamiseta;
        public bool RequiereRecargar { get; private set; }
        private string carpetaTemporal;
        public List<CampoMockup> Campos { get; private set; } = new List<CampoMockup>();
        public Document Documento { get { return documento; } }

        public static string Normalizar(string nombre) { return AnalizadorMasterCorel.NormalizarCodigo(nombre ?? string.Empty); }

        public static string ReferenciaBase(string nombre)
        {
            string codigo = Normalizar(nombre);
            return codigo.EndsWith("_invertido", StringComparison.Ordinal) ? codigo.Substring(0, codigo.Length - 10) : codigo;
        }

        public static List<CampoMockup> DetectarCampos(Shapes objetos, bool partesDirectas = false)
        {
            if (partesDirectas)
            {
                var directos = new Dictionary<string, CampoMockup>(StringComparer.OrdinalIgnoreCase);
                RecogerDestinos(objetos, directos);
                if (directos.Count == 0) throw new InvalidOperationException("La capa Diseño no contiene PowerClips con nombre.");
                return directos.Values.ToList();
            }
            var grupos = new List<Shape>();
            BuscarDiseno(objetos, grupos);
            if (grupos.Count != 1) throw new InvalidOperationException("Debe existir un único grupo Diseño dentro del mockup.");
            var campos = new Dictionary<string, CampoMockup>(StringComparer.OrdinalIgnoreCase);
            RecogerDestinos(grupos[0].Shapes, campos);
            if (campos.Count == 0) throw new InvalidOperationException("El grupo Diseño no contiene PowerClips con nombre.");
            return campos.Values.ToList();
        }

        private static void BuscarDiseno(Shapes objetos, List<Shape> encontrados)
        {
            for (int i = 1; i <= objetos.Count; i++)
            {
                Shape objeto = objetos[i];
                if (objeto.Type != cdrShapeType.cdrGroupShape || objeto.PowerClip != null) continue;
                if (Normalizar(objeto.Name) == "diseno") encontrados.Add(objeto);
                else BuscarDiseno(objeto.Shapes, encontrados);
            }
        }

        private static void RecogerDestinos(Shapes objetos, Dictionary<string, CampoMockup> campos)
        {
            for (int i = 1; i <= objetos.Count; i++)
            {
                Shape objeto = objetos[i];
                string nombre = Normalizar(objeto.Name);
                if (objeto.PowerClip != null)
                {
                    if (string.IsNullOrWhiteSpace(nombre)) throw new InvalidOperationException("Hay un PowerClip sin nombre dentro de Diseño.");
                    if (!campos.ContainsKey(nombre)) campos.Add(nombre, new CampoMockup { Nombre = nombre, Referencia = ReferenciaBase(nombre), Invertido = nombre.EndsWith("_invertido", StringComparison.Ordinal), Destinos = new List<Shape>() });
                    campos[nombre].Destinos.Add(objeto);
                }
                else if (objeto.Type == cdrShapeType.cdrGroupShape) RecogerDestinos(objeto.Shapes, campos);
                else throw new InvalidOperationException("El destino '" + nombre + "' debe ser un PowerClip dentro de Diseño.");
            }
        }


        public static bool EsGrupoCamiseta(string nombre)
        {
            string[] partes = Normalizar(nombre).Split('_');
            return partes.Length >= 3 && (partes[0] == "clasico" || partes[0] == "ranglan")
                && (partes[1] == "varon" || partes[1] == "mujer");
        }

        public static List<Shape> BuscarGruposCamiseta(Shapes objetos)
        {
            var resultado = new List<Shape>();
            for (int i = 1; i <= objetos.Count; i++)
            {
                Shape objeto = objetos[i];
                if (objeto.Type != cdrShapeType.cdrGroupShape || objeto.PowerClip != null) continue;
                if (EsGrupoCamiseta(objeto.Name)) resultado.Add(objeto);
                else resultado.AddRange(BuscarGruposCamiseta(objeto.Shapes));
            }
            return resultado;
        }

        public static List<CampoMockup> DetectarCamposCamiseta(Shapes objetos)
        {
            var campos = new Dictionary<string, CampoMockup>(StringComparer.OrdinalIgnoreCase);
            RecogerCamposCamiseta(objetos, campos);
            if (campos.Count == 0) throw new InvalidOperationException("Nombre los PowerClips: frente, espalda, manga_corta_izquierda o manga_corta_derecha.");
            return campos.Values.ToList();
        }

        private static void RecogerCamposCamiseta(Shapes objetos, Dictionary<string, CampoMockup> campos)
        {
            for (int i = 1; i <= objetos.Count; i++)
            {
                Shape objeto = objetos[i];
                string nombre = Normalizar(objeto.Name);
                bool pieza = nombre == "frente" || nombre == "espalda" || nombre == "manga_corta_izquierda" || nombre == "manga_corta_derecha";
                if (pieza)
                {
                    if (objeto.PowerClip == null) throw new InvalidOperationException("La pieza " + nombre + " debe ser un PowerClip.");
                    if (!campos.ContainsKey(nombre))
                        campos.Add(nombre, new CampoMockup { Nombre = nombre, Referencia = nombre, Destinos = new List<Shape>() });
                    campos[nombre].Destinos.Add(objeto);
                }
                else if (objeto.PowerClip == null && objeto.Type == cdrShapeType.cdrGroupShape)
                    RecogerCamposCamiseta(objeto.Shapes, campos);
                // Conservar decoración, bitmap, rib y piezas aún no habilitadas.
            }
        }

        private bool EsMangaDerechaFrente(CampoMockup campo)
        {
            return (codigoCamiseta == "clasico_varon_v" || codigoCamiseta == "clasico_varon_redondo")
                && campo.Referencia == "manga_corta_derecha";
        }

        private void ColocarMangaDerechaFrente(VGCore.Application app, Shape copia, Shape destino)
        {
            // Neutralizar la rotación de la referencia para medir su marco, no su contenido.
            copia.RotateEx(-copia.RotationAngle, copia.CenterX, copia.CenterY);
            double alto = app.ConvertUnits(196.652, cdrUnit.cdrMillimeter, documento.Unit);
            double ancho = copia.SizeWidth * alto / copia.SizeHeight;
            if (!DimensionValida(ancho)) throw new InvalidOperationException("La manga no tiene un marco válido.");
            double x, y;
            ObtenerPuntoSuperior(destino, out x, out y);
            Shapes anteriores = destino.PowerClip.Shapes;
            for (int i = anteriores.Count; i >= 1; i--) anteriores[i].Delete();
            copia.AddToPowerClip(destino, cdrTriState.cdrFalse);
            copia.SetSize(ancho, alto);
            copia.CenterX = x;
            copia.CenterY = y - alto / 2.0;
            copia.RotateEx(294.088 - copia.RotationAngle, x, y);
            copia.RotationCenterX = x;
            copia.RotationCenterY = y;
        }

        private static void ObtenerPuntoSuperior(Shape destino, out double x, out double y)
        {
            Curve curva = destino.DisplayCurve;
            if (curva == null || curva.Segments.Count == 0)
                throw new InvalidOperationException("No se pudo leer el contorno de " + destino.Name + ".");
            x = 0;
            y = double.NegativeInfinity;
            double centro = destino.CenterX;
            // Cada segmento es lineal o Bézier cúbico. Resolver dy/dt incluye máximos
            // entre nodos; el centro del bounding box no necesariamente está en el contorno.
            for (int i = 1; i <= curva.Segments.Count; i++)
            {
                Segment segmento = curva.Segments[i];
                double y0 = segmento.GetPointAt(0, cdrSegmentOffsetType.cdrParamSegmentOffset).y;
                double d1 = segmento.GetPointAt(1.0 / 3, cdrSegmentOffsetType.cdrParamSegmentOffset).y - y0;
                double d2 = segmento.GetPointAt(2.0 / 3, cdrSegmentOffsetType.cdrParamSegmentOffset).y - y0;
                double d3 = segmento.GetPointAt(1, cdrSegmentOffsetType.cdrParamSegmentOffset).y - y0;
                double a = 4.5 * (d3 - 3 * d2 + 3 * d1);
                double b = 4.5 * (d2 - 2 * d1) - a;
                double c = d3 - a - b;
                double tolerancia = Math.Max(1, Math.Max(Math.Abs(a), Math.Max(Math.Abs(b), Math.Abs(c)))) * 1e-10;
                var candidatos = new List<double> { 0, 0.5, 1 };
                if (Math.Abs(a) <= tolerancia)
                {
                    if (Math.Abs(b) > tolerancia) candidatos.Add(-c / (2 * b));
                }
                else
                {
                    double discriminante = 4 * b * b - 12 * a * c;
                    if (discriminante >= 0)
                    {
                        double raiz = Math.Sqrt(discriminante);
                        candidatos.Add((-2 * b + raiz) / (6 * a));
                        candidatos.Add((-2 * b - raiz) / (6 * a));
                    }
                }
                foreach (double t in candidatos.Where(v => v >= 0 && v <= 1))
                {
                    VGCore.Point punto = segmento.GetPointAt(t, cdrSegmentOffsetType.cdrParamSegmentOffset);
                    double px = punto.x, py = punto.y;
                    if (py > y + tolerancia || (Math.Abs(py - y) <= tolerancia && Math.Abs(px - centro) < Math.Abs(x - centro)))
                    { x = px; y = py; }
                }
            }
            if (double.IsNaN(y) || double.IsInfinity(y)) throw new InvalidOperationException("No se encontró el punto superior de la manga.");
        }

        public void Cargar(VGCore.Application app, string rutaMaster, Mockup mockup)
        {
            if (!File.Exists(rutaMaster)) throw new FileNotFoundException("No se encuentra el master de mockups.", rutaMaster);
            Dispose();
            RequiereRecargar = false;
            carpetaTemporal = Path.Combine(Path.GetTempPath(), "TithorMockup_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(carpetaTemporal);
            string copia = Path.Combine(carpetaTemporal, "Mockup_trabajo.cdr");
            File.Copy(rutaMaster, copia);
            File.SetAttributes(copia, FileAttributes.Normal);
            try
            {
                documento = app.OpenDocument(copia);
                int coincidencias = 0;
                for (int p = 1; p <= documento.Pages.Count; p++)
                {
                    Page pagina = documento.Pages[p];
                    for (int l = 1; l <= pagina.Layers.Count; l++)
                    {
                        Layer capa = pagina.Layers[l];
                        if (EsGrupoCamiseta(mockup.Codigo))
                        {
                            foreach (Shape variante in BuscarGruposCamiseta(capa.Shapes).Where(x => Normalizar(x.Name) == mockup.Codigo))
                            {
                                coincidencias++;
                                pagina.Activate();
                                Campos = DetectarCamposCamiseta(variante.Shapes);
                                codigoCamiseta = Normalizar(variante.Name);
                                variante.CreateSelection();
                                composicion = documento.SelectionRange;
                                capaTrabajo = capa;
                                paginaTrabajo = pagina;
                            }
                            continue;
                        }

                        if (Normalizar(capa.Name) == mockup.Codigo)
                        {
                            coincidencias++;
                            pagina.Activate();
                            Campos = DetectarCampos(capa.Shapes, Normalizar(capa.Name) == "diseno");
                            composicion = capa.Shapes.All();
                            capaTrabajo = capa;
                            paginaTrabajo = pagina;
                        }
                        else
                        {
                            for (int i = 1; i <= capa.Shapes.Count; i++)
                            {
                                Shape grupo = capa.Shapes[i];
                                if (grupo.Type != cdrShapeType.cdrGroupShape || Normalizar(grupo.Name) != mockup.Codigo) continue;
                                coincidencias++;
                                pagina.Activate();
                                bool camiseta = Normalizar(pagina.Name) == "camisetas" && EsGrupoCamiseta(grupo.Name);
                                Campos = camiseta ? DetectarCamposCamiseta(grupo.Shapes) : DetectarCampos(grupo.Shapes);
                                codigoCamiseta = camiseta ? Normalizar(grupo.Name) : null;
                                grupo.CreateSelection();
                                composicion = documento.SelectionRange;
                                capaTrabajo = capa;
                                paginaTrabajo = pagina;
                            }
                        }
                    }
                }
                if (coincidencias != 1) throw new InvalidOperationException("El mockup no se encuentra o está duplicado. Vuelva a analizar y sincronizar el master.");
                capaTrabajo.Visible = true;
                capaTrabajo.Editable = true;
                paginaTrabajo.Activate();
                capaTrabajo.Activate();
            }
            catch { Dispose(); throw; }
        }

        public static Dictionary<string, Shape> ValidarReferenciasCapa(Layer capaDiseno, IEnumerable<CampoMockup> campos)
        {
            var referencias = new Dictionary<string, Shape>(StringComparer.OrdinalIgnoreCase);
                var necesarias = new HashSet<string>(campos.Select(x => x.Referencia), StringComparer.OrdinalIgnoreCase);
                var encontradas = new Dictionary<string, List<Shape>>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i <= capaDiseno.Shapes.Count; i++)
                {
                    Shape objeto = capaDiseno.Shapes[i];
                    string nombre = Normalizar(objeto.Name);
                    if (nombre.StartsWith("diseno_", StringComparison.Ordinal)) nombre = nombre.Substring("diseno_".Length);
                    if (!necesarias.Contains(nombre)) continue;
                    if (!encontradas.ContainsKey(nombre)) encontradas[nombre] = new List<Shape>();
                    encontradas[nombre].Add(objeto);
                }
                var errores = new List<string>();
                foreach (string nombre in necesarias)
                {
                    if (!encontradas.ContainsKey(nombre))
                        { errores.Add("Falta la pieza " + nombre + "."); continue; }
                    if (encontradas[nombre].Count != 1)
                        { errores.Add("La pieza " + nombre + " está duplicada."); continue; }
                    Shape referencia = encontradas[nombre][0];
                    if (referencia.SizeWidth <= 0 || referencia.SizeHeight <= 0)
                        { errores.Add("La pieza " + nombre + " no tiene dimensiones válidas."); continue; }
                    referencias.Add(nombre, referencia);
                }
                if (errores.Count > 0)
                    throw new InvalidOperationException("No se modificó el mockup. Revise la capa " + capaDiseno.Name + ":\n\n• " + string.Join("\n• ", errores));

                return referencias;
        }

        public void Aplicar(VGCore.Application app, Document origen, Shape seleccionado, CampoMockup individual, bool lote, Layer capaOrigen = null)
        {
            if (documento == null) throw new InvalidOperationException("Seleccione primero un mockup.");
            if (EscaladorPowerClip.MismoDocumento(origen, documento)) throw new InvalidOperationException("Seleccione el diseño en su documento original, no en la copia del mockup.");
            var referencias = new Dictionary<string, Shape>(StringComparer.OrdinalIgnoreCase);
            List<CampoMockup> tareas;
            if (lote)
            {
                Layer capaDiseno = capaOrigen ?? origen.ActiveLayer;
                if (capaDiseno == null) throw new InvalidOperationException("Active la capa que contiene las partes del diseño en CorelDRAW.");
                tareas = Campos.ToList();
                referencias = ValidarReferenciasCapa(capaDiseno, tareas);

            }
            else
            {
                if (individual == null || !Campos.Contains(individual)) throw new InvalidOperationException("Seleccione la pieza que desea cambiar.");
                tareas = new List<CampoMockup> { individual };
                referencias.Add(individual.Referencia, seleccionado);
            }
            foreach (Shape referencia in referencias.Values)
                if (referencia == null || !DimensionValida(referencia.SizeWidth) || !DimensionValida(referencia.SizeHeight))
                    throw new InvalidOperationException("El diseño no tiene dimensiones válidas para escalar.");
            foreach (Shape destino in tareas.SelectMany(x => x.Destinos))
            {
                if (destino.Locked || destino.PowerClip == null) throw new InvalidOperationException("Hay destinos bloqueados o sin PowerClip. Vuelva a cargar el mockup.");
                if (!DimensionValida(destino.SizeWidth) || !DimensionValida(destino.SizeHeight))
                    throw new InvalidOperationException("El PowerClip " + destino.Name + " no tiene dimensiones válidas.");
            }

            bool eventos = app.EventsEnabled;
            bool optimizacion = app.Optimization;
            bool abierto = false;
            bool cambios = false;
            try
            {
                app.EventsEnabled = false;
                app.Optimization = true;
                documento.Activate();
                documento.BeginCommandGroup("Tithor - Aplicar mockup");
                abierto = true;
                foreach (CampoMockup tarea in tareas)
                {
                    foreach (Shape destino in tarea.Destinos)
                    {
                        origen.Activate();
                        referencias[tarea.Referencia].Copy();
                        documento.Activate();
                        paginaTrabajo.Activate();
                        capaTrabajo.Activate();
                        cambios = true;
                        Shape copia = capaTrabajo.Paste();
                        if (copia == null) throw new InvalidOperationException("No se pudo pegar la referencia " + tarea.Referencia + ".");
                        if (EsMangaDerechaFrente(tarea))
                        {
                            ColocarMangaDerechaFrente(app, copia, destino);
                            continue;
                        }
                        if (tarea.Invertido) copia.Rotate(180);
                        // Medir el marco de la copia ya pegada, en las unidades del destino.
                        // Si es PowerClip, se usa su tamaño exterior, no el de su contenido.
                        double anchoCopia = copia.SizeWidth;
                        double altoCopia = copia.SizeHeight;
                        double anchoFinal = destino.SizeWidth;
                        if (!DimensionValida(anchoCopia) || !DimensionValida(altoCopia))
                            throw new InvalidOperationException("La referencia " + tarea.Referencia + " no tiene dimensiones válidas.");
                        double altoFinal = altoCopia * (anchoFinal / anchoCopia);
                        if (!DimensionValida(altoFinal))
                            throw new InvalidOperationException("No se pudo calcular el tamaño de " + tarea.Referencia + ".");
                        double x = destino.CenterX;
                        double bordeSuperior = destino.CenterY + destino.SizeHeight / 2.0;
                        Shapes anteriores = destino.PowerClip.Shapes;
                        for (int i = anteriores.Count; i >= 1; i--) anteriores[i].Delete();
                        copia.AddToPowerClip(destino, cdrTriState.cdrFalse);
                        copia.SetSize(anchoFinal, altoFinal);
                        copia.CenterX = x;
                        copia.CenterY = bordeSuperior - altoFinal / 2.0;
                    }
                }
                documento.EndCommandGroup();
                abierto = false;
                foreach (CampoMockup tarea in tareas) tarea.Aplicado = true;
            }
            catch
            {
                if (abierto) { try { documento.EndCommandGroup(); } catch { } abierto = false; }
                if (cambios) { RequiereRecargar = true; try { documento.Undo(); } catch { } }
                throw;
            }
            finally
            {
                try { app.Optimization = optimizacion; } catch { }
                try { app.EventsEnabled = eventos; } catch { }
                try { origen.Activate(); } catch { }
            }
        }

        private static bool DimensionValida(double valor)
        {
            return valor > 0 && !double.IsNaN(valor) && !double.IsInfinity(valor);
        }

        public void Exportar(string ruta, int ladoMayor)
        {
            if (documento == null || composicion == null) throw new InvalidOperationException("Cargue un mockup antes de exportar.");
            documento.Activate();
            paginaTrabajo.Activate();
            capaTrabajo.Activate();
            composicion.CreateSelection();
            double ancho = composicion.SizeWidth;
            double alto = composicion.SizeHeight;
            if (ancho <= 0 || alto <= 0) throw new InvalidOperationException("El mockup no tiene dimensiones válidas.");
            int w = Math.Max(1, (int)Math.Round(ladoMayor * ancho / Math.Max(ancho, alto)));
            int h = Math.Max(1, (int)Math.Round(ladoMayor * alto / Math.Max(ancho, alto)));
            var filtro = documento.ExportBitmap(ruta, cdrFilter.cdrJPEG, cdrExportRange.cdrSelection, cdrImageType.cdrRGBColorImage, w, h, 150, 150);
            filtro.Finish();
            if (!File.Exists(ruta) || new FileInfo(ruta).Length == 0) throw new IOException("CorelDRAW no generó el JPEG.");
        }

        public string CrearVistaPrevia()
        {
            string ruta = Path.Combine(carpetaTemporal, "vista.jpg");
            Exportar(ruta, 1200);
            return ruta;
        }

        public bool EstaAbierto(VGCore.Application app)
        {
            if (documento == null || app == null) return false;
            for (int i = 1; i <= app.Documents.Count; i++)
                if (EscaladorPowerClip.MismoDocumento(app.Documents[i], documento)) return true;
            return false;
        }

        public void OlvidarDocumentoCerrado()
        {
            // No llamar a Close ni acceder al documento que el usuario ya cerró.
            documento = null;
            composicion = null;
            capaTrabajo = null;
            paginaTrabajo = null;
            codigoCamiseta = null;
            Campos.Clear();
            RequiereRecargar = true;
            if (carpetaTemporal != null) { try { Directory.Delete(carpetaTemporal, true); } catch { } }
            carpetaTemporal = null;
        }

        public void Dispose()
        {
            if (documento != null)
            {
                // Solo se descarta el documento abierto desde nuestra copia temporal.
                try { documento.Dirty = false; documento.Close(); } catch { }
            }
            documento = null;
            composicion = null;
            capaTrabajo = null;
            paginaTrabajo = null;
            codigoCamiseta = null;
            Campos.Clear();
            if (carpetaTemporal != null) { try { Directory.Delete(carpetaTemporal, true); } catch { } }
            carpetaTemporal = null;
        }
    }
}
