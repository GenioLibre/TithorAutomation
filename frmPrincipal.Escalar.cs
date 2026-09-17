using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using TithorAutomation.Servicios;

namespace TithorAutomation
    {
    public partial class frmPrincipal
        {
        private VGCore.Document documentoEscalar;
        private string firmaEscalar;
        private readonly EscaladorPowerClip escalador = new EscaladorPowerClip();

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
            cboTallaEscalar.Items.AddRange(new object[]
            {
                "Todas",
                "S",
                "M",
                "L"
            });

            cboPiezaEscalar.SelectedIndex = 0;
            cboTallaEscalar.SelectedIndex = 0;

            chkReemplazarContenidoEscalar.Checked = false;

            dgvEscalar.Rows.Clear();

            lblEstadoEscalar.Text = "Analiza el documento para comenzar.";

            btnAnalizarEscalar.Enabled = true;
            btnAplicarEscalar.Enabled = false;
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

            foreach (var grupo in piezas
                .GroupBy(x => new
                    {
                    x.Pieza,
                    x.Talla,
                    x.Estado
                    })
                .OrderBy(x => x.Key.Pieza)
                .ThenBy(x => x.Key.Talla))
                {
                dgvEscalar.Rows.Add(
                    grupo.Key.Pieza,
                    grupo.Key.Talla,
                    grupo.Count(),
                    grupo.Count(x => x.TieneContenido),
                    grupo.Key.Estado);
                }

            if (piezas.Count == 0)
                {
                lblEstadoEscalar.Text = "No se encontraron piezas en TITHOR_PRODUCCION.";
                }
            else
                {
                lblEstadoEscalar.Text =
                    piezas.Count +
                    " piezas detectadas en " +
                    documentoEscalar.Name;
                }

            firmaEscalar = FirmaEscalar(piezas);

            btnAplicarEscalar.Enabled =
                piezas.Any(x => string.Equals(
                    x.Estado,
                    "Listo",
                    StringComparison.OrdinalIgnoreCase));
            }

        private void btnAnalizarEscalar_Click(object sender, EventArgs e)
            {
            try
                {
                btnAnalizarEscalar.Enabled = false;
                btnAplicarEscalar.Enabled = false;

                firmaEscalar = null;
                documentoEscalar = null;

                dgvEscalar.Rows.Clear();
                lblEstadoEscalar.Text = "Analizando los moldes...";

                documentoEscalar = DocumentoActivoEscalar();

                List<PiezaEscalable> piezas =
                    escalador.Analizar(documentoEscalar);

                MostrarAnalisisEscalar(piezas);
                }
            catch (Exception ex)
                {
                firmaEscalar = null;
                documentoEscalar = null;

                btnAplicarEscalar.Enabled = false;
                lblEstadoEscalar.Text = "No se pudo completar el análisis.";

                MessageBox.Show(
                    this,
                    ex.Message,
                    "Analizar moldes",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                }
            finally
                {
                btnAnalizarEscalar.Enabled = true;
                }
            }

        private void btnAplicarEscalar_Click(object sender, EventArgs e)
            {
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