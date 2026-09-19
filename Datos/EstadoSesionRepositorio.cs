using System;
using System.Data.SQLite;

namespace TithorAutomation.Datos
    {
    public class EstadoSesionRepositorio
        {
        public void Guardar(string clave, string valor)
            {
            if (string.IsNullOrWhiteSpace(clave))
                throw new ArgumentException("La clave de sesión está vacía.", nameof(clave));

            using (SQLiteConnection conexion = BaseDatos.CrearConexion())
                {
                conexion.Open();

                const string sql = @"
INSERT OR REPLACE INTO EstadoSesion
(
    Clave,
    Valor,
    FechaActualizacion
)
VALUES
(
    @Clave,
    @Valor,
    @FechaActualizacion
);";

                using (SQLiteCommand comando = new SQLiteCommand(sql, conexion))
                    {
                    comando.Parameters.AddWithValue("@Clave", clave.Trim());
                    comando.Parameters.AddWithValue("@Valor", valor ?? string.Empty);
                    comando.Parameters.AddWithValue("@FechaActualizacion", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    comando.ExecuteNonQuery();
                    }
                }
            }

        public string Obtener(string clave)
            {
            if (string.IsNullOrWhiteSpace(clave))
                return string.Empty;

            using (SQLiteConnection conexion = BaseDatos.CrearConexion())
                {
                conexion.Open();

                const string sql = "SELECT Valor FROM EstadoSesion WHERE Clave = @Clave LIMIT 1;";

                using (SQLiteCommand comando = new SQLiteCommand(sql, conexion))
                    {
                    comando.Parameters.AddWithValue("@Clave", clave.Trim());
                    object resultado = comando.ExecuteScalar();
                    return resultado == null || resultado == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(resultado);
                    }
                }
            }

        public void Eliminar(params string[] claves)
            {
            if (claves == null || claves.Length == 0)
                return;

            using (SQLiteConnection conexion = BaseDatos.CrearConexion())
                {
                conexion.Open();

                foreach (string clave in claves)
                    {
                    if (string.IsNullOrWhiteSpace(clave))
                        continue;

                    using (SQLiteCommand comando = new SQLiteCommand("DELETE FROM EstadoSesion WHERE Clave = @Clave;", conexion))
                        {
                        comando.Parameters.AddWithValue("@Clave", clave.Trim());
                        comando.ExecuteNonQuery();
                        }
                    }
                }
            }
        }
    }