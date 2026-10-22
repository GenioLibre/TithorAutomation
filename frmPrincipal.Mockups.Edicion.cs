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
            dgvPiezasMockup.MouseDown += (s, e) =>
            {
                if (!operandoMockup && dgvPiezasMockup.HitTest(e.X, e.Y).RowIndex < 0)
                    dgvPiezasMockup.ClearSelection();
            };
            dgvPiezasMockup.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    dgvPiezasMockup.ClearSelection();
                    e.Handled = true;
                }
            };
            chkLoteMockup.CheckedChanged += (s, e) =>
            {
                cboCampoMockup.Enabled = !chkLoteMockup.Checked;
                btnAplicarMockup.Enabled = !operandoMockup && (mockupListo || (chkLoteMockup.Checked && editorMockup.Documento != null));
                MostrarPiezasMockup();
                if (mockupListo)
                    lblEstadoEditorMockup.Text = chkLoteMockup.Checked
                        ? "Sin filas seleccionadas: aplicar todos. Seleccione una fila para aplicar solo ese diseño. Esc quita la selección."
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
                btnCopiarMockup.Enabled = mockupListo;
                btnAplicarMockup.Enabled = mockupListo || (chkLoteMockup.Checked && editorMockup.Documento != null);
                btnGuardarMockup.Enabled = resultadosMockup.Any(EsMockupExportable);
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
            btnCopiarMockup.Enabled = mockupListo;
                btnAplicarMockup.Enabled = mockupListo || (chkLoteMockup.Checked && editorMockup.Documento != null);
                btnGuardarMockup.Enabled = resultadosMockup.Any(EsMockupExportable);
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
            btnCopiarMockup.Enabled = mockupListo;
                btnAplicarMockup.Enabled = mockupListo || (chkLoteMockup.Checked && editorMockup.Documento != null);
                btnGuardarMockup.Enabled = resultadosMockup.Any(EsMockupExportable);
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
                    ? "Sin filas seleccionadas: aplicar todos. Seleccione una fila para aplicar solo ese diseño. Esc quita la selección."
                    : "Seleccione la referencia en CorelDRAW y pulse Aplicar diseño. Se ajusta al ancho y se alinea arriba.";
            });
        }

        private void MostrarPiezasMockup()
        {
            var seleccionados = dgvPiezasMockup.SelectedRows.Cast<DataGridViewRow>()
                .Select(x => x.Tag as Tuple<ResultadoMockup, CampoMockup>).Where(x => x != null).ToList();
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
                    if (dato != null && seleccionados.Any(x => x.Item1 == dato.Item1 && (lote || x.Item2 == dato.Item2)))
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
            if (actualizandoTablaMockup || operandoMockup || dgvPiezasMockup.SelectedRows.Count == 0) return;
            var dato = dgvPiezasMockup.SelectedRows[0].Tag as Tuple<ResultadoMockup, CampoMockup>;
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
            btnCopiarMockup.Enabled = mockupListo && !operandoMockup;
            btnAplicarMockup.Enabled = !operandoMockup && (mockupListo || (chkLoteMockup.Checked && editorMockup.Documento != null));
            btnGuardarMockup.Enabled = !operandoMockup && resultadosMockup.Any(EsMockupExportable);
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
            var filaSeleccionada = dgvPiezasMockup.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault();
            var datoSeleccionado = filaSeleccionada?.Tag as Tuple<ResultadoMockup, CampoMockup>;
            string disenoSeleccionado = datoSeleccionado != null && !datoSeleccionado.Item1.EsOriginal
                ? datoSeleccionado.Item1.Diseno : null;
            EjecutarOperacionMockup(() =>
            {
                var app = ObtenerCorel();
                if ((!mockupListo && !(chkLoteMockup.Checked && editorMockup.Documento != null)) || app.Documents.Count == 0) throw new InvalidOperationException("Cargue el mockup y abra su diseño en CorelDRAW.");
                var origen = app.ActiveDocument;
                if (resultadosMockup.Any(x => EscaladorPowerClip.MismoDocumento(x.Editor.Documento, origen)))
                    throw new InvalidOperationException("Active el documento de los diseños, no una copia del mockup.");
                if (chkLoteMockup.Checked)
                {
                    AplicarTodosLosMockups(app, origen, disenoSeleccionado);
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


        private void AplicarTodosLosMockups(VGCore.Application app, VGCore.Document origen, string disenoSeleccionado)
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
            if (disenoSeleccionado != null)
                capas = capas.Where(x => EditorMockupCorel.Normalizar(x.Name) ==
                    EditorMockupCorel.Normalizar(disenoSeleccionado)).ToList();
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
                throw new InvalidOperationException("No se inició este lote. Los mockups anteriores se conservan.\n\n" + string.Join("\n\n", errores));

            var nuevos = new List<ResultadoMockup>();
            foreach (VGCore.Layer capa in capas)
            {
                lblEstadoEditorMockup.Text = "Aplicando " + (nuevos.Count + 1) + " de " + capas.Count + ": " + capa.Name;
                lblEstadoEditorMockup.Refresh();
                // Reutilizar el documento de este diseño o la copia inicial todavía sin asignar.
                ResultadoMockup resultado = resultadosMockup.FirstOrDefault(x => !x.EsOriginal &&
                    EditorMockupCorel.Normalizar(x.Diseno) == EditorMockupCorel.Normalizar(capa.Name))
                    ?? resultadosMockup.FirstOrDefault(x => x.EsOriginal);
                bool creado = resultado == null;
                if (creado)
                    resultado = new ResultadoMockup { Diseno = capa.Name, Editor = new EditorMockupCorel() };
                bool aplicado = false;
                try
                {
                    if (creado)
                        resultado.Editor.Cargar(app, archivoMockupMasterActual.RutaArchivo, mockupCargado);
                    else if (resultado.Editor.RequiereRecargar || !resultado.Editor.EstaAbierto(app))
                        resultado.Editor.Cargar(app, archivoMockupMasterActual.RutaArchivo, mockupCargado);

                    resultado.Editor.Aplicar(app, origen, null, null, true, capa);
                    aplicado = true;
                    resultado.Diseno = capa.Name;
                    resultado.EsOriginal = false;
                    if (creado) resultadosMockup.Add(resultado);
                    nuevos.Add(resultado);
                    resultado.Vista = null;
                    resultado.Vista = resultado.Editor.CrearVistaPrevia();
                }
                catch (Exception ex)
                {
                    // Conservar los documentos reutilizados y los diseños ya aplicados.
                    // Cerrar solamente una copia nueva que no llegó a completarse.
                    if (creado && !aplicado) resultado.Editor.Dispose();
                    else
                    {
                        if (resultado.Editor.RequiereRecargar) resultado.Vista = null;
                    }
                    ResultadoMockup disponible = nuevos.LastOrDefault(x => !x.Editor.RequiereRecargar)
                        ?? resultadosMockup.FirstOrDefault(x => !x.Editor.RequiereRecargar);
                    if (disponible != null) SeleccionarResultadoMockup(disponible, null);
                    // Permitir reintentar el resultado fallido sin cerrar los que terminaron.
                    mockupListo = resultadosMockup.Count > 0;
                    MostrarPiezasMockup();
                    throw new InvalidOperationException("Proceso detenido en " + capa.Name + ". " +
                        nuevos.Count + " diseños aplicados en este lote.\n" +
                        (aplicado ? "El diseño se aplicó, pero falló la vista previa.\n" : "") +
                        ex.Message, ex);
                }
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

        private bool EsMockupExportable(ResultadoMockup resultado)
        {
            return !resultado.EsOriginal && !resultado.Editor.RequiereRecargar &&
                resultado.Editor.Campos.Any(x => x.Aplicado);
        }

        private void btnGuardarMockup_Click(object sender, EventArgs e)
        {
            var resultados = resultadosMockup.Where(EsMockupExportable).ToList();
            if (resultados.Count == 0)
            {
                MessageBox.Show(this, "No hay imágenes generadas para guardar.", "Guardar todo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dialogo = new FolderBrowserDialog())
            {
                dialogo.Description = "Seleccione la carpeta para guardar todas las imágenes JPEG.";
                if (dialogo.ShowDialog(this) != DialogResult.OK) return;
                string carpeta = dialogo.SelectedPath;
                EjecutarOperacionMockup(() =>
                {
                    var app = ObtenerCorel();
                    int guardados = 0;
                    foreach (ResultadoMockup resultado in resultados)
                    {
                        string temporal = Path.Combine(carpeta, Guid.NewGuid().ToString("N") + ".jpg");
                        try
                        {
                            if (!resultadosMockup.Contains(resultado) || !EsMockupExportable(resultado) || !resultado.Editor.EstaAbierto(app))
                                throw new InvalidOperationException("El mockup ya no está disponible.");
                            string nombre = EditorMockupCorel.Normalizar(resultado.Diseno);
                            if (string.IsNullOrWhiteSpace(nombre)) nombre = "mockup";
                            string destino = Path.Combine(carpeta, nombre + ".jpg");
                            int sufijo = 2;
                            while (File.Exists(destino))
                                destino = Path.Combine(carpeta, nombre + "_" + sufijo++ + ".jpg");
                            lblEstadoEditorMockup.Text = "Guardando " + (guardados + 1) + " de " + resultados.Count + ": " + resultado.Diseno;
                            lblEstadoEditorMockup.Refresh();
                            resultado.Editor.Exportar(temporal, 3000);
                            File.Move(temporal, destino);
                            guardados++;
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException("No se pudo guardar " + resultado.Diseno + ". " +
                                guardados + " de " + resultados.Count + " imágenes guardadas. Los resultados se conservan.\n" + ex.Message, ex);
                        }
                        finally { if (File.Exists(temporal)) File.Delete(temporal); }
                    }
                    lblEstadoEditorMockup.Text = guardados + " imágenes guardadas en " + carpeta;
                    MessageBox.Show(this, lblEstadoEditorMockup.Text, "Guardar todo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }
        }
    }
}
