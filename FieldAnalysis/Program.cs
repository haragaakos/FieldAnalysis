using Npgsql;


namespace FieldAnalysis
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string connectionString = "Host=db-gis-rosys.postgres.database.azure.com;Port=5432;Username=dbuser;Password=dbuser;Database=gis_test"; //adatbázis kapcsolat

            int[,] grid = FetchData(connectionString); //1. Lépés: Adatok kinyerése

            var clusters = ClassifyCells(grid); //2. Lépés: Cellák osztályozása (k-means klaszterezés)
        }
        public static int[,] FetchData(string connectionString)
        {
            int[,] grid = new int[512, 512];
            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();
                string query = "SELECT ST_DumpValues(geom, 1) AS values FROM grids WHERE name = 'Debreceni példa'";
                using var command = new NpgsqlCommand(query, connection);
                using var reader = command.ExecuteReader();
                if (!reader.Read())
                    throw new Exception("Nincs ilyen nevű sor.");

                var values = (double[,])reader.GetValue(0);
                if (values.GetLength(0) != 512 || values.GetLength(1) != 512)
                    throw new Exception($"Érvénytelen raszter méret: {values.GetLength(0)}x{values.GetLength(1)}");

                double min = double.MaxValue, max = double.MinValue;
                for (int i = 0; i < 512; i++)
                {
                    for (int j = 0; j < 512; j++)
                    {
                        grid[i, j] = (int)values[i, j];
                        min = Math.Min(min, values[i, j]);
                        max = Math.Max(max, values[i, j]);
                    }
                }
                Console.WriteLine($"Raszter érték minimum: {min}, maximum: {max}");
            }
            catch (NpgsqlException ex)
            {
                throw new Exception("Adatbázis hiba: " + ex.Message);
            }
            catch (InvalidCastException ex)
            {
                throw new Exception("Típus konverzió hiba: " + ex.Message);
            }
            return grid;
        }
        public static (int[,], double[]) ClassifyCells(int[,] grid)
        {
            int width = grid.GetLength(0);
            int height = grid.GetLength(1);
            int[,] labels = new int[width, height];
            double[] centroids = new double[3];
            Random random = new Random();

            //centroidok inicializálása véletlenszerűen
            centroids[0] = grid[random.Next(width), random.Next(height)];
            centroids[1] = grid[random.Next(width), random.Next(height)];
            centroids[2] = grid[random.Next(width), random.Next(height)];


            bool changed;

            do
            {
                changed = false;
                double[] oldCentroids = (double[])centroids.Clone();

                //párhuzamos osztályozás
                Parallel.For(0, width, i =>
               {
                   for (int j = 0; j < width; j++)
                   {
                       int minimumIndex = 0;
                       double minimumDistance = Math.Abs(grid[i, j] - centroids[0]);
                       for (int k = 1; k < 3; k++)
                       {
                           double distance = Math.Abs(grid[i, j] - centroids[k]);
                           if (distance < minimumDistance)
                           {
                               minimumDistance = distance;
                               minimumIndex = k;
                           }
                       }
                       lock (labels)
                       {
                           if (labels[i, j] != minimumIndex)
                           {
                               labels[i, j] = minimumIndex;
                               changed = true; //ha megváltozott a címke, akkor true
                           }
                       }
                   }
               });
                //centroidok frissítése
                double[] sums = new double[3];
                int[] counts = new int[3];
                for (int i = 0; i < width; i++)
                {
                    for (int j = 0; j < height; j++)
                    {
                        int label = labels[i, j];
                        sums[label] += grid[i, j];
                        counts[label]++;
                    }
                }
                for (int k = 0; k < 3; k++)
                {
                    if (counts[k] > 0)
                    {
                        centroids[k] = sums[k] / counts[k];
                    }
                }
                //korai leállítás
                bool significantChange = false;
                for (int k = 0; k < 3; k++)
                    if (Math.Abs(centroids[k] - oldCentroids[k]) > 0.01)
                        significantChange = true;
                if (!significantChange)
                    break;

            } while (changed);

         return (labels, centroids);
        }
    }
}