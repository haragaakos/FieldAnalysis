using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;

namespace FieldAnalysis
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            string connectionString = "Host=db-gis-rosys.postgres.database.azure.com;Port=5432;Username=dbuser;Password=dbuser;Database=gis_test";
            try
            {
                // 1. Adatok kinyerése
                int[,] grid = FetchData(connectionString);
                SaveRasterImage(grid, "raster.png");

                // 2. Klaszterezés
                var (labels, centroids) = ClassifyCells(grid);
                SaveClusterImage(labels, "clusters.png");

                // Klaszterek eloszlásának kiírása
                int[] clusterCounts = new int[3];
                for (int i = 0; i < 512; i++)
                    for (int j = 0; j < 512; j++)
                        clusterCounts[labels[i, j]]++;
                Console.WriteLine($"Klaszterek eloszlása: {string.Join(", ", clusterCounts)}");
                Console.WriteLine($"Centroidok: {string.Join(", ", centroids.Select(c => c.ToString("F2")))}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hiba: {ex.Message}");
            }
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
                bool hasNonInteger = false;
                for (int i = 0; i < 512; i++)
                {
                    for (int j = 0; j < 512; j++)
                    {
                        if (values[i, j] != Math.Floor(values[i, j]))
                        {
                            hasNonInteger = true;
                            Console.WriteLine($"Tört szám: [{i},{j}] = {values[i, j]}");
                            break;
                        }
                        grid[i, j] = (int)values[i, j];
                        min = Math.Min(min, values[i, j]);
                        max = Math.Max(max, values[i, j]);
                    }
                    if (hasNonInteger) break;
                }
                Console.WriteLine($"Raszter érték minimum: {min}, maximum: {max}");
                Console.WriteLine($"Tört számok: {hasNonInteger}");
            }
            catch (NpgsqlException ex)
            {
                throw new Exception("Adatbázis hiba: " + ex.Message);
            }
            catch (InvalidCastException ex)
            {
                throw new Exception("Típuskonverzió hiba: " + ex.Message);
            }
            return grid;
        }

        public static (int[,], double[]) ClassifyCells(int[,] grid)
        {
            int width = grid.GetLength(0);
            int height = grid.GetLength(1);
            int[,] labels = new int[width, height];
            double[] centroids = InitializeCentroids(grid, 3);
            bool changed;

            do
            {
                changed = false;
                int[,] newLabels = new int[width, height];
                Parallel.For(0, width, i =>
                {
                    for (int j = 0; j < height; j++)
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
                        newLabels[i, j] = minimumIndex;
                    }
                });

                for (int i = 0; i < width; i++)
                    for (int j = 0; j < height; j++)
                        if (labels[i, j] != newLabels[i, j])
                        {
                            labels[i, j] = newLabels[i, j];
                            changed = true;
                        }

                double[] sums = new double[3];
                int[] counts = new int[3];
                for (int i = 0; i < width; i++)
                    for (int j = 0; j < height; j++)
                    {
                        int label = labels[i, j];
                        sums[label] += grid[i, j];
                        counts[label]++;
                    }

                Random random = new Random();
                for (int k = 0; k < 3; k++)
                {
                    if (counts[k] > 0)
                        centroids[k] = sums[k] / counts[k];
                    else
                        centroids[k] = grid[random.Next(width), random.Next(height)];
                }
            } while (changed);

            return (labels, centroids);
        }

        private static double[] InitializeCentroids(int[,] grid, int k)
        {
            int width = grid.GetLength(0), height = grid.GetLength(1);
            double[] centroids = new double[k];
            Random random = new Random();
            centroids[0] = grid[random.Next(width), random.Next(height)];
            for (int i = 1; i < k; i++)
            {
                double[] distances = new double[width * height];
                int index = 0;
                double maxDist = 0;
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        double minDist = double.MaxValue;
                        for (int j = 0; j < i; j++)
                            minDist = Math.Min(minDist, Math.Abs(grid[x, y] - centroids[j]));
                        distances[index++] = minDist * minDist;
                        maxDist = Math.Max(maxDist, distances[index - 1]);
                    }
                }
                double threshold = random.NextDouble() * maxDist;
                index = 0;
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (distances[index] >= threshold)
                        {
                            centroids[i] = grid[x, y];
                            break;
                        }
                        index++;
                    }
                    if (centroids[i] != 0) break;
                }
            }
            return centroids;
        }

        public static void SaveRasterImage(int[,] grid, string filename)
        {
            Bitmap bmp = new Bitmap(512, 512);
            for (int i = 0; i < 512; i++)
                for (int j = 0; j < 512; j++)
                    bmp.SetPixel(i, j, Color.FromArgb(grid[i, j], grid[i, j], grid[i, j]));
            bmp.Save(filename, ImageFormat.Png);
            Console.WriteLine($"Raszter kép mentve: {filename}");
        }

        public static void SaveClusterImage(int[,] labels, string filename)
        {
            Bitmap bmp = new Bitmap(512, 512);
            Color[] colors = { Color.Red, Color.Green, Color.Blue };
            for (int i = 0; i < 512; i++)
                for (int j = 0; j < 512; j++)
                    bmp.SetPixel(i, j, colors[labels[i, j]]);
            bmp.Save(filename, ImageFormat.Png);
            Console.WriteLine($"Klaszter kép mentve: {filename}");
        }
    }
}