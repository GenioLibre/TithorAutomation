using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TithorAutomation.Datos;
using TithorAutomation.Modelos;
using TithorAutomation.Servicios;

namespace TithorAutomation
    {
    public partial class frmPrincipal
        {
        private readonly AnalizadorMockupCorel analizadorMockups = new AnalizadorMockupCorel();
        private readonly MockupRepositorio mockupRepositorio = new MockupRepositorio();
        private ArchivoMockupMaster archivoMockupMasterActual;
        private List<Mockup> mockupsAnalizados = new List<Mockup>();

        private void ConfigurarModuloMockups()
            {
            dgvCatalogoMockups.AllowUserToAddRows = false;
            dgvCatalogoMockups.AllowUserToDeleteRows = false;
            dgvCatalogoMockups.ReadOnly = true;
            dgvCatalogoMockups.RowHeadersVisible = false;
            dgvCatalogoMockups.MultiSelect = false;
            dgvCatalogoMockups.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvCatalogoMockups.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            colEstadoMockup.FillWeight = 22F;
            colCodigoMockup.FillWeight = 36F;
            colProductoMockup.FillWeight = 32F;
            colNombreGrupoMockup.FillWeight = 55F;

            btnSeleccionarMasterMockups.Click -= btnSeleccionarMasterMockups_Click;
            btnSeleccionarMasterMockups.Click += btnSeleccionarMasterMockups_Click;
            btnAbrirUbicacionMasterMockups.Click -= btnAbrirUbicacionMasterMockups_Click;
            btnAbrirUbicacionMasterMockups.Click += btnAbrirUbicacionMasterMockups_Click;
            btnAnalizarMasterMockups.Click -= btnAnalizarMasterMockups_Click;
            btnAnalizarMasterMockups.Click += btnAnalizarMasterMockups_Click;
            btnSincronizarMockups.Click -= btnSincronizarMockups_Click;
            btnSincronizarMockups.Click += btnSincronizarMockups_Click;
            btnMockup.Click -= btnMockup_Click;
            btnMockup.Click += btnMockup_Click;

            CargarConfiguracionMockups();
            }

        private void CargarConfiguracionMockups()
            {
            archivoMockupMasterActual = mockupRepositorio.ObtenerMaster();
            mockupsAnalizados.Clear();
            dgvCatalogoMockups.Rows.Clear();

            if (archivoMockupMasterActual == null)
                {
                txtRutaMasterMockups.Text = string.Empty;
                lblCantidadMockups.Text = "0";
                lblCantidadErroresMockups.Text = "0";
                lblEstadoMockups.Text = "Seleccione un archivo Master de mockups.";
                btnAbrirUbicacionMasterMockups.Enabled = false;
                btnAnalizarMasterMockups.Enabled = false;
                btnSincronizarMockups.Enabled = false;
                return;
                }

            txtRutaMasterMockups.Text = archivoMockupMasterActual.RutaArchivo ?? string.Empty;
            List<Mockup> guardados = mockupRepositorio.ObtenerMockups(archivoMockupMasterActual.Id);
            MostrarMockups(guardados);
            lblEstadoMockups.Text = guardados.Count == 0
                ? "Master registrado. Falta analizar y sincronizar."
                : "Catálogo cargado: " + guardados.Count + " mockups registrados.";
            btnAbrirUbicacionMasterMockups.Enabled = File.Exists(archivoMockupMasterActual.RutaArchivo);
            btnAnalizarMasterMockups.Enabled = File.Exists(archivoMockupMasterActual.RutaArchivo);
            btnSincronizarMockups.Enabled = false;
            }

        private void btnSeleccionarMasterMockups_Click(object sender, EventArgs e)
            {
            using (OpenFileDialog dialogo = new OpenFileDialog())
                {
                dialogo.Title = "Seleccionar Master de mockups";
                dialogo.Filter = "Archivos de CorelDRAW (*.cdr)|*.cdr";
                dialogo.CheckFileExists = true;
                dialogo.Multiselect = false;

                if (dialogo.ShowDialog(this) != DialogResult.OK)
                    return;

                FileInfo informacion = new FileInfo(dialogo.FileName);
                archivoMockupMasterActual = mockupRepositorio.GuardarMaster(new ArchivoMockupMaster
                    {
                    RutaArchivo = informacion.FullName,
                    NombreArchivo = informacion.Name,
                    HashArchivo = CalcularHashArchivo(informacion.FullName),
                    TamanoArchivo = informacion.Length,
                    FechaModificacionArchivo = informacion.LastWriteTimeUtc
                    });

                mockupsAnalizados.Clear();
                dgvCatalogoMockups.Rows.Clear();
                txtRutaMasterMockups.Text = informacion.FullName;
                lblCantidadMockups.Text = "0";
                lblCantidadErroresMockups.Text = "0";
                lblEstadoMockups.Text = "Archivo seleccionado. Pulse Analizar Master.";
                btnAbrirUbicacionMasterMockups.Enabled = true;
                btnAnalizarMasterMockups.Enabled = true;
                btnSincronizarMockups.Enabled = false;
                }
            }

        private void btnAbrirUbicacionMasterMockups_Click(object sender, EventArgs e)
            {
            if (archivoMockupMasterActual == null || !File.Exists(archivoMockupMasterActual.RutaArchivo))
                {
                MessageBox.Show(this, "No se encontró el archivo Master de mockups.", "Mockups", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
                }

            Process.Start("explorer.exe", "/select,\"" + archivoMockupMasterActual.RutaArchivo + "\"");
            }

        private void btnAnalizarMasterMockups_Click(object sender, EventArgs e)
            {
            try
                {
                if (archivoMockupMasterActual == null)
                    throw new InvalidOperationException("Seleccione primero el archivo Master de mockups.");

                btnAnalizarMasterMockups.Enabled = false;
                btnSincronizarMockups.Enabled = false;
                lblEstadoMockups.Text = "Analizando capas y grupos Mockup_*...";
                lblEstadoMockups.Refresh();

                List<Mockup> guardados = mockupRepositorio.ObtenerMockups(archivoMockupMasterActual.Id);
                mockupsAnalizados = analizadorMockups.Analizar(ObtenerCorel(), archivoMockupMasterActual.RutaArchivo);

                foreach (Mockup mockup in mockupsAnalizados)
                    {
                    if (mockup.Estado == "Nuevo" && guardados.Any(x => string.Equals(x.Codigo, mockup.Codigo, StringComparison.OrdinalIgnoreCase)))
                        mockup.Estado = "Correcto";
                    }

                MostrarMockups(mockupsAnalizados);
                int errores = mockupsAnalizados.Count(x => !x.EsValido);
                lblEstadoMockups.Text = errores == 0
                    ? "Análisis terminado. " + mockupsAnalizados.Count + " mockups disponibles."
                    : "Análisis terminado con " + errores + " errores. Revise el catálogo.";
                btnSincronizarMockups.Enabled = mockupsAnalizados.Count > 0 && errores == 0;
                }
            catch (Exception ex)
                {
                mockupsAnalizados.Clear();
                MostrarMockups(mockupsAnalizados);
                lblEstadoMockups.Text = "No se pudo analizar el Master de mockups.";
                MessageBox.Show(this, ex.Message, "Analizar mockups", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            finally
                {
                btnAnalizarMasterMockups.Enabled = archivoMockupMasterActual != null;
                }
            }

        private void MostrarMockups(IList<Mockup> mockups)
            {
            dgvCatalogoMockups.Rows.Clear();

            foreach (Mockup mockup in mockups)
                {
                int indice = dgvCatalogoMockups.Rows.Add(mockup.Estado, mockup.Codigo, mockup.Producto, mockup.NombreGrupo);
                DataGridViewRow fila = dgvCatalogoMockups.Rows[indice];
                fila.Tag = mockup;

                if (!mockup.EsValido)
                    {
                    fila.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 235);
                    fila.Cells[0].Style.ForeColor = Color.FromArgb(200, 45, 45);
                    fila.Cells[0].ToolTipText = mockup.Observacion ?? string.Empty;
                    }
                else if (mockup.Estado == "Nuevo")
                    fila.Cells[0].Style.ForeColor = Color.FromArgb(0, 90, 210);
                else
                    fila.Cells[0].Style.ForeColor = Color.FromArgb(30, 150, 70);
                }

            lblCantidadMockups.Text = mockups.Count.ToString();
            lblCantidadErroresMockups.Text = mockups.Count(x => !x.EsValido).ToString();
            }

        private void btnSincronizarMockups_Click(object sender, EventArgs e)
            {
            try
                {
                if (archivoMockupMasterActual == null || mockupsAnalizados.Count == 0)
                    throw new InvalidOperationException("Analice primero el Master de mockups.");

                Mockup conError = mockupsAnalizados.FirstOrDefault(x => !x.EsValido);

                if (conError != null)
                    throw new InvalidOperationException(conError.NombreGrupo + ": " + conError.Observacion);

                mockupRepositorio.Sincronizar(archivoMockupMasterActual, mockupsAnalizados);

                foreach (Mockup mockup in mockupsAnalizados)
                    mockup.Estado = "Correcto";

                MostrarMockups(mockupsAnalizados);
                btnSincronizarMockups.Enabled = false;
                lblEstadoMockups.Text = "Master sincronizado correctamente. " + mockupsAnalizados.Count + " mockups registrados.";
                }
            catch (Exception ex)
                {
                MessageBox.Show(this, ex.Message, "Sincronizar mockups", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

        private void btnMockup_Click(object sender, EventArgs e)
            {
            tabConfiguracion.SelectedTab = tabMockups;
            MostrarPanel(pnlConfiguracion);
            }
        }
    }
