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
        }
    }
