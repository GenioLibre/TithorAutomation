using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using TithorAutomation.Servicios;
using TithorAutomation.Modelos;

namespace TithorAutomation
    {
    public partial class frmPrincipal
        {
        private VGCore.Document documentoEscalar;
        private string firmaEscalar;
        private readonly EscaladorPowerClip escalador = new EscaladorPowerClip();
        private bool modoGuiadoEscalar;
        private List<TareaEscalar> tareasEscalar = new List<TareaEscalar>();

        private sealed class TareaEscalar
            {
            public string Diseno { get; set; }
            public string Pieza { get; set; }
            public string Estado { get; set; }
            public List<PiezaEscalable> Destinos { get; set; }

            public TareaEscalar()
                {
                Diseno = string.Empty;
                Pieza = string.Empty;
                Estado = "Pendiente";
                Destinos = new List<PiezaEscalable>();
                }
            }

        private void ConfigurarModuloEscalar()
            {
            cboPiezaEscalar.Items.Clear();
            cboPiezaEscalar.Items.AddRange(new object[]
                {
                "Frente",
                "Lateral derecho",
                "Lateral izquierdo",
                "Espalda"
                });

            cboTallaEscalar.Items.Clear();
            cboTallaEscalar.Items.AddRange(new object[] { "Todas", "S", "M", "L" });
            cboPiezaEscalar.SelectedIndex = 0;
            cboTallaEscalar.SelectedIndex = 0;
            chkReemplazarContenidoEscalar.Checked = false;

            modoGuiadoEscalar = false;
            tareasEscalar.Clear();
            ConfigurarColumnasEscalar(false);

            lblEstadoEscalar.Text = "Analiza el documento para comenzar.";
            btnAnalizarEscalar.Enabled = true;
            btnAplicarEscalar.Enabled = false;
            btnAplicarEscalar.Text = "Aplicar diseño";
            btnEscalar.Enabled = true;

            btnEscalar.Click -= btnEscalar_Click;
            btnEscalar.Click += btnEscalar_Click;
            btnAnalizarEscalar.Click -= btnAnalizarEscalar_Click;
            btnAnalizarEscalar.Click += btnAnalizarEscalar_Click;
            btnAplicarEscalar.Click -= btnAplicarEscalar_Click;
            btnAplicarEscalar.Click += btnAplicarEscalar_Click;
            }

        private void btnEscalar_Click(object sender, EventArgs e)
            {
            MostrarPanel(pnlEscalar);
            }

        private VGCore.Document DocumentoActivoEscalar()
            {
            if (vistaPreviaActiva)
                {
                throw new InvalidOperationException("Restaura primero la vista previa de Pantonear.");
                }

            VGCore.Application corel = ObtenerCorel();

            if (corel.Documents.Count == 0)
                {
                throw new InvalidOperationException("Abre el documento con los moldes copiados en CorelDRAW.");
                }

            return corel.ActiveDocument;
            }

        private static string FirmaEscalar(IEnumerable<PiezaEscalable> piezas)
            {
            return string.Join("|", piezas
                .Select(x =>
                    (x.Diseno ?? "") + ":" +
                    (x.NombreGrupo ?? "") + ":" +
                    x.Clave + ":" +
                    x.Pieza + ":" +
                    x.Talla + ":" +
                    x.Estado + ":" +
                    x.TieneContenido + ":" +
                    (x.Contenedor == null
                        ? ""
                        : x.Contenedor.StaticID + ":" +
                          x.Contenedor.SizeWidth.ToString("R", CultureInfo.InvariantCulture) + ":" +
                          x.Contenedor.SizeHeight.ToString("R", CultureInfo.InvariantCulture) + ":" +
                          x.Contenedor.CenterX.ToString("R", CultureInfo.InvariantCulture) + ":" +
                          x.Contenedor.CenterY.ToString("R", CultureInfo.InvariantCulture)))
                .OrderBy(x => x));
            }

        private void MostrarAnalisisEscalar(List<PiezaEscalable> piezas)
            {
            dgvEscalar.Rows.Clear();

            if (modoGuiadoEscalar)
                {
                MostrarColaEscalar(piezas);
                firmaEscalar = FirmaEscalar(piezas);
                return;
                }

            foreach (var grupo in piezas
                .GroupBy(x => new { x.Pieza, x.Talla, x.Estado })
                .OrderBy(x => x.Key.Pieza)
                .ThenBy(x => x.Key.Talla))
                {
                dgvEscalar.Rows.Add(grupo.Key.Pieza, grupo.Key.Talla, grupo.Count(), grupo.Count(x => x.TieneContenido), grupo.Key.Estado);
                }

            lblEstadoEscalar.Text = piezas.Count == 0
                ? "No se encontraron piezas en TITHOR_PRODUCCION."
                : piezas.Count + " piezas detectadas en " + documentoEscalar.Name;

            firmaEscalar = FirmaEscalar(piezas);
            btnAplicarEscalar.Enabled = piezas.Any(x => string.Equals(x.Estado, "Listo", StringComparison.OrdinalIgnoreCase));
            }


        private void ConfigurarColumnasEscalar(bool guiado)
            {
            dgvEscalar.Columns.Clear();
            dgvEscalar.AutoGenerateColumns = false;
            dgvEscalar.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            if (guiado)
                {
                AgregarColumnaEscalar("colEstadoTareaEscalar", "Estado", 75F, 90);
                AgregarColumnaEscalar("colDisenoTareaEscalar", "Diseño", 120F, 120);
                AgregarColumnaEscalar("colPiezaTareaEscalar", "Pieza", 120F, 120);
                AgregarColumnaEscalar("colDestinosTareaEscalar", "Destinos", 65F, 75);
                }
            else
                {
                AgregarColumnaEscalar("colPiezaEscalarManual", "Pieza", 110F, 110);
                AgregarColumnaEscalar("colTallaEscalarManual", "Talla", 60F, 60);
                AgregarColumnaEscalar("colCantidadEscalarManual", "Cantidad", 70F, 70);
                AgregarColumnaEscalar("colContenidoEscalarManual", "Con contenido", 85F, 100);
                AgregarColumnaEscalar("colEstadoEscalarManual", "Estado", 100F, 100);
                }

            cboPiezaEscalar.Visible = !guiado;
            lblPiezaEscalar.Visible = !guiado;
            cboTallaEscalar.Visible = !guiado;
            lblTallaEscalar.Visible = !guiado;
            btnAplicarEscalar.Text = guiado ? "Aplicar y continuar" : "Aplicar diseño";
            }

        private void AgregarColumnaEscalar(string nombre, string titulo, float proporcion, int anchoMinimo)
            {
            DataGridViewTextBoxColumn columna = new DataGridViewTextBoxColumn();
            columna.Name = nombre;
            columna.HeaderText = titulo;
            columna.ReadOnly = true;
            columna.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            columna.FillWeight = proporcion;
            columna.MinimumWidth = anchoMinimo;
            columna.SortMode = DataGridViewColumnSortMode.NotSortable;
            dgvEscalar.Columns.Add(columna);
            }

        private void MostrarColaEscalar(List<PiezaEscalable> piezas)
            {
            tareasEscalar = piezas
                .GroupBy(x => new { Diseno = x.Diseno ?? string.Empty, x.Pieza })
                .Select(grupo => new TareaEscalar
                    {
                    Diseno = grupo.Key.Diseno,
                    Pieza = grupo.Key.Pieza,
                    Destinos = grupo.ToList(),
                    Estado = EstadoTareaEscalar(grupo.ToList())
                    })
                .ToList();

            foreach (TareaEscalar tarea in tareasEscalar)
                {
                int indice = dgvEscalar.Rows.Add(tarea.Estado, tarea.Diseno, tarea.Pieza, tarea.Destinos.Count);
                DataGridViewRow fila = dgvEscalar.Rows[indice];
                fila.Tag = tarea;

                if (tarea.Estado == "Completado")
                    fila.Cells[0].Style.ForeColor = System.Drawing.Color.FromArgb(30, 150, 70);
                else if (tarea.Estado == "Con error")
                    {
                    fila.DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(255, 235, 235);
                    fila.Cells[0].Style.ForeColor = System.Drawing.Color.FromArgb(200, 45, 45);
                    }
                }

            TareaEscalar actual = ObtenerTareaEscalarActual();

            if (piezas.Count == 0)
                lblEstadoEscalar.Text = "No se encontraron destinos del pedido. Vuelva a copiar los moldes.";
            else if (actual == null && tareasEscalar.Any(x => x.Estado == "Con error"))
                lblEstadoEscalar.Text = "Hay destinos con error. Revise los nombres y vuelva a copiar los moldes.";
            else if (actual == null)
                lblEstadoEscalar.Text = "Escalado del pedido completado.";
            else
                lblEstadoEscalar.Text = "Seleccione en CorelDRAW: " + actual.Diseno + " - " + actual.Pieza + ".";

            btnAplicarEscalar.Enabled = actual != null;
            }

        private string EstadoTareaEscalar(List<PiezaEscalable> destinos)
            {
            if (destinos.Any(x => !string.Equals(x.Estado, "Listo", StringComparison.OrdinalIgnoreCase)))
                return "Con error";

            if (destinos.All(x => x.TieneContenido))
                return "Completado";

            if (destinos.Any(x => x.TieneContenido))
                return "Parcial";

            return "Pendiente";
            }

        private TareaEscalar ObtenerTareaEscalarActual()
            {
            return tareasEscalar.FirstOrDefault(x => x.Estado == "Pendiente" || x.Estado == "Parcial");
            }

        private void AplicarTareaGuiada()
            {
            try
                {
                if (documentoEscalar == null || planProduccionActual == null)
                    throw new InvalidOperationException("Analice primero el pedido desde Escalar.");

                VGCore.Document documento = DocumentoActivoEscalar();

                if (!EscaladorPowerClip.MismoDocumento(documento, documentoEscalar))
                    throw new InvalidOperationException("El documento activo cambió. Vuelva a analizarlo.");

                List<PiezaEscalable> piezas = escalador.AnalizarPedido(documento, planProduccionActual);

                if (FirmaEscalar(piezas) != firmaEscalar)
                    {
                    MostrarAnalisisEscalar(piezas);
                    throw new InvalidOperationException("Los moldes cambiaron. Se actualizó la cola; revise y vuelva a aplicar.");
                    }

                TareaEscalar tarea = ObtenerTareaEscalarActual();

                if (tarea == null)
                    throw new InvalidOperationException("No quedan tareas pendientes.");

                VGCore.ShapeRange seleccion = documento.SelectionRange;

                if (seleccion.Count != 1 || seleccion[1].Type != VGCore.cdrShapeType.cdrGroupShape)
                    throw new InvalidOperationException("Seleccione un único diseño agrupado para " + tarea.Diseno + " - " + tarea.Pieza + ".");

                List<PiezaEscalable> destinos = chkReemplazarContenidoEscalar.Checked
                    ? tarea.Destinos
                    : tarea.Destinos.Where(x => !x.TieneContenido).ToList();

                if (destinos.Count == 0)
                    throw new InvalidOperationException("Todos los destinos de esta tarea ya tienen contenido.");

                btnAnalizarEscalar.Enabled = false;
                btnAplicarEscalar.Enabled = false;
                lblEstadoEscalar.Text = "Aplicando " + tarea.Diseno + " - " + tarea.Pieza + "...";

                bool temporizadorActivo = tmrConexionCorel.Enabled;
                tmrConexionCorel.Stop();

                int total;

                try
                    {
                    total = escalador.Aplicar(
                        documento,
                        seleccion[1],
                        destinos,
                        chkReemplazarContenidoEscalar.Checked,
                        delegate (int actual, int cantidad)
                            {
                            lblEstadoEscalar.Text =
                                "Aplicando " + tarea.Diseno + " - " + tarea.Pieza +
                                ": " + actual + " de " + cantidad + "...";
                            lblEstadoEscalar.Refresh();
                            });
                    }
                finally
                    {
                    if (temporizadorActivo)
                        tmrConexionCorel.Start();
                    }

                List<PiezaEscalable> resultado = escalador.AnalizarPedido(documento, planProduccionActual);
                MostrarAnalisisEscalar(resultado);

                TareaEscalar siguiente = ObtenerTareaEscalarActual();
                lblEstadoEscalar.Text = siguiente == null
                    ? "Escalado completado. Se aplicaron " + total + " destinos en el último paso."
                    : "Listo. Ahora seleccione: " + siguiente.Diseno + " - " + siguiente.Pieza + ".";
                }
            catch (Exception ex)
                {
                lblEstadoEscalar.Text = "Revise el mensaje y continúe con la tarea pendiente.";
                MessageBox.Show(this, ex.Message, "Escalar pedido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            finally
                {
                btnAnalizarEscalar.Enabled = true;
                btnAplicarEscalar.Enabled = ObtenerTareaEscalarActual() != null;
                }
            }

        private void btnAnalizarEscalar_Click(object sender, EventArgs e)
            {
            try
                {
                btnAnalizarEscalar.Enabled = false;
                btnAplicarEscalar.Enabled = false;
                firmaEscalar = null;
                documentoEscalar = null;
                tareasEscalar.Clear();
                dgvEscalar.Rows.Clear();
                lblEstadoEscalar.Text = "Analizando los moldes...";

                documentoEscalar = DocumentoActivoEscalar();
                modoGuiadoEscalar = planProduccionActual != null && planProduccionActual.Moldes != null && planProduccionActual.Moldes.Count > 0;
                ConfigurarColumnasEscalar(modoGuiadoEscalar);

                List<PiezaEscalable> piezas = modoGuiadoEscalar
                    ? escalador.AnalizarPedido(documentoEscalar, planProduccionActual)
                    : escalador.Analizar(documentoEscalar);

                MostrarAnalisisEscalar(piezas);
                }
            catch (Exception ex)
                {
                firmaEscalar = null;
                documentoEscalar = null;
                btnAplicarEscalar.Enabled = false;
                lblEstadoEscalar.Text = "No se pudo completar el análisis.";

                MessageBox.Show(this, ex.Message, "Analizar moldes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            finally
                {
                btnAnalizarEscalar.Enabled = true;
                }
            }

        private void btnAplicarEscalar_Click(object sender, EventArgs e)
            {
            if (modoGuiadoEscalar)
                {
                AplicarTareaGuiada();
                return;
                }

            try
                {
                VGCore.Document documento = DocumentoActivoEscalar();

                if (documentoEscalar == null ||
                    firmaEscalar == null ||
                    !EscaladorPowerClip.MismoDocumento(
                        documento,
                        documentoEscalar))
                    {
                    throw new InvalidOperationException(
                        "El documento activo cambió. Vuelve a analizarlo.");
                    }

                List<PiezaEscalable> piezas =
                    escalador.Analizar(documento);

                string firmaActual = FirmaEscalar(piezas);

                if (firmaActual != firmaEscalar)
                    {
                    MostrarAnalisisEscalar(piezas);

                    throw new InvalidOperationException(
                        "Los moldes cambiaron. Se actualizó la lista; revisa los destinos y vuelve a aplicar.");
                    }

                VGCore.ShapeRange seleccion = documento.SelectionRange;

                if (seleccion.Count != 1)
                    {
                    throw new InvalidOperationException(
                        "Selecciona un único diseño agrupado en CorelDRAW.");
                    }

                if (cboPiezaEscalar.SelectedItem == null)
                    {
                    throw new InvalidOperationException(
                        "Selecciona una pieza.");
                    }

                if (cboTallaEscalar.SelectedItem == null)
                    {
                    throw new InvalidOperationException(
                        "Selecciona una talla.");
                    }

                VGCore.Shape diseno = seleccion[1];

                string pieza =
                    cboPiezaEscalar.SelectedItem.ToString();

                string talla =
                    cboTallaEscalar.SelectedItem.ToString();

                List<PiezaEscalable> destinos = piezas
                    .Where(x =>
                        string.Equals(
                            x.Pieza,
                            pieza,
                            StringComparison.OrdinalIgnoreCase) &&
                        (talla == "Todas" ||
                         string.Equals(
                             x.Talla,
                             talla,
                             StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (destinos.Count == 0)
                    {
                    throw new InvalidOperationException(
                        "No se encontraron moldes para la pieza y talla seleccionadas.");
                    }

                escalador.Validar(
                    diseno,
                    destinos,
                    chkReemplazarContenidoEscalar.Checked);

                string aviso =
                    "Se aplicará el diseño a " +
                    destinos.Count +
                    " piezas de " +
                    pieza +
                    " (talla: " +
                    talla +
                    ").\n\n" +
                    "El diseño se escalará proporcionalmente hasta cubrir cada PowerClip, quedará centrado y el original se conservará.";

                int conContenido =
                    destinos.Count(x => x.TieneContenido);

                if (conContenido > 0)
                    {
                    if (chkReemplazarContenidoEscalar.Checked)
                        {
                        aviso +=
                            "\n\nSe reemplazará el contenido existente de " +
                            conContenido +
                            " PowerClips.";
                        }
                    else
                        {
                        aviso +=
                            "\n\n" +
                            conContenido +
                            " PowerClips ya tienen contenido y no serán reemplazados.";
                        }
                    }

                DialogResult confirmacion = MessageBox.Show(
                    this,
                    aviso,
                    "Aplicar diseño",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question);

                if (confirmacion != DialogResult.OK)
                    {
                    return;
                    }

                VGCore.Document documentoConfirmado =
                    DocumentoActivoEscalar();

                if (!EscaladorPowerClip.MismoDocumento(
                        documentoConfirmado,
                        documento) ||
                    FirmaEscalar(escalador.Analizar(documento)) !=
                    firmaEscalar)
                    {
                    throw new InvalidOperationException(
                        "El documento cambió durante la confirmación. Analízalo nuevamente.");
                    }

                btnAnalizarEscalar.Enabled = false;
                btnAplicarEscalar.Enabled = false;
                lblEstadoEscalar.Text = "Aplicando el diseño...";

                int total = escalador.Aplicar(
                    documento,
                    diseno,
                    destinos,
                    chkReemplazarContenidoEscalar.Checked);

                List<PiezaEscalable> resultado =
                    escalador.Analizar(documento);

                MostrarAnalisisEscalar(resultado);

                lblEstadoEscalar.Text =
                    "Diseño aplicado a " +
                    total +
                    " piezas. Ctrl+Z deshace la operación.";
                }
            catch (Exception ex)
                {
                firmaEscalar = null;

                btnAplicarEscalar.Enabled = false;
                lblEstadoEscalar.Text =
                    "Revisa el mensaje y vuelve a analizar.";

                MessageBox.Show(
                    this,
                    ex.Message,
                    "Escalar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                }
            finally
                {
                btnAnalizarEscalar.Enabled = true;
                }
            }
        }
    }