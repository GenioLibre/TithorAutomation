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
        private void ConfigurarFiltrosCatalogo()
            {
            cboEstadoMolde.Items.Clear();

            cboEstadoMolde.Items.Add("Todos");
            cboEstadoMolde.Items.Add("Correctos");
            cboEstadoMolde.Items.Add("Nuevos");
            cboEstadoMolde.Items.Add("Modificados");
            cboEstadoMolde.Items.Add("Inválidos");
            cboEstadoMolde.Items.Add("Duplicados");
            cboEstadoMolde.Items.Add("Eliminados");

            cboEstadoMolde.SelectedIndex = 0;
            cboEstadoMolde.DropDownStyle = ComboBoxStyle.DropDownList;
            }
        private void CargarProductosConfiguracion(int productoIdSeleccionado = 0)
            {
            try
                {
                List<Producto> productos =
                    productoRepositorio.ListarActivos();

                cboProductoConfiguracion.DataSource = null;
                cboProductoConfiguracion.DisplayMember = "Nombre";
                cboProductoConfiguracion.ValueMember = "Id";
                cboProductoConfiguracion.DataSource = productos;

                if (productoIdSeleccionado > 0)
                    {
                    cboProductoConfiguracion.SelectedValue =
                        productoIdSeleccionado;
                    }

                ActualizarEstadoProductoConfiguracion();
                }
            catch (Exception ex)
                {
                lblProductoConfiguracionEstado.Text =
                    "No se pudieron cargar los productos.";

                MessageBox.Show(
                    "No se pudieron cargar los productos.\n\n" +
                    ex.Message,
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                }
            }
        private Producto ObtenerProductoSeleccionado()
            {
            return cboProductoConfiguracion.SelectedItem as Producto;
            }
        private void ActualizarEstadoProductoConfiguracion()
            {
            Producto producto = ObtenerProductoSeleccionado();

            if (producto == null)
                {
                lblProductoConfiguracionEstado.Text =
                    "No hay ningún producto seleccionado.";

                btnEditarProducto.Enabled = false;
                btnEliminarProducto.Enabled = false;
                return;
                }

            lblProductoConfiguracionEstado.Text =
                "Producto seleccionado: " +
                producto.Nombre +
                "  |  Código: " +
                producto.Codigo;

            btnEditarProducto.Enabled = true;
            btnEliminarProducto.Enabled = true;
            }
        private void btnAgregarProducto_Click(object sender, EventArgs e)
            {
            using (frmProducto formulario = new frmProducto())
                {
                if (formulario.ShowDialog(this) == DialogResult.OK)
                    {
                    int nuevoId = formulario.ProductoGuardado.Id;
                    CargarProductosConfiguracion(nuevoId);
                    }
                }
            }
        private void btnEditarProducto_Click(object sender, EventArgs e)
            {
            Producto seleccionado = ObtenerProductoSeleccionado();

            if (seleccionado == null)
                {
                MessageBox.Show(
                    "Seleccione el producto que desea editar.",
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return;
                }

            Producto productoActualizado =
                productoRepositorio.ObtenerPorId(seleccionado.Id);

            if (productoActualizado == null)
                {
                MessageBox.Show(
                    "El producto seleccionado ya no existe.",
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                CargarProductosConfiguracion();
                return;
                }

            using (frmProducto formulario =
                   new frmProducto(productoActualizado))
                {
                if (formulario.ShowDialog(this) == DialogResult.OK)
                    {
                    CargarProductosConfiguracion(
                        formulario.ProductoGuardado.Id
                    );
                    }
                }
            }
        private void btnEliminarProducto_Click(object sender, EventArgs e)
            {
            Producto producto = ObtenerProductoSeleccionado();

            if (producto == null)
                return;

            DialogResult respuesta = MessageBox.Show(
                "¿Desea desactivar el producto \"" +
                producto.Nombre +
                "\"?\n\n" +
                "Sus archivos maestros y moldes permanecerán guardados.",
                "Desactivar producto",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2
            );

            if (respuesta != DialogResult.Yes)
                return;

            try
                {
                productoRepositorio.Desactivar(producto.Id);
                CargarProductosConfiguracion();

                lblProductoConfiguracionEstado.Text =
                    "El producto fue desactivado correctamente.";
                }
            catch (Exception ex)
                {
                MessageBox.Show(
                    "No se pudo desactivar el producto.\n\n" +
                    ex.Message,
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                }
            }
        private void cboProductoConfiguracion_SelectedIndexChanged(object sender, EventArgs e)
            {
            ActualizarEstadoProductoConfiguracion();
            CargarArchivoMasterDelProducto();
            }
        private void CargarArchivoMasterDelProducto()
            {
            moldesAnalizados.Clear();
            tamanoMasterAnalizado = 0;
            fechaMasterAnalizadoUtc = DateTime.MinValue;

            dgvCatalogoMoldes.Rows.Clear();
            dgvCatalogoMoldes.Visible = false;

            btnSincronizarMaster.Enabled = false;

            dgvCatalogoMoldes.Rows.Clear();
            dgvCatalogoMoldes.Visible = false;

            txtBuscarMolde.Enabled = false;
            cboEstadoMolde.Enabled = false;

            lblResumenCatalogo.Text = "Sin analizar";

            try
                {
                Producto producto = ObtenerProductoSeleccionado();

                if (producto == null)
                    {
                    archivoMasterActual = null;
                    LimpiarVistaArchivoMaster();
                    return;
                    }

                archivoMasterActual =
                    archivoMasterRepositorio
                        .ObtenerPrincipalPorProducto(producto.Id);

                MostrarArchivoMaster();
                CargarCatalogoGuardado();
                }
            catch (Exception ex)
                {
                archivoMasterActual = null;
                LimpiarVistaArchivoMaster();

                MessageBox.Show(
                    "No se pudo cargar la configuración del Master.\n\n" +
                    ex.Message,
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                }
            }
        private void CargarCatalogoGuardado()
            {
            if (archivoMasterActual == null ||
                archivoMasterActual.Id <= 0 ||
                !archivoMasterActual.FechaUltimoAnalisis.HasValue)
                {
                dgvCatalogoMoldes.Rows.Clear();
                dgvCatalogoMoldes.Visible = false;

                lblResumenCatalogo.Text = "Sin analizar";
                txtBuscarMolde.Enabled = false;
                cboEstadoMolde.Enabled = false;

                return;
                }

            try
                {
                List<Molde> moldesGuardados = moldeRepositorio.ListarPorMaster(archivoMasterActual.Id);
                Producto producto = ObtenerProductoSeleccionado();

                ConfigurarColumnasCatalogo(producto);

                dgvCatalogoMoldes.Rows.Clear();

                foreach (Molde molde in moldesGuardados)
                    {
                    molde.Estado = "Correcto";
                    molde.Observacion = "Molde registrado en el catálogo.";

                    AgregarFilaCatalogo(molde);
                    }

                dgvCatalogoMoldes.Visible = true;
                dgvCatalogoMoldes.ColumnHeadersVisible = true;

                txtBuscarMolde.Enabled = moldesGuardados.Count > 0;
                cboEstadoMolde.Enabled = moldesGuardados.Count > 0;

                lblResumenCatalogo.Text = moldesGuardados.Count + " moldes registrados";
                AplicarFiltrosCatalogo();
                }
            catch (Exception ex)
                {
                dgvCatalogoMoldes.Rows.Clear();
                dgvCatalogoMoldes.Visible = false;

                lblResumenCatalogo.Text = "Error al cargar el catálogo";

                MessageBox.Show(
                    "No se pudo cargar el catálogo guardado.\n\n" + ex.Message,
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                }
            }
        private void LimpiarVistaArchivoMaster()
            {
            txtRutaMaster.Clear();

            lblEstadoMaster.Text = "No configurado";
            lblEstadoMaster.ForeColor =
                System.Drawing.Color.Gray;

            lblCantidadMoldesMaster.Text = "0";
            lblUltimoAnalisisMaster.Text = "Nunca";
            lblProcesoMaster.Text = "";

            prgAnalisisMaster.Value = 0;
            prgAnalisisMaster.Visible = false;

            btnAbrirUbicacionMaster.Enabled = false;
            btnAnalizarMaster.Enabled = false;
            btnSincronizarMaster.Enabled = false;

            dgvCatalogoMoldes.Rows.Clear();
            lblResumenCatalogo.Text = "0 moldes registrados";

            dgvCatalogoMoldes.ColumnHeadersVisible = false;
            txtBuscarMolde.Enabled = false;
            cboEstadoMolde.Enabled = false;

            dgvCatalogoMoldes.Rows.Clear();
            dgvCatalogoMoldes.Visible = false;

            txtBuscarMolde.Enabled = false;
            cboEstadoMolde.Enabled = false;

            lblResumenCatalogo.Text = "Sin analizar";
            }
        private void MostrarArchivoMaster()
            {
            if (archivoMasterActual == null)
                {
                LimpiarVistaArchivoMaster();
                return;
                }

            txtRutaMaster.Text =
                archivoMasterActual.RutaArchivo ?? string.Empty;

            lblCantidadMoldesMaster.Text =
                archivoMasterActual.CantidadMoldes.ToString();

            lblResumenCatalogo.Text =
                archivoMasterActual.CantidadMoldes +
                " moldes registrados";

            lblUltimoAnalisisMaster.Text =
                archivoMasterActual.FechaUltimoAnalisis.HasValue
                    ? archivoMasterActual
                        .FechaUltimoAnalisis
                        .Value
                        .ToString("dd/MM/yyyy HH:mm")
                    : "Nunca";

            bool existeArchivo =
                File.Exists(archivoMasterActual.RutaArchivo);

            btnAbrirUbicacionMaster.Enabled = existeArchivo;
            btnAnalizarMaster.Enabled = existeArchivo;

            prgAnalisisMaster.Value = 0;
            prgAnalisisMaster.Visible = false;

            if (!existeArchivo)
                {
                lblEstadoMaster.Text =
                    "Archivo no encontrado";

                lblEstadoMaster.ForeColor =
                    System.Drawing.Color.FromArgb(214, 69, 69);

                lblProcesoMaster.Text =
                    "Seleccione nuevamente el archivo Master.";

                btnSincronizarMaster.Enabled = false;
                return;
                }

            if (!archivoMasterActual.FechaUltimoAnalisis.HasValue)
                {
                lblEstadoMaster.Text =
                    "Pendiente de análisis";

                lblEstadoMaster.ForeColor =
                    System.Drawing.Color.FromArgb(220, 145, 0);

                lblProcesoMaster.Text =
                    "El Master todavía no ha sido analizado.";

                btnSincronizarMaster.Enabled = false;
                return;
                }

            FileInfo informacion =
                new FileInfo(archivoMasterActual.RutaArchivo);

            bool archivoModificado =
                ArchivoFueModificado(informacion);

            if (archivoModificado)
                {
                lblEstadoMaster.Text =
                    "Cambios pendientes";

                lblEstadoMaster.ForeColor =
                    System.Drawing.Color.FromArgb(220, 145, 0);

                lblProcesoMaster.Text =
                    "El archivo cambió después del último análisis.";

                btnSincronizarMaster.Enabled = false;
                }
            else
                {
                lblEstadoMaster.Text = "Sincronizado";

                lblEstadoMaster.ForeColor =
                    System.Drawing.Color.FromArgb(30, 160, 80);

                lblProcesoMaster.Text =
                    "El catálogo está actualizado.";

                btnSincronizarMaster.Enabled = false;
                }
            }
        private bool ArchivoFueModificado(FileInfo informacion)
            {
            if (archivoMasterActual == null ||
                !archivoMasterActual.FechaModificacion.HasValue)
                {
                return true;
                }

            DateTime fechaGuardada =
                archivoMasterActual
                    .FechaModificacion
                    .Value
                    .ToUniversalTime();

            DateTime fechaActual =
                informacion.LastWriteTimeUtc;

            double diferenciaSegundos =
                Math.Abs(
                    (fechaActual - fechaGuardada).TotalSeconds
                );

            return diferenciaSegundos > 2 ||
                   informacion.Length !=
                   archivoMasterActual.TamanoArchivo;
            }
        private void btnSeleccionarMaster_Click(object sender, EventArgs e)
            {
            Producto producto = ObtenerProductoSeleccionado();

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

            using (System.Windows.Forms.OpenFileDialog dialogo =
                   new System.Windows.Forms.OpenFileDialog())
                {
                dialogo.Title =
                    "Seleccionar Master para " + producto.Nombre;

                dialogo.Filter =
                    "Archivos CorelDRAW (*.cdr)|*.cdr";

                dialogo.CheckFileExists = true;
                dialogo.CheckPathExists = true;
                dialogo.Multiselect = false;

                if (archivoMasterActual != null &&
                    File.Exists(archivoMasterActual.RutaArchivo))
                    {
                    dialogo.InitialDirectory =
                        Path.GetDirectoryName(
                            archivoMasterActual.RutaArchivo
                        );

                    dialogo.FileName =
                        Path.GetFileName(
                            archivoMasterActual.RutaArchivo
                        );
                    }

                if (dialogo.ShowDialog(this) !=
                    DialogResult.OK)
                    {
                    return;
                    }

                GuardarArchivoMasterSeleccionado(
                    producto,
                    dialogo.FileName
                );
                }
            }
        private void GuardarArchivoMasterSeleccionado(Producto producto, string rutaArchivo)
            {
            if (producto == null || producto.Id <= 0)
                {
                MessageBox.Show(
                    "Seleccione primero un producto válido.",
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return;
                }

            if (string.IsNullOrWhiteSpace(rutaArchivo) ||
                !File.Exists(rutaArchivo) ||
                !string.Equals(Path.GetExtension(rutaArchivo), ".cdr", StringComparison.OrdinalIgnoreCase))
                {
                MessageBox.Show(
                    "Seleccione un archivo Master de CorelDRAW válido.",
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                return;
                }

            Producto productoSeleccionado = ObtenerProductoSeleccionado();

            if (productoSeleccionado == null || productoSeleccionado.Id != producto.Id)
                {
                MessageBox.Show(
                    "El producto seleccionado cambió. Vuelva a seleccionar el archivo Master.",
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                return;
                }

            try
                {
                FileInfo informacion = new FileInfo(rutaArchivo);

                bool masterPerteneceAlProducto =
                    archivoMasterActual != null &&
                    archivoMasterActual.ProductoId == producto.Id;

                bool esMismoArchivo =
                    masterPerteneceAlProducto &&
                    string.Equals(
                        archivoMasterActual.RutaArchivo,
                        informacion.FullName,
                        StringComparison.OrdinalIgnoreCase
                    );

                if (esMismoArchivo)
                    {
                    MessageBox.Show(
                        "El Master \"" +
                        informacion.Name +
                        "\" ya está enlazado al producto \"" +
                        producto.Nombre +
                        "\".",
                        "Master configurado",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );

                    return;
                    }

                if (masterPerteneceAlProducto &&
                    !ConfirmarReemplazoMaster(producto, informacion.Name))
                    {
                    return;
                    }

                ArchivoMaster master =
                    masterPerteneceAlProducto
                        ? archivoMasterActual
                        : new ArchivoMaster();

                master.ProductoId = producto.Id;
                master.RutaArchivo = informacion.FullName;
                master.NombreArchivo = informacion.Name;
                master.EsPrincipal = true;
                master.Activo = true;

                if (!esMismoArchivo)
                    {
                    master.HashArchivo = string.Empty;
                    master.TamanoArchivo = informacion.Length;
                    master.FechaModificacion = informacion.LastWriteTimeUtc;
                    master.FechaUltimoAnalisis = null;
                    master.CantidadMoldes = 0;
                    }

                master.Id = archivoMasterRepositorio.GuardarPrincipal(master);

                archivoMasterActual = master;
                moldesAnalizados.Clear();
                tamanoMasterAnalizado = 0;
                fechaMasterAnalizadoUtc = DateTime.MinValue;

                CargarArchivoMasterDelProducto();

                MessageBox.Show(
                    "El Master \"" +
                    informacion.Name +
                    "\" quedó enlazado al producto \"" +
                    producto.Nombre +
                    "\".\n\nEstado: pendiente de análisis.",
                    "Master configurado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                }
            catch (Exception ex)
                {
                MessageBox.Show(
                    "No se pudo guardar el archivo Master.\n\n" +
                    ex.Message,
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                }
            }
        private bool ConfirmarReemplazoMaster(Producto producto, string nombreArchivoNuevo)
            {
            string nombreArchivoActual =
                archivoMasterActual == null
                    ? string.Empty
                    : archivoMasterActual.NombreArchivo;

            DialogResult respuesta = MessageBox.Show(
                "El producto \"" +
                producto.Nombre +
                "\" ya tiene un Master configurado:\n\n" +
                nombreArchivoActual +
                "\n\nSe reemplazará por:\n\n" +
                nombreArchivoNuevo +
                "\n\nEl nuevo archivo quedará pendiente de análisis. ¿Desea continuar?",
                "Reemplazar Master",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2
            );

            return respuesta == DialogResult.Yes;
            }
        private void btnAbrirUbicacionMaster_Click(object sender, EventArgs e)
            {
            if (archivoMasterActual == null ||
                !File.Exists(archivoMasterActual.RutaArchivo))
                {
                MessageBox.Show(
                    "No se encontró el archivo Master.",
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                return;
                }

            Process.Start(
                "explorer.exe",
                "/select,\"" +
                archivoMasterActual.RutaArchivo +
                "\""
            );
            }
        private void btnAnalizarMaster_Click(object sender, EventArgs e)
            {
            if (archivoMasterActual == null ||
                !File.Exists(archivoMasterActual.RutaArchivo))
                {
                MessageBox.Show(
                    "Seleccione primero un archivo Master válido.",
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                return;
                }

            try
                {
                btnAnalizarMaster.Enabled = false;
                btnSincronizarMaster.Enabled = false;

                prgAnalisisMaster.Visible = true;
                prgAnalisisMaster.Value = 10;

                lblEstadoMaster.Text = "Analizando...";
                lblEstadoMaster.ForeColor =
                    System.Drawing.Color.FromArgb(44, 49, 82);

                lblProcesoMaster.Text =
                    "Abriendo el archivo Master en CorelDRAW.";

                System.Windows.Forms.Application.DoEvents();

                VGCore.Application corel = ObtenerCorel();

                prgAnalisisMaster.Value = 30;

                List<Molde> encontrados =
                    analizadorMaster.Analizar(
                        corel,
                        archivoMasterActual.RutaArchivo
                    );

                FileInfo archivoAnalizado = new FileInfo(archivoMasterActual.RutaArchivo);

                tamanoMasterAnalizado = archivoAnalizado.Length;
                fechaMasterAnalizadoUtc = archivoAnalizado.LastWriteTimeUtc;
                prgAnalisisMaster.Value = 65;

                List<Molde> guardados =
                    moldeRepositorio.ListarPorMaster(
                        archivoMasterActual.Id
                    );

                CompararCatalogos(
                    encontrados,
                    guardados
                );

                moldesAnalizados = encontrados;

                MostrarCatalogoAnalizado(
                    encontrados,
                    guardados
                );

                prgAnalisisMaster.Value = 100;

                FinalizarAnalisisVisual(encontrados);
                }
            catch (Exception ex)
                {
                prgAnalisisMaster.Value = 0;

                lblEstadoMaster.Text = "Error de análisis";
                lblEstadoMaster.ForeColor =
                    System.Drawing.Color.FromArgb(214, 69, 69);

                lblProcesoMaster.Text =
                    "No se pudo analizar el archivo Master.";

                MessageBox.Show(
                    "No se pudo analizar el Master.\n\n" +
                    "Tipo: " + ex.GetType().FullName + "\n" +
                    "Código: 0x" + ex.HResult.ToString("X8") +
                    "\n\n" +
                    ex.Message,
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                }
            finally
                {
                btnAnalizarMaster.Enabled =
                    archivoMasterActual != null &&
                    File.Exists(
                        archivoMasterActual.RutaArchivo
                    );
                }
            }
        private void CompararCatalogos(List<Molde> encontrados, List<Molde> guardados)
            {
            Dictionary<string, Molde> catalogoGuardado =
                new Dictionary<string, Molde>(
                    StringComparer.OrdinalIgnoreCase
                );

            foreach (Molde guardado in guardados)
                {
                if (!catalogoGuardado.ContainsKey(
                    guardado.Codigo))
                    {
                    catalogoGuardado.Add(
                        guardado.Codigo,
                        guardado
                    );
                    }
                }

            foreach (Molde encontrado in encontrados)
                {
                if (encontrado.Estado == "Inválido" ||
                    encontrado.Estado == "Duplicado")
                    {
                    continue;
                    }

                Molde anterior;

                if (!catalogoGuardado.TryGetValue(
                    encontrado.Codigo,
                    out anterior))
                    {
                    encontrado.Estado = "Nuevo";
                    continue;
                    }

                encontrado.Estado =
                    MismosDatosMolde(encontrado, anterior)
                        ? "Correcto"
                        : "Modificado";
                }
            }
        private bool MismosDatosMolde(Molde actual, Molde anterior)
            {
            return TextoIgual(actual.NombreObjeto, anterior.NombreObjeto) &&
                   TextoIgual(actual.Pieza, anterior.Pieza) &&
                   TextoIgual(actual.Talla, anterior.Talla) &&
                   TextoIgual(actual.Corte, anterior.Corte) &&
                   TextoIgual(actual.Manga, anterior.Manga) &&
                   TextoIgual(actual.Cuello, anterior.Cuello) &&
                   actual.Pagina == anterior.Pagina &&
                   TextoIgual(actual.Capa, anterior.Capa);
            }
        private bool TextoIgual(string valor1, string valor2)
            {
            return string.Equals(
                (valor1 ?? string.Empty).Trim(),
                (valor2 ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase
            );
            }
        private void MostrarCatalogoAnalizado(List<Molde> encontrados, List<Molde> guardados)
            {
            Producto producto = ObtenerProductoSeleccionado();

            ConfigurarColumnasCatalogo(producto);

            dgvCatalogoMoldes.Rows.Clear();
            dgvCatalogoMoldes.Visible = true;
            dgvCatalogoMoldes.ColumnHeadersVisible = true;

            txtBuscarMolde.Enabled = true;
            cboEstadoMolde.Enabled = true;

            HashSet<string> codigosEncontrados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Molde molde in encontrados)
                {
                codigosEncontrados.Add(molde.Codigo);
                AgregarFilaCatalogo(molde);
                }

            foreach (Molde guardado in guardados)
                {
                if (codigosEncontrados.Contains(guardado.Codigo))
                    continue;

                guardado.Estado = "Eliminado";
                guardado.Observacion = "Este molde estaba registrado, pero ya no existe.";

                AgregarFilaCatalogo(guardado);
                }

            lblResumenCatalogo.Text = encontrados.Count + " moldes encontrados";
            AplicarFiltrosCatalogo();
            }
        private void AgregarFilaCatalogo(Molde molde)
            {
            int indiceFila =
                dgvCatalogoMoldes.Rows.Add(
                    molde.Estado,
                    molde.Codigo,
                    molde.Pieza,
                    molde.Talla,
                    molde.Corte,
                    molde.Manga,
                    molde.Cuello,
                    molde.Pagina,
                    molde.Capa
                );

            DataGridViewRow fila =
                dgvCatalogoMoldes.Rows[indiceFila];

            fila.Tag = molde;

            fila.Cells[0].ToolTipText =
                molde.Observacion ?? string.Empty;

            switch (molde.Estado)
                {
                case "Correcto":
                    fila.Cells[0].Style.ForeColor =
                        System.Drawing.Color.FromArgb(30, 150, 70);
                    break;

                case "Nuevo":
                    fila.Cells[0].Style.ForeColor =
                        System.Drawing.Color.FromArgb(20, 90, 210);
                    break;

                case "Modificado":
                    fila.Cells[0].Style.ForeColor =
                        System.Drawing.Color.FromArgb(220, 145, 0);
                    break;

                case "Eliminado":
                case "Duplicado":
                case "Inválido":
                    fila.Cells[0].Style.ForeColor =
                        System.Drawing.Color.FromArgb(214, 69, 69);
                    break;
                }
            }
        private void FinalizarAnalisisVisual(List<Molde> encontrados)
            {
            int invalidos = 0;
            int duplicados = 0;

            StringBuilder advertencias =
                new StringBuilder();

            foreach (Molde molde in encontrados)
                {
                if (molde.Estado == "Inválido")
                    {
                    invalidos++;

                    advertencias.AppendLine(
                        "• " +
                        molde.NombreObjeto +
                        ": " +
                        molde.Observacion
                    );
                    }

                if (molde.Estado == "Duplicado")
                    {
                    duplicados++;

                    advertencias.AppendLine(
                        "• Código duplicado: " +
                        molde.Codigo
                    );
                    }
                }

            ValidarPiezasRequeridas(
                encontrados,
                advertencias
            );

            bool existenErrores =
                invalidos > 0 ||
                duplicados > 0 ||
                advertencias.Length > 0;

            if (existenErrores)
                {
                lblEstadoMaster.Text =
                    "Análisis con advertencias";

                lblEstadoMaster.ForeColor =
                    System.Drawing.Color.FromArgb(220, 145, 0);

                lblProcesoMaster.Text =
                    "Revise los elementos marcados.";

                btnSincronizarMaster.Enabled = false;

                MessageBox.Show(
                    "El análisis terminó con advertencias:\n\n" +
                    advertencias,
                    "Revisar Master",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                }
            else
                {
                lblEstadoMaster.Text =
                    "Pendiente de sincronización";

                lblEstadoMaster.ForeColor =
                    System.Drawing.Color.FromArgb(20, 90, 210);

                lblProcesoMaster.Text =
                    encontrados.Count +
                    " moldes listos para sincronizar.";

                btnSincronizarMaster.Enabled =
                    encontrados.Count > 0;
                }
            }
        private void ValidarPiezasRequeridas(List<Molde> moldes, StringBuilder advertencias)
            {
            Producto producto = ObtenerProductoSeleccionado();
            string codigoProducto =
                producto == null
                    ? string.Empty
                    : (producto.Codigo ?? string.Empty).Trim().ToUpperInvariant();

            if (codigoProducto == "CAMISETAS")
                {
                ValidarPiezasRequeridasCamisetas(moldes, advertencias);
                return;
                }

            if (codigoProducto == "FUNDAS" || codigoProducto.Contains("FUNDA"))
                {
                ValidarPiezasRequeridasFundas(moldes, advertencias);
                }
            }
        private void ValidarPiezasRequeridasFundas(List<Molde> moldes, StringBuilder advertencias)
            {
            string[] tallas = { "S", "M", "L" };
            string[] piezas =
                {
                "Frente",
                "Espalda",
                "Lateral izquierdo",
                "Lateral derecho"
                };

            foreach (string talla in tallas)
                {
                foreach (string pieza in piezas)
                    {
                    if (!ExisteMoldeValido(moldes, talla, pieza, string.Empty))
                        {
                        advertencias.AppendLine(
                            "• Falta " +
                            pieza +
                            " de la talla " +
                            talla +
                            "."
                        );
                        }
                    }
                }
            }
        private void ValidarPiezasRequeridasCamisetas(List<Molde> moldes, StringBuilder advertencias)
            {
            Dictionary<string, List<Molde>> grupos =
                new Dictionary<string, List<Molde>>(StringComparer.OrdinalIgnoreCase);

            foreach (Molde molde in moldes)
                {
                if (molde.Estado == "Inválido" ||
                    molde.Estado == "Duplicado" ||
                    string.IsNullOrWhiteSpace(molde.Codigo))
                    {
                    continue;
                    }

                int separador = molde.Codigo.IndexOf("__", StringComparison.Ordinal);

                if (separador <= 0)
                    continue;

                string codigoGrupo = molde.Codigo.Substring(0, separador);

                if (!grupos.ContainsKey(codigoGrupo))
                    grupos.Add(codigoGrupo, new List<Molde>());

                grupos[codigoGrupo].Add(molde);
                }

            foreach (KeyValuePair<string, List<Molde>> grupo in grupos)
                {
                bool tieneFrente = ExistePiezaValida(grupo.Value, "Frente", string.Empty);
                bool tieneEspalda = ExistePiezaValida(grupo.Value, "Espalda", string.Empty);

                if (!tieneFrente)
                    advertencias.AppendLine("• Falta Frente en el grupo " + grupo.Key + ".");

                if (!tieneEspalda)
                    advertencias.AppendLine("• Falta Espalda en el grupo " + grupo.Key + ".");

                bool requiereMangas =
                    grupo.Key.Contains("_clasico_") ||
                    grupo.Key.Contains("_raglan_");

                bool cortaIzquierda = ExistePiezaValida(grupo.Value, "Manga izquierda", "Corta");
                bool cortaDerecha = ExistePiezaValida(grupo.Value, "Manga derecha", "Corta");
                bool largaIzquierda = ExistePiezaValida(grupo.Value, "Manga izquierda", "Larga");
                bool largaDerecha = ExistePiezaValida(grupo.Value, "Manga derecha", "Larga");

                if (cortaIzquierda != cortaDerecha)
                    advertencias.AppendLine("• El grupo " + grupo.Key + " tiene incompleto el par de mangas cortas.");

                if (largaIzquierda != largaDerecha)
                    advertencias.AppendLine("• El grupo " + grupo.Key + " tiene incompleto el par de mangas largas.");

                if (requiereMangas &&
                    !cortaIzquierda &&
                    !cortaDerecha &&
                    !largaIzquierda &&
                    !largaDerecha)
                    {
                    advertencias.AppendLine("• Falta al menos un par de mangas en el grupo " + grupo.Key + ".");
                    }
                }
            }
        private bool ExisteMoldeValido(List<Molde> moldes, string talla, string pieza, string manga)
            {
            foreach (Molde molde in moldes)
                {
                if (molde.Estado == "Inválido" || molde.Estado == "Duplicado")
                    continue;

                if (TextoIgual(molde.Talla, talla) &&
                    TextoIgual(molde.Pieza, pieza) &&
                    (string.IsNullOrWhiteSpace(manga) || TextoIgual(molde.Manga, manga)))
                    {
                    return true;
                    }
                }

            return false;
            }
        private bool ExistePiezaValida(List<Molde> moldes, string pieza, string manga)
            {
            foreach (Molde molde in moldes)
                {
                if (molde.Estado == "Inválido" || molde.Estado == "Duplicado")
                    continue;

                if (TextoIgual(molde.Pieza, pieza) &&
                    (string.IsNullOrWhiteSpace(manga) || TextoIgual(molde.Manga, manga)))
                    {
                    return true;
                    }
                }

            return false;
            }
        private void ConfigurarColumnasCatalogo(Producto producto)
            {
            string codigoProducto = producto == null
                ? string.Empty
                : (producto.Codigo ?? string.Empty).Trim().ToUpperInvariant();

            bool esCamiseta = codigoProducto == "CAMISETAS";
            bool esFunda = codigoProducto == "FUNDAS" ||
                           codigoProducto.Contains("FUNDA");

            colEstadoMolde.Visible = true;
            colCodigoMolde.Visible = true;
            colPiezaMolde.Visible = true;
            colTallaMolde.Visible = true;
            colPaginaMolde.Visible = true;
            colCapaMolde.Visible = true;

            colCorteMolde.Visible = esCamiseta;
            colMangaMolde.Visible = esCamiseta;
            colCuelloMolde.Visible = esCamiseta;

            dgvCatalogoMoldes.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            if (esCamiseta)
                {
                colEstadoMolde.FillWeight = 75;
                colCodigoMolde.FillWeight = 180;
                colPiezaMolde.FillWeight = 100;
                colTallaMolde.FillWeight = 55;
                colCorteMolde.FillWeight = 75;
                colMangaMolde.FillWeight = 75;
                colCuelloMolde.FillWeight = 80;
                colPaginaMolde.FillWeight = 60;
                colCapaMolde.FillWeight = 80;
                }
            else
                {
                // Distribución para Fundas.
                colEstadoMolde.FillWeight = 70;
                colCodigoMolde.FillWeight = 210;
                colPiezaMolde.FillWeight = 130;
                colTallaMolde.FillWeight = 60;
                colPaginaMolde.FillWeight = 60;
                colCapaMolde.FillWeight = 90;
                }
            }
        private void btnSincronizarMaster_Click(object sender, EventArgs e)
            {
            if (archivoMasterActual == null || !File.Exists(archivoMasterActual.RutaArchivo))
                {
                MessageBox.Show(
                    "No existe un archivo Master válido.",
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                return;
                }

            if (moldesAnalizados == null || moldesAnalizados.Count == 0)
                {
                MessageBox.Show(
                    "Primero debe analizar el archivo Master.",
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return;
                }

            string errorValidacion = ValidarCatalogoAntesDeSincronizar();

            if (!string.IsNullOrWhiteSpace(errorValidacion))
                {
                MessageBox.Show(
                    "El catálogo no puede sincronizarse:\n\n" + errorValidacion,
                    "Revisar catálogo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                return;
                }

            FileInfo archivoActual = new FileInfo(archivoMasterActual.RutaArchivo);

            bool cambioDespuesDelAnalisis =
                archivoActual.Length != tamanoMasterAnalizado ||
                Math.Abs((archivoActual.LastWriteTimeUtc - fechaMasterAnalizadoUtc).TotalSeconds) > 2;

            if (cambioDespuesDelAnalisis)
                {
                MessageBox.Show(
                    "El archivo Master cambió después del análisis.\n\nVuelva a analizarlo antes de sincronizar.",
                    "Master modificado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                btnSincronizarMaster.Enabled = false;
                lblEstadoMaster.Text = "Requiere nuevo análisis";
                return;
                }

            DialogResult respuesta = MessageBox.Show(
                "Se guardarán " + moldesAnalizados.Count + " moldes para el producto seleccionado.\n\n¿Desea continuar?",
                "Sincronizar catálogo",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2
            );

            if (respuesta != DialogResult.Yes)
                return;

            try
                {
                btnAnalizarMaster.Enabled = false;
                btnSincronizarMaster.Enabled = false;

                prgAnalisisMaster.Visible = true;
                prgAnalisisMaster.Value = 20;

                lblEstadoMaster.Text = "Sincronizando...";
                lblProcesoMaster.Text = "Guardando el catálogo de moldes.";

                System.Windows.Forms.Application.DoEvents();

                moldeRepositorio.ReemplazarCatalogo(archivoMasterActual.Id, moldesAnalizados);

                prgAnalisisMaster.Value = 65;

                string hashArchivo = CalcularHashArchivo(archivoMasterActual.RutaArchivo);
                DateTime fechaSincronizacion = DateTime.Now;

                archivoMasterRepositorio.ActualizarDatosAnalisis(
                    archivoMasterActual.Id,
                    hashArchivo,
                    archivoActual.Length,
                    archivoActual.LastWriteTimeUtc,
                    fechaSincronizacion,
                    moldesAnalizados.Count
                );

                foreach (Molde molde in moldesAnalizados)
                    {
                    molde.Estado = "Correcto";
                    molde.Observacion = "Molde sincronizado correctamente.";
                    molde.FechaAnalisis = fechaSincronizacion;
                    }

                archivoMasterActual.HashArchivo = hashArchivo;
                archivoMasterActual.TamanoArchivo = archivoActual.Length;
                archivoMasterActual.FechaModificacion = archivoActual.LastWriteTimeUtc;
                archivoMasterActual.FechaUltimoAnalisis = fechaSincronizacion;
                archivoMasterActual.CantidadMoldes = moldesAnalizados.Count;

                prgAnalisisMaster.Value = 100;

                MostrarCatalogoAnalizado(moldesAnalizados, new List<Molde>());

                lblEstadoMaster.Text = "Sincronizado";
                lblEstadoMaster.ForeColor = System.Drawing.Color.FromArgb(30, 160, 80);

                lblCantidadMoldesMaster.Text = moldesAnalizados.Count.ToString();
                lblUltimoAnalisisMaster.Text = fechaSincronizacion.ToString("dd/MM/yyyy HH:mm");
                lblProcesoMaster.Text = "El catálogo está actualizado.";

                MessageBox.Show(
                    "El catálogo se sincronizó correctamente.\n\nMoldes guardados: " + moldesAnalizados.Count,
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                }
            catch (Exception ex)
                {
                prgAnalisisMaster.Value = 0;

                lblEstadoMaster.Text = "Error de sincronización";
                lblEstadoMaster.ForeColor = System.Drawing.Color.FromArgb(214, 69, 69);
                lblProcesoMaster.Text = "No se pudo guardar el catálogo.";

                MessageBox.Show(
                    "No se pudo sincronizar el catálogo.\n\n" + ex.Message,
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                }
            finally
                {
                btnAnalizarMaster.Enabled =
                    archivoMasterActual != null &&
                    File.Exists(archivoMasterActual.RutaArchivo);
                }
            }
        private string ValidarCatalogoAntesDeSincronizar()
            {
            StringBuilder errores = new StringBuilder();

            foreach (Molde molde in moldesAnalizados)
                {
                if (molde.Estado == "Inválido" || molde.Estado == "Duplicado")
                    {
                    errores.AppendLine("• " + molde.Codigo + ": " + molde.Observacion);
                    }
                }

            ValidarPiezasRequeridas(moldesAnalizados, errores);

            return errores.ToString();
            }
        private string CalcularHashArchivo(string rutaArchivo)
            {
            using (SHA256 algoritmo = SHA256.Create())
            using (FileStream archivo = new FileStream(rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                byte[] hash = algoritmo.ComputeHash(archivo);
                StringBuilder resultado = new StringBuilder(hash.Length * 2);

                foreach (byte valor in hash)
                    {
                    resultado.Append(valor.ToString("x2"));
                    }

                return resultado.ToString();
                }
            }
        private void txtBuscarMolde_TextChanged(object sender, EventArgs e)
            {
            AplicarFiltrosCatalogo();
            }
        private void cboEstadoMolde_SelectedIndexChanged(object sender, EventArgs e)
            {
            AplicarFiltrosCatalogo();
            }
        private void AplicarFiltrosCatalogo()
            {
            if (dgvCatalogoMoldes.Rows.Count == 0)
                return;

            string busqueda = (txtBuscarMolde.Text ?? string.Empty).Trim().ToLowerInvariant();
            string estadoBuscado = ObtenerEstadoSeleccionado();

            dgvCatalogoMoldes.CurrentCell = null;

            int visibles = 0;

            foreach (DataGridViewRow fila in dgvCatalogoMoldes.Rows)
                {
                if (fila.IsNewRow)
                    continue;

                Molde molde = fila.Tag as Molde;

                if (molde == null)
                    continue;

                bool coincideEstado =
                    string.IsNullOrWhiteSpace(estadoBuscado) ||
                    TextoIgual(molde.Estado, estadoBuscado);

                bool coincideBusqueda =
                    string.IsNullOrWhiteSpace(busqueda) ||
                    ContieneTexto(molde.Codigo, busqueda) ||
                    ContieneTexto(molde.NombreObjeto, busqueda) ||
                    ContieneTexto(molde.Pieza, busqueda) ||
                    ContieneTexto(molde.Talla, busqueda) ||
                    ContieneTexto(molde.Capa, busqueda) ||
                    ContieneTexto(molde.Estado, busqueda);

                bool mostrar = coincideEstado && coincideBusqueda;

                fila.Visible = mostrar;

                if (mostrar)
                    visibles++;
                }

            lblResumenCatalogo.Text =
                visibles + " de " +
                dgvCatalogoMoldes.Rows.Count +
                " moldes";
            }
        private string ObtenerEstadoSeleccionado()
            {
            string seleccion = Convert.ToString(cboEstadoMolde.SelectedItem);

            switch (seleccion)
                {
                case "Correctos":
                    return "Correcto";

                case "Nuevos":
                    return "Nuevo";

                case "Modificados":
                    return "Modificado";

                case "Inválidos":
                    return "Inválido";

                case "Duplicados":
                    return "Duplicado";

                case "Eliminados":
                    return "Eliminado";

                default:
                    return string.Empty;
                }
            }
        private bool ContieneTexto(string valor, string busqueda)
            {
            if (string.IsNullOrWhiteSpace(valor))
                return false;

            return valor.ToLowerInvariant().Contains(busqueda);
            }
        }
    }
