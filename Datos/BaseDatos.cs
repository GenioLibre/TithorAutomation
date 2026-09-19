using System;
using System.Data.SQLite;
using System.IO;

namespace TithorAutomation.Datos
{
    public static class BaseDatos
    {
        private static readonly string carpetaDatos =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData
                ),
                "TithorAutomation",
                "Datos"
            );

        public static readonly string RutaBaseDatos =
            Path.Combine(
                carpetaDatos,
                "TithorAutomation.db"
            );

        public static string CadenaConexion
        {
            get
            {
                return
                    $"Data Source={RutaBaseDatos};" +
                    "Version=3;" +
                    "Foreign Keys=True;";
            }
        }

        public static SQLiteConnection CrearConexion()
        {
            return new SQLiteConnection(
                CadenaConexion
            );
        }

        public static void Inicializar()
        {
            if (!Directory.Exists(carpetaDatos))
            {
                Directory.CreateDirectory(
                    carpetaDatos
                );
            }

            if (!File.Exists(RutaBaseDatos))
            {
                SQLiteConnection.CreateFile(
                    RutaBaseDatos
                );
            }

            using (
                SQLiteConnection conexion =
                    CrearConexion())
            {
                conexion.Open();

                CrearTablas(conexion);
                InsertarProductosIniciales(conexion);
            }
        }

        private static void CrearTablas(
            SQLiteConnection conexion)
        {
            string sql = @"
CREATE TABLE IF NOT EXISTS Productos
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    Nombre          TEXT NOT NULL UNIQUE,
    Codigo          TEXT NOT NULL UNIQUE,
    Descripcion     TEXT,
    Activo          INTEGER NOT NULL DEFAULT 1,
    FechaCreacion   TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ArchivosMaster
(
    Id                          INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductoId                  INTEGER NOT NULL,
    Ruta                        TEXT NOT NULL,
    NombreArchivo               TEXT NOT NULL,
    HashArchivo                 TEXT,
    TamanoArchivo               INTEGER,
    FechaModificacionArchivo    TEXT,
    UltimoAnalisis              TEXT,
    TotalMoldes                 INTEGER NOT NULL DEFAULT 0,
    EsPrincipal                 INTEGER NOT NULL DEFAULT 1,
    Activo                      INTEGER NOT NULL DEFAULT 1,

    FOREIGN KEY (ProductoId)
        REFERENCES Productos(Id)
        ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Moldes
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    MasterId        INTEGER NOT NULL,
    Codigo          TEXT NOT NULL,
    NombreObjeto    TEXT NOT NULL,
    Pieza           TEXT,
    Talla           TEXT,
    Corte           TEXT,
    Manga           TEXT,
    Cuello          TEXT,
    Pagina          INTEGER,
    Capa            TEXT,
    Estado          TEXT NOT NULL DEFAULT 'Correcto',
    FechaAnalisis   TEXT NOT NULL,

    FOREIGN KEY (MasterId)
        REFERENCES ArchivosMaster(Id)
        ON DELETE CASCADE,

    UNIQUE (MasterId, Codigo)
);

CREATE TABLE IF NOT EXISTS ArchivosMockupMaster
(
    Id                          INTEGER PRIMARY KEY AUTOINCREMENT,
    Ruta                        TEXT NOT NULL,
    NombreArchivo               TEXT NOT NULL,
    HashArchivo                 TEXT,
    TamanoArchivo               INTEGER,
    FechaModificacionArchivo    TEXT,
    UltimoAnalisis              TEXT,
    TotalMockups                INTEGER NOT NULL DEFAULT 0,
    Activo                      INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS Mockups
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    MasterId        INTEGER NOT NULL,
    Codigo          TEXT NOT NULL,
    NombreGrupo     TEXT NOT NULL,
    Producto        TEXT NOT NULL,
    Estado          TEXT NOT NULL DEFAULT 'Correcto',
    FechaAnalisis   TEXT NOT NULL,

    FOREIGN KEY (MasterId)
        REFERENCES ArchivosMockupMaster(Id)
        ON DELETE CASCADE,

    UNIQUE (MasterId, Codigo)
);

CREATE INDEX IF NOT EXISTS IX_Mockups_Master
    ON Mockups(MasterId);

CREATE INDEX IF NOT EXISTS IX_Mockups_Codigo
    ON Mockups(Codigo);

CREATE TABLE IF NOT EXISTS EstadoSesion
(
    Clave               TEXT PRIMARY KEY,
    Valor               TEXT,
    FechaActualizacion  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ConfiguracionProducto
(
    ProductoId              INTEGER PRIMARY KEY,
    AnchoRollo              REAL NOT NULL DEFAULT 1.60,
    Separacion              REAL NOT NULL DEFAULT 0,
    PermitirRotacion        INTEGER NOT NULL DEFAULT 1,
    CerrarMasterSinGuardar  INTEGER NOT NULL DEFAULT 1,

    FOREIGN KEY (ProductoId)
        REFERENCES Productos(Id)
        ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS HistorialMaster
(
    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    MasterId            INTEGER NOT NULL,
    Fecha                TEXT NOT NULL,
    Accion               TEXT NOT NULL,
    MoldesNuevos         INTEGER NOT NULL DEFAULT 0,
    MoldesModificados    INTEGER NOT NULL DEFAULT 0,
    MoldesEliminados     INTEGER NOT NULL DEFAULT 0,
    MoldesConError       INTEGER NOT NULL DEFAULT 0,

    FOREIGN KEY (MasterId)
        REFERENCES ArchivosMaster(Id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_Master_Producto
    ON ArchivosMaster(ProductoId);

CREATE INDEX IF NOT EXISTS IX_Moldes_Master
    ON Moldes(MasterId);

CREATE INDEX IF NOT EXISTS IX_Moldes_Codigo
    ON Moldes(Codigo);
";

            using (
                SQLiteCommand comando =
                    new SQLiteCommand(
                        sql,
                        conexion
                    ))
            {
                comando.ExecuteNonQuery();
            }
        }

        private static void InsertarProductosIniciales(
            SQLiteConnection conexion)
        {
            string sql = @"
INSERT OR IGNORE INTO Productos
(
    Nombre,
    Codigo,
    Descripcion,
    Activo,
    FechaCreacion
)
VALUES
(
    'Camisetas',
    'CAMISETAS',
    'Uniformes, camisetas, bividis y shorts',
    1,
    datetime('now')
);

INSERT OR IGNORE INTO Productos
(
    Nombre,
    Codigo,
    Descripcion,
    Activo,
    FechaCreacion
)
VALUES
(
    'Fundas personalizadas',
    'FUNDAS',
    'Fundas personalizadas para maletas',
    1,
    datetime('now')
);

INSERT OR IGNORE INTO ConfiguracionProducto
(
    ProductoId,
    AnchoRollo,
    Separacion,
    PermitirRotacion,
    CerrarMasterSinGuardar
)
SELECT
    Id,
    1.60,
    0,
    1,
    1
FROM Productos;
";

            using (
                SQLiteCommand comando =
                    new SQLiteCommand(
                        sql,
                        conexion
                    ))
            {
                comando.ExecuteNonQuery();
            }
        }
    }
}