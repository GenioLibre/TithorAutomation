using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using TithorAutomation.Modelos;

namespace TithorAutomation
{
    public partial class frmOpcionesMockupCamiseta : Form
    {
        private List<VarianteMockupCamiseta> variantes = new List<VarianteMockupCamiseta>();
        private bool cargando;
        public OpcionesMockupCamiseta Opciones { get; private set; }

        public frmOpcionesMockupCamiseta()
        {
            InitializeComponent();
        }

        public void Configurar(Mockup mockup, OpcionesMockupCamiseta anterior)
        {
            variantes = mockup.ObtenerVariantes();
            cargando = true;
            cboTipoMockup.SelectedIndex = anterior != null && anterior.Tipo == "ranglan" ? 1 : 0;
            cboCorteMockup.SelectedIndex = anterior != null && anterior.Corte == "mujer" ? 1 : 0;
            cargando = false;
            ActualizarCuellos(anterior == null ? "redondo" : anterior.Cuello);
        }

        private void Configuracion_Cambio(object sender, EventArgs e)
        {
            if (!cargando) ActualizarCuellos("redondo");
        }

        private void ActualizarCuellos(string preferido)
        {
            string tipo = cboTipoMockup.SelectedIndex == 1 ? "ranglan" : "clasico";
            string corte = cboCorteMockup.SelectedIndex == 1 ? "mujer" : "varon";
            bool espalda = variantes.Any(x => x.Tipo == tipo && x.Corte == corte && x.EsEspalda);
            var cuellos = variantes.Where(x => x.Tipo == tipo && x.Corte == corte && !x.EsEspalda)
                .Select(x => x.Cuello).Distinct().OrderBy(x => x).Select(x => new KeyValuePair<string, string>(x,
                    x == "v" ? "V" : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(x.Replace('_', ' ')))).ToList();
            cboCuelloMockup.DataSource = cuellos;
            cboCuelloMockup.DisplayMember = "Value";
            cboCuelloMockup.ValueMember = "Key";
            if (cuellos.Any(x => x.Key == preferido)) cboCuelloMockup.SelectedValue = preferido;
            btnGenerarMockup.Enabled = espalda && cuellos.Count > 0;
            lblEstadoOpcionesMockup.Text = btnGenerarMockup.Enabled
                ? "Se prepararán el frente y la espalda seleccionados."
                : "No hay un frente y una espalda disponibles para esta combinación.";
        }

        private void btnGenerarMockup_Click(object sender, EventArgs e)
        {
            if (!btnGenerarMockup.Enabled || cboCuelloMockup.SelectedValue == null) return;
            Opciones = new OpcionesMockupCamiseta
            {
                Tipo = cboTipoMockup.SelectedIndex == 1 ? "ranglan" : "clasico",
                Corte = cboCorteMockup.SelectedIndex == 1 ? "mujer" : "varon",
                Cuello = Convert.ToString(cboCuelloMockup.SelectedValue)
            };
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
