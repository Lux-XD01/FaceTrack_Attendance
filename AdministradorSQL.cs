// ADMINISTRADOR SQL - Acceso a datos para FaceTrack Attendance
// Requiere el paquete NuGet "Microsoft.Data.SqlClient".
//
// Encapsula toda la comunicación con SQL Server: crea la base y la tabla si no existen,
// guarda/recupera las plantillas biométricas (vector 128D de SFace) y da soporte al panel
// de administración (grilla, búsqueda, edición, borrado).

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Proyecto_ReconocimientoFacial_0._1
{
    public class AdministradorSQL
    {
        private readonly string cadenaConexion;
        private readonly string nombreBaseDeDatos;

        public AdministradorSQL(string servidor, string baseDeDatos, bool autenticacionWindows = true,
                                 string usuario = null, string contraseña = null)
        {
            nombreBaseDeDatos = baseDeDatos;

            SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder
            {
                DataSource = servidor,
                InitialCatalog = baseDeDatos,
                TrustServerCertificate = true // evita el error de certificado SSL en desarrollo local
            };

            if (autenticacionWindows)
                sb.IntegratedSecurity = true;
            else
            {
                sb.UserID = usuario;
                sb.Password = contraseña;
            }

            cadenaConexion = sb.ConnectionString;
        }

        // Crea la base de datos si todavía no existe. Se conecta a "master" porque no se puede
        // usar una base como destino de conexión antes de que esa base exista.
        public void AsegurarBaseDeDatos()
        {
            SqlConnectionStringBuilder sbMaster = new SqlConnectionStringBuilder(cadenaConexion)
            {
                InitialCatalog = "master"
            };

            string sql = $"IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{nombreBaseDeDatos}') " +
                        $"CREATE DATABASE [{nombreBaseDeDatos}];";

            using (SqlConnection cn = new SqlConnection(sbMaster.ConnectionString))
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // Crea la tabla Alumnos si todavía no existe.
        public void AsegurarEsquema()
        {
            const string sql = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Alumnos')
BEGIN
    CREATE TABLE Alumnos (
        Id        INT IDENTITY(1,1) PRIMARY KEY,
        Nombre    NVARCHAR(100) NOT NULL,
        Apellido  NVARCHAR(100) NULL,
        Legajo    NVARCHAR(50)  NULL,
        Estado    NVARCHAR(20)  NOT NULL DEFAULT 'Ausente',
        FechaHora DATETIME NULL,
        Plantilla VARBINARY(MAX) NOT NULL   -- vector 128D de SFace, serializado como bytes
    );
END";
            using (SqlConnection cn = new SqlConnection(cadenaConexion))
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // Inserta un sujeto con nombre automático "Sujeto {Id}", usando el Id real que SQL Server le asigna
        // (IDENTITY). Como ese Id nunca se repite ni se reutiliza (ni borrando filas), el nombre generado
        // tampoco se repite jamás, a diferencia de un contador manual que sí se puede reciclar.
        // Se hace en una transacción: primero se inserta con un nombre provisorio (para obtener el Id),
        // y en la misma transacción se corrige el nombre con ese Id ya conocido.
        public int InsertarSujetoAutomatico(byte[] plantilla)
        {
            using (SqlConnection cn = new SqlConnection(cadenaConexion))
            {
                cn.Open();
                using (SqlTransaction tx = cn.BeginTransaction())
                {
                    int id;
                    using (SqlCommand cmdInsert = new SqlCommand(
                        "INSERT INTO Alumnos (Nombre, Estado, Plantilla) OUTPUT INSERTED.Id VALUES ('(pendiente)', 'Ausente', @Plantilla)",
                        cn, tx))
                    {
                        cmdInsert.Parameters.AddWithValue("@Plantilla", plantilla);
                        id = (int)cmdInsert.ExecuteScalar();
                    }

                    using (SqlCommand cmdUpdate = new SqlCommand(
                        "UPDATE Alumnos SET Nombre = @Nombre WHERE Id = @Id", cn, tx))
                    {
                        cmdUpdate.Parameters.AddWithValue("@Nombre", $"Sujeto {id}");
                        cmdUpdate.Parameters.AddWithValue("@Id", id);
                        cmdUpdate.ExecuteNonQuery();
                    }

                    tx.Commit();
                    return id;
                }
            }
        }

        // Inserta un sujeto con un nombre elegido a mano (no se usa en el registro automático, queda
        // disponible para altas manuales futuras desde el panel de administración).
        public int InsertarSujeto(string nombre, byte[] plantilla)
        {
            const string sql = @"
INSERT INTO Alumnos (Nombre, Estado, Plantilla)
OUTPUT INSERTED.Id
VALUES (@Nombre, 'Ausente', @Plantilla)";

            using (SqlConnection cn = new SqlConnection(cadenaConexion))
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@Nombre", nombre);
                cmd.Parameters.AddWithValue("@Plantilla", plantilla);
                cn.Open();
                return (int)cmd.ExecuteScalar();
            }
        }

        // Marca a un sujeto como presente con la hora actual (se llama al reconocerlo en cámara).
        public void MarcarPresente(int id)
        {
            const string sql = "UPDATE Alumnos SET Estado = 'Presente', FechaHora = @Fecha WHERE Id = @Id";
            using (SqlConnection cn = new SqlConnection(cadenaConexion))
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@Fecha", DateTime.Now);
                cmd.Parameters.AddWithValue("@Id", id);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // Todos los sujetos, listos para bindear directo a un DataGridView.
        public DataTable ObtenerTodos()
        {
            const string sql = "SELECT Id, Nombre, Apellido, Legajo, Estado, FechaHora FROM Alumnos ORDER BY Id";
            DataTable tabla = new DataTable();

            using (SqlConnection cn = new SqlConnection(cadenaConexion))
            using (SqlDataAdapter adaptador = new SqlDataAdapter(sql, cn))
            {
                adaptador.Fill(tabla);
            }
            return tabla;
        }

        public DataTable Buscar(string texto)
        {
            const string sql = @"
SELECT Id, Nombre, Apellido, Legajo, Estado, FechaHora FROM Alumnos
WHERE Nombre LIKE @Texto OR Apellido LIKE @Texto OR Legajo LIKE @Texto
ORDER BY Id";
            DataTable tabla = new DataTable();

            using (SqlConnection cn = new SqlConnection(cadenaConexion))
            using (SqlDataAdapter adaptador = new SqlDataAdapter(sql, cn))
            {
                adaptador.SelectCommand.Parameters.AddWithValue("@Texto", $"%{texto ?? ""}%");
                adaptador.Fill(tabla);
            }
            return tabla;
        }

        // Guarda ediciones hechas a mano en la grilla (Nombre/Apellido/Legajo).
        //
        // IMPORTANTE: hay que pasar la MISMA DataTable que se llenó con ObtenerTodos() y que está
        // pegada al DataGridView, sin copiarla con ToTable(). Una copia con ToTable() pierde el
        // RowState de cada fila (todas quedan como "Added" aunque ya existan en la base), y eso hace
        // que el adaptador intente un INSERT en vez de un UPDATE — es lo que causaba el error de
        // "Cannot insert the value NULL into column 'Plantilla'".
        public void GuardarEdiciones(DataTable tabla)
        {
            const string sql = "SELECT Id, Nombre, Apellido, Legajo, Estado, FechaHora FROM Alumnos";

            using (SqlConnection cn = new SqlConnection(cadenaConexion))
            using (SqlDataAdapter adaptador = new SqlDataAdapter(sql, cn))
            {
                new SqlCommandBuilder(adaptador); // genera UPDATE automáticamente para las filas modificadas
                cn.Open();
                adaptador.Update(tabla); // usa el RowState real de cada fila: Modified -> UPDATE, no INSERT
            }
        }

        public void EliminarSujeto(int id)
        {
            const string sql = "DELETE FROM Alumnos WHERE Id = @Id";
            using (SqlConnection cn = new SqlConnection(cadenaConexion))
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // Borra todo y reinicia el contador de Id. Se usa con doble confirmación desde la UI.
        public void EliminarTodos()
        {
            const string sql = "TRUNCATE TABLE Alumnos;";
            using (SqlConnection cn = new SqlConnection(cadenaConexion))
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // Recupera Id + Nombre + Plantilla de todos los sujetos, para reconstruir la lista de
        // reconocimiento en memoria al iniciar el programa (así no se pierden los registros al reiniciar).
        public List<(int Id, string Nombre, byte[] Plantilla)> ObtenerPlantillas()
        {
            const string sql = "SELECT Id, Nombre, Plantilla FROM Alumnos";
            var lista = new List<(int, string, byte[])>();

            using (SqlConnection cn = new SqlConnection(cadenaConexion))
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        string nombre = reader.GetString(1);
                        byte[] plantilla = (byte[])reader["Plantilla"];
                        lista.Add((id, nombre, plantilla));
                    }
                }
            }
            return lista;
        }
    }
}