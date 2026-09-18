using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TithorAutomation.Modelos;

namespace TithorAutomation.Servicios
    {
    public class PlanificadorCamisetas : IPlanificadorProducto
        {
        private sealed class SeleccionMolde
            {
            public string Capa { get; set; }
            public string CodigoGrupo { get; set; }
            public List<string> Piezas { get; private set; }

            public SeleccionMolde()
                {
                Capa = string.Empty;
                CodigoGrupo = string.Empty;
                Piezas = new List<string>();
                }
            }

        public bool PuedeProcesar(string codigoProducto)
            {
            if (string.IsNullOrWhiteSpace(codigoProducto))
                return false;

            return codigoProducto.IndexOf("CAMISETA", StringComparison.OrdinalIgnoreCase) >= 0;
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

                string prenda = Normalizar(linea.ObtenerCampo("prenda"));
                bool tieneTallaCamiseta = !string.IsNullOrWhiteSpace(linea.ObtenerCampo("talla_camiseta"));
                bool tieneTallaShort = !string.IsNullOrWhiteSpace(linea.ObtenerCampo("talla_short"));
                bool solicitaCamiseta = tieneTallaCamiseta && prenda != "short";
                bool solicitaShort = tieneTallaShort || prenda == "camiseta_short" || prenda == "short";

                if (solicitaCamiseta)
                    AgregarCamiseta(linea, resultado.CodigoProducto, catalogo, plan);

                if (solicitaShort)
                    AgregarShort(linea, resultado.CodigoProducto, catalogo, plan);
                }

            if (plan.Moldes.Count == 0 && plan.Advertencias.Count == 0)
                plan.Advertencias.Add("El pedido no contiene prendas procesables.");

            return plan;
            }

        private void AgregarCamiseta(LineaPedido linea, string codigoProducto, List<Molde> catalogo, PlanProduccion plan)
            {
            string prenda = Normalizar(linea.ObtenerCampo("prenda"));
            string modelo = prenda == "bividi" ? "bividi" : Normalizar(linea.ObtenerCampo("modelo"));
            string corte = NormalizarCorte(linea.ObtenerCampo("corte"));
            string talla = NormalizarTalla(linea.ObtenerCampo("talla_camiseta"));
            string manga = NormalizarManga(linea.ObtenerCampo("manga"));
            string cuello = NormalizarCuello(linea.ObtenerCampo("cuello"));

            List<List<string>> piezasRequeridas = ObtenerPiezasCamiseta(modelo, manga, cuello);
            SeleccionMolde seleccion = BuscarSeleccion(catalogo, modelo, corte, talla, false, piezasRequeridas);

            if (seleccion == null)
                {
                plan.Advertencias.Add(CrearMensajeMoldeFaltante(linea.NumeroFila, modelo, corte, talla, piezasRequeridas));
                return;
                }

            AgregarSolicitud(plan, linea, codigoProducto, seleccion, talla, "Camiseta", 1);
            }

        private void AgregarShort(LineaPedido linea, string codigoProducto, List<Molde> catalogo, PlanProduccion plan)
            {
            string corte = NormalizarCorte(linea.ObtenerCampo("corte"));
            string talla = NormalizarTalla(linea.ObtenerCampo("talla_short"));

            List<List<string>> piezasRequeridas = new List<List<string>>
                {
                new List<string> { "short_izquierdo", "pierna_izquierda" },
                new List<string> { "short_derecho", "pierna_derecha" }
                };

            SeleccionMolde seleccion = BuscarSeleccion(catalogo, "short", corte, talla, true, piezasRequeridas);

            if (seleccion == null)
                {
                plan.Advertencias.Add(CrearMensajeMoldeFaltante(linea.NumeroFila, "short", corte, talla, piezasRequeridas));
                return;
                }

            AgregarSolicitud(plan, linea, codigoProducto, seleccion, talla, "Short", 2);
            }

        private List<List<string>> ObtenerPiezasCamiseta(string modelo, string manga, string cuello)
            {
            List<List<string>> piezas = new List<List<string>>();

            if (modelo == "bividi" || modelo == "manga_cero")
                {
                piezas.Add(ObtenerOpcionesFrente(cuello));
                piezas.Add(new List<string> { "espalda" });
                return piezas;
                }

            piezas.Add(ObtenerOpcionesFrente(cuello));
            piezas.Add(new List<string> { "espalda" });

            if (manga == "larga")
                {
                piezas.Add(new List<string> { "manga_larga_izquierda" });
                piezas.Add(new List<string> { "manga_larga_derecha" });
                }
            else
                {
                piezas.Add(new List<string> { "manga_corta_izquierda", "manga_izquierda" });
                piezas.Add(new List<string> { "manga_corta_derecha", "manga_derecha" });
                }

            return piezas;
            }

        private List<string> ObtenerOpcionesFrente(string cuello)
            {
            if (cuello == "v")
                return new List<string> { "frente_cuello_v", "frente_v" };

            if (cuello == "redondo")
                return new List<string> { "frente_cuello_redondo", "frente_redondo" };

            return new List<string> { "frente" };
            }

        private SeleccionMolde BuscarSeleccion(List<Molde> catalogo, string modelo, string corte, string talla, bool esShort, List<List<string>> piezasRequeridas)
            {
            Dictionary<string, List<Molde>> porCapa = new Dictionary<string, List<Molde>>(StringComparer.OrdinalIgnoreCase);

            foreach (Molde molde in catalogo)
                {
                if (molde == null || !molde.EsValido)
                    continue;

                if (!string.Equals(NormalizarTalla(molde.Talla), talla, StringComparison.OrdinalIgnoreCase))
                    continue;

                string capaNormalizada = Normalizar(molde.Capa);

                if (!CapaCorresponde(capaNormalizada, modelo, corte, esShort))
                    continue;

                if (!porCapa.ContainsKey(molde.Capa))
                    porCapa.Add(molde.Capa, new List<Molde>());

                porCapa[molde.Capa].Add(molde);
                }

            foreach (KeyValuePair<string, List<Molde>> grupoCapa in porCapa)
                {
                SeleccionMolde seleccion = CrearSeleccion(grupoCapa.Key, talla, grupoCapa.Value, piezasRequeridas);

                if (seleccion != null)
                    return seleccion;
                }

            return null;
            }

        private bool CapaCorresponde(string capa, string modelo, string corte, bool esShort)
            {
            if (string.IsNullOrWhiteSpace(capa))
                return false;

            bool contieneShort = ContieneToken(capa, "short");

            if (esShort != contieneShort)
                return false;

            if (!esShort && !ContieneToken(capa, modelo))
                return false;

            if (corte == "varon")
                return ContieneToken(capa, "varon") || ContieneToken(capa, "hombre") || ContieneToken(capa, "masculino");

            if (corte == "mujer")
                return ContieneToken(capa, "mujer") || ContieneToken(capa, "dama") || ContieneToken(capa, "femenino");

            return false;
            }

        private SeleccionMolde CrearSeleccion(string capa, string talla, List<Molde> moldes, List<List<string>> piezasRequeridas)
            {
            SeleccionMolde seleccion = new SeleccionMolde
                {
                Capa = capa,
                CodigoGrupo = "molde_" + talla
                };

            foreach (List<string> alternativas in piezasRequeridas)
                {
                Molde encontrado = BuscarPieza(moldes, alternativas);

                if (encontrado == null)
                    return null;

                if (!seleccion.Piezas.Contains(encontrado.NombreObjeto))
                    seleccion.Piezas.Add(encontrado.NombreObjeto);
                }

            return seleccion;
            }

        private Molde BuscarPieza(List<Molde> moldes, List<string> alternativas)
            {
            foreach (string alternativa in alternativas)
                {
                foreach (Molde molde in moldes)
                    {
                    if (string.Equals(Normalizar(molde.NombreObjeto), alternativa, StringComparison.OrdinalIgnoreCase))
                        return molde;
                    }
                }

            return null;
            }

        private void AgregarSolicitud(PlanProduccion plan, LineaPedido linea, string codigoProducto, SeleccionMolde seleccion, string talla, string tipo, int orden)
            {
            MoldeProduccion solicitud = new MoldeProduccion
                {
                CodigoProducto = codigoProducto,
                Diseno = linea.Diseno,
                Talla = talla.ToUpperInvariant(),
                Pieza = tipo,
                CodigoMolde = seleccion.CodigoGrupo,
                CapaMaster = seleccion.Capa,
                FilaExcel = linea.NumeroFila,
                NumeroUnidad = 1,
                NombreDestino = "prod_f" + linea.NumeroFila + "_" + orden + "_" + LimpiarNombre(tipo) + "_" + seleccion.CodigoGrupo
                };

            foreach (string pieza in seleccion.Piezas)
                solicitud.PiezasIncluidas.Add(pieza);

            CopiarCampos(linea, solicitud);
            plan.Moldes.Add(solicitud);
            }

        private string CrearMensajeMoldeFaltante(int fila, string modelo, string corte, string talla, List<List<string>> piezas)
            {
            List<string> nombres = new List<string>();

            foreach (List<string> alternativas in piezas)
                nombres.Add(string.Join("/", alternativas));

            return "Fila " + fila + ": no existe un grupo completo para " + modelo + ", " + corte + ", talla " + talla.ToUpperInvariant() + ". Piezas requeridas: " + string.Join(", ", nombres) + ".";
            }

        private void CopiarCampos(LineaPedido linea, MoldeProduccion solicitud)
            {
            foreach (KeyValuePair<string, string> campo in linea.Campos)
                solicitud.Campos[campo.Key] = campo.Value;
            }

        private bool ContieneToken(string texto, string token)
            {
            if (string.IsNullOrWhiteSpace(texto) || string.IsNullOrWhiteSpace(token))
                return false;

            string valor = "_" + texto.Trim('_') + "_";
            string buscado = "_" + token.Trim('_') + "_";

            return valor.IndexOf(buscado, StringComparison.OrdinalIgnoreCase) >= 0;
            }

        private string NormalizarCorte(string valor)
            {
            string corte = Normalizar(valor);

            if (corte == "dama" || corte == "mujer" || corte == "femenino")
                return "mujer";

            if (corte == "varon" || corte == "hombre" || corte == "masculino")
                return "varon";

            return corte;
            }

        private string NormalizarManga(string valor)
            {
            string manga = Normalizar(valor);

            if (manga == "larga" || manga == "manga_larga")
                return "larga";

            return "corta";
            }

        private string NormalizarCuello(string valor)
            {
            string cuello = Normalizar(valor);

            if (cuello == "v" || cuello == "cuello_v")
                return "v";

            if (cuello == "redondo" || cuello == "cuello_redondo")
                return "redondo";

            return cuello;
            }

        private string NormalizarTalla(string valor)
            {
            return Normalizar(valor).Replace("_", string.Empty);
            }

        private string Normalizar(string valor)
            {
            return AnalizadorMasterCorel.NormalizarCodigo(valor);
            }

        private string LimpiarNombre(string valor)
            {
            if (string.IsNullOrWhiteSpace(valor))
                return "sin_nombre";

            string nombre = Regex.Replace(valor.Trim(), @"[^a-zA-Z0-9]+", "_").Trim('_').ToLowerInvariant();

            return string.IsNullOrWhiteSpace(nombre) ? "sin_nombre" : nombre;
            }
        }
    }
