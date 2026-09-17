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
        private void TemporizadorCorel_Tick(object sender, EventArgs e)
            {
            ActualizarEstadoCorel();
            }
        private void ActualizarEstadoCorel()
            {
            try
                {
                ConectarCorelAbierto();
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
        private void ConectarCorelAbierto()
            {
            VGCore.Application anterior = corelApp;
            foreach (string progId in new[] { "CorelDRAW.Application.27", "CorelDRAW.Application" })
                {
                try
                    {
                    var abierta = (VGCore.Application)System.Runtime.InteropServices.Marshal.GetActiveObject(progId);
                    if (abierta.Documents.Count > 0)
                        {
                        corelApp = abierta;
                        return;
                        }
                    if (anterior == null) anterior = abierta;
                    }
                catch (System.Runtime.InteropServices.COMException)
                    {
                    // No hay una instancia registrada con este identificador.
                    }
                }
            try
                {
                if (anterior != null) { int cantidad = anterior.Documents.Count; }
                corelApp = anterior;
                }
            catch { corelApp = null; }
            }

        private VGCore.Application ObtenerCorel()
            {
            ConectarCorelAbierto();
            if (corelApp == null)
                throw new InvalidOperationException("Abra CorelDRAW y el documento de destino. Ejecute ambas aplicaciones con el mismo nivel de permisos.");
            return corelApp;
            }
        }
    }
