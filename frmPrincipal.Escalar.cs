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
        private List<TareaLoteEscalar> tareasLoteEscalar = new List<TareaLoteEscalar>();

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

            public override string ToString()
                {
                return "[" + Estado + "] " + Diseno + " - " + Pieza + " (" + Destinos.Count + ")";
                }
            }

        private sealed class TareaLoteEscalar
            {
            public string Diseno { get; set; }
            public string TipoPedido { get; set; }
            public string Estado { get; set; }
            public List<TareaEscalar> Tareas { get; set; }

            public TareaLoteEscalar()
                {
                Diseno = string.Empty;
                TipoPedido = "camiseta";
                Estado = "Pendiente";
                Tareas = new List<TareaEscalar>();
                }

            public override string ToString()
                {
                return "[" + Estado + "] Seleccionar diseño " + Diseno + " - " + TipoPedido;
                }
            }

        private void MostrarProgresoOperacion(System.Windows.Forms.ProgressBar barra, int actual, int total)
            {
            barra.Style = total <= 0 ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            barra.MarqueeAnimationSpeed = total <= 0 ? 30 : 0;
            barra.Value = total <= 0 ? 0 : (int)Math.Max(0, Math.Min(100, (long)actual * 100 / total));
            barra.Refresh();
            }

        private void ConfigurarModuloEscalar()
            {
            if (System.ComponentModel.LicenseManager.UsageMode != System.ComponentModel.LicenseUsageMode.Designtime)
                {
                pnlEscalar.Dock = DockStyle.Fill;
                pnlEscalar.Location = System.Drawing.Point.Empty;
                }

            chkReemplazarContenidoEscalar.Checked = false;
            chkReemplazoPorLote.Checked = false;
            chkReemplazoPorLote.CheckedChanged -= chkReemplazoPorLote_CheckedChanged;
            chkReemplazoPorLote.CheckedChanged += chkReemplazoPorLote_CheckedChanged;

            cboTareaEscalar.SelectedIndexChanged -= cboTareaEscalar_SelectedIndexChanged;
            cboTareaEscalar.SelectedIndexChanged += cboTareaEscalar_SelectedIndexChanged;

            modoGuiadoEscalar = false;
            tareasEscalar.Clear();
            tareasLoteEscalar.Clear();
            ConfigurarColumnasEscalar(true);

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

            if (planProduccionActual == null || documentoEscalar != null)
                return;

            try
                {
                VGCore.Application corel = ObtenerCorel();

                if (corel != null && corel.Documents.Count > 0)
                    btnAnalizarEscalar_Click(null, EventArgs.Empty);
                }
            catch
                {
                lblEstadoEscalar.Text = "Abra el documento del pedido y presione Analizar documento.";
                }
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

            cboTareaEscalar.Visible = true;
            lblPiezaEscalar.Visible = true;
            lblPiezaEscalar.Text = "Tarea:";
            btnAplicarEscalar.Text = "Aplicar diseño";
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

            tareasLoteEscalar = tareasEscalar
                .GroupBy(x => x.Diseno ?? string.Empty)
                .Select(grupo => CrearTareaLoteEscalar(grupo.Key, grupo.ToList()))
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

            ConfigurarComboTareasEscalar();
            ActualizarEstadoSeleccionEscalar(piezas.Count);
            }

        private TareaLoteEscalar CrearTareaLoteEscalar(string diseno, List<TareaEscalar> tareas)
            {
            bool incluyeShort = tareas.Any(x => AnalizadorMasterCorel.NormalizarCodigo(x.Pieza).Contains("short"));
            bool esFunda = planProduccionActual != null &&
                (planProduccionActual.CodigoProducto ?? string.Empty).IndexOf("FUNDA", StringComparison.OrdinalIgnoreCase) >= 0;
            TareaLoteEscalar lote = new TareaLoteEscalar();
            lote.Diseno = string.IsNullOrWhiteSpace(diseno) ? "sin nombre" : diseno;
            lote.TipoPedido = esFunda ? "funda" : (incluyeShort ? "camiseta y short" : "camiseta");
            lote.Tareas = tareas;

            if (tareas.Any(x => x.Estado == "Con error"))
                lote.Estado = "Con error";
            else if (tareas.All(x => x.Estado == "Completado"))
                lote.Estado = "Completado";
            else if (tareas.Any(x => x.Estado == "Completado" || x.Estado == "Parcial"))
                lote.Estado = "Parcial";
            else
                lote.Estado = "Pendiente";

            return lote;
            }

        private void ConfigurarComboTareasEscalar()
            {
            cboTareaEscalar.DataSource = null;

            if (chkReemplazoPorLote.Checked)
                {
                cboTareaEscalar.DataSource = tareasLoteEscalar;
                TareaLoteEscalar pendiente = tareasLoteEscalar.FirstOrDefault(x => x.Estado == "Pendiente" || x.Estado == "Parcial");
                if (pendiente != null) cboTareaEscalar.SelectedItem = pendiente;
                else if (tareasLoteEscalar.Count > 0) cboTareaEscalar.SelectedIndex = 0;
                btnAplicarEscalar.Text = "Aplicar lote";
                }
            else
                {
                cboTareaEscalar.DataSource = tareasEscalar;
                TareaEscalar pendiente = tareasEscalar.FirstOrDefault(x => x.Estado == "Pendiente" || x.Estado == "Parcial");
                if (pendiente != null) cboTareaEscalar.SelectedItem = pendiente;
                else if (tareasEscalar.Count > 0) cboTareaEscalar.SelectedIndex = 0;
                btnAplicarEscalar.Text = "Aplicar diseño";
                }
            }

        private void ActualizarEstadoSeleccionEscalar(int cantidadPiezas)
            {
            if (cantidadPiezas == 0)
                {
                lblEstadoEscalar.Text = "No se encontraron destinos del pedido. Vuelva a copiar los moldes.";
                btnAplicarEscalar.Enabled = false;
                return;
                }

            if (chkReemplazoPorLote.Checked)
                {
                TareaLoteEscalar lote = ObtenerTareaLoteEscalarActual();
                bool hayPendientes = tareasLoteEscalar.Any(x => x.Estado == "Pendiente" || x.Estado == "Parcial");
                if (!hayPendientes && tareasLoteEscalar.Any(x => x.Estado == "Con error"))
                    lblEstadoEscalar.Text = "Hay diseños con error. Revise los nombres de las piezas.";
                else if (!hayPendientes)
                    lblEstadoEscalar.Text = "Escalado por lote completado. Puede elegir un diseño para corregirlo.";
                else if (lote != null)
                    lblEstadoEscalar.Text = "Seleccione en CorelDRAW el grupo completo de " + lote.Diseno + ".";
                btnAplicarEscalar.Enabled = lote != null && lote.Estado != "Con error";
                return;
                }

            TareaEscalar actual = ObtenerTareaEscalarActual();
            bool pendientes = tareasEscalar.Any(x => x.Estado == "Pendiente" || x.Estado == "Parcial");
            if (!pendientes && tareasEscalar.Any(x => x.Estado == "Con error"))
                lblEstadoEscalar.Text = "Hay destinos con error. Revise los nombres y vuelva a copiar los moldes.";
            else if (!pendientes)
                lblEstadoEscalar.Text = "Escalado completado. Puede elegir una tarea para corregirla.";
            else if (actual != null)
                lblEstadoEscalar.Text = "Seleccione en CorelDRAW: " + actual.Diseno + " - " + actual.Pieza + ".";
            btnAplicarEscalar.Enabled = actual != null && actual.Estado != "Con error";
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
            if (cboTareaEscalar != null && cboTareaEscalar.SelectedItem is TareaEscalar)
                return (TareaEscalar)cboTareaEscalar.SelectedItem;

            return tareasEscalar.FirstOrDefault(x => x.Estado == "Pendiente" || x.Estado == "Parcial");
            }

        private TareaLoteEscalar ObtenerTareaLoteEscalarActual()
            {
            if (cboTareaEscalar != null && cboTareaEscalar.SelectedItem is TareaLoteEscalar)
                return (TareaLoteEscalar)cboTareaEscalar.SelectedItem;

            return tareasLoteEscalar.FirstOrDefault(x => x.Estado == "Pendiente" || x.Estado == "Parcial");
            }

        private void chkReemplazoPorLote_CheckedChanged(object sender, EventArgs e)
            {
            if (!modoGuiadoEscalar) return;
            ConfigurarComboTareasEscalar();
            ActualizarEstadoSeleccionEscalar(tareasEscalar.SelectMany(x => x.Destinos).Count());
            }

        private void cboTareaEscalar_SelectedIndexChanged(object sender, EventArgs e)
            {
            if (!modoGuiadoEscalar) return;

            if (chkReemplazoPorLote.Checked)
                {
                TareaLoteEscalar lote = ObtenerTareaLoteEscalarActual();
                if (lote == null)
                    {
                    btnAplicarEscalar.Enabled = false;
                    return;
                    }

                lblEstadoEscalar.Text = lote.Estado == "Completado"
                    ? "Lote completado. Active Reemplazar contenido para corregir " + lote.Diseno + "."
                    : "Seleccione en CorelDRAW el grupo completo de " + lote.Diseno + ".";
                btnAplicarEscalar.Enabled = lote.Estado != "Con error";
                return;
                }

            TareaEscalar tarea = ObtenerTareaEscalarActual();

            if (tarea == null)
                {
                btnAplicarEscalar.Enabled = false;
                return;
                }

            lblEstadoEscalar.Text = tarea.Estado == "Completado"
                ? "Tarea completada. Active Reemplazar contenido para corregir: " + tarea.Diseno + " - " + tarea.Pieza + "."
                : "Seleccione en CorelDRAW: " + tarea.Diseno + " - " + tarea.Pieza + ".";
            btnAplicarEscalar.Enabled = tarea.Estado != "Con error";
            }

        private void BuscarReferenciasLote(VGCore.Shape objeto, Dictionary<string, List<VGCore.Shape>> referencias)
            {
            if (objeto == null) return;

            string codigo = AnalizadorMasterCorel.NormalizarCodigo(objeto.Name ?? string.Empty);
            if (codigo.StartsWith("diseno_"))
                {
                string pieza = NormalizarReferenciaLote(codigo.Substring("diseno_".Length));
                if (!referencias.ContainsKey(pieza)) referencias[pieza] = new List<VGCore.Shape>();
                referencias[pieza].Add(objeto);
                }

            if (objeto.Type == VGCore.cdrShapeType.cdrGroupShape && objeto.PowerClip == null)
                {
                for (int i = 1; i <= objeto.Shapes.Count; i++)
                    BuscarReferenciasLote(objeto.Shapes[i], referencias);
                }
            }

        private string NormalizarReferenciaLote(string codigo)
            {
            string valor = AnalizadorMasterCorel.NormalizarCodigo(codigo ?? string.Empty);

            if (valor.StartsWith("funda_"))
                {
                valor = valor.Substring("funda_".Length);
                string[] partes = valor.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);

                if (partes.Length > 1 && (partes[0] == "s" || partes[0] == "m" || partes[0] == "l"))
                    valor = string.Join("_", partes.Skip(1));
                }

            if (valor == "lado_izquierdo") valor = "lateral_izquierdo";
            if (valor == "lado_derecho") valor = "lateral_derecho";
            return valor;
            }

        private string CodigoReferenciaLote(string pieza)
            {
            return NormalizarReferenciaLote(pieza);
            }

        private void AplicarLoteGuiado()
            {
            int tareasProcesadas = 0;
            int destinosProcesados = 0;

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

                TareaLoteEscalar lote = ObtenerTareaLoteEscalarActual();
                if (lote == null) throw new InvalidOperationException("No quedan diseños pendientes.");

                VGCore.ShapeRange seleccion = documento.SelectionRange;
                if (seleccion.Count != 1 || seleccion[1].Type != VGCore.cdrShapeType.cdrGroupShape)
                    throw new InvalidOperationException("Seleccione un único grupo que contenga todas las referencias de " + lote.Diseno + ".");

                List<TareaEscalar> tareasAplicar = lote.Tareas
                    .Where(x => chkReemplazarContenidoEscalar.Checked || x.Estado != "Completado")
                    .ToList();

                if (tareasAplicar.Count == 0)
                    throw new InvalidOperationException("Este diseño ya está completado. Active Reemplazar contenido para corregirlo.");

                int totalLote = tareasAplicar.Sum(x => x.Destinos.Count(y => chkReemplazarContenidoEscalar.Checked || !y.TieneContenido));
                Dictionary<string, List<VGCore.Shape>> referencias = new Dictionary<string, List<VGCore.Shape>>(StringComparer.OrdinalIgnoreCase);
                BuscarReferenciasLote(seleccion[1], referencias);
                List<string> errores = new List<string>();

                foreach (TareaEscalar tarea in tareasAplicar)
                    {
                    string codigo = CodigoReferenciaLote(tarea.Pieza);
                    if (!referencias.ContainsKey(codigo))
                        errores.Add("Falta diseño_" + codigo + ".");
                    else if (referencias[codigo].Count > 1)
                        errores.Add("La referencia diseño_" + codigo + " está duplicada.");
                    else if (referencias[codigo][0].PowerClip == null || referencias[codigo][0].PowerClip.Shapes.Count == 0)
                        errores.Add("La referencia diseño_" + codigo + " no es un PowerClip con contenido.");
                    }

                if (errores.Count > 0)
                    throw new InvalidOperationException("No se puede iniciar el reemplazo por lote:\n\n• " + string.Join("\n• ", errores));

                foreach (TareaEscalar tarea in tareasAplicar)
                    {
                    List<PiezaEscalable> destinos = chkReemplazarContenidoEscalar.Checked
                        ? tarea.Destinos
                        : tarea.Destinos.Where(x => !x.TieneContenido).ToList();
                    escalador.Validar(referencias[CodigoReferenciaLote(tarea.Pieza)][0], destinos, chkReemplazarContenidoEscalar.Checked);
                    }

                MostrarProgresoOperacion(prgEscalar, 0, totalLote);
                btnAnalizarEscalar.Enabled = false;
                btnAplicarEscalar.Enabled = false;
                chkReemplazoPorLote.Enabled = false;
                UseWaitCursor = true;
                Cursor = Cursors.WaitCursor;
                bool temporizadorActivo = tmrConexionCorel.Enabled;
                tmrConexionCorel.Stop();

                try
                    {
                    foreach (TareaEscalar tarea in tareasAplicar)
                        {
                        List<PiezaEscalable> destinos = chkReemplazarContenidoEscalar.Checked
                            ? tarea.Destinos
                            : tarea.Destinos.Where(x => !x.TieneContenido).ToList();

                        lblEstadoEscalar.Text = "Aplicando lote " + lote.Diseno + ": " + tarea.Pieza + "...";
                        lblEstadoEscalar.Refresh();

                        destinosProcesados += escalador.Aplicar(
                            documento,
                            referencias[CodigoReferenciaLote(tarea.Pieza)][0],
                            destinos,
                            chkReemplazarContenidoEscalar.Checked,
                            delegate (int actual, int cantidad)
                                {
                                MostrarProgresoOperacion(prgEscalar, destinosProcesados + actual, totalLote);
                                lblEstadoEscalar.Text = "Aplicando " + lote.Diseno + " - " + tarea.Pieza + ": " + actual + " de " + cantidad + "...";
                                lblEstadoEscalar.Refresh();
                                },
                            ObtenerCorel());

                        tareasProcesadas++;
                        }
                    }
                finally
                    {
                    if (temporizadorActivo) tmrConexionCorel.Start();
                    }

                List<PiezaEscalable> resultado = escalador.AnalizarPedido(documento, planProduccionActual);
                MostrarAnalisisEscalar(resultado);
                MostrarProgresoOperacion(prgEscalar, 1, 1);
                lblEstadoEscalar.Text = "Lote " + lote.Diseno + " completado. Se procesaron " + destinosProcesados + " destinos.";

                Activate();
                BringToFront();
                MessageBox.Show(this,
                    "Terminó de aplicar el diseño por lote.\n\nDiseño: " + lote.Diseno +
                    "\nPartes procesadas: " + tareasProcesadas +
                    "\nDestinos procesados: " + destinosProcesados +
                    "\n\nLos campos Nombre y Numero se reemplazaron con los datos del Excel.",
                    "Lote aplicado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            catch (Exception ex)
                {
                MostrarProgresoOperacion(prgEscalar, 0, 1);
                lblEstadoEscalar.Text = tareasProcesadas > 0
                    ? "Lote pausado después de " + tareasProcesadas + " partes. Corrija el error y continúe."
                    : "Lote detenido. Corrija las referencias y vuelva a aplicar.";
                MessageBox.Show(this, ex.Message, "Escalar por lote", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            finally
                {
                UseWaitCursor = false;
                Cursor = Cursors.Default;
                btnAnalizarEscalar.Enabled = true;
                chkReemplazoPorLote.Enabled = true;
                TareaLoteEscalar actual = ObtenerTareaLoteEscalarActual();
                btnAplicarEscalar.Enabled = actual != null && actual.Estado != "Con error";
                }
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

                bool seleccionValida = seleccion.Count == 1 &&
                    (seleccion[1].Type == VGCore.cdrShapeType.cdrGroupShape ||
                     (seleccion[1].PowerClip != null && seleccion[1].PowerClip.Shapes.Count > 0));

                if (!seleccionValida)
                    throw new InvalidOperationException("Seleccione un único grupo o PowerClip plantilla para " + tarea.Diseno + " - " + tarea.Pieza + ".");

                List<PiezaEscalable> destinos = chkReemplazarContenidoEscalar.Checked
                    ? tarea.Destinos
                    : tarea.Destinos.Where(x => !x.TieneContenido).ToList();

                if (destinos.Count == 0)
                    throw new InvalidOperationException("Esta tarea ya está completada. Active Reemplazar contenido para corregirla.");

                btnAnalizarEscalar.Enabled = false;
                btnAplicarEscalar.Enabled = false;
                UseWaitCursor = true;
                Cursor = Cursors.WaitCursor;
                MostrarProgresoOperacion(prgEscalar, 0, destinos.Count);
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
                            MostrarProgresoOperacion(prgEscalar, actual, cantidad);
                            lblEstadoEscalar.Text =
                                "Aplicando " + tarea.Diseno + " - " + tarea.Pieza +
                                ": " + actual + " de " + cantidad + "...";
                            lblEstadoEscalar.Refresh();
                            },
                        ObtenerCorel());
                    }
                finally
                    {
                    if (temporizadorActivo)
                        tmrConexionCorel.Start();
                    }

                List<PiezaEscalable> resultado = escalador.AnalizarPedido(documento, planProduccionActual);
                MostrarAnalisisEscalar(resultado);
                MostrarProgresoOperacion(prgEscalar, 1, 1);

                TareaEscalar siguiente = tareasEscalar.FirstOrDefault(x => x.Estado == "Pendiente" || x.Estado == "Parcial");
                lblEstadoEscalar.Text = siguiente == null
                    ? "Escalado completado. Se aplicaron " + total + " destinos en el último paso. Puede elegir una tarea para corregirla."
                    : "Listo. Ahora seleccione: " + siguiente.Diseno + " - " + siguiente.Pieza + ".";

                Activate();
                BringToFront();

                MessageBox.Show(
                    this,
                    "Terminó de aplicar el diseño.\n\n" +
                    "Diseño: " + tarea.Diseno + "\n" +
                    "Pieza: " + tarea.Pieza + "\n" +
                    "Destinos procesados: " + total + "\n\n" +
                    "Los objetos de texto llamados Nombre y Numero se reemplazaron con los datos correspondientes del Excel.",
                    "Diseño aplicado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                }
            catch (Exception ex)
                {
                MostrarProgresoOperacion(prgEscalar, 0, 1);
                lblEstadoEscalar.Text = "Revise el mensaje y continúe con la tarea pendiente.";
                MessageBox.Show(this, ex.Message, "Escalar pedido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            finally
                {
                UseWaitCursor = false;
                Cursor = Cursors.Default;
                btnAnalizarEscalar.Enabled = true;
                TareaEscalar tareaActual = ObtenerTareaEscalarActual();
                btnAplicarEscalar.Enabled = tareaActual != null && tareaActual.Estado != "Con error";
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
                tareasLoteEscalar.Clear();
                dgvEscalar.Rows.Clear();
                MostrarProgresoOperacion(prgEscalar, 0, 0);
                lblEstadoEscalar.Text = "Analizando los moldes...";

                documentoEscalar = DocumentoActivoEscalar();
                if (planProduccionActual == null || planProduccionActual.Moldes == null || planProduccionActual.Moldes.Count == 0)
                    throw new InvalidOperationException("No existe un pedido activo. Analice o recupere primero el Excel desde Moldes.");

                modoGuiadoEscalar = true;
                ConfigurarColumnasEscalar(true);

                List<PiezaEscalable> piezas = escalador.AnalizarPedido(documentoEscalar, planProduccionActual);

                MostrarAnalisisEscalar(piezas);
                MostrarProgresoOperacion(prgEscalar, 1, 1);
                }
            catch (Exception ex)
                {
                MostrarProgresoOperacion(prgEscalar, 0, 1);
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
            if (!modoGuiadoEscalar)
                {
                MessageBox.Show(
                    this,
                    "Analice primero el pedido desde Moldes y después analice el documento en Escalar.",
                    "Escalar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
                }

            if (chkReemplazoPorLote.Checked)
                AplicarLoteGuiado();
            else
                AplicarTareaGuiada();
            }
        }
    }