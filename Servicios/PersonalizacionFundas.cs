using System;
using System.Globalization;
using TithorAutomation.Modelos;

namespace TithorAutomation.Servicios
{
    // Los índices pertenecen a cada fila del Excel, no al diseño ni a la talla.
    public sealed class PersonalizacionFundas
    {
        private readonly string[] nombres;
        private readonly string[] numeros;
        private readonly int cantidad;
        private readonly int fila;

        private PersonalizacionFundas(string[] nombres, string[] numeros, int cantidad, int fila)
        {
            this.nombres = nombres;
            this.numeros = numeros;
            this.cantidad = cantidad;
            this.fila = fila;
        }

        public static PersonalizacionFundas Leer(LineaPedido linea)
        {
            if (linea == null) throw new ArgumentNullException(nameof(linea));
            if (linea.Cantidad <= 0)
                throw Error(linea.NumeroFila, "la cantidad debe ser un entero mayor que cero.");

            string[] nombres = Separar(ValorCampo(linea, "nombre", "nombres"), "Nombre", linea.NumeroFila);
            string[] numeros = Separar(ValorCampo(linea, "numero", "numeros"), "Número", linea.NumeroFila);
            if (nombres.Length > linea.Cantidad)
                throw Error(linea.NumeroFila, "hay " + nombres.Length + " nombres para " + linea.Cantidad + " unidades.");
            if (numeros.Length > linea.Cantidad)
                throw Error(linea.NumeroFila, "hay " + numeros.Length + " números para " + linea.Cantidad + " unidades.");
            if (nombres.Length > 0 && numeros.Length > 0 && nombres.Length != numeros.Length)
                throw Error(linea.NumeroFila, "las listas no coinciden: " + nombres.Length + " nombres y " +
                    numeros.Length + " números. Nombre 1 debe corresponder a número 1 y así sucesivamente.");

            return new PersonalizacionFundas(nombres, numeros, linea.Cantidad, linea.NumeroFila);
        }

        private static string ValorCampo(LineaPedido linea, string singular, string plural)
        {
            string valor = linea.ObtenerCampo(singular);
            string alternativo = linea.ObtenerCampo(plural);
            if (!string.IsNullOrWhiteSpace(valor) && !string.IsNullOrWhiteSpace(alternativo) &&
                !string.Equals(valor.Trim(), alternativo.Trim(), StringComparison.Ordinal))
                throw Error(linea.NumeroFila, "hay valores distintos en " + singular + " y " + plural + ". Use una sola columna.");
            return string.IsNullOrWhiteSpace(valor) ? alternativo : valor;
        }

        private static string[] Separar(string valor, string campo, int fila)
        {
            if (string.IsNullOrWhiteSpace(valor)) return new string[0];
            string[] elementos = valor.Split(new[] { ',' }, StringSplitOptions.None);
            for (int i = 0; i < elementos.Length; i++)
            {
                elementos[i] = elementos[i].Trim();
                if (elementos[i].Length == 0)
                    throw Error(fila, campo + ": el valor " + (i + 1) +
                        " está vacío. Revise las comas; no se pueden saltar posiciones.");
            }
            return elementos;
        }

        public void AplicarUnidad(MoldeProduccion solicitud)
        {
            if (solicitud == null) throw new ArgumentNullException(nameof(solicitud));
            int unidad = solicitud.NumeroUnidad;
            if (unidad < 1 || unidad > cantidad)
                throw Error(fila, "la unidad " + unidad + " está fuera de la cantidad del pedido.");
            solicitud.Campos["nombre"] = unidad <= nombres.Length ? nombres[unidad - 1] : string.Empty;
            solicitud.Campos["numero"] = unidad <= numeros.Length ? numeros[unidad - 1] : string.Empty;
            // Evitar que otros consumidores lean de nuevo la lista completa.
            solicitud.Campos.Remove("nombres");
            solicitud.Campos.Remove("numeros");
        }

        private static InvalidOperationException Error(int fila, string mensaje)
        {
            return new InvalidOperationException("Fila Excel " + fila.ToString(CultureInfo.InvariantCulture) +
                ": personalización de fundas: " + mensaje);
        }
    }
}
