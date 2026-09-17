using System;
using System.Collections.Generic;

namespace TithorAutomation.Modelos
    {
    public class LineaPedido
        {
        public int NumeroFila { get; set; }

        public string Diseno { get; set; }

        public string Talla { get; set; }

        public int Cantidad { get; set; }

        public string Notas { get; set; }

        public bool Procesable { get; set; }

        public string Estado { get; set; }

        public List<string> Mensajes { get; private set; }

        public Dictionary<string, string> Campos { get; private set; }

        public LineaPedido()
            {
            Diseno = string.Empty;
            Talla = string.Empty;
            Notas = string.Empty;
            Estado = "Correcto";
            Procesable = true;

            Mensajes = new List<string>();

            Campos = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase
            );
            }

        public void AgregarAdvertencia(string mensaje)
            {
            if (string.IsNullOrWhiteSpace(mensaje))
                return;

            Mensajes.Add(mensaje);
            Estado = "Advertencia";
            }

        public void MarcarNoProcesable(string mensaje)
            {
            Procesable = false;
            AgregarAdvertencia(mensaje);
            }

        public void AgregarCampo(string nombre, string valor)
            {
            if (string.IsNullOrWhiteSpace(nombre))
                return;

            Campos[nombre.Trim()] = valor ?? string.Empty;
            }

        public string ObtenerCampo(string nombre)
            {
            if (string.IsNullOrWhiteSpace(nombre))
                return string.Empty;

            string valor;

            if (Campos.TryGetValue(nombre.Trim(), out valor))
                return valor ?? string.Empty;

            return string.Empty;
            }

        public string MensajeCompleto
            {
            get
                {
                return string.Join(" | ", Mensajes);
                }
            }
        }
    }