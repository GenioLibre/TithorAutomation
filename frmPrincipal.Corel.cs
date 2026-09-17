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
        }
    }
