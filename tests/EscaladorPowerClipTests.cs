using System;
using TithorAutomation.Servicios;

// Pruebas sin iniciar CorelDRAW ni acceder a documentos o SQLite.
internal static class EscaladorPowerClipTests
{
    private static int comprobaciones;
    private static void Exigir(bool condicion, string mensaje)
    {
        comprobaciones++;
        if (!condicion) throw new Exception(mensaje);
    }
    private static void Nombre(string nombre, string piezaEsperada, string tallaEsperada)
    {
        string pieza, talla;
        Exigir(EscaladorPowerClip.InterpretarNombre(nombre, out pieza, out talla), "No reconoce " + nombre);
        Exigir(pieza == piezaEsperada && talla == tallaEsperada, "Clasificación incorrecta: " + nombre);
    }
    private static void Rechazar(double ancho, double alto, double destino)
    {
        bool rechazo = false;
        try { double w, h; EscaladorPowerClip.CalcularTamanoCobertura(ancho, alto, destino, destino, out w, out h); }
        catch (InvalidOperationException) { rechazo = true; }
        Exigir(rechazo, "Dimensiones inválidas aceptadas");
    }
    public static int Main()
    {
        try { return Ejecutar(); }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static int Ejecutar()
    {
        Nombre("funda_s_frente", "Frente", "S");
        Nombre("FUNDA_M_ESPALDA", "Espalda", "M");
        Nombre(" funda-l-lateral-derecho ", "Lateral derecho", "L");
        Nombre("funda_s_lateral_izquierdo", "Lateral izquierdo", "S");
        foreach (string grupo in new[] { "molde_l", "molde_l", "MOLDE_L", "prod_f3_u1_gatos_molde_l" })
        {
            string piezaGrupo, tallaGrupo;
            string talla = EscaladorPowerClip.TallaDelGrupo(grupo);
            Exigir(talla == "L", "Talla de grupo incorrecta");
            Exigir(EscaladorPowerClip.InterpretarPiezaEnGrupo("Frente", talla, out piezaGrupo, out tallaGrupo)
                && piezaGrupo == "Frente" && tallaGrupo == "L", "No reconoce Frente dentro del grupo");
        }
        string piezaLocal, tallaLocal;
        Exigir(EscaladorPowerClip.InterpretarPiezaEnGrupo("Lado derecho", "S", out piezaLocal, out tallaLocal)
            && piezaLocal == "Lateral derecho", "No reconoce lado derecho");
        Exigir(!EscaladorPowerClip.InterpretarPiezaEnGrupo("Frente", null, out piezaLocal, out tallaLocal), "Pieza sin talla aceptada");
        Exigir(!EscaladorPowerClip.InterpretarPiezaEnGrupo("funda_s_frente", "L", out piezaLocal, out tallaLocal), "Acepta talla contradictoria");
        Exigir(EscaladorPowerClip.TallaDelGrupo("molde_xl") == null, "Acepta talla no soportada");
        foreach (string nombre in new[] { null, "", "molde_s", "funda_xl_frente", "funda_s_frente_extra", "prod_f2_u1_diseno_molde_s", "funda_m_manga" })
        {
            string pieza, talla;
            Exigir(!EscaladorPowerClip.InterpretarNombre(nombre, out pieza, out talla), "Acepta un nombre ajeno: " + nombre);
        }
        Cobertura(100, 200, 500, 500, 500, 1000);
        Cobertura(300, 100, 50, 50, 150, 50);
        Cobertura(40, 80, 40, 80, 40, 80);
        Cobertura(200, 100, 100, 300, 600, 300);
        Rechazar(0, 10, 10);
        Rechazar(10, 0, 10);
        Rechazar(10, 10, -1);
        Rechazar(double.NaN, 10, 10);
        Rechazar(10, double.PositiveInfinity, 10);
        Rechazar(10, 10, double.PositiveInfinity);
        Rechazar(double.MaxValue, 1, 10);
        PedidoTests.Ejecutar();
        Console.WriteLine("PASS: " + comprobaciones + " comprobaciones de nombres, tallas y proporciones.");
        return 0;
    }

    private static void Cobertura(double w, double h, double dw, double dh, double ew, double eh)
    {
        double aw, ah;
        EscaladorPowerClip.CalcularTamanoCobertura(w, h, dw, dh, out aw, out ah);
        Exigir(aw == ew && ah == eh, "Cobertura incorrecta");
        Exigir(aw >= dw && ah >= dh, "El diseño no cubre el destino");
    }
}
