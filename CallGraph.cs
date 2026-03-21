using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Project;
using Project.TaintAnalyze;
using Serilog;
using System.Collections.Generic;

internal class CallGraph
{
    private static readonly CallGraph _instance = new();
    private CallGraph() { }
    public static CallGraph Instance => _instance;

    private readonly Dictionary<IMethodSymbol, HashSet<IMethodSymbol>> _tainted = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<IMethodSymbol, HashSet<IPropertySymbol>> _taintedp = new(SymbolEqualityComparer.Default);
    private readonly HashSet<INamedTypeSymbol> _inherited = new(SymbolEqualityComparer.Default);


    public void AddTainted(IMethodSymbol cur, IMethodSymbol from)
    {
        lock (_tainted)
        {
            if (!_tainted.TryGetValue(cur, out var taintedFrom))
            {
                taintedFrom = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
                _tainted[cur] = taintedFrom;
            }
            taintedFrom.Add(from);
        }
    }

    public void AddTaintedP(IMethodSymbol cur, IPropertySymbol from)
    {
        lock (_taintedp)
        {
            if (!_taintedp.TryGetValue(cur, out var taintedFrom))
            {
                taintedFrom = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
                _taintedp[cur] = taintedFrom;
            }
            taintedFrom.Add(from);
        }
    }

    public void AddPublicGadget(INamedTypeSymbol className)
    {
        lock (_inherited)
        {
            _inherited.Add(className);
        }
    }

    public IReadOnlyDictionary<IMethodSymbol, HashSet<IMethodSymbol>> GetTaintMap()
    {
        return _tainted;
    }

    public IReadOnlyDictionary<IMethodSymbol, HashSet<IPropertySymbol>> GetTaintMapP()
    {
        return _taintedp;
    }


    public IReadOnlySet<INamedTypeSymbol> GetInherited()
    {
        return _inherited;
    }



    public void Build(Compilation compilation)
    {
        foreach (var syntaxTree in compilation.SyntaxTrees!.Where(t => t != null))
        {
            SemanticModel semanticModel = compilation!.GetSemanticModel(syntaxTree!);

            foreach (var classDeclaration in syntaxTree!.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                foreach (var methodDeclaration in classDeclaration.Members.Where(m => m is MethodDeclarationSyntax || m is ConstructorDeclarationSyntax))
                {

                    // 如果声明了 DllImport 就将这个函数加入到 Sink list
                    foreach (var attrList in methodDeclaration.AttributeLists)
                    {
                        foreach (var attr in attrList.Attributes)
                        {
                            var typeInfo = semanticModel.GetTypeInfo(attr);
                            if (typeInfo.Type?.ToDisplayString() == "System.Runtime.InteropServices.DllImportAttribute")
                            {
                                if (semanticModel.GetDeclaredSymbol(methodDeclaration) is IMethodSymbol symbol)
                                {
                                    SinkDetector.RegisterDllImportMethod(symbol);
                                }
                            }
                        }
                    }

                    Analyzer.AnalyzeMethodBody((BaseMethodDeclarationSyntax)methodDeclaration, semanticModel);
                    
                }
            }
        }
    }


    public void Clear()
    {
        _tainted.Clear();
        _tainted.Clear();
        _inherited.Clear();
    }
}
