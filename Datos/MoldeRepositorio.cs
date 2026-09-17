using System;
using System.Collections.Generic;
using System.Data.SQLite;
using TithorAutomation.Modelos;

namespace TithorAutomation.Datos
    {
    public class MoldeRepositorio
        {
        public List<Molde> ListarPorMaster(int masterId)
            {
            List<Molde> moldes = new List<Molde>();

            const string sql = @"
                SELECT
                    Id,
                    MasterId,
                    Codigo,
                    NombreObjeto,
                    Pieza,
                    Talla,
                    Corte,
                    Manga,
                    Cuello,
                    Pagina,
                    Capa,
                    Estado,
                    FechaAnalisis
                FROM Moldes
                WHERE MasterId = @MasterId
                ORDER BY Pagina, Capa, Codigo;";

            using (SQLiteConnection conexion =
                   BaseDatos.CrearConexion())
                {
                conexion.Open();

                using (SQLiteCommand comando =
                       new SQLiteCommand(sql, conexion))
                    {
                    comando.Parameters.AddWithValue(
                        "@MasterId",
                        masterId
                    );

                    using (SQLiteDataReader lector =
                           comando.ExecuteReader())
                        {
                        while (lector.Read())
                            {
                            moldes.Add(LeerMolde(lector));
                            }
                        }
                    }
                }

            return moldes;
            }

        public void ReemplazarCatalogo(
            int masterId,
            IList<Molde> moldes)
            {
            if (masterId <= 0)
                {
                throw new InvalidOperationException(
                    "El archivo Master no es válido."
                );
                }

            if (moldes == null)
                {
                throw new ArgumentNullException(
                    nameof(moldes)
                );
                }

            ValidarCatalogo(moldes);

            using (SQLiteConnection conexion =
                   BaseDatos.CrearConexion())
                {
                conexion.Open();

                using (SQLiteTransaction transaccion =
                       conexion.BeginTransaction())
                    {
                    try
                        {
                        EliminarCatalogoAnterior(
                            masterId,
                            conexion,
                            transaccion
                        );

                        DateTime fechaAnalisis = DateTime.Now;

                        foreach (Molde molde in moldes)
                            {
                            molde.MasterId = masterId;
                            molde.Estado = "Correcto";
                            molde.FechaAnalisis = fechaAnalisis;

                            Insertar(
                                molde,
                                conexion,
                                transaccion
                            );
                            }

                        transaccion.Commit();
                        }
                    catch
                        {
                        transaccion.Rollback();
                        throw;
                        }
                    }
                }
            }

        private void EliminarCatalogoAnterior(
            int masterId,
            SQLiteConnection conexion,
            SQLiteTransaction transaccion)
            {
            const string sql = @"
                DELETE FROM Moldes
                WHERE MasterId = @MasterId;";

            using (SQLiteCommand comando =
                   new SQLiteCommand(
                       sql,
                       conexion,
                       transaccion))
                {
                comando.Parameters.AddWithValue(
                    "@MasterId",
                    masterId
                );

                comando.ExecuteNonQuery();
                }
            }

        private void Insertar(
            Molde molde,
            SQLiteConnection conexion,
            SQLiteTransaction transaccion)
            {
            const string sql = @"
                INSERT INTO Moldes
                (
                    MasterId,
                    Codigo,
                    NombreObjeto,
                    Pieza,
                    Talla,
                    Corte,
                    Manga,
                    Cuello,
                    Pagina,
                    Capa,
                    Estado,
                    FechaAnalisis
                )
                VALUES
                (
                    @MasterId,
                    @Codigo,
                    @NombreObjeto,
                    @Pieza,
                    @Talla,
                    @Corte,
                    @Manga,
                    @Cuello,
                    @Pagina,
                    @Capa,
                    @Estado,
                    @FechaAnalisis
                );";

            using (SQLiteCommand comando =
                   new SQLiteCommand(
                       sql,
                       conexion,
                       transaccion))
                {
                comando.Parameters.AddWithValue(
                    "@MasterId",
                    molde.MasterId
                );

                comando.Parameters.AddWithValue(
                    "@Codigo",
                    molde.Codigo
                );

                comando.Parameters.AddWithValue(
                    "@NombreObjeto",
                    molde.NombreObjeto
                );

                comando.Parameters.AddWithValue(
                    "@Pieza",
                    ValorTexto(molde.Pieza)
                );

                comando.Parameters.AddWithValue(
                    "@Talla",
                    ValorTexto(molde.Talla)
                );

                comando.Parameters.AddWithValue(
                    "@Corte",
                    ValorTexto(molde.Corte)
                );

                comando.Parameters.AddWithValue(
                    "@Manga",
                    ValorTexto(molde.Manga)
                );

                comando.Parameters.AddWithValue(
                    "@Cuello",
                    ValorTexto(molde.Cuello)
                );

                comando.Parameters.AddWithValue(
                    "@Pagina",
                    molde.Pagina
                );

                comando.Parameters.AddWithValue(
                    "@Capa",
                    ValorTexto(molde.Capa)
                );

                comando.Parameters.AddWithValue(
                    "@Estado",
                    molde.Estado
                );

                comando.Parameters.AddWithValue(
                    "@FechaAnalisis",
                    molde.FechaAnalisis.ToString("o")
                );

                comando.ExecuteNonQuery();
                }
            }

        private void ValidarCatalogo(IList<Molde> moldes)
            {
            HashSet<string> codigos =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase
                );

            foreach (Molde molde in moldes)
                {
                if (molde == null)
                    {
                    throw new InvalidOperationException(
                        "El catálogo contiene un molde vacío."
                    );
                    }

                if (!molde.EsValido)
                    {
                    throw new InvalidOperationException(
                        "No se puede sincronizar el molde \"" +
                        molde.NombreObjeto +
                        "\" porque su estado es " +
                        molde.Estado +
                        "."
                    );
                    }

                string codigo = molde.Codigo.Trim();

                if (!codigos.Add(codigo))
                    {
                    throw new InvalidOperationException(
                        "El código \"" +
                        codigo +
                        "\" está duplicado."
                    );
                    }
                }
            }

        private Molde LeerMolde(SQLiteDataReader lector)
            {
            return new Molde
                {
                Id = Convert.ToInt32(lector["Id"]),
                MasterId =
                    Convert.ToInt32(lector["MasterId"]),

                Codigo =
                    Convert.ToString(lector["Codigo"]),

                NombreObjeto =
                    Convert.ToString(lector["NombreObjeto"]),

                Pieza =
                    Convert.ToString(lector["Pieza"]),

                Talla =
                    Convert.ToString(lector["Talla"]),

                Corte =
                    Convert.ToString(lector["Corte"]),

                Manga =
                    Convert.ToString(lector["Manga"]),

                Cuello =
                    Convert.ToString(lector["Cuello"]),

                Pagina =
                    lector["Pagina"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(lector["Pagina"]),

                Capa =
                    Convert.ToString(lector["Capa"]),

                Estado =
                    Convert.ToString(lector["Estado"]),

                FechaAnalisis =
                    LeerFecha(lector["FechaAnalisis"])
                };
            }

        private DateTime LeerFecha(object valor)
            {
            DateTime fecha;

            if (valor != null &&
                valor != DBNull.Value &&
                DateTime.TryParse(
                    Convert.ToString(valor),
                    out fecha))
                {
                return fecha;
                }

            return DateTime.MinValue;
            }

        private object ValorTexto(string valor)
            {
            if (string.IsNullOrWhiteSpace(valor))
                return DBNull.Value;

            return valor.Trim();
            }
        }
    }