using JsonInspector.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JsonInspector
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //var folderPath = @"C:\Users\jmuniz\Downloads\Dashboarddependencias";
            var folderPath = @"C:\Users\jmuniz\Downloads\DashboardsJson";
            var spBaseName = "sp_PerfilAD_ObtenerAQuienPuedoVer";
            var databaseName = "Catalogos";
            var schemaName = "dbo";
            var serverName = "ONBSDB.P.GSLB";

            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine("La carpeta no existe:");
                Console.WriteLine(folderPath);
                Console.ReadKey();
                return;
            }

            var jsonFiles = Directory.GetFiles(folderPath, "*.json", SearchOption.AllDirectories);

            if (jsonFiles.Length == 0)
            {
                Console.WriteLine("No se encontraron archivos JSON en la carpeta.");
                Console.ReadKey();
                return;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var patrones = ConstruirPatronesSp(spBaseName, schemaName, databaseName, serverName);

            var dashboards = new List<DashboardFileInfo>();
            var archivosConTextoCompleto = new List<ArchivoTextoCoincidencia>();
            var erroresLectura = new List<string>();
            var erroresDeserializacion = new List<string>();

            foreach (var file in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);

                    bool contieneTextoCompleto = ContieneAlgunPatron(json, patrones, spBaseName);

                    if (contieneTextoCompleto)
                    {
                        archivosConTextoCompleto.Add(new ArchivoTextoCoincidencia
                        {
                            FileName = Path.GetFileName(file),
                            FullPath = file
                        });
                    }

                    try
                    {
                        var dashboard = JsonSerializer.Deserialize<Dashboard>(json, options);

                        if (dashboard != null)
                        {
                            dashboards.Add(new DashboardFileInfo
                            {
                                Dashboard = dashboard,
                                FileName = Path.GetFileName(file),
                                FullPath = file,
                                ContieneTextoCompleto = contieneTextoCompleto
                            });
                        }
                        else
                        {
                            erroresDeserializacion.Add($"{Path.GetFileName(file)} -> Deserialización devolvió null.");
                        }
                    }
                    catch (Exception ex)
                    {
                        erroresDeserializacion.Add($"{Path.GetFileName(file)} -> {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    erroresLectura.Add($"{Path.GetFileName(file)} -> {ex.Message}");
                }
            }

            Console.WriteLine($"Total de archivos JSON encontrados: {jsonFiles.Length}");
            Console.WriteLine($"Total de dashboards deserializados: {dashboards.Count}");
            Console.WriteLine($"Archivos con coincidencia textual completa: {archivosConTextoCompleto.Count}");
            Console.WriteLine($"Errores de lectura: {erroresLectura.Count}");
            Console.WriteLine($"Errores de deserialización: {erroresDeserializacion.Count}");
            Console.WriteLine();
            Console.WriteLine($"Buscando SP (base): {spBaseName}");
            Console.WriteLine("Variantes consideradas:");

            foreach (var patron in patrones.SelectMany(p => p.VariantesExactas).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  - {patron}");
            }

            Console.WriteLine("Detección flexible adicional:");
            Console.WriteLine($"  - nombre original: {spBaseName}");
            Console.WriteLine($"  - nombre normalizado(con guin bajo, con comillas, se eliminan espacios, etc): {NormalizeLoose(spBaseName)}");
            
            var hallazgos = new List<HallazgoSp>();

            foreach (var item in dashboards)
            {
                var dashboard = item.Dashboard;

                foreach (var dataProvider in dashboard.DataProviders ?? new List<DataProvider>())
                {
                    if (dataProvider.Query == null)
                        continue;

                    var coincidencias = BuscarCoincidencias(
                        dataProvider.Query,
                        patrones,
                        spBaseName,
                        "Query");

                    if (coincidencias.Count > 0)
                    {
                        hallazgos.Add(new HallazgoSp
                        {
                            DashboardNum = dashboard.DashboardNum,
                            DashboardName = dashboard.DashboardName,
                            Archivo = item.FileName,
                            FullPath = item.FullPath,
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
                    x.FullPath,
                    x.DataProviderId,
                    x.DataProviderName
                })
                .Select(g => new
                {
                    g.Key.DashboardNum,
                    g.Key.DashboardName,
                    g.Key.Archivo,
                    g.Key.FullPath,
                    g.Key.DataProviderId,
                    g.Key.DataProviderName,
                    TotalCoincidencias = g.SelectMany(x => x.Coincidencias).Count(),
                    Rutas = g.SelectMany(x => x.Coincidencias)
                             .Select(c => c.Ruta)
                             .Distinct()
                             .OrderBy(r => r)
                             .ToList(),
                    VariantesEncontradas = g.SelectMany(x => x.Coincidencias)
                                            .Select(c => c.Texto)
                                            .Distinct()
                                            .OrderBy(x => x)
                                            .ToList()
                })
                .OrderBy(x => x.DashboardNum)
                .ThenBy(x => x.DataProviderId)
                .ToList();

            var archivosConCoincidenciaEstructurada = resumen
                .Select(x => x.FullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var soloCoincidenciaTextual = archivosConTextoCompleto
                .Where(x => !archivosConCoincidenciaEstructurada.Contains(x.FullPath))
                .OrderBy(x => x.FileName)
                .ToList();

            Console.WriteLine("RESULTADO DE COINCIDENCIAS ESTRUCTURADAS");
            Console.WriteLine(new string('=', 100));

            if (resumen.Count == 0)
            {
                Console.WriteLine("No se encontraron coincidencias estructuradas en Query.SqlQuery.");
            }
            else
            {
                Console.WriteLine($"Dashboards encontrados por análisis estructurado: {resumen.Count}");
                Console.WriteLine();

                foreach (var item in resumen)
                {
                    Console.WriteLine($"DashboardNum: {item.DashboardNum}");
                    Console.WriteLine($"DashboardName: {item.DashboardName}");
                    Console.WriteLine($"Archivo: {item.Archivo}");
                    Console.WriteLine($"FullPath: {item.FullPath}");
                    Console.WriteLine($"DataProviderId: {item.DataProviderId}");
                    Console.WriteLine($"DataProviderName: {item.DataProviderName}");
                    Console.WriteLine($"TotalCoincidenciasEnElArbol: {item.TotalCoincidencias}");
                    Console.WriteLine("Variantes encontradas:");

                    foreach (var variante in item.VariantesEncontradas)
                    {
                        Console.WriteLine($"  - {variante}");
                    }

                    Console.WriteLine("Rutas encontradas:");

                    foreach (var ruta in item.Rutas)
                    {
                        Console.WriteLine($"  - {ruta}");
                    }

                    Console.WriteLine(new string('-', 100));
                }
            }

            Console.WriteLine();
            Console.WriteLine("ARCHIVOS CON COINCIDENCIA TEXTUAL COMPLETA PERO SIN COINCIDENCIA ESTRUCTURADA");
            Console.WriteLine(new string('=', 100));

            if (soloCoincidenciaTextual.Count == 0)
            {
                Console.WriteLine("No hay diferencias. Todo lo encontrado por texto también fue encontrado en Query.SqlQuery.");
            }
            else
            {
                Console.WriteLine($"Archivos que contienen alguna variante de la SP pero NO fueron encontrados en Query.SqlQuery: {soloCoincidenciaTextual.Count}");
                Console.WriteLine();

                foreach (var item in soloCoincidenciaTextual)
                {
                    Console.WriteLine($"Archivo: {item.FileName}");
                    Console.WriteLine($"FullPath: {item.FullPath}");
                    Console.WriteLine(new string('-', 100));
                }
            }

            Console.WriteLine();
            Console.WriteLine("ERRORES DE LECTURA");
            Console.WriteLine(new string('=', 100));

            if (erroresLectura.Count == 0)
            {
                Console.WriteLine("Sin errores de lectura.");
            }
            else
            {
                foreach (var error in erroresLectura)
                {
                    Console.WriteLine(error);
                }
            }

            Console.WriteLine();
            Console.WriteLine("ERRORES DE DESERIALIZACIÓN");
            Console.WriteLine(new string('=', 100));

            if (erroresDeserializacion.Count == 0)
            {
                Console.WriteLine("Sin errores de deserialización.");
            }
            else
            {
                foreach (var error in erroresDeserializacion)
                {
                    Console.WriteLine(error);
                }
            }

            Console.WriteLine();
            Console.WriteLine("PROCESO TERMINADO.");
            Console.ReadKey();
        }

        static List<Coincidencia> BuscarCoincidencias(
            QueryNode node,
            List<PatronBusquedaSp> patrones,
            string spBaseName,
            string rutaActual)
        {
            var resultados = new List<Coincidencia>();

            if (node == null)
                return resultados;

            if (!string.IsNullOrWhiteSpace(node.SqlQuery))
            {
                var variantes = ObtenerVariantesEncontradas(node.SqlQuery, patrones);

                foreach (var variante in variantes)
                {
                    resultados.Add(new Coincidencia
                    {
                        Ruta = $"{rutaActual}.SqlQuery",
                        Texto = variante
                    });
                }

                if (variantes.Count == 0 && ContieneNombreFlexible(node.SqlQuery, spBaseName))
                {
                    resultados.Add(new Coincidencia
                    {
                        Ruta = $"{rutaActual}.SqlQuery",
                        Texto = $"[FLEXIBLE] {spBaseName}"
                    });
                }
            }

            var children = node.Children ?? new List<QueryNode>();

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var rutaHijo = $"{rutaActual}.Children[{i}]";
                resultados.AddRange(BuscarCoincidencias(child, patrones, spBaseName, rutaHijo));
            }

            return resultados;
        }

        static bool ContieneAlgunPatron(string text, List<PatronBusquedaSp> patrones, string spBaseName)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            foreach (var patron in patrones)
            {
                if (patron.Regex.IsMatch(text))
                    return true;
            }

            return ContieneNombreFlexible(text, spBaseName);
        }

        static List<string> ObtenerVariantesEncontradas(string text, List<PatronBusquedaSp> patrones)
        {
            var encontrados = new List<(int Index, int Length, string Value)>();

            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            foreach (var patron in patrones)
            {
                var matches = patron.Regex.Matches(text);

                foreach (Match match in matches)
                {
                    if (match.Success && !string.IsNullOrWhiteSpace(match.Value))
                    {
                        encontrados.Add((match.Index, match.Length, match.Value.Trim()));
                    }
                }
            }

            var finales = encontrados
                .OrderByDescending(x => x.Length)
                .ThenBy(x => x.Index)
                .Aggregate(new List<(int Index, int Length, string Value)>(), (acc, actual) =>
                {
                    bool solapado = acc.Any(x =>
                        actual.Index < x.Index + x.Length &&
                        x.Index < actual.Index + actual.Length);

                    if (!solapado)
                        acc.Add(actual);

                    return acc;
                })
                .OrderBy(x => x.Index)
                .Select(x => x.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return finales;
        }

        static bool ContieneNombreFlexible(string text, string spBaseName)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var textNormalized = NormalizeLoose(text);
            var spNormalized = NormalizeLoose(spBaseName);

            return textNormalized.Contains(spNormalized, StringComparison.OrdinalIgnoreCase);
        }

        static string NormalizeLoose(string input)
        {
            var sb = new StringBuilder(input.Length);

            foreach (var ch in input)
            {
                if (char.IsLetterOrDigit(ch) || ch == '_')
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
            }

            return sb.ToString();
        }

        static List<PatronBusquedaSp> ConstruirPatronesSp(string spBaseName, string schemaName, string databaseName, string serverName)
        {
            var patrones = new List<PatronBusquedaSp>();

            string[] ObjForms = BuildForms(spBaseName);
            string[] SchForms = BuildForms(schemaName);
            string[] DbForms = BuildForms(databaseName);
            string[] SrvForms = BuildForms(serverName);

            AddPattern(patrones, Expand(ObjForms));
            AddPattern(patrones, Expand(SchForms, ObjForms));
            AddPattern(patrones, Expand(DbForms, new[] { "" }, ObjForms));
            AddPattern(patrones, Expand(DbForms, SchForms, ObjForms));
            AddPattern(patrones, Expand(SrvForms, DbForms, SchForms, ObjForms));

            AddPattern(patrones, PrefixExec(Expand(ObjForms)));
            AddPattern(patrones, PrefixExec(Expand(SchForms, ObjForms)));
            AddPattern(patrones, PrefixExec(Expand(DbForms, new[] { "" }, ObjForms)));
            AddPattern(patrones, PrefixExec(Expand(DbForms, SchForms, ObjForms)));
            AddPattern(patrones, PrefixExec(Expand(SrvForms, DbForms, SchForms, ObjForms)));

            return patrones;
        }

        static void AddPattern(List<PatronBusquedaSp> patrones, List<string> variantes)
        {
            foreach (var variante in variantes.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var regexText = $@"(?<![\w\]]){Regex.Escape(variante).Replace(@"\.", @"\s*\.\s*")}(?![\w\[])";

                patrones.Add(new PatronBusquedaSp
                {
                    Descripcion = variante,
                    VariantesExactas = new List<string> { variante },
                    Regex = new Regex(regexText, RegexOptions.IgnoreCase | RegexOptions.Compiled)
                });
            }
        }

        static string[] BuildForms(string value)
        {
            return new[]
            {
                value,
                $"[{value}]"
            };
        }

        static List<string> Expand(params string[][] parts)
        {
            var result = new List<string> { "" };

            foreach (var partGroup in parts)
            {
                var next = new List<string>();

                foreach (var prefix in result)
                {
                    foreach (var part in partGroup)
                    {
                        if (part == "")
                        {
                            next.Add(string.IsNullOrEmpty(prefix) ? "" : prefix + "..");
                        }
                        else if (string.IsNullOrEmpty(prefix))
                        {
                            next.Add(part);
                        }
                        else if (prefix.EndsWith(".."))
                        {
                            next.Add(prefix + part);
                        }
                        else
                        {
                            next.Add(prefix + "." + part);
                        }
                    }
                }

                result = next;
            }

            return result;
        }

        static List<string> PrefixExec(List<string> values)
        {
            return values.Select(v => $"EXEC {v}")
                         .Concat(values.Select(v => $"EXECUTE {v}"))
                         .ToList();
        }
    }
}
