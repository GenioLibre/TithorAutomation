using TithorAutomation.Modelos;

namespace TithorAutomation.Servicios
    {
    public interface ILectorPedidoProducto
        {
        bool PuedeLeer(string codigoProducto);

        ResultadoAnalisisPedido Analizar(int productoId, string codigoProducto, string rutaExcel);
        }
    }