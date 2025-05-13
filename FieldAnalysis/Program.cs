using Npgsql;

namespace FieldAnalysis
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string connectionString = "Host=db-gis-rosys.postgres.database.azure.com;Port=5432;Username=dbuser;Password=dbuser;Database=gis_test"; //adatbázis kapcsolat

            int[,] grid = FetchData(connectionString); //adatbázis lekérdezés
        }
        public static int[,] FetchData(string connectionString)
        {
            int [,] grid = new int[512, 512];
            using var connection = new NpgsqlConnection(connectionString);
            {
                connection.Open();
                string query = "SELECT ST_DumpValues(geom, 1) AS values FROM grids WHERE name = 'debrecen_field'";
                using var command = new NpgsqlCommand(query, connection);
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    var values = (double[][]) reader.GetValue(0);
                    for(int i = 0; i < 512; i++)
                    {
                        for(int j = 0; j < 512; j++)
                        {
                            grid[i, j] = (int)values[i][j]; //a lekérdezett értékek beírása a grid tömbbe
                        }
                    }
                }
            }

            return grid;
        }
    }
}