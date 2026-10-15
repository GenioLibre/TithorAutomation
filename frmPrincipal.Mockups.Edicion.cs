using System;
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
        private readonly EditorMockupCorel editorMockup = new EditorMockupCorel();
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
            chkLoteMockup.CheckedChanged += (s, e) => { cboCampoMockup.Enabled = !chkLoteMockup.Checked; };
            FormClosed += (s, e) => { editorMockup.Dispose(); if (picVistaMockup.Image != null) picVistaMockup.Image.Dispose(); };
            dgvPiezasMockup.ColumnHeadersDefaultCellStyle = dgvCatalogoMockups.ColumnHeadersDefaultCellStyle.Clone();
            dgvPiezasMockup.DefaultCellStyle = dgvCatalogoMockups.DefaultCellStyle.Clone();
            dgvPiezasMockup.AlternatingRowsDefaultCellStyle = dgvCatalogoMockups.AlternatingRowsDefaultCellStyle.Clone();
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
            cboMockup.Enabled = btnRecargarMockup.Enabled = btnAplicarMockup.Enabled = btnGuardarMockup.Enabled = false;
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
                btnAplicarMockup.Enabled = btnGuardarMockup.Enabled = mockupListo;
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
            if (editorMockup.Documento != null && MessageBox.Show(this, "¿Cerrar la copia de trabajo actual y cargar el original? Se descartarán sus cambios.", "Mockups", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            CargarMockupSeleccionado();
        }

        private void MostrarSeleccionMockup()
        {
            RevisarDocumentoMockup();
            var elegido = cboMockup.SelectedItem as Mockup;
            mockupListo = editorMockup.Documento != null && mockupCargado != null && elegido != null
                && elegido.Id == mockupCargado.Id && !editorMockup.RequiereRecargar;
            btnAplicarMockup.Enabled = btnGuardarMockup.Enabled = mockupListo;
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
            if (operandoMockup || editorMockup.Documento == null) return;
            // Solo consulta: nunca abre, activa ni impide cerrar un documento.
            bool abierto;
            try { abierto = editorMockup.EstaAbierto(corelApp); }
            catch { return; } // Corel puede estar ocupado o mostrando un diálogo.
            if (abierto) return;
            editorMockup.OlvidarDocumentoCerrado();
            mockupListo = false;
            mockupCargado = null;
            vistaMockupCargado = null;
            cboCampoMockup.DataSource = null;
            dgvPiezasMockup.Rows.Clear();
            btnAplicarMockup.Enabled = btnGuardarMockup.Enabled = false;
            lblEstadoEditorMockup.Text = "Mockup cerrado. Pulse Cargar mockup cuando quiera volver a abrirlo.";
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
                editorMockup.Cargar(ObtenerCorel(), archivoMockupMasterActual.RutaArchivo, elegido);
                cboCampoMockup.DataSource = editorMockup.Campos;
                MostrarPiezasMockup();
                if (!vistaGuardada)
                {
                    string nueva = editorMockup.CrearVistaPrevia();
                    GuardarJpegVistaMockup(nueva, rutaVista);
                    MostrarJpegMockup(rutaVista);
                }
                vistaMockupCargado = rutaVista;
                mockupCargado = elegido;
                mockupListo = true;
                lblEstadoEditorMockup.Text = "Seleccione la referencia en CorelDRAW y pulse Aplicar diseño. No se cambia el tamaño.";
            });
        }

        private void MostrarPiezasMockup()
        {
            dgvPiezasMockup.Rows.Clear();
            foreach (var campo in editorMockup.Campos)
                dgvPiezasMockup.Rows.Add(campo.Aplicado ? "Aplicado" : "Pendiente", campo.Nombre, campo.Referencia, campo.Invertido ? "180°" : "0°", campo.Destinos.Count);
        }

        private void ActualizarVistaMockup()
        {
            vistaMockupCargado = editorMockup.CrearVistaPrevia();
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
                var seleccion = origen.SelectionRange;
                if (seleccion.Count != 1) throw new InvalidOperationException(chkLoteMockup.Checked ? "Seleccione el grupo Diseño completo en CorelDRAW." : "Seleccione una única referencia en CorelDRAW.");
                try
                {
                    editorMockup.Aplicar(app, origen, seleccion[1], cboCampoMockup.SelectedItem as CampoMockup, chkLoteMockup.Checked);
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
                MostrarPiezasMockup();
                try { ActualizarVistaMockup(); }
                catch (Exception ex) { throw new InvalidOperationException("Diseño aplicado, pero no se pudo actualizar la vista previa: " + ex.Message, ex); }
                lblEstadoEditorMockup.Text = "Diseño aplicado. Vista previa actualizada.";
            });
        }

        private void btnGuardarMockup_Click(object sender, EventArgs e)
        {
            if (!mockupListo) return;
            using (var dialogo = new SaveFileDialog())
            {
                dialogo.Title = "Guardar mockup como JPEG";
                dialogo.Filter = "Imagen JPEG (*.jpg)|*.jpg";
                dialogo.DefaultExt = "jpg";
                dialogo.AddExtension = true;
                dialogo.OverwritePrompt = true;
                dialogo.FileName = (cboMockup.SelectedItem as Mockup)?.Codigo + ".jpg";
                if (dialogo.ShowDialog(this) != DialogResult.OK) return;
                EjecutarOperacionMockup(() =>
                {
                    string extension = Path.GetExtension(dialogo.FileName);
                    if (!string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Use una extensión .jpg o .jpeg para guardar el mockup.");
                    // Exportar a un archivo intermedio antes de reemplazar el destino elegido.
                    string temporal = Path.Combine(Path.GetDirectoryName(dialogo.FileName), Guid.NewGuid().ToString("N") + ".jpg");
                    try
                    {
                        editorMockup.Exportar(temporal, 3000);
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
