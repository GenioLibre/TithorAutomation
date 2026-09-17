using System.Collections.Generic;
using TithorAutomation.Modelos;

namespace TithorAutomation.Servicios
    {
    public interface IPlanificadorProducto
        {
        bool PuedeProcesar(string codigoProducto);

        PlanProduccion CrearPlan(ResultadoAnalisisPedido resultado, List<Molde> catalogo);
        }
    }