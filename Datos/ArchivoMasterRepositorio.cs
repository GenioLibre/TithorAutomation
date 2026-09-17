using System;
using System.Data.SQLite;
using TithorAutomation.Modelos;

namespace TithorAutomation.Datos
    {
    public class ArchivoMasterRepositorio
        {
        public ArchivoMaster ObtenerPrincipalPorProducto(int productoId)
            {
            const string sql = @"
                SELECT
                    Id,
                    ProductoId,
                    Ruta AS RutaArchivo,
                    NombreArchivo,
                    HashArchivo,
                    TamanoArchivo,
                    FechaModificacionArchivo AS FechaModificacion,
                    UltimoAnalisis AS FechaUltimoAnalisis,
                    TotalMoldes AS CantidadMoldes,
                    EsPrincipal,
                    Activo
                FROM ArchivosMaster
                WHERE ProductoId = @ProductoId
                  AND EsPrincipal = 1
                  AND Activo = 1
                ORDER BY Id DESC
                LIMIT 1;";

            using (SQLiteConnection conexion =
                   BaseDatos.CrearConexion())
                {
                conexion.Open();

                using (SQLiteCommand comando =
                       new SQLiteCommand(sql, conexion))
                    {
                    comando.Parameters.AddWithValue(
                        "@ProductoId",
                        productoId
                    );

                    using (SQLiteDataReader lector =
                           comando.ExecuteReader())
                        {
                        if (!lector.Read())
                            return null;

                        return LeerArchivoMaster(lector);
                        }
                    }
                }
            }

        public int GuardarPrincipal(ArchivoMaster archivo)
            {
            Validar(archivo);

            using (SQLiteConnection conexion =
                   BaseDatos.CrearConexion())
                {
                conexion.Open();

                using (SQLiteTransaction transaccion =
                       conexion.BeginTransaction())
                    {
                    try
                        {
                        DesmarcarPrincipales(
                            archivo.ProductoId,
                            archivo.Id,
                            conexion,
                            transaccion
                        );

                        if (archivo.Id > 0)
                            {
                            Actualizar(
                                archivo,
                                conexion,
                                transaccion
                            );
                            }
                        else
                            {
                            archivo.Id = Insertar(
                                archivo,
                                conexion,
                                transaccion
                            );
                            }

                        transaccion.Commit();
                        return archivo.Id;
                        }
                    catch
                        {
                        transaccion.Rollback();
                        throw;
                        }
                    }
                }
            }

        public void ActualizarDatosAnalisis(
            int archivoMasterId,
            string hashArchivo,
            long tamanoArchivo,
            DateTime fechaModificacion,
            DateTime fechaAnalisis,
            int cantidadMoldes)
            {
            const string sql = @"
                UPDATE ArchivosMaster
                SET HashArchivo = @HashArchivo,
                    TamanoArchivo = @TamanoArchivo,
                    FechaModificacionArchivo = @FechaModificacion,
                    UltimoAnalisis = @FechaUltimoAnalisis,
                    TotalMoldes = @CantidadMoldes
                WHERE Id = @Id;";

            using (SQLiteConnection conexion =
                   BaseDatos.CrearConexion())
                {
                conexion.Open();

                using (SQLiteCommand comando =
                       new SQLiteCommand(sql, conexion))
                    {
                    comando.Parameters.AddWithValue(
                        "@HashArchivo",
                        hashArchivo ?? string.Empty
                    );

                    comando.Parameters.AddWithValue(
                        "@TamanoArchivo",
                        tamanoArchivo
                    );

                    comando.Parameters.AddWithValue(
                        "@FechaModificacion",
                        fechaModificacion.ToString("o")
                    );

                    comando.Parameters.AddWithValue(
                        "@FechaUltimoAnalisis",
                        fechaAnalisis.ToString("o")
                    );

                    comando.Parameters.AddWithValue(
                        "@CantidadMoldes",
                        cantidadMoldes
                    );

                    comando.Parameters.AddWithValue(
                        "@Id",
                        archivoMasterId
                    );

                    comando.ExecuteNonQuery();
                    }
                }
            }

        private int Insertar(
            ArchivoMaster archivo,
            SQLiteConnection conexion,
            SQLiteTransaction transaccion)
            {
            const string sql = @"
                INSERT INTO ArchivosMaster
                (
                    ProductoId,
                    Ruta,
                    NombreArchivo,
                    HashArchivo,
                    TamanoArchivo,
                    FechaModificacionArchivo,
                    UltimoAnalisis,
                    TotalMoldes,
                    EsPrincipal,
                    Activo
                )
                VALUES
                (
                    @ProductoId,
                    @RutaArchivo,
                    @NombreArchivo,
                    @HashArchivo,
                    @TamanoArchivo,
                    @FechaModificacion,
                    @FechaUltimoAnalisis,
                    @CantidadMoldes,
                    1,
                    1
                );

                SELECT last_insert_rowid();";

            using (SQLiteCommand comando =
                   new SQLiteCommand(sql, conexion, transaccion))
                {
                AgregarParametros(comando, archivo);

                object resultado = comando.ExecuteScalar();
                return Convert.ToInt32(resultado);
                }
            }

        private void Actualizar(
            ArchivoMaster archivo,
            SQLiteConnection conexion,
            SQLiteTransaction transaccion)
            {
            const string sql = @"
                UPDATE ArchivosMaster
                SET ProductoId = @ProductoId,
                    Ruta = @RutaArchivo,
                    NombreArchivo = @NombreArchivo,
                    HashArchivo = @HashArchivo,
                    TamanoArchivo = @TamanoArchivo,
                    FechaModificacionArchivo = @FechaModificacion,
                    UltimoAnalisis = @FechaUltimoAnalisis,
                    TotalMoldes = @CantidadMoldes,
                    EsPrincipal = 1,
                    Activo = 1
                WHERE Id = @Id;";

            using (SQLiteCommand comando =
                   new SQLiteCommand(sql, conexion, transaccion))
                {
                AgregarParametros(comando, archivo);

                comando.Parameters.AddWithValue(
                    "@Id",
                    archivo.Id
                );

                comando.ExecuteNonQuery();
                }
            }

        private void DesmarcarPrincipales(
            int productoId,
            int archivoIdExcluido,
            SQLiteConnection conexion,
            SQLiteTransaction transaccion)
            {
            const string sql = @"
                UPDATE ArchivosMaster
                SET EsPrincipal = 0
                WHERE ProductoId = @ProductoId
                  AND Id <> @IdExcluido;";

            using (SQLiteCommand comando =
                   new SQLiteCommand(sql, conexion, transaccion))
                {
                comando.Parameters.AddWithValue(
                    "@ProductoId",
                    productoId
                );

                comando.Parameters.AddWithValue(
                    "@IdExcluido",
                    archivoIdExcluido
                );

                comando.ExecuteNonQuery();
                }
            }

        private void AgregarParametros(
            SQLiteCommand comando,
            ArchivoMaster archivo)
            {
            comando.Parameters.AddWithValue(
                "@ProductoId",
                archivo.ProductoId
            );

            comando.Parameters.AddWithValue(
                "@RutaArchivo",
                archivo.RutaArchivo
            );

            comando.Parameters.AddWithValue(
                "@NombreArchivo",
                archivo.NombreArchivo
            );

            comando.Parameters.AddWithValue(
                "@HashArchivo",
                archivo.HashArchivo ?? string.Empty
            );

            comando.Parameters.AddWithValue(
                "@TamanoArchivo",
                archivo.TamanoArchivo
            );

            comando.Parameters.AddWithValue(
                "@FechaModificacion",
                ConvertirFecha(archivo.FechaModificacion)
            );

            comando.Parameters.AddWithValue(
                "@FechaUltimoAnalisis",
                ConvertirFecha(archivo.FechaUltimoAnalisis)
            );

            comando.Parameters.AddWithValue(
                "@CantidadMoldes",
                archivo.CantidadMoldes
            );
            }

        private object ConvertirFecha(DateTime? fecha)
            {
            if (!fecha.HasValue)
                return DBNull.Value;

            return fecha.Value.ToString("o");
            }

        private ArchivoMaster LeerArchivoMaster(
            SQLiteDataReader lector)
            {
            return new ArchivoMaster
                {
                Id = Convert.ToInt32(lector["Id"]),
                ProductoId =
                    Convert.ToInt32(lector["ProductoId"]),

                RutaArchivo =
                    Convert.ToString(lector["RutaArchivo"]),

                NombreArchivo =
                    Convert.ToString(lector["NombreArchivo"]),

                HashArchivo =
                    Convert.ToString(lector["HashArchivo"]),

                TamanoArchivo =
                    lector["TamanoArchivo"] == DBNull.Value
                        ? 0
                        : Convert.ToInt64(
                            lector["TamanoArchivo"]
                        ),

                FechaModificacion =
                    LeerFecha(lector["FechaModificacion"]),

                FechaUltimoAnalisis =
                    LeerFecha(lector["FechaUltimoAnalisis"]),

                CantidadMoldes =
                    lector["CantidadMoldes"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(
                            lector["CantidadMoldes"]
                        ),

                EsPrincipal =
                    Convert.ToInt32(
                        lector["EsPrincipal"]
                    ) == 1,

                Activo =
                    Convert.ToInt32(
                        lector["Activo"]
                    ) == 1
                };
            }

        private DateTime? LeerFecha(object valor)
            {
            if (valor == null || valor == DBNull.Value)
                return null;

            DateTime fecha;

            if (DateTime.TryParse(
                Convert.ToString(valor),
                out fecha))
                {
                return fecha;
                }

            return null;
            }

        private void Validar(ArchivoMaster archivo)
            {
            if (archivo == null)
                {
                throw new ArgumentNullException(
                    nameof(archivo)
                );
                }

            if (archivo.ProductoId <= 0)
                {
                throw new InvalidOperationException(
                    "El archivo Master no tiene un producto válido."
                );
                }

            if (string.IsNullOrWhiteSpace(
                archivo.RutaArchivo))
                {
                throw new InvalidOperationException(
                    "La ruta del archivo Master está vacía."
                );
                }

            if (string.IsNullOrWhiteSpace(
                archivo.NombreArchivo))
                {
                throw new InvalidOperationException(
                    "El nombre del archivo Master está vacío."
                );
                }
            }
        }
    }