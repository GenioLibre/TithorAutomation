using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TithorAutomation.Datos;

namespace TithorAutomation
{
    internal static class Program
    {
        /// <summary>
        /// Punto de entrada principal para la aplicación.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                BaseDatos.Inicializar();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No se pudo inicializar la base de datos.\n\n" +
                    ex.Message,
                    "Tithor Automation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                return;
            }

            Application.Run(new frmPrincipal());
        }
    }
}
