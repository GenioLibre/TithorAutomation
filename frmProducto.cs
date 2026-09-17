using System;
using System.Windows.Forms;
using TithorAutomation.Datos;
using TithorAutomation.Modelos;

namespace TithorAutomation
{
    public partial class frmProducto : Form
    {
        private readonly ProductoRepositorio productoRepositorio;
        private Producto productoActual;
        private bool codigoModificadoManualmente;
        private bool cargandoDatos;

        public Producto ProductoGuardado { get; private set; }

        // Constructor para agregar un producto
        public frmProducto()
        {
            InitializeComponent();

            productoRepositorio = new ProductoRepositorio();
            productoActual = null;

            ConfigurarFormularioNuevo();
        }

        // Constructor para editar un producto existente
        public frmProducto(Producto producto) : this()
        {
            if (producto == null)
                throw new ArgumentNullException(nameof(producto));

            productoActual = producto;
            CargarProducto();
        }

        private void ConfigurarFormularioNuevo()
        {
            Text = "Agregar producto";
            lblTituloProducto.Text = "AGREGAR PRODUCTO";

            txtNombreProducto.Clear();
            txtCodigoProducto.Clear();
            txtDescripcionProducto.Clear();

            chkProductoActivo.Checked = true;
            codigoModificadoManualmente = false;

            txtNombreProducto.Focus();
        }

        private void CargarProducto()
        {
            cargandoDatos = true;

            Text = "Editar producto";
            lblTituloProducto.Text = "EDITAR PRODUCTO";

            txtNombreProducto.Text = productoActual.Nombre;
            txtCodigoProducto.Text = productoActual.Codigo;
            txtDescripcionProducto.Text = productoActual.Descripcion;
            chkProductoActivo.Checked = productoActual.Activo;

            codigoModificadoManualmente = true;
            cargandoDatos = false;
        }

        private void txtNombreProducto_TextChanged(object sender, EventArgs e)
        {
            if (cargandoDatos || codigoModificadoManualmente)
                return;

            txtCodigoProducto.Text =
                ProductoRepositorio.NormalizarCodigo(txtNombreProducto.Text);
        }

        private void txtCodigoProducto_TextChanged(object sender, EventArgs e)
        {
            if (cargandoDatos)
                return;

            // Si el usuario escribe directamente en el código,
            // dejamos de reemplazarlo automáticamente.
            if (txtCodigoProducto.Focused)
                codigoModificadoManualmente = true;
        }

        private void btnGuardarProducto_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ValidarFormulario())
                    return;

                btnGuardarProducto.Enabled = false;

                if (productoActual == null)
                {
                    Producto nuevoProducto = new Producto
                    {
                        Nombre = txtNombreProducto.Text.Trim(),
                        Codigo = ProductoRepositorio.NormalizarCodigo(
                            txtCodigoProducto.Text
                        ),
                        Descripcion = txtDescripcionProducto.Text.Trim(),
                        Activo = chkProductoActivo.Checked
                    };

                    int nuevoId = productoRepositorio.Agregar(nuevoProducto);

                    nuevoProducto.Id = nuevoId;
                    ProductoGuardado = nuevoProducto;
                }
                else
                {
                    productoActual.Nombre =
                        txtNombreProducto.Text.Trim();

                    productoActual.Codigo =
                        ProductoRepositorio.NormalizarCodigo(
                            txtCodigoProducto.Text
                        );

                    productoActual.Descripcion =
                        txtDescripcionProducto.Text.Trim();

                    productoActual.Activo =
                        chkProductoActivo.Checked;

                    productoRepositorio.Actualizar(productoActual);
                    ProductoGuardado = productoActual;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Validación",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                btnGuardarProducto.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No se pudo guardar el producto.\n\n" +
                    ex.Message,
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                btnGuardarProducto.Enabled = true;
            }
        }

        private bool ValidarFormulario()
        {
            string nombre = txtNombreProducto.Text.Trim();
            string codigo = ProductoRepositorio.NormalizarCodigo(
                txtCodigoProducto.Text
            );

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MessageBox.Show(
                    "Ingrese el nombre del producto.",
                    "Datos incompletos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                txtNombreProducto.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(codigo))
            {
                MessageBox.Show(
                    "Ingrese un código interno para el producto.",
                    "Datos incompletos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                txtCodigoProducto.Focus();
                return false;
            }

            int idExcluido = productoActual == null
                ? 0
                : productoActual.Id;

            if (productoRepositorio.ExisteCodigo(codigo, idExcluido))
            {
                MessageBox.Show(
                    "Ya existe otro producto con el código \"" +
                    codigo + "\".",
                    "Código duplicado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                txtCodigoProducto.Focus();
                txtCodigoProducto.SelectAll();
                return false;
            }

            txtCodigoProducto.Text = codigo;
            return true;
        }

        private void btnCancelarProducto_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        private void lblNombreProducto_Click(object sender, EventArgs e)
        {
            // El label no necesita realizar ninguna acción.
        }

        private void txtDescripcionProducto_TextChanged(object sender, EventArgs e)
        {
        }

        private void frmProducto_Load(object sender, EventArgs e)
            {

            }
        }
}