using System;
using System.Collections.Generic;
using System.Data.SQLite;
using TithorAutomation.Modelos;

namespace TithorAutomation.Datos
    {
    public sealed class MockupRepositorio
        {
        public ArchivoMockupMaster ObtenerMaster()
            {
            using (SQLiteConnection conexion = BaseDatos.CrearConexion())
                {
                conexion.Open();
                string sql = "SELECT Id, Ruta, NombreArchivo, HashArchivo, TamanoArchivo, FechaModificacionArchivo, UltimoAnalisis, TotalMockups FROM ArchivosMockupMaster WHERE Activo = 1 ORDER BY Id DESC LIMIT 1;";

                using (SQLiteCommand comando = new SQLiteCommand(sql, conexion))
                using (SQLiteDataReader lector = comando.ExecuteReader())
                    {
                    if (!lector.Read()) return null;

                    return new ArchivoMockupMaster
                        {
                        Id = Convert.ToInt32(lector["Id"]),
                        RutaArchivo = Convert.ToString(lector["Ruta"]),
                        NombreArchivo = Convert.ToString(lector["NombreArchivo"]),
                        HashArchivo = Convert.ToString(lector["HashArchivo"]),
                        TamanoArchivo = lector["TamanoArchivo"] == DBNull.Value ? 0 : Convert.ToInt64(lector["TamanoArchivo"]),
                        FechaModificacionArchivo = ConvertirFecha(lector["FechaModificacionArchivo"]),
                        FechaUltimoAnalisis = lector["UltimoAnalisis"] == DBNull.Value ? (DateTime?)null : ConvertirFecha(lector["UltimoAnalisis"]),
                        CantidadMockups = Convert.ToInt32(lector["TotalMockups"])
                        };
                    }
                }
            }

        public ArchivoMockupMaster GuardarMaster(ArchivoMockupMaster master)
            {
            using (SQLiteConnection conexion = BaseDatos.CrearConexion())
                {
                conexion.Open();
                using (SQLiteTransaction transaccion = conexion.BeginTransaction())
                    {
                    new SQLiteCommand("UPDATE ArchivosMockupMaster SET Activo = 0;", conexion, transaccion).ExecuteNonQuery();

                    string sql = @"INSERT INTO ArchivosMockupMaster (Ruta, NombreArchivo, HashArchivo, TamanoArchivo, FechaModificacionArchivo, TotalMockups, Activo)
VALUES (@Ruta, @Nombre, @Hash, @Tamano, @Fecha, 0, 1); SELECT last_insert_rowid();";

                    using (SQLiteCommand comando = new SQLiteCommand(sql, conexion, transaccion))
                        {
                        comando.Parameters.AddWithValue("@Ruta", master.RutaArchivo);
                        comando.Parameters.AddWithValue("@Nombre", master.NombreArchivo);
                        comando.Parameters.AddWithValue("@Hash", master.HashArchivo ?? string.Empty);
                        comando.Parameters.AddWithValue("@Tamano", master.TamanoArchivo);
                        comando.Parameters.AddWithValue("@Fecha", master.FechaModificacionArchivo.ToString("o"));
                        master.Id = Convert.ToInt32((long)comando.ExecuteScalar());
                        }

                    transaccion.Commit();
                    return master;
                    }
                }
            }

        public List<Mockup> ObtenerMockups(int masterId)
            {
            List<Mockup> resultado = new List<Mockup>();

            using (SQLiteConnection conexion = BaseDatos.CrearConexion())
                {
                conexion.Open();
                string sql = "SELECT Id, MasterId, Codigo, NombreGrupo, Producto, Estado, FechaAnalisis FROM Mockups WHERE MasterId = @MasterId ORDER BY Id;";

                using (SQLiteCommand comando = new SQLiteCommand(sql, conexion))
                    {
                    comando.Parameters.AddWithValue("@MasterId", masterId);

                    using (SQLiteDataReader lector = comando.ExecuteReader())
                        {
                        while (lector.Read())
                            {
                            resultado.Add(new Mockup
                                {
                                Id = Convert.ToInt32(lector["Id"]),
                                MasterId = Convert.ToInt32(lector["MasterId"]),
                                Codigo = Convert.ToString(lector["Codigo"]),
                                NombreGrupo = Convert.ToString(lector["NombreGrupo"]),
                                Producto = Convert.ToString(lector["Producto"]),
                                Estado = Convert.ToString(lector["Estado"]),
                                FechaAnalisis = ConvertirFecha(lector["FechaAnalisis"])
                                });
                            }
                        }
                    }
                }

            return resultado;
            }

        public void Sincronizar(ArchivoMockupMaster master, IList<Mockup> mockups)
            {
            using (SQLiteConnection conexion = BaseDatos.CrearConexion())
                {
                conexion.Open();
                using (SQLiteTransaction transaccion = conexion.BeginTransaction())
                    {
                    using (SQLiteCommand borrar = new SQLiteCommand("DELETE FROM Mockups WHERE MasterId = @MasterId;", conexion, transaccion))
                        {
                        borrar.Parameters.AddWithValue("@MasterId", master.Id);
                        borrar.ExecuteNonQuery();
                        }

                    string sql = @"INSERT INTO Mockups (MasterId, Codigo, NombreGrupo, Producto, Estado, FechaAnalisis)
VALUES (@MasterId, @Codigo, @NombreGrupo, @Producto, @Estado, @Fecha);";

                    foreach (Mockup mockup in mockups)
                        {
                        using (SQLiteCommand comando = new SQLiteCommand(sql, conexion, transaccion))
                            {
                            comando.Parameters.AddWithValue("@MasterId", master.Id);
                            comando.Parameters.AddWithValue("@Codigo", mockup.Codigo);
                            comando.Parameters.AddWithValue("@NombreGrupo", mockup.NombreGrupo);
                            comando.Parameters.AddWithValue("@Producto", mockup.Producto);
                            comando.Parameters.AddWithValue("@Estado", "Correcto");
                            comando.Parameters.AddWithValue("@Fecha", DateTime.Now.ToString("o"));
                            comando.ExecuteNonQuery();
                            }
                        }

                    using (SQLiteCommand actualizar = new SQLiteCommand("UPDATE ArchivosMockupMaster SET UltimoAnalisis = @Fecha, TotalMockups = @Total WHERE Id = @Id;", conexion, transaccion))
                        {
                        actualizar.Parameters.AddWithValue("@Fecha", DateTime.Now.ToString("o"));
                        actualizar.Parameters.AddWithValue("@Total", mockups.Count);
                        actualizar.Parameters.AddWithValue("@Id", master.Id);
                        actualizar.ExecuteNonQuery();
                        }

                    transaccion.Commit();
                    }
                }
            }

        private DateTime ConvertirFecha(object valor)
            {
            DateTime fecha;
            return DateTime.TryParse(Convert.ToString(valor), out fecha) ? fecha : DateTime.MinValue;
            }
        }
    }
