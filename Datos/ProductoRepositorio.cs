using System;
using System.Collections.Generic;
using System.Data.SQLite;
using TithorAutomation.Modelos;

namespace TithorAutomation.Datos
{
    public class ProductoRepositorio
    {
        public List<Producto> ListarActivos()
        {
            List<Producto> productos =
                new List<Producto>();

            const string sql = @"
SELECT
    Id,
    Nombre,
    Codigo,
    Descripcion,
    Activo,
    FechaCreacion
FROM Productos
WHERE Activo = 1
ORDER BY Nombre;
";

            using (
                SQLiteConnection conexion =
                    BaseDatos.CrearConexion())
            using (
                SQLiteCommand comando =
                    new SQLiteCommand(
                        sql,
                        conexion
                    ))
            {
                conexion.Open();

                using (
                    SQLiteDataReader lector =
                        comando.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        productos.Add(
                            LeerProducto(lector)
                        );
                    }
                }
            }

            return productos;
        }

        public Producto ObtenerPorId(
            int productoId)
        {
            const string sql = @"
SELECT
    Id,
    Nombre,
    Codigo,
    Descripcion,
    Activo,
    FechaCreacion
FROM Productos
WHERE Id = @Id
LIMIT 1;
";

            using (
                SQLiteConnection conexion =
                    BaseDatos.CrearConexion())
            using (
                SQLiteCommand comando =
                    new SQLiteCommand(
                        sql,
                        conexion
                    ))
            {
                comando.Parameters.AddWithValue(
                    "@Id",
                    productoId
                );

                conexion.Open();

                using (
                    SQLiteDataReader lector =
                        comando.ExecuteReader())
                {
                    if (lector.Read())
                    {
                        return LeerProducto(lector);
                    }
                }
            }

            return null;
        }

        public int Agregar(
            Producto producto)
        {
            ValidarProducto(producto);

            string codigo =
                NormalizarCodigo(producto.Codigo);

            if (ExisteCodigo(codigo))
            {
                throw new InvalidOperationException(
                    "Ya existe un producto con el código " +
                    codigo + "."
                );
            }

            const string sql = @"
INSERT INTO Productos
(
    Nombre,
    Codigo,
    Descripcion,
    Activo,
    FechaCreacion
)
VALUES
(
    @Nombre,
    @Codigo,
    @Descripcion,
    1,
    datetime('now')
);

SELECT last_insert_rowid();
";

            using (
                SQLiteConnection conexion =
                    BaseDatos.CrearConexion())
            using (
                SQLiteCommand comando =
                    new SQLiteCommand(
                        sql,
                        conexion
                    ))
            {
                comando.Parameters.AddWithValue(
                    "@Nombre",
                    producto.Nombre.Trim()
                );

                comando.Parameters.AddWithValue(
                    "@Codigo",
                    codigo
                );

                comando.Parameters.AddWithValue(
                    "@Descripcion",
                    producto.Descripcion == null
                        ? ""
                        : producto.Descripcion.Trim()
                );

                conexion.Open();

                int nuevoId =
                    Convert.ToInt32(
                        comando.ExecuteScalar()
                    );

                CrearConfiguracionPredeterminada(
                    nuevoId,
                    conexion
                );

                return nuevoId;
            }
        }

        public void Actualizar(
            Producto producto)
        {
            if (producto.Id <= 0)
            {
                throw new ArgumentException(
                    "El producto no tiene un identificador válido."
                );
            }

            ValidarProducto(producto);

            string codigo =
                NormalizarCodigo(producto.Codigo);

            if (ExisteCodigo(
                codigo,
                producto.Id))
            {
                throw new InvalidOperationException(
                    "Ya existe otro producto con el código " +
                    codigo + "."
                );
            }

            const string sql = @"
UPDATE Productos
SET
    Nombre = @Nombre,
    Codigo = @Codigo,
    Descripcion = @Descripcion
WHERE Id = @Id;
";

            using (
                SQLiteConnection conexion =
                    BaseDatos.CrearConexion())
            using (
                SQLiteCommand comando =
                    new SQLiteCommand(
                        sql,
                        conexion
                    ))
            {
                comando.Parameters.AddWithValue(
                    "@Nombre",
                    producto.Nombre.Trim()
                );

                comando.Parameters.AddWithValue(
                    "@Codigo",
                    codigo
                );

                comando.Parameters.AddWithValue(
                    "@Descripcion",
                    producto.Descripcion == null
                        ? ""
                        : producto.Descripcion.Trim()
                );

                comando.Parameters.AddWithValue(
                    "@Id",
                    producto.Id
                );

                conexion.Open();
                comando.ExecuteNonQuery();
            }
        }

        public void Desactivar(
            int productoId)
        {
            const string sql = @"
UPDATE Productos
SET Activo = 0
WHERE Id = @Id;
";

            using (
                SQLiteConnection conexion =
                    BaseDatos.CrearConexion())
            using (
                SQLiteCommand comando =
                    new SQLiteCommand(
                        sql,
                        conexion
                    ))
            {
                comando.Parameters.AddWithValue(
                    "@Id",
                    productoId
                );

                conexion.Open();
                comando.ExecuteNonQuery();
            }
        }

        public bool ExisteCodigo(
            string codigo,
            int excluirProductoId = 0)
        {
            const string sql = @"
SELECT COUNT(*)
FROM Productos
WHERE Codigo = @Codigo
AND
(
    @ExcluirId = 0
    OR Id <> @ExcluirId
);
";

            using (
                SQLiteConnection conexion =
                    BaseDatos.CrearConexion())
            using (
                SQLiteCommand comando =
                    new SQLiteCommand(
                        sql,
                        conexion
                    ))
            {
                comando.Parameters.AddWithValue(
                    "@Codigo",
                    NormalizarCodigo(codigo)
                );

                comando.Parameters.AddWithValue(
                    "@ExcluirId",
                    excluirProductoId
                );

                conexion.Open();

                int cantidad =
                    Convert.ToInt32(
                        comando.ExecuteScalar()
                    );

                return cantidad > 0;
            }
        }

        private void CrearConfiguracionPredeterminada(
            int productoId,
            SQLiteConnection conexion)
        {
            const string sql = @"
INSERT OR IGNORE INTO ConfiguracionProducto
(
    ProductoId,
    AnchoRollo,
    Separacion,
    PermitirRotacion,
    CerrarMasterSinGuardar
)
VALUES
(
    @ProductoId,
    1.60,
    0,
    1,
    1
);
";

            using (
                SQLiteCommand comando =
                    new SQLiteCommand(
                        sql,
                        conexion
                    ))
            {
                comando.Parameters.AddWithValue(
                    "@ProductoId",
                    productoId
                );

                comando.ExecuteNonQuery();
            }
        }

        private Producto LeerProducto(
            SQLiteDataReader lector)
        {
            DateTime fechaCreacion;

            DateTime.TryParse(
                Convert.ToString(
                    lector["FechaCreacion"]
                ),
                out fechaCreacion
            );

            return new Producto
            {
                Id = Convert.ToInt32(
                    lector["Id"]
                ),

                Nombre = Convert.ToString(
                    lector["Nombre"]
                ),

                Codigo = Convert.ToString(
                    lector["Codigo"]
                ),

                Descripcion = Convert.ToString(
                    lector["Descripcion"]
                ),

                Activo = Convert.ToInt32(
                    lector["Activo"]
                ) == 1,

                FechaCreacion = fechaCreacion
            };
        }

        private void ValidarProducto(
            Producto producto)
        {
            if (producto == null)
            {
                throw new ArgumentNullException(
                    nameof(producto)
                );
            }

            if (string.IsNullOrWhiteSpace(
                producto.Nombre))
            {
                throw new ArgumentException(
                    "Ingrese el nombre del producto."
                );
            }

            if (string.IsNullOrWhiteSpace(
                producto.Codigo))
            {
                throw new ArgumentException(
                    "Ingrese el código del producto."
                );
            }
        }

        public static string NormalizarCodigo(
            string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return "";

            string resultado =
                codigo.Trim().ToUpperInvariant();

            resultado =
                resultado.Replace(" ", "_");

            resultado =
                resultado.Replace("-", "_");

            return resultado;
        }
    }
}