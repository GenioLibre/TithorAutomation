using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TithorAutomation.Modelos;
using TithorAutomation.Servicios;

namespace TithorAutomation
{
    public partial class frmPrincipal
    {
        private EditorMockupCorel editorMockup = new EditorMockupCorel();
        private sealed class ResultadoMockup
        {
            public string Diseno { get; set; }
            public EditorMockupCorel Editor { get; set; }
            public string Vista { get; set; }
            public bool EsOriginal { get; set; }
        }
        private readonly List<ResultadoMockup> resultadosMockup = new List<ResultadoMockup>();
        private ResultadoMockup resultadoMockupActual;
        private bool actualizandoTablaMockup;
        private bool cargandoListaMockups;
        private bool operandoMockup;
        private bool mockupListo;
        private Mockup mockupCargado;
        private string vistaMockupCargado;

        private void ConfigurarEditorMockups()
        {
            cboMockup.SelectedIndexChanged += cboMockup_SelectedIndexChanged;
            btnRecargarMockup.Click += btnRecargarMockup_Click;
            btnAplicarMockup.Click += btnAplicarMockup_Click;
            btnGuardarMockup.Click += btnGuardarMockup_Click;
            btnCopiarMockup.Click += btnCopiarMockup_Click;
            dgvPiezasMockup.SelectionChanged += dgvPiezasMockup_SelectionChanged;
            chkLoteMockup.CheckedChanged += (s, e) =>
            {
                cboCampoMockup.Enabled = !chkLoteMockup.Checked;
                MostrarPiezasMockup();
                if (mockupListo)
                    lblEstadoEditorMockup.Text = chkLoteMockup.Checked
                        ? "Active el documento con sus capas de diseño. Se generará un mockup por capa."
                        : "Seleccione una referencia en CorelDRAW y la pieza que desea reemplazar.";
            };
            FormClosed += (s, e) => { CerrarResultadosMockup(); if (picVistaMockup.Image != null) picVistaMockup.Image.Dispose(); };
            dgvPiezasMockup.ColumnHeadersDefaultCellStyle = dgvCatalogoMockups.ColumnHeadersDefaultCellStyle.Clone();
            dgvPiezasMockup.DefaultCellStyle = dgvCatalogoMockups.DefaultCellStyle.Clone();
            dgvPiezasMockup.AlternatingRowsDefaultCellStyle = dgvCatalogoMockups.AlternatingRowsDefaultCellStyle.Clone();
            cboCampoMockup.Enabled = !chkLoteMockup.Checked;
            MostrarPiezasMockup();
        }

        private void CargarListaMockups()
        {
            string anterior = (cboMockup.SelectedItem as Mockup)?.Codigo;
            archivoMockupMasterActual = mockupRepositorio.ObtenerMaster();
            cargandoListaMockups = true;
            try
            {
                cboMockup.DataSource = archivoMockupMasterActual == null ? null : mockupRepositorio.ObtenerMockups(archivoMockupMasterActual.Id);
                if (anterior != null)
                    foreach (Mockup item in cboMockup.Items)
                        if (item.Codigo == anterior) { cboMockup.SelectedItem = item; break; }
            }
            finally { cargandoListaMockups = false; }
            if (cboMockup.SelectedItem == null)
                lblEstadoEditorMockup.Text = "Sincronice primero el master en Configuración > Mockups.";
        }

        private void EjecutarOperacionMockup(Action accion)
        {
            if (operandoMockup) return;
            RevisarDocumentoMockup();
            operandoMockup = true;
            bool timerActivo = tmrConexionCorel.Enabled;
            tmrConexionCorel.Stop();
            VGCore.Document anterior = null;
            cboMockup.Enabled = btnRecargarMockup.Enabled = btnAplicarMockup.Enabled = btnGuardarMockup.Enabled = btnCopiarMockup.Enabled = false;
            dgvPiezasMockup.Enabled = chkLoteMockup.Enabled = cboCampoMockup.Enabled = false;
            UseWaitCursor = true;
            try
            {
                VGCore.Application app = ObtenerCorel();
                if (app.Documents.Count > 0) anterior = app.ActiveDocument;
                if (vistaPreviaActiva) throw new InvalidOperationException("Restaure primero el resaltado de Pantonear.");
                accion();
            }
            catch (Exception ex)
            {
                lblEstadoEditorMockup.Text = "Proceso detenido: " + ex.Message;
                MessageBox.Show(this, ex.Message, "Mockups", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                try { if (anterior != null) anterior.Activate(); } catch { }
                UseWaitCursor = false;
                operandoMockup = false;
                cboMockup.Enabled = btnRecargarMockup.Enabled = true;
                btnAplicarMockup.Enabled = btnGuardarMockup.Enabled = btnCopiarMockup.Enabled = mockupListo;
                dgvPiezasMockup.Enabled = chkLoteMockup.Enabled = true;
                cboCampoMockup.Enabled = !chkLoteMockup.Checked;
                if (timerActivo) tmrConexionCorel.Start();
            }
        }

        private void cboMockup_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cargandoListaMockups || operandoMockup) return;
            MostrarSeleccionMockup();
        }

        private void btnRecargarMockup_Click(object sender, EventArgs e)
        {
            RevisarDocumentoMockup();
            if (editorMockup.Documento != null && MessageBox.Show(this, "¿Cerrar las copias de mockups actuales y cargar el original? Se descartarán sus cambios.", "Mockups", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            CargarMockupSeleccionado();
        }

        private void MostrarSeleccionMockup()
        {
            RevisarDocumentoMockup();
            var elegido = cboMockup.SelectedItem as Mockup;
            mockupListo = editorMockup.Documento != null && mockupCargado != null && elegido != null
                && elegido.Id == mockupCargado.Id && !editorMockup.RequiereRecargar;
            btnAplicarMockup.Enabled = btnGuardarMockup.Enabled = btnCopiarMockup.Enabled = mockupListo;
            if (mockupListo)
            {
                cboCampoMockup.DataSource = editorMockup.Campos;
                MostrarPiezasMockup();
                if (File.Exists(vistaMockupCargado)) MostrarJpegMockup(vistaMockupCargado);
                lblEstadoEditorMockup.Text = "Mockup cargado. Puede editarlo o cerrarlo desde CorelDRAW.";
                return;
            }
            cboCampoMockup.DataSource = null;
            dgvPiezasMockup.Rows.Clear();
            Image vieja = picVistaMockup.Image;
            picVistaMockup.Image = null;
            if (vieja != null) vieja.Dispose();
            if (elegido != null && archivoMockupMasterActual != null)
            {
                string ruta = RutaVistaOriginalMockup(elegido);
                if (File.Exists(ruta))
                {
                    try { MostrarJpegMockup(ruta); } catch { }
                }
            }
            lblEstadoEditorMockup.Text = elegido == null ? "Seleccione un mockup sincronizado." : "Vista previa guardada. Pulse Cargar mockup para abrir una copia en CorelDRAW.";
        }

        private void RevisarDocumentoMockup()
        {
            if (operandoMockup) return;
            bool cambio = false;
            foreach (ResultadoMockup resultado in resultadosMockup.ToList())
            {
                bool abierto;
                try { abierto = resultado.Editor.EstaAbierto(corelApp); }
                catch { return; }
                if (abierto) continue;
                resultado.Editor.OlvidarDocumentoCerrado();
                resultadosMockup.Remove(resultado);
                cambio = true;
            }
            if (!cambio) return;
            if (!resultadosMockup.Contains(resultadoMockupActual))
            {
                resultadoMockupActual = resultadosMockup.FirstOrDefault(x => !x.EsOriginal) ?? resultadosMockup.FirstOrDefault();
                if (resultadoMockupActual != null) SeleccionarResultadoMockup(resultadoMockupActual, null);
                else
                {
                    mockupListo = false;
                    editorMockup = new EditorMockupCorel();
                    vistaMockupCargado = null;
                    cboCampoMockup.DataSource = null;
                    LimpiarImagenMockup();
                }
            }
            var seleccionado = cboMockup.SelectedItem as Mockup;
            if (seleccionado == null || mockupCargado == null || seleccionado.Id != mockupCargado.Id)
            {
                mockupListo = false;
                dgvPiezasMockup.Rows.Clear();
            }
            else MostrarPiezasMockup();
            btnAplicarMockup.Enabled = btnGuardarMockup.Enabled = btnCopiarMockup.Enabled = mockupListo;
            lblEstadoEditorMockup.Text = "Se retiraron los mockups cerrados. Puede seleccionar otro resultado o cargar el original.";
        }

        private string RutaVistaOriginalMockup(Mockup mockup)
        {
            string carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TithorAutomation", "Mockups", "VistasPrevias", archivoMockupMasterActual.Id.ToString());
            return Path.Combine(carpeta, EditorMockupCorel.Normalizar(mockup.Codigo) + ".jpg");
        }

        private void GuardarJpegVistaMockup(string origen, string destino)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destino));
            string temporal = destino + "." + Guid.NewGuid().ToString("N") + ".jpg";
            try
            {
                File.Copy(origen, temporal);
                if (File.Exists(destino)) File.Replace(temporal, destino, null);
                else File.Move(temporal, destino);
            }
            finally { if (File.Exists(temporal)) File.Delete(temporal); }
        }

        private void GenerarVistasOriginalesMockups()
        {
            bool timerActivo = tmrConexionCorel.Enabled;
            VGCore.Document anterior = null;
            tmrConexionCorel.Stop();
            UseWaitCursor = true;
            btnSincronizarMockups.Enabled = false;
            try
            {
                if (vistaPreviaActiva) throw new InvalidOperationException("Restaure primero el resaltado de Pantonear.");
                var app = ObtenerCorel();
                if (app.Documents.Count > 0) anterior = app.ActiveDocument;
                using (var editor = new EditorMockupCorel())
                {
                    for (int i = 0; i < mockupsAnalizados.Count; i++)
                    {
                        Mockup mockup = mockupsAnalizados[i];
                        lblEstadoMockups.Text = "Guardando vista previa " + (i + 1) + " de " + mockupsAnalizados.Count + ": " + mockup.Producto;
                        lblEstadoMockups.Refresh();
                        editor.Cargar(app, archivoMockupMasterActual.RutaArchivo, mockup);
                        GuardarJpegVistaMockup(editor.CrearVistaPrevia(), RutaVistaOriginalMockup(mockup));
                    }
                }
            }
            finally
            {
                try { if (anterior != null) anterior.Activate(); } catch { }
                UseWaitCursor = false;
                btnSincronizarMockups.Enabled = true;
                if (timerActivo) tmrConexionCorel.Start();
            }
        }

        private void CargarMockupSeleccionado()
        {
            var elegido = cboMockup.SelectedItem as Mockup;
            Image vieja = picVistaMockup.Image;
            picVistaMockup.Image = null;
            if (vieja != null) vieja.Dispose();
            bool vistaGuardada = false;
            string rutaVista = null;
            if (elegido != null && archivoMockupMasterActual != null)
            {
                rutaVista = RutaVistaOriginalMockup(elegido);
                if (File.Exists(rutaVista))
                {
                    try
                    {
                        MostrarJpegMockup(rutaVista);
                        picVistaMockup.Refresh();
                        vistaGuardada = true;
                    }
                    catch { /* Una vista dañada se vuelve a generar desde la copia del master. */ }
                }
            }
            mockupListo = false;
            EjecutarOperacionMockup(() =>
            {
                cboCampoMockup.DataSource = null;
                dgvPiezasMockup.Rows.Clear();
                if (elegido == null || archivoMockupMasterActual == null) throw new InvalidOperationException("Sincronice y seleccione un mockup.");
                lblEstadoEditorMockup.Text = "Preparando mockup...";
                lblEstadoEditorMockup.Refresh();
                CerrarResultadosMockup();
                editorMockup = new EditorMockupCorel();
                editorMockup.Cargar(ObtenerCorel(), archivoMockupMasterActual.RutaArchivo, elegido);
                cboCampoMockup.DataSource = editorMockup.Campos;
                if (!vistaGuardada)
                {
                    string nueva = editorMockup.CrearVistaPrevia();
                    GuardarJpegVistaMockup(nueva, rutaVista);
                    MostrarJpegMockup(rutaVista);
                }
                vistaMockupCargado = rutaVista;
                mockupCargado = elegido;
                mockupListo = true;
                resultadoMockupActual = new ResultadoMockup { Diseno = "Original", Editor = editorMockup, Vista = rutaVista, EsOriginal = true };
                resultadosMockup.Add(resultadoMockupActual);
                MostrarPiezasMockup();
                lblEstadoEditorMockup.Text = chkLoteMockup.Checked
                    ? "Active el documento con sus capas de diseño. Se generará un mockup por capa."
                    : "Seleccione la referencia en CorelDRAW y pulse Aplicar diseño. No se cambia el tamaño.";
            });
        }

        private void MostrarPiezasMockup()
        {
            actualizandoTablaMockup = true;
            try
            {
                dgvPiezasMockup.Rows.Clear();
                var visibles = resultadosMockup.Any(x => !x.EsOriginal)
                    ? resultadosMockup.Where(x => !x.EsOriginal) : resultadosMockup;
                bool lote = chkLoteMockup.Checked;
                colPiezaEditorMockup.Visible = !lote;
                colReferenciaEditorMockup.Visible = !lote;
                colRotacionEditorMockup.Visible = !lote;
                foreach (ResultadoMockup resultado in visibles)
                {
                    if (lote)
                    {
                        var campos = resultado.Editor.Campos;
                        string estado = resultado.Editor.RequiereRecargar ? "Con error"
                            : campos.Count > 0 && campos.All(x => x.Aplicado) ? "Aplicado"
                            : campos.Any(x => x.Aplicado) ? "Parcial" : "Pendiente";
                        int indice = dgvPiezasMockup.Rows.Add(estado, resultado.Diseno, null, null, null,
                            campos.Sum(x => x.Destinos.Count));
                        dgvPiezasMockup.Rows[indice].Tag = Tuple.Create(resultado, (CampoMockup)null);
                    }
                    else
                    {
                        foreach (CampoMockup campo in resultado.Editor.Campos)
                        {
                            int indice = dgvPiezasMockup.Rows.Add(resultado.Editor.RequiereRecargar ? "Con error" : campo.Aplicado ? "Aplicado" : "Pendiente",
                                resultado.Diseno, campo.Nombre, campo.Referencia, campo.Invertido ? "180°" : "0°", campo.Destinos.Count);
                            dgvPiezasMockup.Rows[indice].Tag = Tuple.Create(resultado, campo);
                        }
                    }
                }
                dgvPiezasMockup.ClearSelection();
                foreach (DataGridViewRow fila in dgvPiezasMockup.Rows)
                {
                    var dato = fila.Tag as Tuple<ResultadoMockup, CampoMockup>;
                    if (dato != null && dato.Item1 == resultadoMockupActual)
                    {
                        dgvPiezasMockup.CurrentCell = fila.Cells[0];
                        fila.Selected = true;
                        break;
                    }
                }
            }
            finally { actualizandoTablaMockup = false; }
        }

        private void dgvPiezasMockup_SelectionChanged(object sender, EventArgs e)
        {
            if (actualizandoTablaMockup || operandoMockup || dgvPiezasMockup.CurrentRow == null) return;
            var dato = dgvPiezasMockup.CurrentRow.Tag as Tuple<ResultadoMockup, CampoMockup>;
            if (dato != null) SeleccionarResultadoMockup(dato.Item1, dato.Item2);
        }

        private void SeleccionarResultadoMockup(ResultadoMockup resultado, CampoMockup campo)
        {
            resultadoMockupActual = resultado;
            editorMockup = resultado.Editor;
            vistaMockupCargado = resultado.Vista;
            cboCampoMockup.DataSource = null;
            cboCampoMockup.DataSource = editorMockup.Campos;
            if (campo != null) cboCampoMockup.SelectedItem = campo;
            mockupListo = editorMockup.Documento != null && !editorMockup.RequiereRecargar;
            LimpiarImagenMockup();
            if (File.Exists(resultado.Vista))
            {
                try { MostrarJpegMockup(resultado.Vista); }
                catch (Exception ex) { lblEstadoEditorMockup.Text = "No se pudo mostrar la vista previa: " + ex.Message; }
            }
            btnAplicarMockup.Enabled = btnGuardarMockup.Enabled = btnCopiarMockup.Enabled = mockupListo && !operandoMockup;
            lblEstadoEditorMockup.Text = "Diseño seleccionado: " + resultado.Diseno;
        }

        private void LimpiarImagenMockup()
        {
            Image anterior = picVistaMockup.Image;
            picVistaMockup.Image = null;
            if (anterior != null) anterior.Dispose();
        }

        private void CerrarResultadosMockup()
        {
            foreach (EditorMockupCorel editor in resultadosMockup.Select(x => x.Editor).Concat(new[] { editorMockup }).Distinct())
                editor.Dispose();
            resultadosMockup.Clear();
            resultadoMockupActual = null;
        }

        private void ActualizarVistaMockup()
        {
            vistaMockupCargado = editorMockup.CrearVistaPrevia();
            if (resultadoMockupActual != null) resultadoMockupActual.Vista = vistaMockupCargado;
            MostrarJpegMockup(vistaMockupCargado);
        }

        private void MostrarJpegMockup(string ruta)
        {
            using (Image imagen = Image.FromFile(ruta))
            {
                Image vieja = picVistaMockup.Image;
                picVistaMockup.Image = new Bitmap(imagen);
                if (vieja != null) vieja.Dispose();
            }
        }

        private void btnAplicarMockup_Click(object sender, EventArgs e)
        {
            EjecutarOperacionMockup(() =>
            {
                var app = ObtenerCorel();
                if (!mockupListo || app.Documents.Count == 0) throw new InvalidOperationException("Cargue el mockup y abra su diseño en CorelDRAW.");
                var origen = app.ActiveDocument;
                if (resultadosMockup.Any(x => EscaladorPowerClip.MismoDocumento(x.Editor.Documento, origen)))
                    throw new InvalidOperationException("Active el documento de los diseños, no una copia del mockup.");
                if (chkLoteMockup.Checked)
                {
                    AplicarTodosLosMockups(app, origen);
                    return;
                }
                var seleccion = origen.SelectionRange;
                if (!chkLoteMockup.Checked && seleccion.Count != 1) throw new InvalidOperationException("Seleccione una única referencia en CorelDRAW.");
                try
                {
                    editorMockup.Aplicar(app, origen, seleccion.Count == 1 ? seleccion[1] : null, cboCampoMockup.SelectedItem as CampoMockup, chkLoteMockup.Checked);
                }
                catch (Exception ex)
                {
                    // Una validación fallida no descarta los reemplazos anteriores.
                    if (editorMockup.RequiereRecargar)
                    {
                        mockupListo = false;
                        throw new InvalidOperationException(ex.Message + "\nPulse Cargar mockup antes de volver a aplicar.", ex);
                    }
                    throw;
                }
                if (resultadoMockupActual != null && resultadoMockupActual.EsOriginal)
                {
                    resultadoMockupActual.Diseno = origen.ActiveLayer.Name;
                    resultadoMockupActual.EsOriginal = false;
                }
                MostrarPiezasMockup();
                try { ActualizarVistaMockup(); }
                catch (Exception ex) { throw new InvalidOperationException("Diseño aplicado, pero no se pudo actualizar la vista previa: " + ex.Message, ex); }
                lblEstadoEditorMockup.Text = "Diseño aplicado. Vista previa actualizada.";
            });
        }


        private void AplicarTodosLosMockups(VGCore.Application app, VGCore.Document origen)
        {
            var necesarias = new HashSet<string>(editorMockup.Campos.Select(x => x.Referencia), StringComparer.OrdinalIgnoreCase);
            // Los diseños de Mockups se detectan únicamente en el documento de CorelDRAW.
            var capas = new List<VGCore.Layer>();
            for (int p = 1; p <= origen.Pages.Count; p++)
                for (int l = 1; l <= origen.Pages[p].Layers.Count; l++)
                {
                    VGCore.Layer capa = origen.Pages[p].Layers[l];
                    string nombre = EditorMockupCorel.Normalizar(capa.Name);
                    if (nombre == "guias" || nombre == "guides" || nombre == "tithor_produccion") continue;
                    bool candidata = false;
                    for (int i = 1; i <= capa.Shapes.Count && !candidata; i++)
                    {
                        string pieza = EditorMockupCorel.Normalizar(capa.Shapes[i].Name);
                        if (pieza.StartsWith("diseno_", StringComparison.Ordinal)) pieza = pieza.Substring("diseno_".Length);
                        candidata = necesarias.Contains(pieza);
                    }
                    if (candidata) capas.Add(capa);
                }
            var errores = new List<string>();
            foreach (var grupo in capas.GroupBy(x => EditorMockupCorel.Normalizar(x.Name)))
                if (grupo.Count() > 1) errores.Add("La capa " + grupo.First().Name + " está duplicada.");
            foreach (VGCore.Layer capa in capas)
            {
                try { EditorMockupCorel.ValidarReferenciasCapa(capa, editorMockup.Campos); }
                catch (Exception ex) { errores.Add(ex.Message); }
            }
            if (capas.Count == 0) errores.Add("No se encontraron capas de diseño con las partes del mockup.");
            if (errores.Count > 0)
                throw new InvalidOperationException("No se generó ningún mockup.\n\n" + string.Join("\n\n", errores));

            var nuevos = new List<ResultadoMockup>();
            try
            {
                foreach (VGCore.Layer capa in capas)
                {
                    lblEstadoEditorMockup.Text = "Generando " + (nuevos.Count + 1) + " de " + capas.Count + ": " + capa.Name;
                    lblEstadoEditorMockup.Refresh();
                    var resultado = new ResultadoMockup { Diseno = capa.Name, Editor = new EditorMockupCorel() };
                    nuevos.Add(resultado);
                    resultado.Editor.Cargar(app, archivoMockupMasterActual.RutaArchivo, mockupCargado);
                    resultado.Editor.Aplicar(app, origen, null, null, true, capa);
                    resultado.Vista = resultado.Editor.CrearVistaPrevia();
                }
            }
            catch
            {
                // Solo son copias temporales de esta ejecución; conservar los resultados anteriores.
                foreach (ResultadoMockup resultado in nuevos) resultado.Editor.Dispose();
                throw;
            }
            foreach (ResultadoMockup nuevo in nuevos)
            {
                foreach (ResultadoMockup anterior in resultadosMockup.Where(x => !x.EsOriginal &&
                    EditorMockupCorel.Normalizar(x.Diseno) == EditorMockupCorel.Normalizar(nuevo.Diseno)).ToList())
                {
                    anterior.Editor.Dispose();
                    resultadosMockup.Remove(anterior);
                }
                resultadosMockup.Add(nuevo);
            }
            SeleccionarResultadoMockup(nuevos[0], null);
            MostrarPiezasMockup();
            lblEstadoEditorMockup.Text = nuevos.Count + " mockups generados. Seleccione una fila para ver, guardar o copiar su diseño.";
            MessageBox.Show(this, lblEstadoEditorMockup.Text, "Mockups terminados", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string NombreArchivoMockup()
        {
            string nombre = EditorMockupCorel.Normalizar(resultadoMockupActual == null ? mockupCargado?.Codigo : resultadoMockupActual.Diseno);
            return string.IsNullOrWhiteSpace(nombre) ? "mockup" : nombre;
        }

        private void btnCopiarMockup_Click(object sender, EventArgs e)
        {
            if (!mockupListo) return;
            EjecutarOperacionMockup(() =>
            {
                if (!mockupListo) throw new InvalidOperationException("Seleccione un mockup abierto.");
                string temporal = Path.Combine(Path.GetTempPath(), "Tithor_copiar_" + Guid.NewGuid().ToString("N") + ".jpg");
                try
                {
                    editorMockup.Exportar(temporal, 3000);
                    using (Image imagen = Image.FromFile(temporal))
                    using (var copia = new Bitmap(imagen))
                        Clipboard.SetImage(copia);
                    lblEstadoEditorMockup.Text = "Imagen copiada: " + resultadoMockupActual.Diseno + ". Puede pegarla con Ctrl+V.";
                }
                finally { if (File.Exists(temporal)) File.Delete(temporal); }
            });
        }

        private void btnGuardarMockup_Click(object sender, EventArgs e)
        {
            if (!mockupListo) return;
            ResultadoMockup elegido = resultadoMockupActual;
            using (var dialogo = new SaveFileDialog())
            {
                dialogo.Title = "Guardar mockup como JPEG";
                dialogo.Filter = "Imagen JPEG (*.jpg)|*.jpg";
                dialogo.DefaultExt = "jpg";
                dialogo.AddExtension = true;
                dialogo.OverwritePrompt = true;
                dialogo.FileName = NombreArchivoMockup() + ".jpg";
                if (dialogo.ShowDialog(this) != DialogResult.OK) return;
                EjecutarOperacionMockup(() =>
                {
                    if (elegido == null || !resultadosMockup.Contains(elegido) || !elegido.Editor.EstaAbierto(ObtenerCorel()))
                        throw new InvalidOperationException("El mockup seleccionado se cerró. No se guardó ninguna imagen.");
                    string extension = Path.GetExtension(dialogo.FileName);
                    if (!string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Use una extensión .jpg o .jpeg para guardar el mockup.");
                    // Exportar a un archivo intermedio antes de reemplazar el destino elegido.
                    string temporal = Path.Combine(Path.GetDirectoryName(dialogo.FileName), Guid.NewGuid().ToString("N") + ".jpg");
                    try
                    {
                        elegido.Editor.Exportar(temporal, 3000);
                        if (File.Exists(dialogo.FileName)) File.Replace(temporal, dialogo.FileName, null);
                        else File.Move(temporal, dialogo.FileName);
                    }
                    finally { if (File.Exists(temporal)) File.Delete(temporal); }
                    lblEstadoEditorMockup.Text = "JPEG guardado: " + dialogo.FileName;
                    MessageBox.Show(this, "Mockup exportado correctamente.", "Mockups", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }
        }
    }
}
