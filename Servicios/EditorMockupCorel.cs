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
                                Campos = DetectarCampos(grupo.Shapes);
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

        public void Aplicar(VGCore.Application app, Document origen, Shape seleccionado, CampoMockup individual, bool lote)
        {
            if (documento == null) throw new InvalidOperationException("Seleccione primero un mockup.");
            if (EscaladorPowerClip.MismoDocumento(origen, documento)) throw new InvalidOperationException("Seleccione el diseño en su documento original, no en la copia del mockup.");
            var referencias = new Dictionary<string, Shape>(StringComparer.OrdinalIgnoreCase);
            List<CampoMockup> tareas;
            if (lote)
            {
                Layer capaDiseno = origen.ActiveLayer;
                if (capaDiseno == null) throw new InvalidOperationException("Active la capa que contiene las partes del diseño en CorelDRAW.");
                tareas = Campos.ToList();
                var necesarias = new HashSet<string>(tareas.Select(x => x.Referencia), StringComparer.OrdinalIgnoreCase);
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

            }
            else
            {
                if (individual == null || !Campos.Contains(individual)) throw new InvalidOperationException("Seleccione la pieza que desea cambiar.");
                tareas = new List<CampoMockup> { individual };
                referencias.Add(individual.Referencia, seleccionado);
            }
            foreach (Shape destino in tareas.SelectMany(x => x.Destinos))
                if (destino.Locked || destino.PowerClip == null) throw new InvalidOperationException("Hay destinos bloqueados o sin PowerClip. Vuelva a cargar el mockup.");

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
                        if (tarea.Invertido) copia.Rotate(180);
                        double x = destino.CenterX;
                        double y = destino.CenterY;
                        Shapes anteriores = destino.PowerClip.Shapes;
                        for (int i = anteriores.Count; i >= 1; i--) anteriores[i].Delete();
                        copia.AddToPowerClip(destino, cdrTriState.cdrFalse);
                        copia.CenterX = x;
                        copia.CenterY = y;
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
            Campos.Clear();
            if (carpetaTemporal != null) { try { Directory.Delete(carpetaTemporal, true); } catch { } }
            carpetaTemporal = null;
        }
    }
}
