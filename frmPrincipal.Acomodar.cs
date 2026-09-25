using System;
using System.Drawing;
using System.Windows.Forms;
using TithorAutomation.Modelos;
using TithorAutomation.Servicios;

namespace TithorAutomation
    {
    public partial class frmPrincipal
        {
        private readonly AcomodadorCorel acomodadorCorel = new AcomodadorCorel();
        private ResultadoAcomodo resultadoAcomodoActual;
        private VGCore.Document documentoAcomodoActual;

        private void ConfigurarModuloAcomodar()
            {
            nudAnchoMaterial.Minimum = 100;
            nudAnchoMaterial.Maximum = 10000;
            nudAnchoMaterial.Increment = 50;
            nudAnchoMaterial.DecimalPlaces = 0;
            nudAnchoMaterial.Value = 1600;

            nudSeparacionElementos.Minimum = 0;
            nudSeparacionElementos.Maximum = 500;
            nudSeparacionElementos.Increment = 0.5M;
            nudSeparacionElementos.DecimalPlaces = 1;
            nudSeparacionElementos.Value = 5;

            chkPermitirRotacion.Checked = false;

            dgvAcomodo.Rows.Clear();

            lblCantidadPiezasAcomodar.Text = "0";
            lblAltoResultado.Text = "—";
            lblNoProcesablesAcomodar.Text = "0";
            lblEstadoAcomodar.Text = "Listo para analizar";

            prgAcomodar.Minimum = 0;
            prgAcomodar.Maximum = 100;
            prgAcomodar.Value = 0;

            btnAnalizarAcomodo.Enabled = true;
            btnAcomodarElementos.Enabled = false;

            nudAnchoMaterial.ValueChanged -= ConfiguracionAcomodo_Cambio;
            nudAnchoMaterial.ValueChanged += ConfiguracionAcomodo_Cambio;

            nudSeparacionElementos.ValueChanged -= ConfiguracionAcomodo_Cambio;
            nudSeparacionElementos.ValueChanged += ConfiguracionAcomodo_Cambio;

            chkPermitirRotacion.CheckedChanged -= ConfiguracionAcomodo_Cambio;
            chkPermitirRotacion.CheckedChanged += ConfiguracionAcomodo_Cambio;

            btnAnalizarAcomodo.Click -= btnAnalizarAcomodo_Click;
            btnAnalizarAcomodo.Click += btnAnalizarAcomodo_Click;

            btnAcomodarElementos.Click -= btnAcomodarElementos_Click;
            btnAcomodarElementos.Click += btnAcomodarElementos_Click;
            }

        private void ConfiguracionAcomodo_Cambio(object sender, EventArgs e)
            {
            InvalidarAnalisisAcomodo(
                "La configuración cambió. Analice nuevamente.");
            }

        private void InvalidarAnalisisAcomodo(string mensaje)
            {
            prgAcomodar.Style = ProgressBarStyle.Continuous;
            prgAcomodar.MarqueeAnimationSpeed = 0;
            resultadoAcomodoActual = null;
            documentoAcomodoActual = null;

            dgvAcomodo.Rows.Clear();

            lblCantidadPiezasAcomodar.Text = "0";
            lblAltoResultado.Text = "—";
            lblNoProcesablesAcomodar.Text = "0";
            lblEstadoAcomodar.Text = mensaje;

            prgAcomodar.Value = 0;

            btnAnalizarAcomodo.Enabled = true;
            btnAcomodarElementos.Enabled = false;
            }

        private VGCore.Document ObtenerDocumentoAcomodo()
            {
            if (vistaPreviaActiva)
                {
                throw new InvalidOperationException(
                    "Restaura primero la vista previa de Pantonear.");
                }

            VGCore.Application corel = ObtenerCorel();

            if (corel.Documents.Count == 0)
                {
                throw new InvalidOperationException(
                    "Abre el documento con los moldes de producción.");
                }

            return corel.ActiveDocument;
            }

        private void btnAnalizarAcomodo_Click(object sender, EventArgs e)
            {
            try
                {
                btnAnalizarAcomodo.Enabled = false;
                btnAcomodarElementos.Enabled = false;

                dgvAcomodo.Rows.Clear();
                MostrarProgresoOperacion(prgAcomodar, 0, 0);
                lblEstadoAcomodar.Text = "Analizando TITHOR_PRODUCCION...";

                lblEstadoAcomodar.Refresh();

                VGCore.Application corel = ObtenerCorel();
                VGCore.Document documento = ObtenerDocumentoAcomodo();

                double anchoMaterial =
                    Convert.ToDouble(nudAnchoMaterial.Value);

                double separacion =
                    Convert.ToDouble(nudSeparacionElementos.Value);

                bool permitirRotacion =
                    chkPermitirRotacion.Checked;

                ResultadoAcomodo resultado =
                    acomodadorCorel.Analizar(
                        corel,
                        documento,
                        anchoMaterial,
                        separacion,
                        permitirRotacion);

                // El cálculo no tiene un total fijo: se mantiene indeterminado hasta finalizar.

                MostrarResultadoAcomodo(resultado);

                resultadoAcomodoActual = resultado;
                documentoAcomodoActual = documento;

                MostrarProgresoOperacion(prgAcomodar, 1, 1);

                if (resultado.TotalPiezas == 0)
                    {
                    lblEstadoAcomodar.Text =
                        "No se encontraron objetos para acomodar.";

                    btnAcomodarElementos.Enabled = false;
                    }
                else if (resultado.TotalNoProcesables > 0)
                    {
                    lblEstadoAcomodar.Text =
                        "Análisis terminado: " +
                        resultado.TotalNoProcesables +
                        " piezas no caben en el ancho configurado.";

                    btnAcomodarElementos.Enabled = false;
                    }
                else
                    {
                    lblEstadoAcomodar.Text =
                    "Análisis terminado: " +
                    resultado.TotalProcesables +
                    " piezas listas para acomodar.";

                    btnAcomodarElementos.Enabled = true;
                    }
                }
            catch (Exception ex)
                {
                InvalidarAnalisisAcomodo(
                    "No se pudo completar el análisis.");

                MessageBox.Show(
                    this,
                    ex.Message,
                    "Analizar acomodo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                }
            finally
                {
                btnAnalizarAcomodo.Enabled = true;
                }
            }

        private void MostrarResultadoAcomodo(ResultadoAcomodo resultado)
            {
            dgvAcomodo.Rows.Clear();

            foreach (PiezaAcomodable pieza in resultado.Piezas)
                {
                int indice = dgvAcomodo.Rows.Add(
                    pieza.Estado,
                    pieza.Pieza,
                    pieza.Talla,
                    pieza.AnchoOriginal.ToString("0.##") + " mm",
                    pieza.AltoOriginal.ToString("0.##") + " mm",
                    pieza.Rotada ? "90°" : "No",
                    string.IsNullOrWhiteSpace(pieza.Grupo)
                        ? "-"
                        : pieza.Grupo);

                DataGridViewRow fila =
                    dgvAcomodo.Rows[indice];

                fila.Tag = pieza;

                if (!pieza.Procesable)
                    {
                    fila.DefaultCellStyle.BackColor =
                        Color.FromArgb(255, 235, 235);

                    fila.DefaultCellStyle.ForeColor =
                        Color.FromArgb(190, 45, 45);
                    }
                }

            lblCantidadPiezasAcomodar.Text =
                resultado.TotalPiezas.ToString();

            lblNoProcesablesAcomodar.Text =
                resultado.TotalNoProcesables.ToString();

            lblAltoResultado.Text =
            resultado.AltoEstimado.ToString("0.##") +
            " mm";
            }

        private void btnAcomodarElementos_Click(object sender, EventArgs e)
            {
            if (resultadoAcomodoActual == null || resultadoAcomodoActual.Piezas.Count == 0)
                {
                MessageBox.Show("Primero debes analizar los elementos.", "Acomodar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
                }

            if (resultadoAcomodoActual.TotalNoProcesables > 0)
                {
                MessageBox.Show("Existen piezas que no caben dentro del ancho configurado.\n\nAumenta el ancho del material o permite la rotación.", "Acomodar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
                }

            VGCore.Application corel = ObtenerCorel();

            if (corel == null)
                {
                MessageBox.Show("No se pudo conectar con CorelDRAW.", "Acomodar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
                }

            VGCore.Document documentoActual = ObtenerDocumentoAcomodo();

            if (documentoActual == null)
                {
                MessageBox.Show("No existe un documento abierto en CorelDRAW.", "Acomodar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
                }

            if (documentoActual == null)
                {
                MessageBox.Show("No existe un documento abierto en CorelDRAW.", "Acomodar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
                }

            if (!EsMismoDocumentoAcomodo(documentoActual, documentoAcomodoActual))
                {
                MessageBox.Show("El documento activo cambió después del análisis.\n\nVuelve a analizar los elementos antes de acomodarlos.", "Acomodar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                InvalidarAnalisisAcomodo("El documento activo cambió. Vuelve a analizar los elementos.");
                return;
                }

            DialogResult confirmacion = MessageBox.Show(
                "Se volverá a analizar la página activa y se acomodarán sus piezas de TITHOR_PRODUCCION.\n\nLa operación podrá deshacerse con un solo Ctrl + Z.\n\n¿Deseas continuar?",
                "Confirmar acomodo",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmacion != DialogResult.Yes)
                {
                return;
                }

            try
                {
                btnAnalizarAcomodo.Enabled = false;
                btnAcomodarElementos.Enabled = false;
                nudAnchoMaterial.Enabled = false;
                nudSeparacionElementos.Enabled = false;
                chkPermitirRotacion.Enabled = false;

                MostrarProgresoOperacion(prgAcomodar, 0, 0);
                lblEstadoAcomodar.Text = "Acomodando elementos en CorelDRAW...";

                lblEstadoAcomodar.Refresh();

                resultadoAcomodoActual = acomodadorCorel.AcomodarPaginaActiva(
                    corel, documentoActual,
                    Convert.ToDouble(nudAnchoMaterial.Value),
                    Convert.ToDouble(nudSeparacionElementos.Value),
                    chkPermitirRotacion.Checked,
                    delegate (int actual, int total)
                        {
                        MostrarProgresoOperacion(prgAcomodar, actual, total);
                        lblEstadoAcomodar.Text = "Acomodando pieza " + actual + " de " + total + "...";
                        lblEstadoAcomodar.Refresh();
                        });

                MostrarResultadoAcomodo(resultadoAcomodoActual);

                MarcarFilasComoAcomodadas();

                prgAcomodar.Style = ProgressBarStyle.Blocks;
                MostrarProgresoOperacion(prgAcomodar, 1, 1);

                lblAltoResultado.Text = resultadoAcomodoActual.AltoEstimado.ToString("0.0") + " mm";
                lblEstadoAcomodar.Text = "Acomodo terminado correctamente.";
                lblEstadoAcomodar.ForeColor = System.Drawing.Color.SeaGreen;

                resultadoAcomodoActual = null;
                documentoAcomodoActual = null;

                MessageBox.Show("Las piezas fueron acomodadas correctamente.\n\nPuedes deshacer toda la operación con un solo Ctrl + Z.", "Acomodar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            catch (Exception ex)
                {
                prgAcomodar.Style = ProgressBarStyle.Blocks;
                prgAcomodar.Value = 0;

                lblEstadoAcomodar.Text = "No se pudo completar el acomodo.";
                lblEstadoAcomodar.ForeColor = System.Drawing.Color.Firebrick;

                MessageBox.Show("No se pudieron acomodar los elementos.\n\n" + ex.Message, "Error de acomodo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            finally
                {
                btnAnalizarAcomodo.Enabled = true;
                btnAcomodarElementos.Enabled = false;
                nudAnchoMaterial.Enabled = true;
                nudSeparacionElementos.Enabled = true;
                chkPermitirRotacion.Enabled = true;
                }
            }

        private bool EsMismoDocumentoAcomodo(VGCore.Document documentoActual, VGCore.Document documentoAnalizado)
            {
            if (documentoActual == null || documentoAnalizado == null)
                {
                return false;
                }

            if (ReferenceEquals(documentoActual, documentoAnalizado))
                {
                return true;
                }

            try
                {
                string rutaActual = documentoActual.FullFileName;
                string rutaAnalizada = documentoAnalizado.FullFileName;

                if (!string.IsNullOrWhiteSpace(rutaActual) && !string.IsNullOrWhiteSpace(rutaAnalizada))
                    {
                    return string.Equals(rutaActual, rutaAnalizada, StringComparison.OrdinalIgnoreCase);
                    }

                return string.Equals(documentoActual.Name, documentoAnalizado.Name, StringComparison.OrdinalIgnoreCase);
                }
            catch
                {
                return false;
                }
            }

        private void MarcarFilasComoAcomodadas()
            {
            foreach (DataGridViewRow fila in dgvAcomodo.Rows)
                {
                if (!fila.IsNewRow)
                    {
                    fila.Cells[colEstadoAcomodo.Index].Value = "Acomodado";
                    }
                }
            }


        }
    }
