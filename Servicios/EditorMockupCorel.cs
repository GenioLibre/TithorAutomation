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
        private readonly HashSet<string> ribsFrente = new HashSet<string>();
        private readonly HashSet<string> mangasFrente = new HashSet<string>();
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
            return VarianteMockupCamiseta.DesdeCodigo(Normalizar(nombre)) != null;
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
                bool rib = AdaptadorRibMockup.EsNombre(nombre);
                bool pieza = rib || nombre == "frente" || nombre == "espalda" || nombre == "manga_corta_izquierda" || nombre == "manga_corta_derecha";
                if (pieza)
                {
                    if (objeto.PowerClip == null && !(rib && objeto.Type == cdrShapeType.cdrCurveShape)) throw new InvalidOperationException("La pieza " + nombre + " debe ser un PowerClip (rib también admite una curva cerrada).");
                    if (rib) nombre = "rib";
                    if (!campos.ContainsKey(nombre))
                        campos.Add(nombre, new CampoMockup { Nombre = nombre, Referencia = nombre, Destinos = new List<Shape>() });
                    campos[nombre].Destinos.Add(objeto);
                }
                else if (objeto.PowerClip == null && objeto.Type == cdrShapeType.cdrGroupShape)
                    RecogerCamposCamiseta(objeto.Shapes, campos);
                // Conservar decoración, bitmap y piezas aún no habilitadas.
            }
        }

        private bool EsMangaCamiseta(CampoMockup campo)
        {
            return (codigoCamiseta == "clasico_varon_v" || codigoCamiseta == "clasico_varon_redondo" || codigoCamiseta == "clasico_varon_espalda")
                && (campo.Referencia == "manga_corta_derecha" || campo.Referencia == "manga_corta_izquierda");
        }

        private double AnguloManga(CampoMockup campo, Shape destino)
        {
            bool posterior = mangasFrente.Count > 0
                ? !mangasFrente.Contains(Convert.ToString(destino.StaticID))
                : codigoCamiseta == "clasico_varon_espalda";
            bool izquierda = campo.Referencia == "manga_corta_izquierda";
            // Frente: derecha positiva, izquierda negativa.
            // Espalda: izquierda positiva, derecha negativa.
            return izquierda == posterior ? 294.088 : -294.088;
        }

        private void ColocarMangaCamiseta(VGCore.Application app, Shape copia, Shape destino, double anguloFinal, Action<string> informar)
        {
            informar("Preparando el marco de la manga");
            copia.RotateEx(-copia.RotationAngle, copia.CenterX, copia.CenterY);
            double alto = app.ConvertUnits(196.652, cdrUnit.cdrMillimeter, documento.Unit);
            double desplazamiento = app.ConvertUnits(10.0, cdrUnit.cdrMillimeter, documento.Unit);
            double ancho = copia.SizeWidth * alto / copia.SizeHeight;
            if (!DimensionValida(ancho)) throw new InvalidOperationException("La manga no tiene un marco válido.");
            double x, y;
            informar("Calculando el punto superior del contorno");
            ObtenerPuntoSuperior(destino, out x, out y);
            informar("Escalando la manga a 196.652 mm de alto");
            copia.SetSize(ancho, alto);
            copia.CenterX = x;
            copia.CenterY = y - alto / 2.0;
            // Conservar la alineación superior previa; bajar solo el pivote 10 mm
            // dentro del rectángulo antes de girar.
            double pivoteY = y - desplazamiento;
            informar("Rotando la manga a " + anguloFinal.ToString(System.Globalization.CultureInfo.InvariantCulture) + " grados");
            copia.RotateEx(anguloFinal - copia.RotationAngle, x, pivoteY);
            copia.RotationCenterX = x;
            copia.RotationCenterY = pivoteY;
            ReemplazarContenidoMockup(copia, destino, informar);
        }

        private static void ReemplazarContenidoMockup(Shape copia, Shape destino, Action<string> informar)
        {
            informar("Leyendo el contenido anterior del PowerClip");
            var anteriores = new List<Shape>();
            if (destino.PowerClip != null)
            {
                Shapes contenido = destino.PowerClip.Shapes;
                for (int i = 1; i <= contenido.Count; i++) anteriores.Add(contenido[i]);
            }
            // Insertar antes de borrar mantiene el PowerClip con contenido durante
            // toda la operación. La copia ya tiene su tamaño y posición finales.
            informar("Insertando el diseño en el PowerClip");
            copia.AddToPowerClip(destino, cdrTriState.cdrFalse);
            informar("Eliminando el contenido anterior del PowerClip");
            for (int i = anteriores.Count - 1; i >= 0; i--) anteriores[i].Delete();
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

        public void Cargar(VGCore.Application app, string rutaMaster, Mockup mockup, OpcionesMockupCamiseta opciones = null)
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
                if (!string.IsNullOrWhiteSpace(mockup.NombrePagina))
                {
                    CargarPagina(app, mockup, opciones ?? new OpcionesMockupCamiseta());
                    return;
                }
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

        public static List<Tuple<Layer, Shape>> BuscarVariantesPagina(Page pagina)
        {
            var resultado = new List<Tuple<Layer, Shape>>();
            for (int l = 1; l <= pagina.Layers.Count; l++)
            {
                Layer capa = pagina.Layers[l];
                if (Normalizar(capa.Name) == "guias" || Normalizar(capa.Name) == "guides") continue;
                resultado.AddRange(BuscarGruposCamiseta(capa.Shapes).Select(x => Tuple.Create(capa, x)));
            }
            return resultado;
        }

        public static List<Tuple<Layer, Shape>> BuscarContenedoresFundas(Page pagina)
        {
            var resultado = new List<Tuple<Layer, Shape>>();
            for (int l = 1; l <= pagina.Layers.Count; l++)
            {
                Layer capa = pagina.Layers[l];
                string nombre = Normalizar(capa.Name);
                if (nombre == "diseno" || nombre.StartsWith("mockup_", StringComparison.Ordinal))
                    resultado.Add(Tuple.Create(capa, (Shape)null));
                else
                    for (int i = 1; i <= capa.Shapes.Count; i++)
                    {
                        Shape grupo = capa.Shapes[i];
                        nombre = Normalizar(grupo.Name);
                        if (grupo.Type == cdrShapeType.cdrGroupShape && grupo.PowerClip == null &&
                            (nombre == "diseno" || nombre.StartsWith("mockup_", StringComparison.Ordinal)))
                            resultado.Add(Tuple.Create(capa, grupo));
                    }
            }
            if (resultado.Count == 0)
            {
                // Con una única capa de contenido, su nombre no define el producto:
                // lo define la página Fundas.
                for (int l = 1; l <= pagina.Layers.Count; l++)
                {
                    Layer capa = pagina.Layers[l];
                    string nombre = Normalizar(capa.Name);
                    if (nombre != "guias" && nombre != "guides" && capa.Shapes.Count > 0)
                        resultado.Add(Tuple.Create(capa, (Shape)null));
                }
            }
            return resultado;
        }

        public static List<CampoMockup> DetectarCamposFundas(Shapes objetos, string nombre)
        {
            bool directos = Normalizar(nombre) == "diseno" || (objetos.Count > 0 &&
                Enumerable.Range(1, objetos.Count).All(i => objetos[i].PowerClip != null));
            return DetectarCampos(objetos, directos);
        }

        private void CargarPagina(VGCore.Application app, Mockup mockup, OpcionesMockupCamiseta opciones)
        {
            var paginas = new List<Page>();
            for (int p = 1; p <= documento.Pages.Count; p++)
                if (AnalizadorMockupCorel.CodigoPagina(documento.Pages[p].Name) == mockup.Codigo) paginas.Add(documento.Pages[p]);
            if (paginas.Count != 1) throw new InvalidOperationException("No se encuentra una única página " + mockup.Producto + ". Vuelva a sincronizar.");
            paginaTrabajo = paginas[0];
            paginaTrabajo.Activate();
            composicion = app.CreateShapeRange();
            if (mockup.EsCamiseta)
            {
                var grupos = BuscarVariantesPagina(paginaTrabajo);
                var seleccion = grupos.Where(x =>
                {
                    var variante = VarianteMockupCamiseta.DesdeCodigo(Normalizar(x.Item2.Name));
                    return variante.Tipo == opciones.Tipo && variante.Corte == opciones.Corte &&
                        (variante.EsEspalda || variante.Cuello == opciones.Cuello);
                }).ToList();
                if (seleccion.Count != 2 || seleccion.Count(x => VarianteMockupCamiseta.DesdeCodigo(Normalizar(x.Item2.Name)).EsEspalda) != 1)
                    throw new InvalidOperationException("Falta el frente o la espalda, o hay duplicados para la camiseta seleccionada. Vuelva a analizar el master.");
                var campos = new Dictionary<string, CampoMockup>(StringComparer.OrdinalIgnoreCase);
                foreach (var entrada in seleccion)
                {
                    Shape grupo = entrada.Item2;
                    var variante = VarianteMockupCamiseta.DesdeCodigo(Normalizar(grupo.Name));
                    entrada.Item1.Visible = true;
                    entrada.Item1.Editable = true;
                    grupo.Visible = true;
                    grupo.Locked = false;
                    grupo.CenterY = paginaTrabajo.CenterY;
                    foreach (CampoMockup campo in DetectarCamposCamiseta(grupo.Shapes))
                    {
                        if (!variante.EsEspalda && campo.Referencia == "rib")
                            foreach (Shape destino in campo.Destinos) ribsFrente.Add(Convert.ToString(destino.StaticID));
                        if (!variante.EsEspalda && (campo.Referencia == "manga_corta_derecha" || campo.Referencia == "manga_corta_izquierda"))
                            foreach (Shape destino in campo.Destinos) mangasFrente.Add(Convert.ToString(destino.StaticID));
                        if (!campos.ContainsKey(campo.Nombre)) campos.Add(campo.Nombre, campo);
                        else campos[campo.Nombre].Destinos.AddRange(campo.Destinos);
                    }
                    if (!variante.EsEspalda) codigoCamiseta = Normalizar(grupo.Name);
                    composicion.Add(grupo);
                }
                // La copia temporal contiene únicamente las dos vistas elegidas.
                // Los grupos del master original nunca se modifican.
                foreach (var entrada in grupos)
                    if (!seleccion.Any(x => Normalizar(x.Item2.Name) == Normalizar(entrada.Item2.Name))) entrada.Item2.Delete();
                Campos = campos.Values.ToList();
                capaTrabajo = seleccion[0].Item1;
            }
            else
            {
                var contenedores = BuscarContenedoresFundas(paginaTrabajo);
                if (contenedores.Count != 1) throw new InvalidOperationException("La página Fundas debe contener un solo mockup.");
                var entrada = contenedores[0];
                capaTrabajo = entrada.Item1;
                Shapes objetos = entrada.Item2 == null ? capaTrabajo.Shapes : entrada.Item2.Shapes;
                string nombre = entrada.Item2 == null ? capaTrabajo.Name : entrada.Item2.Name;
                Campos = DetectarCamposFundas(objetos, nombre);
                if (entrada.Item2 == null) composicion = objetos.All();
                else { entrada.Item2.Visible = true; composicion.Add(entrada.Item2); }
            }
            capaTrabajo.Visible = true;
            capaTrabajo.Editable = true;
            capaTrabajo.Activate();
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
            try { AplicarInterno(app, origen, seleccionado, individual, lote, capaOrigen); }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                throw new InvalidOperationException("CorelDRAW falló al validar las referencias o los destinos antes del reemplazo. Código: 0x" +
                    ex.HResult.ToString("X8") + "\n" + ex.Message, ex);
            }
        }

        private void AplicarInterno(VGCore.Application app, Document origen, Shape seleccionado, CampoMockup individual, bool lote, Layer capaOrigen)
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
                if (destino.Locked || (destino.PowerClip == null && !(codigoCamiseta != null && AdaptadorRibMockup.EsNombre(Normalizar(destino.Name)) && destino.Type == cdrShapeType.cdrCurveShape))) throw new InvalidOperationException("Hay destinos bloqueados o sin PowerClip. Vuelva a cargar el mockup.");
                if (!DimensionValida(destino.SizeWidth) || !DimensionValida(destino.SizeHeight))
                    throw new InvalidOperationException("El PowerClip " + destino.Name + " no tiene dimensiones válidas.");
            }

            var tramosRib = new Dictionary<string, TramoRibMockup>();
            bool aplicarRib = !string.IsNullOrWhiteSpace(codigoCamiseta) && tareas.Any(t => t.Referencia == "rib");

            bool eventos = app.EventsEnabled;
            bool optimizacion = app.Optimization;
            bool abierto = false;
            bool cambios = false;
            string etapa = "Iniciando el reemplazo";
            string pieza = "Sin iniciar";
            int indiceDestino = 0;
            try
            {
                app.EventsEnabled = false;
                app.Optimization = true;
                documento.Activate();
                documento.BeginCommandGroup("Tithor - Aplicar mockup");
                abierto = true;
                if (aplicarRib)
                {
                    // Medir una copia en el documento de trabajo. SizeWidth/Height
                    // del original rotado son su caja envolvente, no el rectángulo
                    // horizontal que se utilizará para distribuir el estampado.
                    pieza = "rib";
                    etapa = "Preparando la orientación del rib";
                    origen.Activate();
                    referencias["rib"].Copy();
                    documento.Activate();
                    paginaTrabajo.Activate();
                    capaTrabajo.Activate();
                    cambios = true;
                    Shape muestraRib = capaTrabajo.Paste();
                    if (muestraRib == null) throw new InvalidOperationException("No se pudo copiar el diseño rib.");
                    AdaptadorRibMockup.NormalizarOrientacion(muestraRib);
                    var destinosRib = Campos.Where(t => t.Referencia == "rib").SelectMany(t => t.Destinos)
                        .OrderBy(d => ribsFrente.Contains(Convert.ToString(d.StaticID)) ? 0 : 1)
                        .ThenBy(d => d.CenterX).ToList();
                    etapa = "Validando los tramos del cuello";
                    foreach (var tramo in AdaptadorRibMockup.Preparar(destinosRib, muestraRib.SizeWidth / muestraRib.SizeHeight))
                        tramosRib.Add(Convert.ToString(tramo.Destino.StaticID), tramo);
                    muestraRib.Delete();
                }
                foreach (CampoMockup tarea in tareas)
                {
                    indiceDestino = 0;
                    pieza = tarea.Nombre;
                    foreach (Shape destino in tarea.Destinos)
                    {
                        indiceDestino++;
                        etapa = "Copiando la referencia desde el documento de diseño";
                        origen.Activate();
                        referencias[tarea.Referencia].Copy();
                        etapa = "Activando el documento de mockup";
                        documento.Activate();
                        paginaTrabajo.Activate();
                        capaTrabajo.Activate();
                        cambios = true;
                        etapa = "Pegando la referencia en el mockup";
                        Shape copia = capaTrabajo.Paste();
                        if (copia == null) throw new InvalidOperationException("No se pudo pegar la referencia " + tarea.Referencia + ".");
                        if (tarea.Referencia == "rib" && tramosRib.Count > 0)
                        {
                            Shape rib = AdaptadorRibMockup.Deformar(copia, tramosRib[Convert.ToString(destino.StaticID)], capaTrabajo, valor => etapa = valor);
                            ReemplazarContenidoMockup(rib, destino, valor => etapa = valor);
                            continue;
                        }
                        if (EsMangaCamiseta(tarea))
                        {
                            double angulo = AnguloManga(tarea, destino);
                            ColocarMangaCamiseta(app, copia, destino, angulo, valor => etapa = valor);
                            continue;
                        }
                        etapa = "Midiendo el marco del diseño y el PowerClip de destino";
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
                        etapa = "Escalando y alineando el diseño";
                        copia.SetSize(anchoFinal, altoFinal);
                        copia.CenterX = x;
                        copia.CenterY = bordeSuperior - altoFinal / 2.0;
                        ReemplazarContenidoMockup(copia, destino, valor => etapa = valor);
                    }
                }
                etapa = "Finalizando el reemplazo";
                documento.EndCommandGroup();
                abierto = false;
                foreach (CampoMockup tarea in tareas) tarea.Aplicado = true;
            }
            catch (Exception ex)
            {
                if (abierto) { try { documento.EndCommandGroup(); } catch { } abierto = false; }
                string recuperacion = "No se modificó el mockup.";
                if (cambios)
                {
                    RequiereRecargar = true;
                    try { documento.Undo(); recuperacion = "Se deshicieron los cambios de este intento."; }
                    catch (Exception errorUndo) { recuperacion = "No se pudo deshacer este intento: " + errorUndo.Message; }
                }
                throw new InvalidOperationException("Pieza: " + pieza + " (destino " + indiceDestino + ").\n" +
                    "Etapa: " + etapa + ".\nCódigo: 0x" + ex.HResult.ToString("X8") +
                    "\n" + ex.Message + "\n" + recuperacion, ex);
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
            // Camisetas: incluir el fondo y los elementos de la página, no solo
            // los grupos de frente y espalda. Fundas conserva su exportación actual.
            bool paginaCompleta = !string.IsNullOrWhiteSpace(codigoCamiseta);
            if (!paginaCompleta) composicion.CreateSelection();
            double ancho = paginaCompleta ? paginaTrabajo.SizeWidth : composicion.SizeWidth;
            double alto = paginaCompleta ? paginaTrabajo.SizeHeight : composicion.SizeHeight;
            if (ancho <= 0 || alto <= 0) throw new InvalidOperationException("El mockup no tiene dimensiones válidas.");
            int w = Math.Max(1, (int)Math.Round(ladoMayor * ancho / Math.Max(ancho, alto)));
            int h = Math.Max(1, (int)Math.Round(ladoMayor * alto / Math.Max(ancho, alto)));
            cdrExportRange rango = paginaCompleta ? cdrExportRange.cdrCurrentPage : cdrExportRange.cdrSelection;
            var filtro = documento.ExportBitmap(ruta, cdrFilter.cdrJPEG, rango, cdrImageType.cdrRGBColorImage, w, h, 150, 150);
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
            mangasFrente.Clear();
            ribsFrente.Clear();
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
            mangasFrente.Clear();
            ribsFrente.Clear();
            Campos.Clear();
            if (carpetaTemporal != null) { try { Directory.Delete(carpetaTemporal, true); } catch { } }
            carpetaTemporal = null;
        }
    }
}
