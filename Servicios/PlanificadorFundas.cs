using System;
using System.Collections.Generic;
using System.Linq;
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
            "superior",
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

            var selecciones = new Dictionary<string, List<Molde>>(StringComparer.OrdinalIgnoreCase);

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

                string codigoGrupo = "molde_" + talla.ToLowerInvariant();
                List<Molde> seleccion;
                if (!selecciones.TryGetValue(talla, out seleccion))
                    {
                    seleccion = BuscarGrupoFundas(catalogo, codigoGrupo, talla);
                    selecciones[talla] = seleccion;
                    }
                var errores = ValidarGrupoFundas(seleccion, talla);
                if (errores.Count > 0)
                    {
                    plan.Advertencias.Add("Fila " + linea.NumeroFila + ", Moldes Fundas / " + codigoGrupo + ": " + string.Join(", ", errores) + ".");
                    continue;
                    }

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
                        CapaMaster = seleccion[0].Capa,
                        FilaExcel = linea.NumeroFila,
                        NumeroUnidad = numeroUnidad,
                        NombreDestino = $"prod_f{linea.NumeroFila}_u{numeroUnidad}_{nombreDiseno}_{codigoGrupo}"
                        };

                    solicitud.PiezasIncluidas.AddRange(seleccion.Select(x => x.NombreObjeto));
                    CopiarCampos(linea, solicitud);

                    plan.Moldes.Add(solicitud);
                    }
                }

            if (plan.Moldes.Count == 0 && plan.Advertencias.Count == 0)
                plan.Advertencias.Add("El pedido no contiene unidades procesables.");

            return plan;
            }

        public void ValidarCatalogo(ResultadoAnalisisPedido resultado, List<Molde> catalogo)
            {
            if (resultado == null) throw new ArgumentNullException(nameof(resultado));
            if (catalogo == null) throw new ArgumentNullException(nameof(catalogo));

            foreach (LineaPedido linea in resultado.Lineas)
                {
                if (!linea.Procesable) continue;
                string talla = NormalizarTalla(linea.Talla);
                string grupo = "molde_" + talla.ToLowerInvariant();
                List<Molde> seleccion = BuscarGrupoFundas(catalogo, grupo, talla);
                if (seleccion.Count == 0)
                    {
                    linea.MarcarNoProcesable("La talla '" + linea.Talla + "' no existe en el catálogo sincronizado del Master de este producto. Analice y sincronice el Master.");
                    continue;
                    }
                foreach (string error in ValidarGrupoFundas(seleccion, talla))
                    linea.MarcarNoProcesable("Talla " + linea.Talla + ": " + error + ".");
                }
            }

        private string NormalizarTalla(string talla)
            {
            if (string.IsNullOrWhiteSpace(talla))
                return string.Empty;

            return AnalizadorMasterCorel.NormalizarCodigo(talla).ToUpperInvariant();
            }

        private List<Molde> BuscarGrupoFundas(List<Molde> catalogo, string grupo, string talla)
            {
            var candidatos = catalogo.Where(x => x != null && PerteneceAlGrupo(x, grupo, talla)).ToList();
            var actuales = candidatos.Where(x => AnalizadorMasterCorel.NormalizarCodigo(x.Capa) == "moldes_fundas").ToList();
            if (actuales.Count > 0) return actuales;

            // Compatibilidad con el master anterior: solo nombres funda_<talla>_<pieza>.
            string prefijo = "funda_" + talla.ToLowerInvariant() + "_";
            var antiguos = candidatos.Where(x => AnalizadorMasterCorel.NormalizarCodigo(x.NombreObjeto).StartsWith(prefijo, StringComparison.Ordinal)).ToList();
            if (antiguos.Select(x => AnalizadorMasterCorel.NormalizarCodigo(x.Capa)).Distinct().Count() == 1)
                return antiguos;
            return new List<Molde>();
            }

        private bool PerteneceAlGrupo(Molde molde, string grupo, string talla)
            {
            string[] segmentos = (molde.Codigo ?? string.Empty).Split(new[] { "__" }, StringSplitOptions.None);
            if (segmentos.Length >= 3)
                return AnalizadorMasterCorel.NormalizarCodigo(segmentos[1]) == grupo;
            // Catálogos antiguos sin código estructural.
            return string.Equals(molde.Talla, talla, StringComparison.OrdinalIgnoreCase)
                && AnalizadorMasterCorel.NormalizarCodigo(molde.NombreObjeto).StartsWith("funda_" + talla.ToLowerInvariant() + "_", StringComparison.Ordinal);
            }

        private string CodigoPiezaFunda(string nombre, string talla)
            {
            string codigo = AnalizadorMasterCorel.NormalizarCodigo(nombre);
            string prefijo = "funda_" + talla.ToLowerInvariant() + "_";
            if (codigo.StartsWith(prefijo, StringComparison.Ordinal)) codigo = codigo.Substring(prefijo.Length);
            if (codigo == "lado_izquierdo") codigo = "lateral_izquierdo";
            if (codigo == "lado_derecho") codigo = "lateral_derecho";
            return codigo;
            }

        private List<string> ValidarGrupoFundas(List<Molde> seleccion, string talla)
            {
            var errores = new List<string>();
            if (seleccion.Count == 0)
                {
                errores.Add("no se encontró el grupo de talla en la capa de Fundas; analice y sincronice el master");
                return errores;
                }
            foreach (Molde molde in seleccion.Where(x => !x.EsValido))
                errores.Add(molde.NombreObjeto + ": " + molde.Observacion);
            foreach (var duplicado in seleccion.GroupBy(x => CodigoPiezaFunda(x.NombreObjeto, talla)).Where(x => x.Count() > 1))
                errores.Add("pieza duplicada " + duplicado.Key);
            var disponibles = new HashSet<string>(seleccion.Where(x => x.EsValido).Select(x => CodigoPiezaFunda(x.NombreObjeto, talla)));
            foreach (string pieza in piezasRequeridas)
                if (!disponibles.Contains(pieza)) errores.Add("falta " + pieza);
            return errores;
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
