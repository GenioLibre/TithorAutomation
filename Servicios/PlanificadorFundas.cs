using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TithorAutomation.Modelos;

namespace TithorAutomation.Servicios
    {
    public class PlanificadorFundas : IPlanificadorProducto
        {
        private readonly string[] piezasRequeridas =
        {
            "frente",
            "espalda",
            "lateral_izquierdo",
            "lateral_derecho"
        };

        public bool PuedeProcesar(string codigoProducto)
            {
            if (string.IsNullOrWhiteSpace(codigoProducto))
                return false;

            return codigoProducto.IndexOf("FUNDA", StringComparison.OrdinalIgnoreCase) >= 0;
            }

        public PlanProduccion CrearPlan(ResultadoAnalisisPedido resultado, List<Molde> catalogo)
            {
            if (resultado == null)
                throw new ArgumentNullException(nameof(resultado));

            if (catalogo == null)
                throw new ArgumentNullException(nameof(catalogo));

            PlanProduccion plan = new PlanProduccion
                {
                CodigoProducto = resultado.CodigoProducto
                };

            foreach (LineaPedido linea in resultado.Lineas)
                {
                if (!linea.Procesable)
                    continue;

                string talla = NormalizarTalla(linea.Talla);

                if (string.IsNullOrWhiteSpace(talla))
                    {
                    plan.Advertencias.Add($"Fila {linea.NumeroFila}: la talla '{linea.Talla}' no está soportada.");
                    continue;
                    }

                List<string> moldesFaltantes = ObtenerMoldesFaltantes(talla, catalogo);

                if (moldesFaltantes.Count > 0)
                    {
                    plan.Advertencias.Add(
                        $"Fila {linea.NumeroFila}, talla {talla}: faltan los moldes " +
                        string.Join(", ", moldesFaltantes) + "."
                    );

                    continue;
                    }

                string codigoGrupo = $"molde_{talla.ToLowerInvariant()}";
                string nombreDiseno = LimpiarNombre(linea.Diseno);

                for (int numeroUnidad = 1; numeroUnidad <= linea.Cantidad; numeroUnidad++)
                    {
                    MoldeProduccion solicitud = new MoldeProduccion
                        {
                        CodigoProducto = resultado.CodigoProducto,
                        Diseno = linea.Diseno,
                        Talla = talla,
                        Pieza = "Conjunto",
                        CodigoMolde = codigoGrupo,
                        FilaExcel = linea.NumeroFila,
                        NumeroUnidad = numeroUnidad,
                        NombreDestino = $"prod_f{linea.NumeroFila}_u{numeroUnidad}_{nombreDiseno}_{codigoGrupo}"
                        };

                    CopiarCampos(linea, solicitud);

                    plan.Moldes.Add(solicitud);
                    }
                }

            if (plan.Moldes.Count == 0 && plan.Advertencias.Count == 0)
                plan.Advertencias.Add("El pedido no contiene unidades procesables.");

            return plan;
            }

        private string NormalizarTalla(string talla)
            {
            if (string.IsNullOrWhiteSpace(talla))
                return string.Empty;

            string valor = talla.Trim().ToUpperInvariant();

            if (valor == "S" || valor == "M" || valor == "L")
                return valor;

            return string.Empty;
            }

        private List<string> ObtenerMoldesFaltantes(string talla, List<Molde> catalogo)
            {
            List<string> faltantes = new List<string>();
            string tallaCodigo = talla.ToLowerInvariant();

            foreach (string pieza in piezasRequeridas)
                {
                string codigoEsperado = $"funda_{tallaCodigo}_{pieza}";

                if (!ExisteMolde(catalogo, codigoEsperado))
                    faltantes.Add(codigoEsperado);
                }

            return faltantes;
            }

        private bool ExisteMolde(List<Molde> catalogo, string codigo)
            {
            foreach (Molde molde in catalogo)
                {
                if (string.Equals(molde.Codigo, codigo, StringComparison.OrdinalIgnoreCase))
                    return true;
                }

            return false;
            }

        private void CopiarCampos(LineaPedido linea, MoldeProduccion solicitud)
            {
            if (linea.Campos == null)
                return;

            foreach (KeyValuePair<string, string> campo in linea.Campos)
                solicitud.Campos[campo.Key] = campo.Value;
            }

        private string LimpiarNombre(string valor)
            {
            if (string.IsNullOrWhiteSpace(valor))
                return "sin_diseno";

            string nombre = Regex.Replace(valor.Trim(), @"[^a-zA-Z0-9]+", "_");
            nombre = nombre.Trim('_').ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(nombre))
                return "sin_diseno";

            return nombre;
            }
        }
    }