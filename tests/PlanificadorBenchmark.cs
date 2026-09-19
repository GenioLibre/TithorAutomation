using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using TithorAutomation.Modelos;
using TithorAutomation.Servicios;
class Benchmark
{
    static string Firma(PlanProduccion plan)
    {
        return string.Join("\n", plan.Moldes.Select(x => x.NombreDestino + "|" + x.CodigoMolde + "|" + x.Diseno + "|" + x.FilaExcel + "|" + x.NumeroUnidad + "|" + x.CapaMaster + "|" + string.Join(",", x.PiezasIncluidas) + "|" + string.Join(",", x.Campos.Select(c => c.Key + "=" + c.Value))));
    }
    static double Medir(Func<PlanProduccion> crear)
    {
        var tiempos = new List<double>();
        for (int i = 0; i < 3; i++) { var reloj = Stopwatch.StartNew(); crear(); reloj.Stop(); tiempos.Add(reloj.Elapsed.TotalMilliseconds); }
        tiempos.Sort(); return tiempos[1];
    }
    static void Probar(string nombre, IPlanificadorProducto antes, IPlanificadorProducto despues, ResultadoAnalisisPedido pedido, List<Molde> catalogo)
    {
        var a = antes.CrearPlan(pedido, catalogo);
        var b = despues.CrearPlan(pedido, catalogo);
        if (!a.EsValido || !b.EsValido || Firma(a) != Firma(b)) throw new Exception("Planes distintos: " + nombre);
        double ta = Medir(() => antes.CrearPlan(pedido, catalogo));
        double tb = Medir(() => despues.CrearPlan(pedido, catalogo));
        Console.WriteLine("{0}: filas={1}, catalogo={2}, antes={3:F2}ms, despues={4:F2}ms, factor={5:F1}x; planes identicos", nombre, pedido.Lineas.Count, catalogo.Count, ta, tb, ta/tb);
    }
    static int Main()
    {
        var catalogo = new List<Molde>();
        for (int i = 0; i < 1200; i++) catalogo.Add(new Molde { Codigo = "otros__molde_s__pieza_" + i, NombreObjeto = "pieza_" + i, Talla = "S", Capa = "otro_varon", Estado = "Correcto" });
        var fundas = new List<Molde>(catalogo);
        foreach (var talla in new[] { "s", "m", "l" }) foreach (var pieza in new[] { "frente", "espalda", "lateral_izquierdo", "lateral_derecho" })
            fundas.Add(new Molde { Codigo = "fundas__molde_" + talla + "__funda_" + talla + "_" + pieza, NombreObjeto = "funda_" + talla + "_" + pieza, Estado = "Correcto" });
        var camisetas = new List<Molde>(catalogo);
        foreach (var pieza in new[] { "frente_cuello_v", "espalda", "manga_corta_izquierda", "manga_corta_derecha" })
            camisetas.Add(new Molde { Codigo = "clasica__molde_s__" + pieza, NombreObjeto = pieza, Talla = "S", Capa = "clasica_varon", Estado = "Correcto" });
        var pf = new ResultadoAnalisisPedido { CodigoProducto = "FUNDAS" };
        var pc = new ResultadoAnalisisPedido { CodigoProducto = "CAMISETAS" };
        for (int i = 0; i < 1000; i++)
        {
            pf.Lineas.Add(new LineaPedido { NumeroFila = i + 2, Talla = new[] { "S", "M", "L" }[i%3], Cantidad = 2, Diseno = "Diseno " + i });
            var l = new LineaPedido { NumeroFila = i + 2, Cantidad = 1, Diseno = "Equipo" };
            l.AgregarCampo("prenda", "camiseta"); l.AgregarCampo("modelo", "clasica"); l.AgregarCampo("corte", "varon"); l.AgregarCampo("talla_camiseta", "S"); l.AgregarCampo("manga", "corta"); l.AgregarCampo("cuello", "v"); l.AgregarCampo("nombre", "Jugador " + i);
            pc.Lineas.Add(l);
        }
        Probar("Fundas", new AntesPlanificadorFundas(), new PlanificadorFundas(), pf, fundas);
        Probar("Camisetas", new AntesPlanificadorCamisetas(), new PlanificadorCamisetas(), pc, camisetas);
        return 0;
    }
}
