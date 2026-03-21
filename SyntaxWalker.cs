using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Project.TaintAnalyze;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project
{
    internal class SyntaxWalker(SemanticModel model) : CSharpSyntaxWalker
    {
        private readonly SemanticModel semanticModel = model;
        private HashSet<ISymbol> taintedSymbols = [];

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (semanticModel.GetDeclaredSymbol(node) is INamedTypeSymbol classSymbol && Analyzer.InheritsFromPublicGadget(classSymbol))
                CallGraph.Instance.AddPublicGadget(classSymbol);
            
            base.VisitClassDeclaration(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // 将 DllImport 函数注册为 Sink
            foreach (var attrList in node.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var typeInfo = semanticModel.GetTypeInfo(attr);
                    if (typeInfo.Type?.ToDisplayString() == "System.Runtime.InteropServices.DllImportAttribute")
                    {
                        if (semanticModel.GetDeclaredSymbol(node) is IMethodSymbol symbol)
                        {
                            SinkDetector.RegisterDllImportMethod(symbol);
                        }
                    }
                }
            }

            // 初始化 taintedSymbols
            foreach (var parameter in node.ParameterList.Parameters)
            {
                var symbol = semanticModel.GetDeclaredSymbol(parameter);
                if (symbol != null)
                {
                    taintedSymbols.Add(symbol);
                }
            }
            base.VisitMethodDeclaration(node);
            taintedSymbols.Clear();
        }

        public override void VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            if (node.IsKind(SyntaxKind.SetAccessorDeclaration))
            {
                // 如果是 set 访问器
                taintedSymbols.Add(semanticModel.GetDeclaredSymbol(node)?.Parameters.FirstOrDefault(p => p.Name == "value"));
            }

            base.VisitAccessorDeclaration(node);
            taintedSymbols.Clear();
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            // 初始化 taintedSymbols
            foreach (var parameter in node.ParameterList.Parameters)
            {
                var symbol = semanticModel.GetDeclaredSymbol(parameter);
                if (symbol != null)
                {
                    taintedSymbols.Add(symbol);
                }
            }
            base.VisitConstructorDeclaration(node);
            taintedSymbols.Clear();
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var ancestor = node.AncestorsAndSelf().FirstOrDefault(n =>
                            n is MethodDeclarationSyntax ||
                            n is ConstructorDeclarationSyntax ||
                            n is PropertyDeclarationSyntax);
            var ancestorSymbol = semanticModel.GetDeclaredSymbol(ancestor);

            if (semanticModel.GetSymbolInfo(node).Symbol is IMethodSymbol symbol)
            {
                var arguments = node.ArgumentList.Arguments;
                foreach (var arg in arguments)
                {
                    var argSymbolInfo = semanticModel.GetSymbolInfo(arg.Expression);
                    var argSymbol = argSymbolInfo.Symbol ?? (argSymbolInfo.CandidateSymbols.Length > 0 ? argSymbolInfo.CandidateSymbols[0] : null);

                    if (argSymbol != null && taintedSymbols.Contains(argSymbol))
                    {
                        if (ancestorSymbol is IMethodSymbol aMethodSymbol)
                        {
                            CallGraph.Instance.AddTainted(symbol, aMethodSymbol);
                        }else if(ancestorSymbol is IPropertySymbol aPropertySymbol)
                        {
                            CallGraph.Instance.AddTaintedP(symbol, aPropertySymbol);
                        }

                        break;
                    }
                }
            }
            else
            {
                Log.Warning(ancestorSymbol.ToDisplayString());
                Log.Warning("    -> [Unresolved Symbol] " + node.ToString());
                var diagnostics = semanticModel.GetDiagnostics(node.Span);
                foreach (var diag in diagnostics)
                {
                    Log.Warning($"    [Diag] {diag.Id}: {diag.GetMessage()}");
                }
            }

            base.VisitInvocationExpression(node);
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var ancestor = node.AncestorsAndSelf().FirstOrDefault(n =>
                            n is MethodDeclarationSyntax ||
                            n is ConstructorDeclarationSyntax ||
                            n is PropertyDeclarationSyntax);
            var ancestorSymbol = semanticModel.GetDeclaredSymbol(ancestor);

            if (semanticModel.GetSymbolInfo(node).Symbol is IMethodSymbol symbol)
            {
                var arguments = node.ArgumentList.Arguments;
                foreach (var arg in arguments)
                {
                    var argSymbolInfo = semanticModel.GetSymbolInfo(arg.Expression);
                    var argSymbol = argSymbolInfo.Symbol ?? (argSymbolInfo.CandidateSymbols.Length > 0 ? argSymbolInfo.CandidateSymbols[0] : null);

                    if (argSymbol != null && taintedSymbols.Contains(argSymbol))
                    {
                        if (ancestorSymbol is IMethodSymbol aMethodSymbol)
                        {
                            CallGraph.Instance.AddTainted(symbol, aMethodSymbol);
                        }
                        else if (ancestorSymbol is IPropertySymbol aPropertySymbol)
                        {
                            CallGraph.Instance.AddTaintedP(symbol, aPropertySymbol);
                        }

                        break;
                    }
                }
            }

            base.VisitObjectCreationExpression(node);
        }
    }
}
