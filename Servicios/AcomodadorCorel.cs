using System;
using System.Text.RegularExpressions;
using TithorAutomation.Modelos;
using VGCore;
using System.Collections.Generic;
using System.Linq;

namespace TithorAutomation.Servicios
    {
    public class AcomodadorCorel
        {
        private const string NombreCapaProduccion = "TITHOR_PRODUCCION";

        public ResultadoAcomodo Analizar(Application corel, Document documento, double anchoMaterialMm, double separacionMm, bool permitirRotacion)
            {
            if (corel == null)
                {
                throw new ArgumentNullException(nameof(corel));
                }

            if (documento == null)
                {
                throw new ArgumentNullException(nameof(documento));
                }

            if (anchoMaterialMm <= 0)
                {
                throw new InvalidOperationException("El ancho del material debe ser mayor que cero.");
                }

            if (separacionMm < 0)
                {
                throw new InvalidOperationException("La separación no puede ser negativa.");
                }

            Page pagina = documento.ActivePage;
            Layer capaProduccion = BuscarCapaProduccion(pagina);

            if (capaProduccion == null)
                {
                throw new InvalidOperationException(
                    "No se encontró la capa TITHOR_PRODUCCION en la página activa.");
                }

            ResultadoAcomodo resultado = new ResultadoAcomodo
                {
                PaginaAnalizada = pagina,
                AnchoMaterial = anchoMaterialMm,
                Separacion = separacionMm
                };

            int orden = 1;

            for (int i = 1; i <= capaProduccion.Shapes.Count; i++)
                {
                Shape objetoSuperior = capaProduccion.Shapes[i];

                if (EsGrupoProduccion(objetoSuperior))
                    {
                    AnalizarGrupoProduccion(
                        corel,
                        documento,
                        objetoSuperior,
                        resultado,
                        anchoMaterialMm,
                        permitirRotacion,
                        ref orden);
                    }
                else
                    {
                    PiezaAcomodable pieza = CrearPieza(
                        corel,
                        documento,
                        objetoSuperior,
                        null,
                        orden,
                        anchoMaterialMm,
                        permitirRotacion);

                    resultado.Piezas.Add(pieza);
                    orden++;
                    }
                }
            PlanificarAcomodo(resultado, permitirRotacion);
            return resultado;
            }

        private static Layer BuscarCapaProduccion(Page pagina)
            {
            for (int i = 1; i <= pagina.Layers.Count; i++)
                {
                Layer capa = pagina.Layers[i];

                if (string.Equals(
                    capa.Name,
                    NombreCapaProduccion,
                    StringComparison.OrdinalIgnoreCase))
                    {
                    return capa;
                    }
                }

            return null;
            }

        private static bool EsGrupoProduccion(Shape objeto)
            {
            if (objeto == null)
                {
                return false;
                }

            if (objeto.Type != cdrShapeType.cdrGroupShape)
                {
                return false;
                }

            string nombre = NombreSeguro(objeto)
                .Trim()
                .ToLowerInvariant();

            bool esGrupoPedido =
                nombre.StartsWith("prod_");

            bool esGrupoMolde =
                nombre.StartsWith("molde_") ||
                nombre.Contains("_molde_");

            return esGrupoPedido || esGrupoMolde;
            }

        private static void AnalizarGrupoProduccion(Application corel, Document documento, Shape grupo, ResultadoAcomodo resultado, double anchoMaterialMm, bool permitirRotacion, ref int orden)
            {
            if (grupo.Shapes.Count == 0)
                {
                resultado.Piezas.Add(
                    CrearPieza(
                        corel,
                        documento,
                        grupo,
                        null,
                        orden,
                        anchoMaterialMm,
                        permitirRotacion));

                orden++;
                return;
                }

            for (int i = 1; i <= grupo.Shapes.Count; i++)
                {
                Shape objeto = grupo.Shapes[i];

                PiezaAcomodable pieza = CrearPieza(
                    corel,
                    documento,
                    objeto,
                    grupo,
                    orden,
                    anchoMaterialMm,
                    permitirRotacion);

                resultado.Piezas.Add(pieza);
                orden++;
                }
            }

        private static PiezaAcomodable CrearPieza(Application corel, Document documento, Shape objeto, Shape grupoOrigen, int orden, double anchoMaterialMm, bool permitirRotacion)
            {
            string nombre = NombreSeguro(objeto);
            string nombreGrupo = NombreSeguro(grupoOrigen);

            double anchoMm = ConvertirAMilimetros(
                corel,
                documento,
                objeto.SizeWidth);

            double altoMm = ConvertirAMilimetros(
                corel,
                documento,
                objeto.SizeHeight);

            bool cabeNormal = anchoMm <= anchoMaterialMm;
            bool cabeRotada = permitirRotacion &&
                              altoMm <= anchoMaterialMm;

            bool procesable =
                anchoMm > 0 &&
                altoMm > 0 &&
                (cabeNormal || cabeRotada);

            string estado;

            if (anchoMm <= 0 || altoMm <= 0)
                {
                estado = "Medida inválida";
                }
            else if (cabeNormal)
                {
                estado = "Listo";
                }
            else if (cabeRotada)
                {
                estado = "Listo, requiere rotación";
                }
            else
                {
                estado = "Excede el ancho";
                }

            return new PiezaAcomodable
                {
                Orden = orden,
                StaticId = objeto.StaticID,
                GrupoOrigenStaticId =
                    grupoOrigen == null ? 0 : grupoOrigen.StaticID,

                Nombre = nombre,
                Pieza = DetectarPieza(nombre),
                Talla = DetectarTalla(nombre, nombreGrupo),
                Grupo = nombreGrupo,
                Estado = estado,

                AnchoOriginal = anchoMm,
                AltoOriginal = altoMm,
                AnchoAcomodado = anchoMm,
                AltoAcomodado = altoMm,

                Procesable = procesable,
                Rotada = false,

                Forma = objeto,
                GrupoOrigen = grupoOrigen
                };
            }

        private static double ConvertirAMilimetros(Application corel, Document documento, double medida)
            {
            return corel.ConvertUnits(
                medida,
                documento.Unit,
                cdrUnit.cdrMillimeter);
            }

        private static string NombreSeguro(Shape objeto)
            {
            if (objeto == null)
                {
                return string.Empty;
                }

            try
                {
                return objeto.Name ?? string.Empty;
                }
            catch
                {
                return string.Empty;
                }
            }

        private static string DetectarTalla(string nombreObjeto, string nombreGrupo)
            {
            string texto =
                (nombreObjeto + "_" + nombreGrupo).ToLowerInvariant();

            Match coincidencia = Regex.Match(
                texto,
                @"(?:^|_)(3xl|2xl|xxxl|xxl|xl|xs|s|m|l|[2-9]|1[0-9])(?:_|$)",
                RegexOptions.IgnoreCase);

            if (!coincidencia.Success)
                {
                return "-";
                }

            string talla = coincidencia.Groups[1].Value.ToUpperInvariant();

            if (talla == "XXL")
                {
                return "2XL";
                }

            if (talla == "XXXL")
                {
                return "3XL";
                }

            return talla;
            }

        private static string DetectarPieza(string nombre)
            {
            string texto = nombre
                .Trim()
                .ToLowerInvariant()
                .Replace("-", "_")
                .Replace(" ", "_");

            if (texto.Contains("lateral_izquierdo"))
                {
                return "Lateral izquierdo";
                }

            if (texto.Contains("lateral_derecho"))
                {
                return "Lateral derecho";
                }

            if (texto.Contains("manga_izquierda"))
                {
                return "Manga izquierda";
                }

            if (texto.Contains("manga_derecha"))
                {
                return "Manga derecha";
                }

            if (texto.Contains("pierna_izquierda"))
                {
                return "Pierna izquierda";
                }

            if (texto.Contains("pierna_derecha"))
                {
                return "Pierna derecha";
                }

            if (texto.Contains("espalda"))
                {
                return "Espalda";
                }

            if (texto.Contains("frente"))
                {
                return "Frente";
                }

            if (texto.Contains("cuello"))
                {
                return "Cuello";
                }

            if (string.IsNullOrWhiteSpace(nombre))
                {
                return "Sin nombre";
                }

            return nombre.Replace("_", " ");
            }
        public void Acomodar(Application corel, Document documento, ResultadoAcomodo resultado)
            {
            if (corel == null)
                {
                throw new ArgumentNullException(nameof(corel));
                }

            if (documento == null)
                {
                throw new ArgumentNullException(nameof(documento));
                }

            if (resultado == null || resultado.Piezas == null || resultado.Piezas.Count == 0)
                {
                throw new InvalidOperationException("No existe un análisis de acomodo para aplicar.");
                }

            if (resultado.TotalNoProcesables > 0)
                {
                throw new InvalidOperationException("Existen piezas que no pueden acomodarse dentro del ancho configurado.");
                }

            Page pagina = documento.ActivePage;

            if (pagina == null)
                {
                throw new InvalidOperationException("El documento no tiene una página activa.");
                }

            if (!EscaladorPowerClip.MismoObjetoCom(pagina, resultado.PaginaAnalizada))
                throw new InvalidOperationException("La página activa cambió después del análisis. Vuelva a analizar antes de acomodar.");

            bool grupoComandosIniciado = false;
            bool huboCambios = false;
            Exception errorAplicacion = null;

            try
                {
                documento.BeginCommandGroup("Acomodar elementos");
                grupoComandosIniciado = true;

                double anchoPagina = ConvertirMilimetrosADocumento(corel, documento, resultado.AnchoMaterial);
                double altoFinalMm = Math.Max(resultado.AltoEstimado, 10.0);
                double altoPagina = ConvertirMilimetrosADocumento(corel, documento, altoFinalMm);

                pagina.SetSize(anchoPagina, altoPagina);
                huboCambios = true;

                double limiteIzquierdo = pagina.LeftX;
                double limiteSuperior = pagina.TopY;

                DesagruparGruposSuperiores(resultado);

                foreach (PiezaAcomodable pieza in resultado.Piezas)
                    {
                    if (!pieza.Procesable || pieza.Forma == null)
                        {
                        continue;
                        }

                    if (pieza.Rotada)
                        {
                        pieza.Forma.Rotate(90.0);
                        }

                    double xDestino = limiteIzquierdo + ConvertirMilimetrosADocumento(corel, documento, pieza.XDestino);
                    double yDestino = limiteSuperior - ConvertirMilimetrosADocumento(corel, documento, pieza.YDestino);

                    pieza.Forma.CenterX = xDestino;
                    pieza.Forma.CenterY = yDestino;
                    }
                }
            catch (Exception ex)
                {
                errorAplicacion = ex;
                }
            finally
                {
                if (grupoComandosIniciado)
                    {
                    try
                        {
                        documento.EndCommandGroup();
                        }
                    catch
                        {
                        }
                    }

                try
                    {
                    corel.Refresh();
                    }
                catch
                    {
                    }
                }

            if (errorAplicacion != null)
                {
                if (huboCambios)
                    {
                    try
                        {
                        documento.Undo();
                        corel.Refresh();
                        }
                    catch
                        {
                        }
                    }

                throw new InvalidOperationException("No se pudo aplicar el acomodo en CorelDRAW. Los cambios realizados fueron revertidos.", errorAplicacion);
                }
            }

        private class FilaAcomodo
            {
            public double Y { get; set; }
            public double Alto { get; set; }
            public double AnchoUsado { get; set; }
            public int Cantidad { get; set; }
            }

        private static void PlanificarAcomodo(ResultadoAcomodo resultado, bool permitirRotacion)
            {
            if (resultado == null || resultado.Piezas == null)
                {
                return;
                }

            List<PiezaAcomodable> piezas = resultado.Piezas.Where(x => x.Procesable).ToList();
            HashSet<PiezaAcomodable> piezasRotadas = new HashSet<PiezaAcomodable>();

            foreach (PiezaAcomodable pieza in piezas)
                {
                pieza.Rotada = false;

                bool entraNormal = pieza.AnchoOriginal <= resultado.AnchoMaterial;
                bool entraRotada = permitirRotacion && pieza.AltoOriginal <= resultado.AnchoMaterial;

                if (!entraNormal && entraRotada)
                    {
                    piezasRotadas.Add(pieza);
                    }
                else if (!entraNormal)
                    {
                    pieza.Procesable = false;
                    pieza.Estado = "No cabe en el ancho";
                    }
                }

            piezas = resultado.Piezas.Where(x => x.Procesable).ToList();

            DistribucionCalculada mejorDistribucion = CalcularDistribucion(piezas, piezasRotadas, resultado.AnchoMaterial, resultado.Separacion);

            if (mejorDistribucion == null)
                {
                resultado.AltoEstimado = 0;
                return;
                }

            if (permitirRotacion)
                {
                bool huboMejora = true;

                while (huboMejora)
                    {
                    huboMejora = false;

                    PiezaAcomodable mejorPiezaParaRotar = null;
                    DistribucionCalculada distribucionMejorada = mejorDistribucion;

                    foreach (PiezaAcomodable pieza in piezas)
                        {
                        if (piezasRotadas.Contains(pieza))
                            {
                            continue;
                            }

                        if (pieza.AltoOriginal > resultado.AnchoMaterial)
                            {
                            continue;
                            }

                        HashSet<PiezaAcomodable> pruebaRotadas = new HashSet<PiezaAcomodable>(piezasRotadas);
                        pruebaRotadas.Add(pieza);

                        DistribucionCalculada distribucionPrueba = CalcularDistribucion(piezas, pruebaRotadas, resultado.AnchoMaterial, resultado.Separacion);

                        if (distribucionPrueba == null)
                            {
                            continue;
                            }

                        if (distribucionPrueba.AltoTotal < distribucionMejorada.AltoTotal - 0.1)
                            {
                            mejorPiezaParaRotar = pieza;
                            distribucionMejorada = distribucionPrueba;
                            }
                        }

                    if (mejorPiezaParaRotar != null)
                        {
                        piezasRotadas.Add(mejorPiezaParaRotar);
                        mejorDistribucion = distribucionMejorada;
                        huboMejora = true;
                        }
                    }
                }

            foreach (PiezaAcomodable pieza in resultado.Piezas)
                {
                pieza.Rotada = false;
                pieza.AnchoAcomodado = pieza.AnchoOriginal;
                pieza.AltoAcomodado = pieza.AltoOriginal;
                pieza.XDestino = 0;
                pieza.YDestino = 0;
                }

            foreach (KeyValuePair<PiezaAcomodable, PosicionCalculada> elemento in mejorDistribucion.Posiciones)
                {
                PiezaAcomodable pieza = elemento.Key;
                PosicionCalculada posicion = elemento.Value;

                pieza.Rotada = piezasRotadas.Contains(pieza);
                pieza.AnchoAcomodado = posicion.Ancho;
                pieza.AltoAcomodado = posicion.Alto;
                pieza.XDestino = posicion.X;
                pieza.YDestino = posicion.Y;
                }

            resultado.AltoEstimado = mejorDistribucion.AltoTotal;
            }
        private static DistribucionCalculada CalcularDistribucion(List<PiezaAcomodable> piezas, HashSet<PiezaAcomodable> piezasRotadas, double anchoMaterial, double separacion)
            {
            List<PiezaAcomodable> ordenadas = piezas
                .OrderByDescending(x => ObtenerAltoAcomodado(x, piezasRotadas))
                .ThenByDescending(x => ObtenerAnchoAcomodado(x, piezasRotadas))
                .ThenBy(x => x.Orden)
                .ToList();

            List<FilaCalculada> filas = new List<FilaCalculada>();

            foreach (PiezaAcomodable pieza in ordenadas)
                {
                double ancho = ObtenerAnchoAcomodado(pieza, piezasRotadas);
                double alto = ObtenerAltoAcomodado(pieza, piezasRotadas);

                if (ancho > anchoMaterial)
                    {
                    return null;
                    }

                FilaCalculada mejorFila = null;
                double menorEspacioRestante = double.MaxValue;

                foreach (FilaCalculada fila in filas)
                    {
                    double separacionPrevia = fila.Elementos.Count > 0 ? separacion : 0;
                    double anchoFinal = fila.AnchoUsado + separacionPrevia + ancho;

                    if (anchoFinal > anchoMaterial)
                        {
                        continue;
                        }

                    double espacioRestante = anchoMaterial - anchoFinal;

                    if (espacioRestante < menorEspacioRestante)
                        {
                        menorEspacioRestante = espacioRestante;
                        mejorFila = fila;
                        }
                    }

                if (mejorFila == null)
                    {
                    mejorFila = new FilaCalculada();
                    mejorFila.Alto = alto;
                    filas.Add(mejorFila);
                    }

                double separacionElemento = mejorFila.Elementos.Count > 0 ? separacion : 0;
                double inicioX = mejorFila.AnchoUsado + separacionElemento;

                mejorFila.Elementos.Add(new ElementoFilaCalculada
                    {
                    Pieza = pieza,
                    InicioX = inicioX,
                    Ancho = ancho,
                    Alto = alto
                    });

                mejorFila.AnchoUsado = inicioX + ancho;

                if (alto > mejorFila.Alto)
                    {
                    mejorFila.Alto = alto;
                    }
                }

            DistribucionCalculada distribucion = new DistribucionCalculada();
            double inicioY = 0;

            foreach (FilaCalculada fila in filas)
                {
                foreach (ElementoFilaCalculada elemento in fila.Elementos)
                    {
                    distribucion.Posiciones[elemento.Pieza] = new PosicionCalculada
                        {
                        X = elemento.InicioX + (elemento.Ancho / 2.0),
                        Y = inicioY + (elemento.Alto / 2.0),
                        Ancho = elemento.Ancho,
                        Alto = elemento.Alto
                        };
                    }

                inicioY += fila.Alto;

                if (fila != filas[filas.Count - 1])
                    {
                    inicioY += separacion;
                    }
                }

            distribucion.AltoTotal = inicioY;
            return distribucion;
            }

        private static double ObtenerAnchoAcomodado(PiezaAcomodable pieza, HashSet<PiezaAcomodable> piezasRotadas)
            {
            return piezasRotadas.Contains(pieza) ? pieza.AltoOriginal : pieza.AnchoOriginal;
            }

        private static double ObtenerAltoAcomodado(PiezaAcomodable pieza, HashSet<PiezaAcomodable> piezasRotadas)
            {
            return piezasRotadas.Contains(pieza) ? pieza.AnchoOriginal : pieza.AltoOriginal;
            }
        private static void ColocarPieza(PiezaAcomodable pieza, List<FilaAcomodo> filas, double anchoMaterial, double separacion, bool permitirRotacion)
            {
            FilaAcomodo mejorFila = null;
            bool mejorRotacion = false;
            double mejorAncho = 0;
            double mejorAlto = 0;
            double mejorSobrante = double.MaxValue;

            foreach (FilaAcomodo fila in filas)
                {
                EvaluarPosicion(
                    pieza.AnchoOriginal,
                    pieza.AltoOriginal,
                    false,
                    fila,
                    anchoMaterial,
                    separacion,
                    ref mejorFila,
                    ref mejorRotacion,
                    ref mejorAncho,
                    ref mejorAlto,
                    ref mejorSobrante);

                if (permitirRotacion)
                    {
                    EvaluarPosicion(
                        pieza.AltoOriginal,
                        pieza.AnchoOriginal,
                        true,
                        fila,
                        anchoMaterial,
                        separacion,
                        ref mejorFila,
                        ref mejorRotacion,
                        ref mejorAncho,
                        ref mejorAlto,
                        ref mejorSobrante);
                    }
                }

            if (mejorFila == null)
                {
                CrearNuevaFila(
                    pieza,
                    filas,
                    anchoMaterial,
                    separacion,
                    permitirRotacion);

                return;
                }

            double separacionAnterior =
                mejorFila.Cantidad > 0 ? separacion : 0;

            double xInicial =
                mejorFila.AnchoUsado +
                separacionAnterior;

            pieza.Rotada = mejorRotacion;
            pieza.AnchoAcomodado = mejorAncho;
            pieza.AltoAcomodado = mejorAlto;

            pieza.XDestino =
                xInicial +
                mejorAncho / 2.0;

            pieza.YDestino =
                mejorFila.Y +
                mejorAlto / 2.0;

            pieza.Estado =
                mejorRotacion
                    ? "Listo, rotar 90°"
                    : "Listo";

            mejorFila.AnchoUsado =
                xInicial +
                mejorAncho;

            mejorFila.Cantidad++;
            }

        private static void EvaluarPosicion(double ancho, double alto, bool rotada, FilaAcomodo fila, double anchoMaterial, double separacion, ref FilaAcomodo mejorFila, ref bool mejorRotacion, ref double mejorAncho, ref double mejorAlto, ref double mejorSobrante)
            {
            const double tolerancia = 0.001;

            if (alto > fila.Alto + tolerancia)
                {
                return;
                }

            double separacionAnterior =
                fila.Cantidad > 0 ? separacion : 0;

            double anchoFinal =
                fila.AnchoUsado +
                separacionAnterior +
                ancho;

            if (anchoFinal > anchoMaterial + tolerancia)
                {
                return;
                }

            double sobrante =
                anchoMaterial -
                anchoFinal;

            if (sobrante >= mejorSobrante)
                {
                return;
                }

            mejorFila = fila;
            mejorRotacion = rotada;
            mejorAncho = ancho;
            mejorAlto = alto;
            mejorSobrante = sobrante;
            }

        private static void CrearNuevaFila(PiezaAcomodable pieza, List<FilaAcomodo> filas, double anchoMaterial, double separacion, bool permitirRotacion)
            {
            bool cabeNormal =
                pieza.AnchoOriginal <= anchoMaterial;

            bool cabeRotada =
                permitirRotacion &&
                pieza.AltoOriginal <= anchoMaterial;

            if (!cabeNormal && !cabeRotada)
                {
                pieza.Procesable = false;
                pieza.Estado = "Excede el ancho";
                return;
                }

            bool rotar = false;

            if (!cabeNormal && cabeRotada)
                {
                rotar = true;
                }
            else if (cabeNormal && cabeRotada)
                {
                double altoNormal =
                    pieza.AltoOriginal;

                double altoRotado =
                    pieza.AnchoOriginal;

                rotar = altoRotado < altoNormal;
                }

            double ancho =
                rotar
                    ? pieza.AltoOriginal
                    : pieza.AnchoOriginal;

            double alto =
                rotar
                    ? pieza.AnchoOriginal
                    : pieza.AltoOriginal;

            double y = 0;

            if (filas.Count > 0)
                {
                FilaAcomodo ultimaFila =
                    filas[filas.Count - 1];

                y =
                    ultimaFila.Y +
                    ultimaFila.Alto +
                    separacion;
                }

            FilaAcomodo nuevaFila = new FilaAcomodo
                {
                Y = y,
                Alto = alto,
                AnchoUsado = ancho,
                Cantidad = 1
                };

            filas.Add(nuevaFila);

            pieza.Rotada = rotar;
            pieza.AnchoAcomodado = ancho;
            pieza.AltoAcomodado = alto;

            pieza.XDestino =
                ancho / 2.0;

            pieza.YDestino =
                y +
                alto / 2.0;

            pieza.Estado =
                rotar
                    ? "Listo, rotar 90°"
                    : "Listo";
            }

        private static void DesagruparGruposSuperiores(ResultadoAcomodo resultado)
            {
            HashSet<int> gruposProcesados = new HashSet<int>();

            foreach (PiezaAcomodable pieza in resultado.Piezas)
                {
                if (!pieza.Procesable || pieza.GrupoOrigen == null)
                    {
                    continue;
                    }

                int identificador = pieza.GrupoOrigenStaticId;

                if (gruposProcesados.Contains(identificador))
                    {
                    continue;
                    }

                pieza.GrupoOrigen.Ungroup();
                gruposProcesados.Add(identificador);
                }
            }

        private static double ConvertirMilimetrosADocumento(Application corel, Document documento, double valorMilimetros)
            {
            return corel.ConvertUnits(valorMilimetros, cdrUnit.cdrMillimeter, documento.Unit);
            }

        private class DistribucionCalculada
            {
            public DistribucionCalculada()
                {
                Posiciones = new Dictionary<PiezaAcomodable, PosicionCalculada>();
                }

            public Dictionary<PiezaAcomodable, PosicionCalculada> Posiciones { get; set; }
            public double AltoTotal { get; set; }
            }

        private class FilaCalculada
            {
            public FilaCalculada()
                {
                Elementos = new List<ElementoFilaCalculada>();
                }

            public List<ElementoFilaCalculada> Elementos { get; set; }
            public double AnchoUsado { get; set; }
            public double Alto { get; set; }
            }

        private class ElementoFilaCalculada
            {
            public PiezaAcomodable Pieza { get; set; }
            public double InicioX { get; set; }
            public double Ancho { get; set; }
            public double Alto { get; set; }
            }

        private class PosicionCalculada
            {
            public double X { get; set; }
            public double Y { get; set; }
            public double Ancho { get; set; }
            public double Alto { get; set; }
            }
        }
    }
        
        
        
