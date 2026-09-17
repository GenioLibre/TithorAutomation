using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using TithorAutomation.Servicios;

namespace TithorAutomation
{
    public partial class frmPrincipal
    {
        private Panel pnlEscalar;
        private DataGridView tablaEscalar;
        private ComboBox piezaEscalar;
        private ComboBox tallaEscalar;
        private CheckBox reemplazarEscalar;
        private Label estadoEscalar;
        private Button analizarEscalar;
        private Button aplicarEscalar;
        private VGCore.Document documentoEscalar;
        private string firmaEscalar;
        private readonly EscaladorPowerClip escalador = new EscaladorPowerClip();

        private void InicializarEscalar()
        {
            pnlEscalar = new Panel { Name = "pnlEscalar", Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24, 76, 24, 20), Visible = false };
            var titulo = new Label { Text = "Escalar", Font = new Font("Segoe UI", 20.25F, FontStyle.Bold), AutoSize = true, Location = new Point(24, 18) };
            var contenido = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            contenido.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            contenido.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            contenido.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            contenido.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            contenido.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            contenido.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Analiza los moldes copiados. Selecciona tu diseño agrupado en CorelDRAW y elige la pieza de destino. Se ajustará a la altura, sin deformarse, y quedará centrado.", AutoEllipsis = true }, 0, 0);
            tablaEscalar = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false,
                BackgroundColor = Color.White, BorderStyle = BorderStyle.None, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false, EnableHeadersVisualStyles = false
            };
            tablaEscalar.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(44, 49, 82);
            tablaEscalar.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            tablaEscalar.ColumnHeadersHeight = 36;
            tablaEscalar.RowTemplate.Height = 30;
            tablaEscalar.Columns.Add("Pieza", "Pieza");
            tablaEscalar.Columns.Add("Talla", "Talla");
            tablaEscalar.Columns.Add("Cantidad", "Cantidad");
            tablaEscalar.Columns.Add("Contenido", "Con contenido");
            tablaEscalar.Columns.Add("Estado", "Estado");
            contenido.Controls.Add(tablaEscalar, 0, 1);
            var filtros = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
            piezaEscalar = new ComboBox { Width = 190, DropDownStyle = ComboBoxStyle.DropDownList };
            piezaEscalar.Items.AddRange(new object[] { "Frente", "Lateral derecho", "Lateral izquierdo", "Espalda" });
            piezaEscalar.SelectedIndex = 0;
            tallaEscalar = new ComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            tallaEscalar.Items.AddRange(new object[] { "Todas", "S", "M", "L" });
            tallaEscalar.SelectedIndex = 0;
            filtros.Controls.Add(new Label { Text = "Pieza", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
            filtros.Controls.Add(piezaEscalar);
            filtros.Controls.Add(new Label { Text = "Talla", AutoSize = true, Padding = new Padding(12, 5, 0, 0) });
            filtros.Controls.Add(tallaEscalar);
            contenido.Controls.Add(filtros, 0, 2);
            reemplazarEscalar = new CheckBox { Dock = DockStyle.Fill, Text = "Reemplazar contenido existente en los PowerClips elegidos", Checked = false };
            contenido.Controls.Add(reemplazarEscalar, 0, 3);
            var acciones = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            acciones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            acciones.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
            acciones.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            estadoEscalar = new Label { Dock = DockStyle.Fill, Text = "Analiza el documento para comenzar.", AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            analizarEscalar = CrearBotonEscalar("Analizar Documento", Color.FromArgb(44, 48, 82), Color.White);
            aplicarEscalar = CrearBotonEscalar("Aplicar diseño", Color.FromArgb(250, 216, 68), Color.Black);
            aplicarEscalar.Enabled = false;
            analizarEscalar.Click += AnalizarEscalar_Click;
            aplicarEscalar.Click += AplicarEscalar_Click;
            acciones.Controls.Add(estadoEscalar, 0, 0);
            acciones.Controls.Add(analizarEscalar, 1, 0);
            acciones.Controls.Add(aplicarEscalar, 2, 0);
            contenido.Controls.Add(acciones, 0, 4);
            pnlEscalar.Controls.Add(contenido);
            // El encabezado queda fuera del área con padding.
            var cabecera = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.White };
            cabecera.Controls.Add(titulo);
            pnlEscalar.Padding = new Padding(24, 0, 24, 20);
            pnlEscalar.Controls.Add(cabecera);
            pnlContenido.Controls.Add(pnlEscalar);
            btnEscalar.Enabled = true;
            btnEscalar.Click += (sender, e) => MostrarPanel(pnlEscalar);
        }

        private Button CrearBotonEscalar(string texto, Color fondo, Color frente)
        {
            var boton = new Button { Text = texto, Dock = DockStyle.Fill, Margin = new Padding(8, 4, 0, 4), FlatStyle = FlatStyle.Flat, BackColor = fondo, ForeColor = frente, UseVisualStyleBackColor = false, Cursor = Cursors.Hand };
            boton.FlatAppearance.BorderSize = 0;
            return boton;
        }

        private VGCore.Document DocumentoActivoEscalar()
        {
            if (vistaPreviaActiva) throw new InvalidOperationException("Restaura primero la vista previa de Pantonear.");
            VGCore.Application corel = ObtenerCorel();
            if (corel.Documents.Count == 0) throw new InvalidOperationException("Abre el documento con los moldes copiados en CorelDRAW.");
            return corel.ActiveDocument;
        }

        private static string FirmaEscalar(IEnumerable<PiezaEscalable> piezas)
        {
            return string.Join("|", piezas.Select(x => x.Clave + ":" + x.Pieza + ":" + x.Talla + ":" + x.Estado + ":" + x.TieneContenido + ":" +
                (x.Contenedor == null ? "" : x.Contenedor.StaticID + ":" + x.Contenedor.SizeWidth.ToString("R", CultureInfo.InvariantCulture) + ":" +
                x.Contenedor.SizeHeight.ToString("R", CultureInfo.InvariantCulture) + ":" + x.Contenedor.CenterX.ToString("R", CultureInfo.InvariantCulture) + ":" + x.Contenedor.CenterY.ToString("R", CultureInfo.InvariantCulture))).OrderBy(x => x));
        }

        private void MostrarAnalisisEscalar(List<PiezaEscalable> piezas)
        {
            tablaEscalar.Rows.Clear();
            foreach (var grupo in piezas.GroupBy(x => new { x.Pieza, x.Talla, x.Estado }).OrderBy(x => x.Key.Pieza).ThenBy(x => x.Key.Talla))
                tablaEscalar.Rows.Add(grupo.Key.Pieza, grupo.Key.Talla, grupo.Count(), grupo.Count(x => x.TieneContenido), grupo.Key.Estado);
            estadoEscalar.Text = piezas.Count == 0 ? "No se encontraron piezas en TITHOR_PRODUCCION." : piezas.Count + " piezas detectadas en " + documentoEscalar.Name;
            firmaEscalar = FirmaEscalar(piezas);
            aplicarEscalar.Enabled = piezas.Any(x => x.Estado == "Listo");
        }

        private void AnalizarEscalar_Click(object sender, EventArgs e)
        {
            try
            {
                aplicarEscalar.Enabled = false;
                firmaEscalar = null;
                tablaEscalar.Rows.Clear();
                documentoEscalar = DocumentoActivoEscalar();
                MostrarAnalisisEscalar(escalador.Analizar(documentoEscalar));
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Analizar moldes", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void AplicarEscalar_Click(object sender, EventArgs e)
        {
            try
            {
                VGCore.Document documento = DocumentoActivoEscalar();
                if (!EscaladorPowerClip.MismoDocumento(documento, documentoEscalar) || firmaEscalar == null)
                    throw new InvalidOperationException("El documento activo cambió. Vuelve a analizarlo.");
                var piezas = escalador.Analizar(documento);
                if (FirmaEscalar(piezas) != firmaEscalar)
                {
                    MostrarAnalisisEscalar(piezas);
                    throw new InvalidOperationException("Los moldes cambiaron. Se actualizó la lista; revisa los destinos y vuelve a aplicar.");
                }
                var seleccion = documento.SelectionRange;
                if (seleccion.Count != 1) throw new InvalidOperationException("Selecciona un único diseño agrupado en CorelDRAW.");
                VGCore.Shape diseno = seleccion[1];
                string pieza = (string)piezaEscalar.SelectedItem, talla = (string)tallaEscalar.SelectedItem;
                var destinos = piezas.Where(x => x.Pieza == pieza && (talla == "Todas" || x.Talla == talla)).ToList();
                escalador.Validar(diseno, destinos, reemplazarEscalar.Checked);
                string aviso = "Se aplicará el diseño a " + destinos.Count + " piezas de " + pieza + " (talla: " + talla + ").\n\n" +
                    "Ajuste proporcional a la altura y centrado. El diseño original se conserva.";
                if (destinos.Any(x => x.TieneContenido)) aviso += "\n\nSe reemplazará el contenido de " + destinos.Count(x => x.TieneContenido) + " PowerClips.";
                if (MessageBox.Show(this, aviso, "Aplicar diseño", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                if (!EscaladorPowerClip.MismoDocumento(DocumentoActivoEscalar(), documento) || FirmaEscalar(escalador.Analizar(documento)) != firmaEscalar)
                    throw new InvalidOperationException("El documento cambió durante la confirmación. Analízalo de nuevo.");
                analizarEscalar.Enabled = aplicarEscalar.Enabled = false;
                int total = escalador.Aplicar(documento, diseno, destinos, reemplazarEscalar.Checked);
                MostrarAnalisisEscalar(escalador.Analizar(documento));
                estadoEscalar.Text = "Diseño aplicado a " + total + " piezas. Ctrl+Z deshace la operación.";
            }
            catch (Exception ex)
            {
                firmaEscalar = null;
                aplicarEscalar.Enabled = false;
                estadoEscalar.Text = "Revisa el mensaje y vuelve a analizar.";
                MessageBox.Show(this, ex.Message, "Escalar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { analizarEscalar.Enabled = true; }
        }
    }
}
