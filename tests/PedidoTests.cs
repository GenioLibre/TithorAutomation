using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using TithorAutomation.Modelos;
using TithorAutomation.Servicios;

internal static class PedidoTests
{
    private static int comprobaciones;
    private static void Exigir(bool condicion, string mensaje)
    {
        comprobaciones++;
        if (!condicion) throw new Exception(mensaje);
    }
    private static void Rechazar(Action accion, string mensaje)
    {
        bool rechazado = false;
        try { accion(); } catch (InvalidOperationException) { rechazado = true; }
        Exigir(rechazado, mensaje);
    }

    public static void Ejecutar()
    {
        CantidadesExcel();
        Identificadores();
        PlanesFundas();
        PlanesCamisetas();
        Console.WriteLine("PASS: " + comprobaciones + " comprobaciones de pedidos, cantidades y nombres.");
    }

    private static void CantidadesExcel()
    {
        string archivo = Path.Combine(Path.GetTempPath(), "tithor-test-" + Guid.NewGuid().ToString("N") + ".xlsx");
        CultureInfo anterior = CultureInfo.CurrentCulture;
        try
        {
            using (var libro = new XLWorkbook())
            {
                var hoja = libro.AddWorksheet("Detalle de Fundas");
                hoja.Cell(1, 1).Value = "Diseño";
                hoja.Cell(1, 2).Value = "Talla";
                hoja.Cell(1, 3).Value = "Cantidad";
                for (int fila = 2; fila <= 13; fila++)
                {
                    hoja.Cell(fila, 1).Value = "Prueba";
                    hoja.Cell(fila, 2).Value = "S";
                }
                hoja.Cell(2, 3).Value = "1,5";
                hoja.Cell(3, 3).Value = "1.5";
                hoja.Cell(4, 3).Value = 1.5;
                hoja.Cell(4, 3).Style.NumberFormat.Format = "0";
                hoja.Cell(5, 3).Value = 1500;
                hoja.Cell(5, 3).Style.NumberFormat.Format = "#,##0.00";
                hoja.Cell(6, 3).Value = " 12 ";
                hoja.Cell(7, 3).Value = 0;
                hoja.Cell(8, 3).Value = -1;
                hoja.Cell(9, 3).Value = 2147483648d;
                hoja.Cell(10, 3).FormulaA1 = "2+3";
                hoja.Cell(11, 3).Value = true;
                hoja.Cell(12, 3).Value = new DateTime(2026, 1, 1);
                hoja.Cell(13, 3).FormulaA1 = "3/2";
                libro.SaveAs(archivo);
            }
            foreach (string cultura in new[] { "en-US", "es-PE", "es-ES" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultura);
                var resultado = new LectorPedidoFundas().Analizar(1, "FUNDAS", archivo);
                foreach (var linea in resultado.Lineas)
                {
                    int esperado = linea.NumeroFila == 5 ? 1500 : linea.NumeroFila == 6 ? 12 : linea.NumeroFila == 10 ? 5 : 0;
                    Exigir(linea.Procesable == (esperado > 0), "Aceptación incorrecta en " + cultura + ", fila " + linea.NumeroFila);
                    if (esperado > 0) Exigir(linea.Cantidad == esperado, "Cantidad alterada por formato/cultura");
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
            if (File.Exists(archivo)) File.Delete(archivo);
        }
    }

    private static void Identificadores()
    {
        var plan = new PlanProduccion();
        plan.Moldes.Add(new MoldeProduccion { NombreDestino = "prod_f2_u1_gatos_molde_s" });
        CopiadorMoldesCorel.ValidarNombresDisponibles(plan, new[] { "otro_pedido" });
        Rechazar(() => CopiadorMoldesCorel.ValidarNombresDisponibles(plan, new[] { "PROD_F2_U1_GATOS_MOLDE_S" }), "Aceptó copia duplicada");
        plan.Moldes.Add(new MoldeProduccion { NombreDestino = "prod_f2_u1_gatos_molde_s" });
        Rechazar(() => CopiadorMoldesCorel.ValidarNombresDisponibles(plan, new string[0]), "Aceptó nombres repetidos dentro del plan");
        plan.Moldes.Clear();
        plan.Moldes.Add(new MoldeProduccion());
        Rechazar(() => CopiadorMoldesCorel.ValidarNombresDisponibles(plan, new string[0]), "Aceptó nombre vacío");
    }

    private static List<Molde> CatalogoFundas()
    {
        var catalogo = new List<Molde>();
        foreach (string talla in new[] { "s", "m", "l" })
            foreach (string pieza in new[] { "frente", "espalda", "lateral_izquierdo", "lateral_derecho" })
                catalogo.Add(new Molde { Codigo = "capa__molde_" + talla + "__funda_" + talla + "_" + pieza,
                    NombreObjeto = "funda_" + talla + "_" + pieza, Estado = "Correcto" });
        return catalogo;
    }

    private static void PlanesFundas()
    {
        var pedido = new ResultadoAnalisisPedido { CodigoProducto = "FUNDAS" };
        pedido.Lineas.Add(new LineaPedido { NumeroFila = 2, Diseno = "Gatos", Talla = "S", Cantidad = 2 });
        pedido.Lineas.Add(new LineaPedido { NumeroFila = 3, Diseno = "Perros", Talla = "M", Cantidad = 3 });
        pedido.Lineas.Add(new LineaPedido { NumeroFila = 4, Talla = "S", Cantidad = 5, Procesable = false });
        var catalogo = CatalogoFundas();
        var planificador = new PlanificadorFundas();
        var plan = planificador.CrearPlan(pedido, catalogo);
        Exigir(plan.EsValido && plan.TotalMoldes == 5, "Conteo de fundas incorrecto");
        Exigir(plan.Moldes.Select(x => x.NombreDestino).Distinct().Count() == 5, "Nombres no únicos");
        Exigir(plan.Moldes[1].NumeroUnidad == 2 && plan.Moldes[2].Diseno == "Perros", "Se alteró el orden o diseño");
        catalogo[0].Estado = "Inválido";
        Exigir(!planificador.CrearPlan(pedido, catalogo).EsValido, "Aceptó molde inválido o reutilizó caché obsoleta");
        catalogo.RemoveAt(0);
        Exigir(!planificador.CrearPlan(pedido, catalogo).EsValido, "Aceptó catálogo incompleto");
    }

    private static void PlanesCamisetas()
    {
        var catalogo = new List<Molde>();
        foreach (string pieza in new[] { "frente_cuello_v", "frente_cuello_redondo", "espalda", "manga_corta_izquierda", "manga_corta_derecha", "manga_larga_izquierda", "manga_larga_derecha" })
            catalogo.Add(new Molde { Codigo = "clasica__molde_s__" + pieza, NombreObjeto = pieza,
                Talla = "S", Capa = "clasica_varon", Estado = "Correcto" });
        var pedido = new ResultadoAnalisisPedido { CodigoProducto = "CAMISETAS" };
        for (int i = 0; i < 6; i++)
        {
            var linea = new LineaPedido { NumeroFila = i + 2, Cantidad = 1, Diseno = "Equipo" };
            linea.AgregarCampo("prenda", "camiseta");
            linea.AgregarCampo("modelo", "clasica");
            linea.AgregarCampo("corte", "varon");
            linea.AgregarCampo("talla_camiseta", "S");
            linea.AgregarCampo("manga", i % 2 == 0 ? "corta" : "larga");
            linea.AgregarCampo("cuello", i % 2 == 0 ? "v" : "redondo");
            linea.AgregarCampo("nombre", "Jugador " + i);
            pedido.Lineas.Add(linea);
        }
        var planificador = new PlanificadorCamisetas();
        var plan = planificador.CrearPlan(pedido, catalogo);
        Exigir(plan.EsValido && plan.TotalMoldes == 6, "Conteo de camisetas incorrecto");
        for (int i = 0; i < 6; i++)
        {
            Exigir(plan.Moldes[i].PiezasIncluidas.Contains(i % 2 == 0 ? "manga_corta_izquierda" : "manga_larga_izquierda"), "Caché mezcla mangas");
            Exigir(plan.Moldes[i].PiezasIncluidas.Contains(i % 2 == 0 ? "frente_cuello_v" : "frente_cuello_redondo"), "Caché mezcla cuellos");
            Exigir(plan.Moldes[i].ObtenerCampo("nombre") == "Jugador " + i, "Caché mezcla personalización");
        }
        catalogo.RemoveAt(0);
        Exigir(!planificador.CrearPlan(pedido, catalogo).EsValido, "Caché persiste entre pedidos");
    }
}
