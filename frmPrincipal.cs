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
    public partial class frmPrincipal : Form
        {
        private readonly List<IPlanificadorProducto> planificadoresProducto = new List<IPlanificadorProducto> { new PlanificadorFundas() };
        private readonly CopiadorMoldesCorel copiadorMoldesCorel = new CopiadorMoldesCorel();
        private PlanProduccion planProduccionActual;
        private readonly List<ILectorPedidoProducto> lectoresPedido = new List<ILectorPedidoProducto> {
            new LectorPedidoFundas()
        };
        private ResultadoAnalisisPedido resultadoPedidoActual;
        private bool pedidoAprobado;
        private long tamanoMasterAnalizado;
        private DateTime fechaMasterAnalizadoUtc;
        private readonly MoldeRepositorio moldeRepositorio = new MoldeRepositorio();
        private readonly AnalizadorMasterCorel analizadorMaster = new AnalizadorMasterCorel();
        private List<Molde> moldesAnalizados = new List<Molde>();
        private readonly ArchivoMasterRepositorio archivoMasterRepositorio = new ArchivoMasterRepositorio();
        private ArchivoMaster archivoMasterActual;
        private VGCore.Application corelApp;
        private readonly Dictionary<string, ColorDetectado> coloresDetectados = new Dictionary<string, ColorDetectado>();
        private int objetosAnalizados;
        private int degradadosEncontrados;
        private int objetosIgnorados;
        private readonly ProductoRepositorio productoRepositorio = new ProductoRepositorio();
        private int gruposComplejosEncontrados;
        private readonly List<string> nombresGruposComplejos = new List<string>();
        private readonly List<VGCore.Shape> objetosParaCambios = new List<VGCore.Shape>();
        private bool vistaPreviaActiva = false;
        private VGCore.Document documentoVistaPrevia = null;
        private void TemporizadorCorel_Tick(object sender, EventArgs e)
            {
            ActualizarEstadoCorel();
            }
        private class ColorDetectado
            {
            public int C { get; set; }
            public int M { get; set; }
            public int Y { get; set; }
            public int K { get; set; }

            public bool TieneTrazo { get; set; }

            public int Apariciones { get; set; }

            public bool TieneUniforme { get; set; }
            public bool TieneDegradado { get; set; }

            public List<VGCore.Color> Referencias { get; } =
                new List<VGCore.Color>();

            public string Clave
                {
                get
                    {
                    return $"C{C}_M{M}_Y{Y}_K{K}";
                    }
                }

            public string Tipo
                {
                get
                    {
                    List<string> tipos = new List<string>();

                    if (TieneUniforme)
                        tipos.Add("Relleno uniforme");

                    if (TieneDegradado)
                        tipos.Add("Degradado");

                    if (TieneTrazo)
                        tipos.Add("Trazo");

                    return string.Join(" / ", tipos);
                    }
                }
            }
        public frmPrincipal()
            {
            InitializeComponent();
            ConfigurarFiltrosCatalogo();
            CargarProductosConfiguracion();

            ConfigurarTabla();
            AplicarEstiloGridProduccion();

            btnProduccion.Enabled = true;
            btnProduccion.Visible = true;

            tmrConexionCorel.Interval = 1000;
            tmrConexionCorel.Start();

            ActualizarEstadoCorel();

            

            dgvCatalogoMoldes.ColumnHeadersHeightSizeMode =
            DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            dgvCatalogoMoldes.ColumnHeadersHeight = 30;

            dgvCatalogoMoldes.ColumnHeadersDefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleLeft;

            dgvCatalogoMoldes.ColumnHeadersDefaultCellStyle.Padding =
                new System.Windows.Forms.Padding(6, 0, 6, 0);

            dgvCatalogoMoldes.ColumnHeadersDefaultCellStyle.WrapMode =
                DataGridViewTriState.False;

            btnVistaPrevia.Enabled = false;
            btnAplicar.Enabled = false;

            prgProceso.Minimum = 0;
            prgProceso.Maximum = 100;
            prgProceso.Value = 0;

            lblEstadoProceso.Text = "Listo para analizar";

            pnlContenido.Visible = true;
            MostrarPanel(pnlPantonear);
            pnlEstadoCorelGlobal.BringToFront();
            CargarProductosProduccion();
            LimpiarPedidoProduccion(true);
            }
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
        private void ConfigurarTabla()
            {
            // Validación de los valores CMYK editables
            colCNuevo.ValueType = typeof(int);
            colMNuevo.ValueType = typeof(int);
            colYNuevo.ValueType = typeof(int);
            colKNuevo.ValueType = typeof(int);

            colCNuevo.MaxInputLength = 3;
            colMNuevo.MaxInputLength = 3;
            colYNuevo.MaxInputLength = 3;
            colKNuevo.MaxInputLength = 3;

            // Columnas que el usuario no puede modificar
            colMuestra.ReadOnly = true;
            colCmykOriginal.ReadOnly = true;
            colApariciones.ReadOnly = true;
            colTipo.ReadOnly = true;

            // Comportamiento general
            dgvColores.AllowUserToAddRows = false;
            dgvColores.AllowUserToDeleteRows = false;
            dgvColores.AllowUserToResizeRows = false;

            dgvColores.RowHeadersVisible = false;
            dgvColores.MultiSelect = false;

            dgvColores.SelectionMode =
                DataGridViewSelectionMode.FullRowSelect;

            dgvColores.Anchor =
                AnchorStyles.Top |
                AnchorStyles.Bottom |
                AnchorStyles.Left |
                AnchorStyles.Right;

            // Todas las columnas se adaptan proporcionalmente
            dgvColores.AutoSizeColumnsMode =
                DataGridViewAutoSizeColumnsMode.Fill;

            ConfigurarColumnaPantone(
                colMuestra,
                "Muestra",
                8F,
                85
            );

            ConfigurarColumnaPantone(
                colCmykOriginal,
                "CMYK original",
                18F,
                130
            );

            ConfigurarColumnaPantone(
                colCNuevo,
                "C",
                8F,
                65
            );

            ConfigurarColumnaPantone(
                colMNuevo,
                "M",
                8F,
                65
            );

            ConfigurarColumnaPantone(
                colYNuevo,
                "Y",
                8F,
                65
            );

            ConfigurarColumnaPantone(
                colKNuevo,
                "K",
                8F,
                65
            );

            ConfigurarColumnaPantone(
                colApariciones,
                "Apariciones",
                12F,
                100
            );

            ConfigurarColumnaPantone(
                colTipo,
                "Tipo",
                30F,
                140
            );

            // Encabezados
            dgvColores.EnableHeadersVisualStyles = false;

            dgvColores.ColumnHeadersHeightSizeMode =
                DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            dgvColores.ColumnHeadersHeight = 42;

            dgvColores.ColumnHeadersDefaultCellStyle.WrapMode =
                DataGridViewTriState.False;

            dgvColores.ColumnHeadersDefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleLeft;

            dgvColores.ColumnHeadersDefaultCellStyle.Padding =
                new System.Windows.Forms.Padding(6, 0, 6, 0);

            // Encabezados centrados
            colMuestra.HeaderCell.Style.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            colCNuevo.HeaderCell.Style.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            colMNuevo.HeaderCell.Style.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            colYNuevo.HeaderCell.Style.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            colKNuevo.HeaderCell.Style.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            colApariciones.HeaderCell.Style.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            // Contenido centrado
            colMuestra.DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            colCNuevo.DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            colMNuevo.DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            colYNuevo.DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            colKNuevo.DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            colApariciones.DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            // Apariencia de la muestra de color
            DataGridViewImageColumn columnaImagen =
                colMuestra as DataGridViewImageColumn;

            if (columnaImagen != null)
                {
                columnaImagen.ImageLayout =
                    DataGridViewImageCellLayout.Zoom;
                }
            }
        private void ConfigurarColumnaPantone(DataGridViewColumn columna, string titulo, float proporcion, int anchoMinimo)
            {
            columna.HeaderText = titulo;

            columna.AutoSizeMode =
                DataGridViewAutoSizeColumnMode.Fill;

            columna.FillWeight = proporcion;
            columna.MinimumWidth = anchoMinimo;

            columna.SortMode =
                DataGridViewColumnSortMode.NotSortable;
            }
        private void ActualizarEstadoCorel()
            {
            try
                {
                if (corelApp == null)
                    {
                    lblEstadoCorel.Text =
                        "● CorelDRAW desconectado";

                    lblEstadoCorel.ForeColor =
                        System.Drawing.Color.FromArgb(
                            217,
                            83,
                            79
                        );

                    lblDocumentoActivo.Text =
                        "Sin conexión";

                    return;
                    }

                int cantidadDocumentos =
                    corelApp.Documents.Count;

                lblEstadoCorel.Text =
                    "● CorelDRAW 2026 conectado";

                lblEstadoCorel.ForeColor =
                    System.Drawing.Color.FromArgb(
                        46,
                        166,
                        111
                    );

                if (cantidadDocumentos == 0)
                    {
                    lblDocumentoActivo.Text =
                        "Sin documento abierto";

                    lblDocumentoActivo.ForeColor =
                        System.Drawing.Color.FromArgb(
                            105,
                            112,
                            137
                        );

                    return;
                    }

                string nombreDocumento =
                    corelApp.ActiveDocument.Name;

                lblDocumentoActivo.Text =
                    "Documento: " + nombreDocumento;

                lblDocumentoActivo.ForeColor =
                    System.Drawing.Color.FromArgb(
                        44,
                        49,
                        82
                    );
                }
            catch
                {
                corelApp = null;

                lblEstadoCorel.Text =
                    "● CorelDRAW desconectado";

                lblEstadoCorel.ForeColor =
                    System.Drawing.Color.FromArgb(
                        217,
                        83,
                        79
                    );

                lblDocumentoActivo.Text =
                    "Sin documento abierto";

                lblDocumentoActivo.ForeColor =
                    System.Drawing.Color.FromArgb(
                        105,
                        112,
                        137
                    );
                }
            }
        private VGCore.Application ObtenerCorel()
            {
            try
                {
                if (corelApp != null)
                    {
                    int documentos = corelApp.Documents.Count;
                    return corelApp;
                    }
                }
            catch
                {
                corelApp = null;
                }

            Type tipoCorel = Type.GetTypeFromProgID(
                "CorelDRAW.Application.27",
                true
            );

            corelApp = (VGCore.Application)
                Activator.CreateInstance(tipoCorel);

            corelApp.Visible = true;

            return corelApp;
            }
        private void btnAnalizar_Click(object sender, EventArgs e)
            {
            if (vistaPreviaActiva)
                {
                RestaurarVistaPrevia();

                if (vistaPreviaActiva)
                    return;
                }

            ReiniciarAnalisis();

            prgProceso.Style = ProgressBarStyle.Marquee;
            lblEstadoProceso.Text = "Analizando selección...";

            try
                {
                VGCore.Application corel = ObtenerCorel();

                if (corel.Documents.Count == 0)
                    {
                    MessageBox.Show(
                        "No hay ningún documento abierto en CorelDRAW.",
                        "Tithor Automation",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    lblEstadoProceso.Text =
                        "No hay ningún documento abierto";

                    return;
                    }

                VGCore.ShapeRange seleccion =
                    corel.ActiveSelectionRange;

                if (seleccion == null || seleccion.Count == 0)
                    {
                    MessageBox.Show(
                        "Seleccione uno o más objetos en CorelDRAW.",
                        "Tithor Automation",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );

                    lblEstadoProceso.Text =
                        "No hay objetos seleccionados";

                    return;
                    }

                for (int i = 1; i <= seleccion.Count; i++)
                    {
                    AnalizarObjeto(seleccion[i]);
                    }

                MostrarColoresEnTabla();
                ActualizarContadores();
                MostrarAdvertenciaGruposComplejos();

                btnVistaPrevia.Enabled =
                    coloresDetectados.Count > 0;

                btnAplicar.Enabled =
                    coloresDetectados.Count > 0;

                lblEstadoProceso.Text =
                    $"Análisis terminado: " +
                    $"{coloresDetectados.Count} colores únicos, " +
                    $"{gruposComplejosEncontrados} grupos complejos";
                }
            catch (Exception ex)
                {
                lblEstadoProceso.Text =
                    "Error durante el análisis";

                MessageBox.Show(
                    $"Tipo: {ex.GetType().FullName}\n" +
                    $"Código: 0x{ex.HResult:X8}\n\n" +
                    ex.Message,
                    "Error de análisis",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                }
            finally
                {
                prgProceso.Style = ProgressBarStyle.Blocks;
                prgProceso.Value = 0;
                }
            }
        private void ReiniciarAnalisis()
            {
            LiberarMuestrasAnteriores();

            dgvColores.Rows.Clear();
            coloresDetectados.Clear();
            objetosParaCambios.Clear();
            nombresGruposComplejos.Clear();

            objetosAnalizados = 0;
            degradadosEncontrados = 0;
            objetosIgnorados = 0;
            gruposComplejosEncontrados = 0;

            lblObjetosCantidad.Text = "0";
            lblColoresCantidad.Text = "0";
            lblDegradadosCantidad.Text = "0";
            lblIgnoradosCantidad.Text = "0";

            btnVistaPrevia.Enabled = false;
            btnAplicar.Enabled = false;
            }
        private void AnalizarObjeto(VGCore.Shape objeto)
            {
            if (objeto == null)
                return;

            objetosAnalizados++;

            try
                {
                if (objeto.Type ==
                    VGCore.cdrShapeType.cdrBitmapShape)
                    {
                    objetosIgnorados++;
                    return;
                    }

                if (!objetosParaCambios.Contains(objeto))
                    {
                    objetosParaCambios.Add(objeto);
                    }

                if (objeto.Type ==
                    VGCore.cdrShapeType.cdrGroupShape)
                    {
                    // El grupo se analiza como un solo conjunto.
                    // No se recorren sus objetos internos.
                    AnalizarGrupoComoConjunto(objeto);
                    return;
                    }

                AnalizarRelleno(objeto);

                if (chkIncluirTrazos.Checked)
                    {
                    AnalizarTrazo(objeto);
                    }

                // Los PowerClips sí se recorren.
                AnalizarPowerClip(objeto);
                }
            catch
                {
                objetosIgnorados++;
                }
            }
        private void AnalizarPropiedadesGeneralesDelGrupo(VGCore.Shape grupo)
            {
            bool propiedadEncontrada = false;

            try
                {
                VGCore.Fill relleno = grupo.Fill;

                if (relleno != null &&
                    relleno.Type ==
                    VGCore.cdrFillType.cdrUniformFill)
                    {
                    RegistrarColor(
                        relleno.UniformColor,
                        false
                    );

                    propiedadEncontrada = true;
                    }
                }
            catch
                {
                }

            if (chkIncluirTrazos.Checked)
                {
                try
                    {
                    VGCore.Outline trazo =
                        grupo.Outline;

                    if (trazo != null &&
                        trazo.Type !=
                        VGCore.cdrOutlineType.cdrNoOutline)
                        {
                        RegistrarColor(
                            trazo.Color,
                            false,
                            true
                        );

                        propiedadEncontrada = true;
                        }
                    }
                catch
                    {
                    }
                }

            if (!propiedadEncontrada)
                {
                RegistrarGrupoComplejo(
                    grupo,
                    "CorelDRAW no devolvió una propiedad común editable"
                );
                }
            }
        private void AnalizarGrupoComoConjunto(VGCore.Shape grupo)
            {
            try
                {
                bool tieneRellenoComun = false;
                bool tieneTrazoComun = false;
                bool posibleGrupoMixto = false;

                // =====================================================
                // RELLENO GENERAL DEL GRUPO
                // =====================================================

                VGCore.Fill relleno = grupo.Fill;

                if (relleno != null)
                    {
                    if (relleno.Type ==
                        VGCore.cdrFillType.cdrUniformFill)
                        {
                        if (EsColorBlanco(
                            relleno.UniformColor))
                            {
                            // Corel suele mostrar blanco cuando
                            // un grupo contiene varios colores.
                            if (grupo.Shapes.Count > 1)
                                {
                                posibleGrupoMixto = true;
                                }
                            else
                                {
                                tieneRellenoComun = true;
                                }
                            }
                        else
                            {
                            tieneRellenoComun = true;
                            }
                        }
                    else if (relleno.Type ==
                             VGCore.cdrFillType.cdrFountainFill)
                        {
                        tieneRellenoComun = true;
                        }
                    else if (relleno.Type !=
                             VGCore.cdrFillType.cdrNoFill)
                        {
                        posibleGrupoMixto = true;
                        }
                    }

                // =====================================================
                // TRAZO GENERAL DEL GRUPO
                // =====================================================

                if (chkIncluirTrazos.Checked)
                    {
                    VGCore.Outline trazo =
                        grupo.Outline;

                    if (trazo != null &&
                        trazo.Type !=
                        VGCore.cdrOutlineType.cdrNoOutline)
                        {
                        tieneTrazoComun = true;
                        }
                    }

                // Si Corel devuelve blanco y tampoco existe
                // un trazo común, lo tratamos como grupo mixto.
                if (posibleGrupoMixto &&
                    !tieneTrazoComun)
                    {
                    RegistrarGrupoComplejo(
                        grupo,
                        "CorelDRAW muestra blanco como propiedad general. " +
                        "El grupo puede contener varios colores."
                    );

                    return;
                    }

                // =====================================================
                // REGISTRAR PROPIEDADES COMUNES
                // =====================================================

                if (tieneRellenoComun)
                    {
                    AnalizarRelleno(grupo);
                    }

                if (tieneTrazoComun)
                    {
                    AnalizarTrazo(grupo);
                    }

                if (!tieneRellenoComun &&
                    !tieneTrazoComun)
                    {
                    RegistrarGrupoComplejo(
                        grupo,
                        "No se encontró un relleno o trazo común"
                    );
                    }
                }
            catch (Exception ex)
                {
                RegistrarGrupoComplejo(
                    grupo,
                    "CorelDRAW no pudo determinar una propiedad común: " +
                    ex.Message
                );
                }
            }
        private bool EsColorBlanco(VGCore.Color colorOriginal)
            {
            if (colorOriginal == null)
                return false;

            try
                {
                VGCore.Color copia =
                    corelApp.CreateColor();

                copia.CopyAssign(colorOriginal);
                copia.ConvertToCMYK();

                return
                    copia.CMYKCyan == 0 &&
                    copia.CMYKMagenta == 0 &&
                    copia.CMYKYellow == 0 &&
                    copia.CMYKBlack == 0;
                }
            catch
                {
                return false;
                }
            }
        private void AnalizarPowerClip(VGCore.Shape contenedor)
            {
            try
                {
                VGCore.PowerClip powerClip =
                    contenedor.PowerClip;

                if (powerClip == null)
                    return;

                VGCore.Shapes contenido =
                    powerClip.Shapes;

                if (contenido == null ||
                    contenido.Count == 0)
                    {
                    return;
                    }

                for (int i = 1;
                     i <= contenido.Count;
                     i++)
                    {
                    AnalizarObjeto(contenido[i]);
                    }
                }
            catch
                {
                // El objeto no contiene un PowerClip.
                }
            }
        private void AnalizarRelleno(VGCore.Shape objeto)
            {
            try
                {
                VGCore.Fill relleno = objeto.Fill;

                if (relleno == null)
                    {
                    objetosIgnorados++;
                    return;
                    }

                switch (relleno.Type)
                    {
                    case VGCore.cdrFillType.cdrUniformFill:

                        RegistrarColor(
                            relleno.UniformColor,
                            false
                        );

                        break;

                    case VGCore.cdrFillType.cdrFountainFill:

                        degradadosEncontrados++;

                        // Color inicial del degradado.
                        RegistrarColor(
                            relleno.Fountain.StartColor,
                            true
                        );

                        // Todos los nodos intermedios.
                        VGCore.FountainColors nodos =
                            relleno.Fountain.Colors;

                        for (int i = 1;
                             i <= nodos.Count;
                             i++)
                            {
                            RegistrarColor(
                                nodos[i].Color,
                                true
                            );
                            }

                        // Color final del degradado.
                        RegistrarColor(
                            relleno.Fountain.EndColor,
                            true
                        );

                        break;

                    case VGCore.cdrFillType.cdrNoFill:

                        objetosIgnorados++;
                        break;

                    default:

                        // Patrones, texturas y rellenos no compatibles.
                        objetosIgnorados++;
                        break;
                    }
                }
            catch
                {
                objetosIgnorados++;
                }
            }
        private string ObtenerFirmaColor(VGCore.Color colorOriginal)
            {
            if (colorOriginal == null)
                return "SIN_COLOR";

            try
                {
                VGCore.Color copia =
                    corelApp.CreateColor();

                copia.CopyAssign(colorOriginal);
                copia.ConvertToCMYK();

                return
                    $"C{copia.CMYKCyan}_" +
                    $"M{copia.CMYKMagenta}_" +
                    $"Y{copia.CMYKYellow}_" +
                    $"K{copia.CMYKBlack}";
                }
            catch
                {
                return "COLOR_NO_COMPATIBLE";
                }
            }
        private void RegistrarGrupoComplejo(VGCore.Shape grupo, string motivo = "Propiedades diferentes")
            {
            gruposComplejosEncontrados++;
            objetosIgnorados++;

            string nombreGrupo = "";

            try
                {
                nombreGrupo = grupo.Name;
                }
            catch
                {
                nombreGrupo = "";
                }

            if (string.IsNullOrWhiteSpace(nombreGrupo))
                {
                nombreGrupo =
                    $"Grupo sin nombre #{gruposComplejosEncontrados}";
                }

            string detalle =
                nombreGrupo +
                Environment.NewLine +
                "Motivo: " +
                motivo;

            if (!nombresGruposComplejos.Contains(detalle))
                {
                nombresGruposComplejos.Add(detalle);
                }
            }
        private void AnalizarTrazo(VGCore.Shape objeto)
            {
            try
                {
                VGCore.Outline trazo = objeto.Outline;

                if (trazo == null)
                    return;

                if (trazo.Type ==
                    VGCore.cdrOutlineType.cdrNoOutline)
                    {
                    return;
                    }

                RegistrarColor(
                    trazo.Color,
                    false,
                    true
                );
                }
            catch
                {
                objetosIgnorados++;
                }
            }
        private void RegistrarColor(VGCore.Color colorOriginal, bool esDegradado, bool esTrazo = false)
            {
            if (colorOriginal == null)
                return;

            try
                {
                // Creamos una copia para que el análisis
                // no modifique el diseño original.
                VGCore.Color copia =
                    corelApp.CreateColor();

                copia.CopyAssign(colorOriginal);
                copia.ConvertToCMYK();

                int c = copia.CMYKCyan;
                int m = copia.CMYKMagenta;
                int y = copia.CMYKYellow;
                int k = copia.CMYKBlack;

                if (!chkModificarBlancoNegro.Checked)
                    {
                    bool esBlanco =
                        c == 0 &&
                        m == 0 &&
                        y == 0 &&
                        k == 0;

                    bool esNegro =
                        c == 0 &&
                        m == 0 &&
                        y == 0 &&
                        k == 100;

                    if (esBlanco || esNegro)
                        {
                        objetosIgnorados++;
                        return;
                        }
                    }

                string clave =
                    $"C{c}_M{m}_Y{y}_K{k}";

                ColorDetectado encontrado;

                if (!coloresDetectados.TryGetValue(
                    clave,
                    out encontrado))
                    {
                    encontrado = new ColorDetectado
                        {
                        C = c,
                        M = m,
                        Y = y,
                        K = k
                        };

                    coloresDetectados.Add(
                        clave,
                        encontrado
                    );
                    }

                encontrado.Apariciones++;
                encontrado.Referencias.Add(colorOriginal);

                if (esTrazo)
                    {
                    encontrado.TieneTrazo = true;
                    }
                else if (esDegradado)
                    {
                    encontrado.TieneDegradado = true;
                    }
                else
                    {
                    encontrado.TieneUniforme = true;
                    }
                }
            catch
                {
                objetosIgnorados++;
                }
            }
        private void MostrarColoresEnTabla()
            {
            dgvColores.Rows.Clear();

            foreach (
                ColorDetectado color
                in coloresDetectados.Values)
                {
                int indiceFila =
                    dgvColores.Rows.Add();

                DataGridViewRow fila =
                    dgvColores.Rows[indiceFila];

                fila.Cells[colMuestra.Name].Value =
                    CrearMuestraColor(color);

                fila.Cells[colCmykOriginal.Name].Value =
                    $"C:{color.C} " +
                    $"M:{color.M} " +
                    $"Y:{color.Y} " +
                    $"K:{color.K}";

                fila.Cells[colCNuevo.Name].Value =
                    color.C;

                fila.Cells[colMNuevo.Name].Value =
                    color.M;

                fila.Cells[colYNuevo.Name].Value =
                    color.Y;

                fila.Cells[colKNuevo.Name].Value =
                    color.K;

                fila.Cells[colApariciones.Name].Value =
                    color.Apariciones;

                fila.Cells[colTipo.Name].Value =
                    color.Tipo;

                fila.Tag = color;
                }

            dgvColores.ClearSelection();
            }
        private void ActualizarContadores()
            {
            lblObjetosCantidad.Text =
                objetosAnalizados.ToString();

            lblColoresCantidad.Text =
                coloresDetectados.Count.ToString();

            lblDegradadosCantidad.Text =
                degradadosEncontrados.ToString();

            lblIgnoradosCantidad.Text =
                objetosIgnorados.ToString();
            }
        private void MostrarAdvertenciaGruposComplejos()
            {
            if (gruposComplejosEncontrados == 0)
                return;

            StringBuilder mensaje = new StringBuilder();

            mensaje.AppendLine(
                $"Se encontraron {gruposComplejosEncontrados} " +
                "grupos con diferentes propiedades."
            );

            mensaje.AppendLine();
            mensaje.AppendLine("Grupos que debe desagrupar:");
            mensaje.AppendLine();

            foreach (string nombre in nombresGruposComplejos)
                {
                mensaje.AppendLine("• " + nombre);
                }

            mensaje.AppendLine();
            mensaje.AppendLine(
                "Estos grupos no fueron incluidos en el análisis."
            );

            mensaje.AppendLine(
                "Desagrúpelos en CorelDRAW y vuelva a analizar."
            );

            MessageBox.Show(
                mensaje.ToString(),
                "Grupos complejos encontrados",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            }
        private System.Drawing.Bitmap CrearMuestraColor(ColorDetectado color)
            {
            const int ancho = 44;
            const int alto = 22;

            System.Drawing.Bitmap imagen =
                new System.Drawing.Bitmap(ancho, alto);

            System.Drawing.Color colorPantalla =
                ConvertirCmykARgb(
                    color.C,
                    color.M,
                    color.Y,
                    color.K
                );

            using (Graphics graphics = Graphics.FromImage(imagen))
            using (SolidBrush brocha = new SolidBrush(colorPantalla))
            using (Pen borde = new Pen(System.Drawing.Color.DimGray))
                {
                graphics.FillRectangle(
                    brocha,
                    1,
                    1,
                    ancho - 2,
                    alto - 2
                );

                graphics.DrawRectangle(
                    borde,
                    0,
                    0,
                    ancho - 1,
                    alto - 1
                );
                }

            return imagen;
            }
        private System.Drawing.Color ConvertirCmykARgb(int c, int m, int y, int k)
            {
            double cyan = c / 100.0;
            double magenta = m / 100.0;
            double amarillo = y / 100.0;
            double negro = k / 100.0;

            int rojo = (int)(
                255 *
                (1 - cyan) *
                (1 - negro)
            );

            int verde = (int)(
                255 *
                (1 - magenta) *
                (1 - negro)
            );

            int azul = (int)(
                255 *
                (1 - amarillo) *
                (1 - negro)
            );

            return System.Drawing.Color.FromArgb(
                LimitarRgb(rojo),
                LimitarRgb(verde),
                LimitarRgb(azul)
            );
            }
        private int LimitarRgb(int valor)
            {
            return Math.Max(
                0,
                Math.Min(255, valor)
            );
            }
        private void LiberarMuestrasAnteriores()
            {
            foreach (
                DataGridViewRow fila
                in dgvColores.Rows)
                {
                System.Drawing.Image imagen =
                    fila.Cells[colMuestra.Name].Value
                    as System.Drawing.Image;

                if (imagen != null)
                    {
                    imagen.Dispose();
                    }
                }
            }
        private void dgvColores_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
            {
            string nombreColumna =
                dgvColores.Columns[e.ColumnIndex].Name;

            bool esColumnaCmyk =
                nombreColumna == colCNuevo.Name ||
                nombreColumna == colMNuevo.Name ||
                nombreColumna == colYNuevo.Name ||
                nombreColumna == colKNuevo.Name;

            if (!esColumnaCmyk)
                return;

            string contenido =
                Convert.ToString(
                    e.FormattedValue
                ).Trim();

            int valor;

            if (!int.TryParse(
                    contenido,
                    out valor) ||
                valor < 0 ||
                valor > 100)
                {
                e.Cancel = true;

                dgvColores
                    .Rows[e.RowIndex]
                    .Cells[e.ColumnIndex]
                    .ErrorText =
                    "Ingrese un número entre 0 y 100.";
                }
            else
                {
                dgvColores
                    .Rows[e.RowIndex]
                    .Cells[e.ColumnIndex]
                    .ErrorText = "";
                }
            }
        private void dgvColores_CellEndEdit(object sender, DataGridViewCellEventArgs e)
            {
            dgvColores
                .Rows[e.RowIndex]
                .Cells[e.ColumnIndex]
                .ErrorText = "";
            }
        private void btnAplicar_Click(object sender, EventArgs e)
            {
            if (dgvColores.Rows.Count == 0)
                {
                MessageBox.Show(
                    "Primero debe analizar una selección.",
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return;
                }

            // Finalizar cualquier edición activa.
            dgvColores.EndEdit();

            if (!ValidarTodosLosValoresCmyk())
                return;

            DialogResult confirmacion =
                MessageBox.Show(
                    "¿Desea aplicar los nuevos valores CMYK?\n\n" +
                    "Se modificarán los rellenos, degradados y trazos " +
                    "encontrados durante el análisis.",
                    "Confirmar cambios",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

            if (confirmacion != DialogResult.Yes)
                return;

            if (vistaPreviaActiva)
                {
                RestaurarVistaPrevia();

                if (vistaPreviaActiva)
                    return;
                }

            VGCore.Application corel = null;
            VGCore.Document documento = null;

            bool grupoDeshacerIniciado = false;
            int coloresModificados = 0;
            int erroresColor = 0;
            int transparenciasEliminadas = 0;

            try
                {
                corel = ObtenerCorel();

                if (corel.Documents.Count == 0)
                    {
                    MessageBox.Show(
                        "No hay ningún documento abierto.",
                        "Tithor Automation",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                    }

                documento = corel.ActiveDocument;

                lblEstadoProceso.Text =
                    "Aplicando cambios...";

                prgProceso.Style =
                    ProgressBarStyle.Marquee;

                documento.BeginCommandGroup(
                    "Tithor - Pantonear CMYK"
                );

                grupoDeshacerIniciado = true;

                corel.EventsEnabled = false;
                corel.Optimization = true;

                foreach (DataGridViewRow fila
                         in dgvColores.Rows)
                    {
                    if (fila.IsNewRow)
                        continue;

                    ColorDetectado colorDetectado =
                        fila.Tag as ColorDetectado;

                    if (colorDetectado == null)
                        continue;

                    int c = Convert.ToInt32(
                        fila.Cells[colCNuevo.Name].Value
                    );

                    int m = Convert.ToInt32(
                        fila.Cells[colMNuevo.Name].Value
                    );

                    int y = Convert.ToInt32(
                        fila.Cells[colYNuevo.Name].Value
                    );

                    int k = Convert.ToInt32(
                        fila.Cells[colKNuevo.Name].Value
                    );

                    foreach (VGCore.Color referencia
                             in colorDetectado.Referencias)
                        {
                        try
                            {
                            referencia.CMYKAssign(
                                c,
                                m,
                                y,
                                k
                            );

                            coloresModificados++;
                            }
                        catch
                            {
                            erroresColor++;
                            }
                        }

                    ActualizarFilaDespuesDeAplicar(
                        fila,
                        colorDetectado,
                        c,
                        m,
                        y,
                        k
                    );
                    }

                if (chkEliminarTransparencias.Checked)
                    {
                    foreach (VGCore.Shape objeto
                             in objetosParaCambios)
                        {
                        try
                            {
                            objeto.Transparency
                                .ApplyNoTransparency();

                            transparenciasEliminadas++;
                            }
                        catch
                            {
                            // Algunos tipos de objeto no permiten
                            // modificar la transparencia.
                            }
                        }
                    }

                lblEstadoProceso.Text =
                    "Cambios aplicados correctamente";
                }
            catch (Exception ex)
                {
                lblEstadoProceso.Text =
                    "Error al aplicar los cambios";

                MessageBox.Show(
                    $"Tipo: {ex.GetType().FullName}\n" +
                    $"Código: 0x{ex.HResult:X8}\n\n" +
                    ex.Message,
                    "Error al aplicar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                }
            finally
                {
                if (corel != null)
                    {
                    try
                        {
                        corel.EventsEnabled = true;
                        corel.Optimization = false;
                        }
                    catch
                        {
                        }
                    }

                if (grupoDeshacerIniciado &&
                    documento != null)
                    {
                    try
                        {
                        documento.EndCommandGroup();
                        }
                    catch
                        {
                        }
                    }

                if (corel != null)
                    {
                    try
                        {
                        corel.ActiveWindow.Refresh();
                        }
                    catch
                        {
                        }
                    }

                prgProceso.Style =
                    ProgressBarStyle.Blocks;

                prgProceso.Value = 0;
                }

            MessageBox.Show(
                "Proceso terminado.\n\n" +
                $"Colores modificados: {coloresModificados}\n" +
                $"Transparencias procesadas: {transparenciasEliminadas}\n" +
                $"Cambios no aplicados: {erroresColor}\n\n" +
                "Puede deshacer todo desde CorelDRAW con Ctrl + Z.",
                "Tithor Automation",
                MessageBoxButtons.OK,
                erroresColor == 0
                    ? MessageBoxIcon.Information
                    : MessageBoxIcon.Warning
            );
            }
        private void frmPrincipal_FormClosing(object sender, FormClosingEventArgs e)
            {
            if (vistaPreviaActiva)
                {
                RestaurarVistaPrevia();

                if (vistaPreviaActiva)
                    {
                    DialogResult respuesta =
                        MessageBox.Show(
                            "La vista previa sigue activa y no pudo restaurarse.\n\n" +
                            "¿Desea cerrar de todas maneras?",
                            "Vista previa activa",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning
                        );

                    if (respuesta == DialogResult.No)
                        {
                        e.Cancel = true;
                        }
                    }
                }
            }
        private bool ValidarTodosLosValoresCmyk()
            {
            foreach (DataGridViewRow fila
                     in dgvColores.Rows)
                {
                if (fila.IsNewRow)
                    continue;

                string[] columnas =
                {
            colCNuevo.Name,
            colMNuevo.Name,
            colYNuevo.Name,
            colKNuevo.Name
        };

                foreach (string nombreColumna
                         in columnas)
                    {
                    object contenido =
                        fila.Cells[nombreColumna].Value;

                    int valor;

                    if (!int.TryParse(
                            Convert.ToString(contenido),
                            out valor) ||
                        valor < 0 ||
                        valor > 100)
                        {
                        dgvColores.CurrentCell =
                            fila.Cells[nombreColumna];

                        MessageBox.Show(
                            "Todos los valores CMYK deben ser " +
                            "números entre 0 y 100.",
                            "Valor CMYK incorrecto",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning
                        );

                        return false;
                        }
                    }
                }

            return true;
            }
        private void ActualizarFilaDespuesDeAplicar(DataGridViewRow fila, ColorDetectado color, int c, int m, int y, int k)
            {
            System.Drawing.Image imagenAnterior =
                fila.Cells[colMuestra.Name].Value
                as System.Drawing.Image;

            color.C = c;
            color.M = m;
            color.Y = y;
            color.K = k;

            fila.Cells[colCmykOriginal.Name].Value =
                $"C:{c} M:{m} Y:{y} K:{k}";

            fila.Cells[colMuestra.Name].Value =
                CrearMuestraColor(color);

            imagenAnterior?.Dispose();
            }
        private void AplicarColoresTemporales()
            {
            foreach (DataGridViewRow fila
                     in dgvColores.Rows)
                {
                if (fila.IsNewRow)
                    continue;

                ColorDetectado colorDetectado =
                    fila.Tag as ColorDetectado;

                if (colorDetectado == null)
                    continue;

                int c = Convert.ToInt32(
                    fila.Cells[colCNuevo.Name].Value
                );

                int m = Convert.ToInt32(
                    fila.Cells[colMNuevo.Name].Value
                );

                int y = Convert.ToInt32(
                    fila.Cells[colYNuevo.Name].Value
                );

                int k = Convert.ToInt32(
                    fila.Cells[colKNuevo.Name].Value
                );

                foreach (VGCore.Color referencia
                         in colorDetectado.Referencias)
                    {
                    referencia.CMYKAssign(
                        c,
                        m,
                        y,
                        k
                    );
                    }
                }
            }
        private void AplicarTransparenciasTemporales()
            {
            foreach (VGCore.Shape objeto
                     in objetosParaCambios)
                {
                try
                    {
                    objeto.Transparency
                        .ApplyNoTransparency();
                    }
                catch
                    {
                    // Ignorar objetos no compatibles.
                    }
                }
            }
        private void RestaurarVistaPrevia()
            {
            if (!vistaPreviaActiva ||
                documentoVistaPrevia == null)
                {
                return;
                }

            try
                {
                VGCore.Application corel =
                    ObtenerCorel();

                lblEstadoProceso.Text =
                    "Restaurando diseño original...";

                documentoVistaPrevia.Undo();

                corel.ActiveWindow.Refresh();

                lblEstadoProceso.Text =
                    "Diseño original restaurado";
                }
            catch (Exception ex)
                {
                MessageBox.Show(
                    "No se pudo restaurar la vista previa.\n\n" +
                    ex.Message,
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                return;
                }

            vistaPreviaActiva = false;
            documentoVistaPrevia = null;

            btnVistaPrevia.Text =
                "Vista previa";
            }
        private void btnVistaPrevia_Click(object sender, EventArgs e)
            {
            if (vistaPreviaActiva)
                {
                RestaurarVistaPrevia();
                return;
                }

            dgvColores.EndEdit();

            if (!ValidarTodosLosValoresCmyk())
                return;

            VGCore.Application corel = null;
            VGCore.Document documento = null;

            bool grupoIniciado = false;
            bool vistaAplicada = false;

            try
                {
                corel = ObtenerCorel();

                if (corel.Documents.Count == 0)
                    {
                    MessageBox.Show(
                        "No hay ningún documento abierto.",
                        "Tithor Automation",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                    }

                documento = corel.ActiveDocument;

                lblEstadoProceso.Text =
                    "Generando vista previa...";

                prgProceso.Style =
                    ProgressBarStyle.Marquee;

                documento.BeginCommandGroup(
                    "Tithor - Vista previa CMYK"
                );

                grupoIniciado = true;

                corel.EventsEnabled = false;
                corel.Optimization = true;

                AplicarColoresTemporales();

                if (chkEliminarTransparencias.Checked)
                    {
                    AplicarTransparenciasTemporales();
                    }

                vistaAplicada = true;
                }
            catch (Exception ex)
                {
                MessageBox.Show(
                    $"No se pudo generar la vista previa.\n\n" +
                    $"Tipo: {ex.GetType().FullName}\n" +
                    $"Código: 0x{ex.HResult:X8}\n\n" +
                    ex.Message,
                    "Error de vista previa",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                }
            finally
                {
                if (corel != null)
                    {
                    try
                        {
                        corel.EventsEnabled = true;
                        corel.Optimization = false;
                        }
                    catch
                        {
                        }
                    }

                if (grupoIniciado &&
                    documento != null)
                    {
                    try
                        {
                        documento.EndCommandGroup();
                        }
                    catch
                        {
                        }
                    }

                if (corel != null)
                    {
                    try
                        {
                        corel.ActiveWindow.Refresh();
                        }
                    catch
                        {
                        }
                    }

                prgProceso.Style =
                    ProgressBarStyle.Blocks;

                prgProceso.Value = 0;
                }

            if (vistaAplicada)
                {
                vistaPreviaActiva = true;
                documentoVistaPrevia = documento;

                btnVistaPrevia.Text =
                    "Restaurar original";

                lblEstadoProceso.Text =
                    "Vista previa activa";

                MessageBox.Show(
                    "Vista previa aplicada.\n\n" +
                    "Revise el resultado en CorelDRAW.\n" +
                    "No edite el documento mientras la vista previa esté activa.\n\n" +
                    "Presione “Restaurar original” para volver al diseño anterior.",
                    "Vista previa",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                }
            }
        private void lblDegradadosTitulo_Click(object sender, EventArgs e)
            {

            }
        private void lblDegradadosCantidad_Click(object sender, EventArgs e)
            {

            }
        private void frmPrincipal_Load(object sender, EventArgs e)
            {

            }
        private void MostrarPanel(System.Windows.Forms.Panel panelSeleccionado)
            {
            pnlPantonear.Visible = false;
            pnlProduccion.Visible = false;
            pnlConfiguracion.Visible = false;

            panelSeleccionado.Visible = true;
            panelSeleccionado.BringToFront();

            pnlEstadoCorelGlobal.Visible = true;
            pnlEstadoCorelGlobal.BringToFront();
            }
        private void btnPantonear_Click(object sender, EventArgs e)
            {
            MostrarPanel(pnlPantonear);
            }
        private void btnConfiguracion_Click(object sender, EventArgs e)
            {
            MostrarPanel(pnlConfiguracion);
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
                    "Seleccionar archivo Master de CorelDRAW";

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
            try
                {
                FileInfo informacion =
                    new FileInfo(rutaArchivo);

                bool esMismoArchivo =
                    archivoMasterActual != null &&
                    string.Equals(
                        archivoMasterActual.RutaArchivo,
                        informacion.FullName,
                        StringComparison.OrdinalIgnoreCase
                    );

                ArchivoMaster master =
                    archivoMasterActual ?? new ArchivoMaster();

                master.ProductoId = producto.Id;
                master.RutaArchivo = informacion.FullName;
                master.NombreArchivo = informacion.Name;
                master.EsPrincipal = true;
                master.Activo = true;

                if (!esMismoArchivo)
                    {
                    master.HashArchivo = string.Empty;
                    master.TamanoArchivo = informacion.Length;
                    master.FechaModificacion =
                        informacion.LastWriteTimeUtc;

                    master.FechaUltimoAnalisis = null;
                    master.CantidadMoldes = 0;
                    }

                master.Id =
                    archivoMasterRepositorio
                        .GuardarPrincipal(master);

                archivoMasterActual = master;
                moldesAnalizados.Clear();
                tamanoMasterAnalizado = 0;
                fechaMasterAnalizadoUtc = DateTime.MinValue;

                dgvCatalogoMoldes.Rows.Clear();
                dgvCatalogoMoldes.Visible = false;

                btnSincronizarMaster.Enabled = false;

                MostrarArchivoMaster();

                MessageBox.Show(
                    "El archivo Master fue guardado para el producto \"" +
                    producto.Nombre +
                    "\".",
                    "Tithor Automation",
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
                    bool encontrado = false;

                    foreach (Molde molde in moldes)
                        {
                        if (molde.Estado == "Inválido" ||
                            molde.Estado == "Duplicado")
                            {
                            continue;
                            }

                        if (TextoIgual(molde.Talla, talla) &&
                            TextoIgual(molde.Pieza, pieza))
                            {
                            encontrado = true;
                            break;
                            }
                        }

                    if (!encontrado)
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
                    "No se pudieron cargar los productos en Producción.\n\n" + ex.Message,
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                }
            }
        private void LimpiarPedidoProduccion(bool limpiarRuta)
            {
            resultadoPedidoActual = null;
            pedidoAprobado = false;

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

            btnAprobarPedido.Enabled = false;
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
                pedidoAprobado = false;

                dgvPedidoProduccion.Rows.Clear();
                dgvPedidoProduccion.Columns.Clear();
                dgvPedidoProduccion.Visible = false;

                btnAnalizarExcelProduccion.Enabled = true;
                btnAprobarPedido.Enabled = false;
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

                System.Windows.Forms.Application.DoEvents();

                resultadoPedidoActual = lector.Analizar(
                    producto.Id,
                    producto.Codigo,
                    txtRutaExcelProduccion.Text
                );

                pedidoAprobado = false;

                MostrarResultadoPedido(resultadoPedidoActual);

                btnAprobarPedido.Enabled = resultadoPedidoActual.PuedeAprobar;
                btnCopiarMoldesPedido.Enabled = false;

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
        private void ConfigurarColumnasPedido()
            {
            dgvPedidoProduccion.Columns.Clear();
            dgvPedidoProduccion.AutoGenerateColumns = false;
            dgvPedidoProduccion.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            AgregarColumnaPedido("colEstadoPedido", "Estado", 75F, 90);
            AgregarColumnaPedido("colFilaPedido", "Fila", 45F, 50);
            AgregarColumnaPedido("colDisenoPedido", "Diseño", 150F, 140);
            AgregarColumnaPedido("colTallaPedido", "Talla", 60F, 60);
            AgregarColumnaPedido("colCantidadPedido", "Cantidad", 70F, 75);
            AgregarColumnaPedido("colNotasPedido", "Notas", 140F, 120);
            AgregarColumnaPedido("colObservacionesPedido", "Observaciones", 220F, 180);
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
            ConfigurarColumnasPedido();

            dgvPedidoProduccion.Rows.Clear();

            foreach (LineaPedido linea in resultado.Lineas)
                {
                int indice = dgvPedidoProduccion.Rows.Add(
                    linea.Estado,
                    linea.NumeroFila,
                    linea.Diseno,
                    linea.Talla,
                    linea.Cantidad > 0 ? linea.Cantidad.ToString() : "",
                    linea.Notas,
                    linea.MensajeCompleto
                );

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
            lblDisenosPedidoValor.Text = resultado.ObtenerDisenos().Count.ToString();
            lblUnidadesPedidoValor.Text = resultado.TotalUnidades.ToString();
            lblAdvertenciasPedidoValor.Text = advertencias.ToString();

            lblResultadoPedido.Text =
                resultado.TotalFilasProcesables + " filas listas, " +
                resultado.TotalFilasOmitidas + " filas serán omitidas.";

            dgvPedidoProduccion.Visible = true;
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
        private void btnAprobarPedido_Click(object sender, EventArgs e)
            {
            if (resultadoPedidoActual == null || !resultadoPedidoActual.PuedeAprobar)
                return;

            DialogResult respuesta = MessageBox.Show(
                "Se aprobarán " + resultadoPedidoActual.TotalUnidades + " unidades válidas.\n\nLas filas con advertencias serán omitidas.\n\n¿Desea continuar?",
                "Aprobar pedido",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2
            );

            if (respuesta != DialogResult.Yes)
                return;

            pedidoAprobado = true;

            btnAprobarPedido.Enabled = false;
            btnCopiarMoldesPedido.Enabled = true;

            cboProductoProduccion.Enabled = false;
            btnCargarExcelProduccion.Enabled = false;
            btnAnalizarExcelProduccion.Enabled = false;

            lblEstadoExcelProduccion.Text = "Pedido aprobado.";
            lblResultadoPedido.Text = "Pedido aprobado y listo para copiar moldes.";
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
            bool copiaTerminada = false;

            try
                {
                if (!pedidoAprobado || resultadoPedidoActual == null)
                    {
                    MessageBox.Show(
                        "Primero debe analizar y aprobar el archivo Excel.",
                        "Producción",
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
                        "Producción",
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
                        "Producción",
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
                        "Producción",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                    }

                if (!System.IO.File.Exists(master.RutaArchivo))
                    {
                    MessageBox.Show(
                        $"No se encontró el archivo Master:\n\n{master.RutaArchivo}",
                        "Producción",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                    }

                if (!master.FechaUltimoAnalisis.HasValue)
                    {
                    MessageBox.Show(
                        "El archivo Master todavía no ha sido analizado.\n\nAnalícelo desde Configuración antes de continuar.",
                        "Producción",
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
                        "Producción",
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
                        "Producción",
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
                        "Revisar producción",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                    }

                if (plan.TotalMoldes == 0)
                    {
                    MessageBox.Show(
                        "El plan de producción no contiene moldes para copiar.",
                        "Producción",
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
                        "Producción",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                    }

                VGCore.Document documentoDestino = corel.ActiveDocument;

                DialogResult confirmacion = MessageBox.Show(
                    $"Se copiarán {plan.TotalMoldes} moldes.\n\n" +
                    $"Producto: {producto.Nombre}\n" +
                    $"Documento destino: {documentoDestino.Name}\n" +
                    $"Master: {master.NombreArchivo}\n\n" +
                    "¿Desea continuar?",
                    "Copiar moldes",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (confirmacion != DialogResult.Yes)
                    return;

                btnCopiarMoldesPedido.Enabled = false;
                btnAprobarPedido.Enabled = false;
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
                                System.Windows.Forms.Application.DoEvents();
                            }
                );

                planProduccionActual = plan;
                copiaTerminada = true;

                lblResultadoPedido.Text =
                    $"Producción preparada: {totalCopiado} conjuntos copiados en {documentoDestino.Name}.";

                btnCopiarMoldesPedido.Text = "COPIAR NUEVAMENTE";
                btnCopiarMoldesPedido.Enabled = true;
                btnNuevoPedido.Enabled = true;

                MessageBox.Show(
                    $"Se copiaron correctamente {totalCopiado} moldes.\n\n" +
                    $"Documento: {documentoDestino.Name}\n" +
                    "Capa creada: TITHOR_PRODUCCION\n\n" +
                    "Puede usar Ctrl+Z una sola vez para deshacer toda la copia.",
                    "Producción preparada",
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
                btnCopiarMoldesPedido.Enabled = pedidoAprobado;
                btnNuevoPedido.Enabled = resultadoPedidoActual != null;
                btnAprobarPedido.Enabled = resultadoPedidoActual != null && !pedidoAprobado;
                }
            }
        private void btnProduccion_Click(object sender, EventArgs e)
            {
            MostrarPanel(pnlProduccion);

            if (cboProductoProduccion.Items.Count == 0)
                CargarProductosProduccion();
            }
        private void AplicarEstiloGridProduccion()
            {
            dgvPedidoProduccion.EnableHeadersVisualStyles = false;
            dgvPedidoProduccion.BackgroundColor = System.Drawing.Color.White;
            dgvPedidoProduccion.BorderStyle = BorderStyle.None;
            dgvPedidoProduccion.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvPedidoProduccion.GridColor = System.Drawing.Color.FromArgb(220, 222, 230);

            dgvPedidoProduccion.AllowUserToAddRows = false;
            dgvPedidoProduccion.AllowUserToDeleteRows = false;
            dgvPedidoProduccion.AllowUserToResizeRows = false;
            dgvPedidoProduccion.RowHeadersVisible = false;
            dgvPedidoProduccion.MultiSelect = false;
            dgvPedidoProduccion.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            dgvPedidoProduccion.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgvPedidoProduccion.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvPedidoProduccion.ColumnHeadersHeight = 42;

            dgvPedidoProduccion.ColumnHeadersDefaultCellStyle.BackColor =
                System.Drawing.Color.FromArgb(44, 49, 82);

            dgvPedidoProduccion.ColumnHeadersDefaultCellStyle.ForeColor =
                System.Drawing.Color.White;

            dgvPedidoProduccion.ColumnHeadersDefaultCellStyle.SelectionBackColor =
                System.Drawing.Color.FromArgb(44, 49, 82);

            dgvPedidoProduccion.ColumnHeadersDefaultCellStyle.SelectionForeColor =
                System.Drawing.Color.White;

            dgvPedidoProduccion.ColumnHeadersDefaultCellStyle.Font =
                new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular);

            dgvPedidoProduccion.ColumnHeadersDefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleLeft;

            dgvPedidoProduccion.ColumnHeadersDefaultCellStyle.Padding =
                new Padding(8, 0, 6, 0);

            dgvPedidoProduccion.DefaultCellStyle.BackColor =
                System.Drawing.Color.White;

            dgvPedidoProduccion.DefaultCellStyle.ForeColor =
                System.Drawing.Color.FromArgb(35, 38, 55);

            dgvPedidoProduccion.DefaultCellStyle.SelectionBackColor =
                System.Drawing.Color.FromArgb(255, 241, 178);

            dgvPedidoProduccion.DefaultCellStyle.SelectionForeColor =
                System.Drawing.Color.FromArgb(35, 38, 55);

            dgvPedidoProduccion.DefaultCellStyle.Font =
                new System.Drawing.Font("Segoe UI", 9F);

            dgvPedidoProduccion.DefaultCellStyle.Padding =
                new Padding(8, 0, 6, 0);

            dgvPedidoProduccion.AlternatingRowsDefaultCellStyle.BackColor =
                System.Drawing.Color.FromArgb(247, 248, 252);

            dgvPedidoProduccion.RowTemplate.Height = 34;
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

            resultadoPedidoActual = null;
            planProduccionActual = null;
            pedidoAprobado = false;

            txtRutaExcelProduccion.Clear();
            dgvPedidoProduccion.Rows.Clear();

            lblFilasPedidoValor.Text = "0";
            lblDisenosPedidoValor.Text = "0";
            lblUnidadesPedidoValor.Text = "0";
            lblAdvertenciasPedidoValor.Text = "0";

            lblEstadoExcelProduccion.Text = "Seleccione un archivo Excel";
            lblResultadoPedido.Text = "Listo para cargar un nuevo pedido";

            btnAnalizarExcelProduccion.Enabled = false;
            btnAprobarPedido.Enabled = false;
            btnCopiarMoldesPedido.Enabled = false;
            btnNuevoPedido.Enabled = false;

            btnAprobarPedido.Text = "APROBAR PEDIDO";
            btnCopiarMoldesPedido.Text = "COPIAR MOLDES";

            txtRutaExcelProduccion.Focus();
            }
        }

    }