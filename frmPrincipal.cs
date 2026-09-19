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
        private readonly List<IPlanificadorProducto> planificadoresProducto = new List<IPlanificadorProducto> { new PlanificadorFundas(), new PlanificadorCamisetas() };
        private readonly CopiadorMoldesCorel copiadorMoldesCorel = new CopiadorMoldesCorel();
        private PlanProduccion planProduccionActual;
        private readonly List<ILectorPedidoProducto> lectoresPedido = new List<ILectorPedidoProducto>
            {
            new LectorPedidoFundas(),
            new LectorPedidoCamisetas()
            };
        private ResultadoAnalisisPedido resultadoPedidoActual;
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
        public frmPrincipal()
            {
            InitializeComponent();

            ConfigurarFiltrosCatalogo();
            ConfigurarModuloMockups();
            ConfigurarModuloAcomodar();
            ConfigurarModuloEscalar();
            CargarProductosConfiguracion();
            ConfigurarTabla();
            ConfigurarGridProduccion();

            btnProduccion.Enabled = true;
            btnProduccion.Visible = true;

            tmrConexionCorel.Interval = 1000;
            tmrConexionCorel.Start();

            ActualizarEstadoCorel();

            

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
        private void frmPrincipal_Load(object sender, EventArgs e)
            {

            }
        private void MostrarPanel(System.Windows.Forms.Panel panelSeleccionado)
            {
            pnlPantonear.Visible = false;
            pnlProduccion.Visible = false;
            pnlEscalar.Visible = false;
            pnlAcomodar.Visible = false;
            pnlConfiguracion.Visible = false;

            panelSeleccionado.Visible = true;
            panelSeleccionado.BringToFront();

            ActualizarBotonNavegacion(btnPantonear, panelSeleccionado == pnlPantonear);
            ActualizarBotonNavegacion(btnProduccion, panelSeleccionado == pnlProduccion);
            ActualizarBotonNavegacion(btnConfiguracion, panelSeleccionado == pnlConfiguracion);
            ActualizarBotonNavegacion(btnEscalar, panelSeleccionado == pnlEscalar);
            ActualizarBotonNavegacion(btnMockup, panelSeleccionado == pnlConfiguracion && tabConfiguracion.SelectedTab == tabMockups);

            pnlEstadoCorelGlobal.Visible = true;
            pnlEstadoCorelGlobal.BringToFront();
            }
        private void ActualizarBotonNavegacion(System.Windows.Forms.Button boton, bool seleccionado)
            {
            System.Drawing.Color fondo = seleccionado
                ? System.Drawing.Color.FromArgb(250, 216, 68)
                : boton.Parent.BackColor;

            boton.UseVisualStyleBackColor = false;
            boton.BackColor = fondo;
            boton.ForeColor = seleccionado ? System.Drawing.Color.Black : System.Drawing.Color.White;
            boton.FlatAppearance.MouseOverBackColor = seleccionado ? fondo : System.Drawing.Color.FromArgb(55, 65, 81);
            boton.FlatAppearance.MouseDownBackColor = seleccionado ? fondo : System.Drawing.Color.FromArgb(75, 85, 99);
            }
        private void btnPantonear_Click(object sender, EventArgs e)
            {
            MostrarPanel(pnlPantonear);
            }
        private void btnConfiguracion_Click(object sender, EventArgs e)
            {
            MostrarPanel(pnlConfiguracion);
            }
        private void btnProduccion_Click(object sender, EventArgs e)
            {
            MostrarPanel(pnlProduccion);

            if (cboProductoProduccion.Items.Count == 0)
                CargarProductosProduccion();
            }
        private void btnAcomodar_Click_1(object sender, EventArgs e)
            {
            MostrarPanel(pnlAcomodar);
            }
        }

    }
