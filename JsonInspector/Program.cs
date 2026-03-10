// See https://aka.ms/new-console-template for more information
using JsonInspector.Models;
using System.Text.Json;

namespace JsonInspector
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var folderPath = @"C:\Users\jmuniz\Downloads\Dashboarddependencias";
            var spToFind = "sp_PerfilAD_ObtenerAQuienPuedoVer";

            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine("La carpeta no existe:");
                Console.WriteLine(folderPath);
                return;
            }

            var jsonFiles = Directory.GetFiles(folderPath, "*.json", SearchOption.AllDirectories);

            if (jsonFiles.Length == 0)
            {
                Console.WriteLine("No se encontraron archivos JSON en la carpeta.");
                return;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var dashboards = new List<(Dashboard Dashboard, string FileName)>();

            foreach (var file in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var dashboard = JsonSerializer.Deserialize<Dashboard>(json, options);

                    if (dashboard != null)
                    {
                        dashboards.Add((dashboard, Path.GetFileName(file)));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error leyendo {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            Console.WriteLine($"Total de dashboards leídos: {dashboards.Count}");
            Console.WriteLine();
            Console.WriteLine($"Buscando SP: {spToFind}");
            Console.WriteLine(new string('-', 80));

            var hallazgos = new List<HallazgoSp>();

            foreach (var item in dashboards)
            {
                var dashboard = item.Dashboard;

                foreach (var dataProvider in dashboard.DataProviders)
                {
                    if (dataProvider.Query == null)
                        continue;

                    var coincidencias = BuscarCoincidencias(
                        dataProvider.Query,
                        spToFind,
                        "Query");

                    if (coincidencias.Count > 0)
                    {
                        hallazgos.Add(new HallazgoSp
                        {
                            DashboardNum = dashboard.DashboardNum,
                            DashboardName = dashboard.DashboardName,
                            Archivo = item.FileName,
                            DataProviderId = dataProvider.DataProviderId,
                            DataProviderName = dataProvider.Name,
                            Coincidencias = coincidencias
                        });
                    }
                }
            }

            var resumen = hallazgos
                .GroupBy(x => new
                {
                    x.DashboardNum,
                    x.DashboardName,
                    x.Archivo,
                    x.DataProviderId,
                    x.DataProviderName
                })
                .Select(g => new
                {
                    g.Key.DashboardNum,
                    g.Key.DashboardName,
                    g.Key.Archivo,
                    g.Key.DataProviderId,
                    g.Key.DataProviderName,
                    TotalCoincidencias = g.SelectMany(x => x.Coincidencias).Count(),
                    Rutas = g.SelectMany(x => x.Coincidencias)
                             .Select(c => c.Ruta)
                             .Distinct()
                             .OrderBy(r => r)
                             .ToList()
                })
                .OrderBy(x => x.DashboardNum)
                .ThenBy(x => x.DataProviderId)
                .ToList();

            if (resumen.Count == 0)
            {
                Console.WriteLine("No se encontraron coincidencias.");
            }
            else
            {
                Console.WriteLine($"Dashboards encontrados: {resumen.Count}");
                Console.WriteLine();

                foreach (var item in resumen)
                {
                    Console.WriteLine($"DashboardNum: {item.DashboardNum}");
                    Console.WriteLine($"DashboardName: {item.DashboardName}");
                    Console.WriteLine($"Archivo: {item.Archivo}");
                    Console.WriteLine($"DataProviderId: {item.DataProviderId}");
                    Console.WriteLine($"DataProviderName: {item.DataProviderName}");
                    Console.WriteLine($"TotalCoincidenciasEnElArbol: {item.TotalCoincidencias}");
                    Console.WriteLine("Rutas encontradas:");

                    foreach (var ruta in item.Rutas)
                    {
                        Console.WriteLine($"  - {ruta}");
                    }

                    Console.WriteLine(new string('-', 80));
                }
            }

            Console.ReadKey();
        }

        static List<Coincidencia> BuscarCoincidencias(QueryNode node, string textToFind, string rutaActual)
        {
            var resultados = new List<Coincidencia>();

            if (!string.IsNullOrWhiteSpace(node.SqlQuery) &&
                node.SqlQuery.Contains(textToFind, StringComparison.OrdinalIgnoreCase))
            {
                resultados.Add(new Coincidencia
                {
                    Ruta = $"{rutaActual}.SqlQuery",
                    Texto = textToFind
                });
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                var rutaHijo = $"{rutaActual}.Children[{i}]";

                resultados.AddRange(BuscarCoincidencias(child, textToFind, rutaHijo));
            }

            return resultados;
        }
    }
}
