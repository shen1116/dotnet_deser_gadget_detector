using Microsoft.CodeAnalysis;
using Org.BouncyCastle.Asn1.X509;
using Serilog;
using System.Collections.Concurrent;

namespace Project
{
    internal class SinkDetector
    {
        public static void GetGadgetChain()
        {
            var gadgets = new HashSet<string>();
            var taintMap = CallGraph.Instance.GetTaintMap();
            var sinks = taintMap.Keys
                .Where(p => Sinks.Any(s => $"{p.ToDisplayString()}".StartsWith(s)))
                .ToList();


            foreach (var sink in sinks)
            {
                var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
                var path = new List<IMethodSymbol>();

                void Dfs(IMethodSymbol symbol)
                {
                    if (!visited.Add(symbol))
                        return;

                    path.Add(symbol);

                    if (SourceDetector.IsSourceMethod(symbol) && path.Count != 1)
                    {
                        string gadget = "Taint Path:";
                        foreach (var (method, depth) in path.Select((m, i) => (m, i)))
                        {
                            gadget += $"{new string('\t', depth)}{method.ToDisplayString()}";
                            gadgets.Add(gadget);
                        }
                    }

                    if (taintMap.TryGetValue(symbol, out var sources))
                    {
                        foreach (var source in sources)
                        {
                            Dfs(source);
                        }
                    }

                    path.RemoveAt(path.Count - 1);
                }

                Dfs(sink);
            }

            foreach (var gadget in gadgets)
            {
                Log.Information(gadget);
            }

        }

        public static void RegisterDllImportMethod(IMethodSymbol method)
        {
            var methodName = method.ToDisplayString();
            if (!Sinks.Contains(methodName))
            {
                Sinks.Add(methodName);
            }
        }


        private static readonly ConcurrentBag<string> Sinks =
        [
            "System.Diagnostics.Process.Start",
            "System.Net.WebClient.UploadData",
            "System.Windows.Markup.XamlReader.Parse",
            "System.Windows.Markup.XamlReader.Load",
            "System.Reflection.Assembly.LoadFile",
            "System.Reflection.Assembly.LoadFrom",
            "System.Reflection.Assembly.Load",
            "System.Xml.XmlDocument.Load",
            "System.IO.File",
            "System.IO.Directory",
            "System.Runtime.Serialization.Formatters.Binary.BinaryFormatter.Deserialize",
            "System.Environment",
            // P/Invoke：遍历函数时，如果发现函数声明了 dllimport，就将它加入到 sinks，反正分析过程会记录调用。
        ];
    }
}
