using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Text;
using VGCore;
using System.Diagnostics;
using System.IO;
using TithorAutomation.Datos;
using TithorAutomation.Modelos;
using TithorAutomation.Servicios;
using System.Security.Cryptography;

namespace TithorAutomation
    {
    public partial class frmPrincipal
        {
        private void CargarProductosProduccion()
            {
            try
                {
                List<Producto> productos = productoRepositorio.ListarActivos();

                cboProductoProduccion.DataSource = null;
                cboProductoProduccion.DisplayMember = "Nombre";
                cboProductoProduccion.ValueMember = "Id";
                cboProductoProduccion.DataSource = productos;
                }
            catch (Exception ex)
                {
                MessageBox.Show(
                    "No se pudieron cargar los productos en Moldes.\n\n" + ex.Message,
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                }
            }
        private void LimpiarPedidoProduccion(bool limpiarRuta)
            {
            resultadoPedidoActual = null;
            planProduccionActual = null;

            cboProductoProduccion.Enabled = true;
            btnCargarExcelProduccion.Enabled = true;
            btnNuevoPedido.Enabled = true;
            btnCopiarMoldesPedido.Text = "Copiar moldes";

            if (limpiarRuta)
                txtRutaExcelProduccion.Clear();

            dgvPedidoProduccion.Rows.Clear();
            dgvPedidoProduccion.Columns.Clear();
            dgvPedidoProduccion.Visible = false;

            lblFilasPedidoValor.Text = "0";
            lblDisenosPedidoValor.Text = "0";
            lblUnidadesPedidoValor.Text = "0";
            lblAdvertenciasPedidoValor.Text = "0";

            lblResultadoPedido.Text = "Pedido sin analizar";
            lblEstadoExcelProduccion.Text = "Seleccione un producto y un archivo Excel.";

            btnAnalizarExcelProduccion.Enabled =
                !string.IsNullOrWhiteSpace(txtRutaExcelProduccion.Text);

            btnCopiarMoldesPedido.Enabled = false;
            }
        private void cboProductoProduccion_SelectedIndexChanged(object sender, EventArgs e)
            {
            LimpiarPedidoProduccion(true);
            }
        private void btnCargarExcelProduccion_Click(object sender, EventArgs e)
            {
            Producto producto = cboProductoProduccion.SelectedItem as Producto;

            if (producto == null)
                {
                MessageBox.Show(
                    "Seleccione primero un producto.",
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return;
                }

            using (OpenFileDialog dialogo = new OpenFileDialog())
                {
                dialogo.Title = "Seleccionar archivo del pedido";
                dialogo.Filter = "Archivos Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm";
                dialogo.CheckFileExists = true;
                dialogo.Multiselect = false;

                if (dialogo.ShowDialog(this) != DialogResult.OK)
                    return;

                txtRutaExcelProduccion.Text = dialogo.FileName;

                resultadoPedidoActual = null;

                dgvPedidoProduccion.Rows.Clear();
                dgvPedidoProduccion.Columns.Clear();
                dgvPedidoProduccion.Visible = false;

                btnAnalizarExcelProduccion.Enabled = true;
                btnCopiarMoldesPedido.Enabled = false;

                lblEstadoExcelProduccion.Text = "Archivo listo para analizar.";
                lblResultadoPedido.Text = "Pedido pendiente de análisis.";
                }
            }
        private ILectorPedidoProducto ObtenerLectorPedido(string codigoProducto)
            {
            foreach (ILectorPedidoProducto lector in lectoresPedido)
                {
                if (lector.PuedeLeer(codigoProducto))
                    return lector;
                }

            return null;
            }
        private void btnAnalizarExcelProduccion_Click(object sender, EventArgs e)
            {
            Producto producto = cboProductoProduccion.SelectedItem as Producto;

            if (producto == null)
                {
                MessageBox.Show(
                    "Seleccione un producto.",
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return;
                }

            if (!File.Exists(txtRutaExcelProduccion.Text))
                {
                MessageBox.Show(
                    "Seleccione un archivo Excel válido.",
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                return;
                }

            ILectorPedidoProducto lector = ObtenerLectorPedido(producto.Codigo);

            if (lector == null)
                {
                MessageBox.Show(
                    "Todavía no existe un lector de Excel para el producto \"" + producto.Nombre + "\".",
                    "Producto no implementado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return;
                }

            try
                {
                btnAnalizarExcelProduccion.Enabled = false;
                lblEstadoExcelProduccion.Text = "Analizando archivo Excel...";

                lblEstadoExcelProduccion.Refresh();

                resultadoPedidoActual = lector.Analizar(
                    producto.Id,
                    producto.Codigo,
                    txtRutaExcelProduccion.Text
                );


                MostrarResultadoPedido(resultadoPedidoActual);

                btnCopiarMoldesPedido.Enabled = resultadoPedidoActual.PuedeAprobar;

                lblEstadoExcelProduccion.Text = "Análisis terminado.";
                btnNuevoPedido.Enabled = true;

                MostrarAdvertenciasPedido(resultadoPedidoActual);
                }
            catch (Exception ex)
                {
                LimpiarPedidoProduccion(false);

                lblEstadoExcelProduccion.Text = "Error durante el análisis.";

                MessageBox.Show(
                    "No se pudo analizar el archivo Excel.\n\n" + ex.Message,
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                }
            finally
                {
                btnAnalizarExcelProduccion.Enabled =
                    File.Exists(txtRutaExcelProduccion.Text);
                }
            }
        private void ConfigurarColumnasPedido(ResultadoAnalisisPedido resultado)
            {
            dgvPedidoProduccion.Columns.Clear();
            dgvPedidoProduccion.AutoGenerateColumns = false;
            dgvPedidoProduccion.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            AgregarColumnaPedido("colEstadoPedido", "Estado", 75F, 90);
            AgregarColumnaPedido("colFilaPedido", "Fila Excel", 55F, 70);

            if (EsPedidoCamisetas(resultado))
                {
                AgregarColumnaPedido("colNumeroOrdenPedido", "N°", 45F, 50);
                AgregarColumnaPedido("colModeloPedido", "Modelo", 85F, 90);
                AgregarColumnaPedido("colDisenoCamisetaPedido", "Diseño", 105F, 110);
                AgregarColumnaPedido("colNombrePedido", "Nombre", 110F, 110);
                AgregarColumnaPedido("colPrendaPedido", "Prenda", 105F, 110);
                AgregarColumnaPedido("colNumeroPedido", "Número", 55F, 65);
                AgregarColumnaPedido("colTallaCamisetaPedido", "Talla camiseta", 75F, 90);
                AgregarColumnaPedido("colTallaShortPedido", "Talla short", 70F, 85);
                AgregarColumnaPedido("colCortePedido", "Corte", 70F, 75);
                AgregarColumnaPedido("colMangaPedido", "Manga", 70F, 75);
                AgregarColumnaPedido("colCuelloPedido", "Cuello", 70F, 75);
                AgregarColumnaPedido("colObservacionesPedido", "Observaciones", 140F, 140);
                return;
                }

            AgregarColumnaPedido("colDisenoPedido", "Diseño", 150F, 140);
            AgregarColumnaPedido("colTallaPedido", "Talla", 60F, 60);
            AgregarColumnaPedido("colCantidadPedido", "Cantidad", 70F, 75);
            AgregarColumnaPedido("colNotasPedido", "Notas", 140F, 120);
            AgregarColumnaPedido("colObservacionesPedido", "Observaciones", 220F, 180);
            }

        private bool EsPedidoCamisetas(ResultadoAnalisisPedido resultado)
            {
            if (resultado == null || string.IsNullOrWhiteSpace(resultado.CodigoProducto))
                return false;

            return resultado.CodigoProducto.IndexOf("CAMISETA", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        private void AgregarColumnaPedido(string nombre, string titulo, float proporcion, int anchoMinimo)
            {
            DataGridViewTextBoxColumn columna = new DataGridViewTextBoxColumn();

            columna.Name = nombre;
            columna.HeaderText = titulo;
            columna.ReadOnly = true;
            columna.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            columna.FillWeight = proporcion;
            columna.MinimumWidth = anchoMinimo;
            columna.SortMode = DataGridViewColumnSortMode.NotSortable;

            dgvPedidoProduccion.Columns.Add(columna);
            }
        private void MostrarResultadoPedido(ResultadoAnalisisPedido resultado)
            {
            ConfigurarColumnasPedido(resultado);
            dgvPedidoProduccion.Rows.Clear();

            bool pedidoCamisetas = EsPedidoCamisetas(resultado);

            foreach (LineaPedido linea in resultado.Lineas)
                {
                object[] valores = pedidoCamisetas
                    ? CrearValoresFilaCamisetas(linea)
                    : CrearValoresFilaGeneral(linea);

                int indice = dgvPedidoProduccion.Rows.Add(valores);
                DataGridViewRow fila = dgvPedidoProduccion.Rows[indice];
                fila.Tag = linea;

                if (!linea.Procesable)
                    {
                    fila.DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(255, 235, 235);
                    fila.Cells["colEstadoPedido"].Style.ForeColor = System.Drawing.Color.FromArgb(200, 45, 45);
                    }
                else
                    {
                    fila.Cells["colEstadoPedido"].Style.ForeColor = System.Drawing.Color.FromArgb(30, 150, 70);
                    }
                }

            int advertencias = resultado.TotalFilasOmitidas + resultado.AdvertenciasGenerales.Count;

            lblFilasPedidoValor.Text = resultado.TotalFilasProcesables.ToString();
            lblDisenosPedidoTitulo.Text = "Diseños";
            lblDisenosPedidoValor.Text = resultado.ObtenerDisenos().Count.ToString();
            lblUnidadesPedidoValor.Text = resultado.TotalUnidades.ToString();
            lblAdvertenciasPedidoValor.Text = advertencias.ToString();

            lblResultadoPedido.Text =
                resultado.TotalFilasProcesables + " filas listas, " +
                resultado.TotalFilasOmitidas + " filas serán omitidas.";

            dgvPedidoProduccion.Visible = true;
            }

        private object[] CrearValoresFilaCamisetas(LineaPedido linea)
            {
            return new object[]
                {
                linea.Estado,
                linea.NumeroFila,
                linea.ObtenerCampo("n"),
                linea.ObtenerCampo("modelo"),
                linea.Diseno,
                linea.ObtenerCampo("nombre"),
                linea.ObtenerCampo("prenda"),
                linea.ObtenerCampo("numero"),
                MostrarTallaPedido(linea.ObtenerCampo("talla_camiseta")),
                MostrarTallaPedido(linea.ObtenerCampo("talla_short")),
                linea.ObtenerCampo("corte"),
                linea.ObtenerCampo("manga"),
                linea.ObtenerCampo("cuello"),
                linea.MensajeCompleto
                };
            }

        private object[] CrearValoresFilaGeneral(LineaPedido linea)
            {
            return new object[]
                {
                linea.Estado,
                linea.NumeroFila,
                linea.Diseno,
                linea.Talla,
                linea.Cantidad > 0 ? linea.Cantidad.ToString() : string.Empty,
                linea.Notas,
                linea.MensajeCompleto
                };
            }

        private string MostrarTallaPedido(string talla)
            {
            return string.IsNullOrWhiteSpace(talla)
                ? string.Empty
                : talla.Trim().ToUpperInvariant();
            }
        private void MostrarAdvertenciasPedido(ResultadoAnalisisPedido resultado)
            {
            if (resultado.AdvertenciasGenerales.Count == 0 && resultado.TotalFilasOmitidas == 0)
                return;

            StringBuilder mensaje = new StringBuilder();

            foreach (string advertencia in resultado.AdvertenciasGenerales)
                mensaje.AppendLine("• " + advertencia);

            foreach (LineaPedido linea in resultado.Lineas)
                {
                if (!linea.Procesable)
                    mensaje.AppendLine("• Fila " + linea.NumeroFila + ": " + linea.MensajeCompleto);
                }

            MessageBox.Show(
                "El Excel se analizó con advertencias:\n\n" + mensaje,
                "Revisar pedido",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            }
        private IPlanificadorProducto ObtenerPlanificadorProducto(string codigoProducto)
            {
            foreach (IPlanificadorProducto planificador in planificadoresProducto)
                {
                if (planificador.PuedeProcesar(codigoProducto))
                    return planificador;
                }

            return null;
            }
        private void btnCopiarMoldesPedido_Click(object sender, EventArgs e)
            {
            bool temporizadorActivo = tmrConexionCorel.Enabled;

            try
                {
                if (resultadoPedidoActual == null || !resultadoPedidoActual.PuedeAprobar)
                    {
                    MessageBox.Show(
                        "Primero debe analizar un archivo Excel con filas válidas.",
                        "Moldes",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                    }

                Producto producto = cboProductoProduccion.SelectedItem as Producto;

                if (producto == null)
                    {
                    MessageBox.Show(
                        "Seleccione el producto que desea producir.",
                        "Moldes",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                    }

                IPlanificadorProducto planificador = ObtenerPlanificadorProducto(producto.Codigo);

                if (planificador == null)
                    {
                    MessageBox.Show(
                        $"El producto '{producto.Nombre}' todavía no tiene un planificador de producción.",
                        "Moldes",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                    }

                ArchivoMaster master = archivoMasterRepositorio.ObtenerPrincipalPorProducto(producto.Id);

                if (master == null)
                    {
                    MessageBox.Show(
                        "Este producto no tiene un archivo Master configurado.",
                        "Moldes",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                    }

                if (!System.IO.File.Exists(master.RutaArchivo))
                    {
                    MessageBox.Show(
                        $"No se encontró el archivo Master:\n\n{master.RutaArchivo}",
                        "Moldes",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                    }

                if (!master.FechaUltimoAnalisis.HasValue)
                    {
                    MessageBox.Show(
                        "El archivo Master todavía no ha sido analizado.\n\nAnalícelo desde Configuración antes de continuar.",
                        "Moldes",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                    }

                List<Molde> catalogo = moldeRepositorio.ListarPorMaster(master.Id);

                if (catalogo == null || catalogo.Count == 0)
                    {
                    MessageBox.Show(
                        "El catálogo de moldes está vacío.\n\nAnalice y sincronice el Master desde Configuración.",
                        "Moldes",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                    }

                PlanProduccion plan = planificador.CrearPlan(resultadoPedidoActual, catalogo);

                if (plan == null)
                    {
                    MessageBox.Show(
                        "No se pudo crear el plan de producción.",
                        "Moldes",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );

                    return;
                    }

                if (!plan.EsValido)
                    {
                    string detalleAdvertencias = plan.Advertencias.Count > 0
                        ? string.Join("\n• ", plan.Advertencias)
                        : "El plan contiene errores sin especificar.";

                    MessageBox.Show(
                        "No se pueden copiar los moldes:\n\n• " + detalleAdvertencias,
                        "Revisar moldes",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                    }

                if (plan.TotalMoldes == 0)
                    {
                    MessageBox.Show(
                        "El plan de producción no contiene moldes para copiar.",
                        "Moldes",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );

                    return;
                    }

                VGCore.Application corel = ObtenerCorel();

                if (corel == null || corel.Documents.Count == 0)
                    {
                    MessageBox.Show(
                        "Abra el documento de producción en CorelDRAW antes de copiar los moldes.",
                        "Moldes",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                    }

                VGCore.Document documentoDestino = corel.ActiveDocument;

                DialogResult confirmacion = MessageBox.Show(
                    $"Se copiarán {plan.TotalMoldes} moldes.\n\n" +
                    $"Producto: {producto.Nombre}\n" +
                    $"Documento abierto: {documentoDestino.Name}\n" +
                    $"Master: {master.NombreArchivo}\n\n" +
                    "¿Desea continuar?",
                    "Copiar moldes",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (confirmacion != DialogResult.Yes)
                    return;

                btnCopiarMoldesPedido.Enabled = false;
                btnNuevoPedido.Enabled = false;
                tmrConexionCorel.Stop();
                cboProductoProduccion.Enabled = false;
                btnCargarExcelProduccion.Enabled = false;
                btnAnalizarExcelProduccion.Enabled = false;
                lblResultadoPedido.Text = $"Preparando {plan.TotalMoldes} moldes...";

                int totalCopiado = copiadorMoldesCorel.Copiar(
                    corel,
                    documentoDestino,
                    master.RutaArchivo,
                    plan,
                    delegate (int actual, int total)
                        {
                            lblResultadoPedido.Text = $"Copiando molde {actual} de {total}...";

                            if (actual % 5 == 0 || actual == total)
                                lblResultadoPedido.Refresh();
                            }
                );

                planProduccionActual = plan;

                lblResultadoPedido.Text =
                    $"Moldes preparados: {totalCopiado} conjuntos copiados en {documentoDestino.Name}.";

                btnCopiarMoldesPedido.Text = "Copiar nuevamente";
                btnCopiarMoldesPedido.Enabled = true;
                btnNuevoPedido.Enabled = true;

                MessageBox.Show(
                    $"Se copiaron correctamente {totalCopiado} moldes.\n\n" +
                    $"Documento: {documentoDestino.Name}\n" +
                    "Capa creada: TITHOR_PRODUCCION\n\n" +
                    "Puede usar Ctrl+Z una sola vez para deshacer toda la copia.",
                    "Moldes preparados",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                }
            catch (Exception ex)
                {
                lblResultadoPedido.Text = "No se pudieron copiar los moldes.";

                MessageBox.Show(
                    $"No se pudieron copiar los moldes.\n\n" +
                    $"Tipo: {ex.GetType().FullName}\n" +
                    $"Código: 0x{ex.HResult:X8}\n\n" +
                    ex.Message,
                    "Error de producción",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                }
            finally
                {
                if (temporizadorActivo) tmrConexionCorel.Start();
                cboProductoProduccion.Enabled = true;
                btnCargarExcelProduccion.Enabled = true;
                btnAnalizarExcelProduccion.Enabled = File.Exists(txtRutaExcelProduccion.Text);
                btnCopiarMoldesPedido.Enabled = resultadoPedidoActual != null && resultadoPedidoActual.PuedeAprobar;
                btnNuevoPedido.Enabled = resultadoPedidoActual != null;
                }
            }
        private void ConfigurarGridProduccion()
            {
            dgvPedidoProduccion.AllowUserToAddRows = false;
            dgvPedidoProduccion.AllowUserToDeleteRows = false;
            dgvPedidoProduccion.AllowUserToResizeRows = false;
            dgvPedidoProduccion.MultiSelect = false;
            dgvPedidoProduccion.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            }

        private void btnNuevoPedido_Click(object sender, EventArgs e)
            {
            if (resultadoPedidoActual != null)
                {
                DialogResult respuesta = MessageBox.Show(
                    "Se limpiará el pedido actual para comenzar uno nuevo.\n\n" +
                    "Los moldes que ya fueron copiados en CorelDRAW no se eliminarán.\n\n" +
                    "¿Desea continuar?",
                    "Nuevo pedido",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (respuesta != DialogResult.Yes)
                    return;
                }

            LimpiarPedidoProduccion(true);
            lblResultadoPedido.Text = "Listo para cargar un nuevo pedido";

            txtRutaExcelProduccion.Focus();
            }
        }
    }
