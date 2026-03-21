using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project
{
    internal class Analyzer()
    {
        public static void AnalyzeMethodBody(BaseMethodDeclarationSyntax methodDeclaration, SemanticModel semanticModel)
        {
            // 更好的处理方法是根据函数的类别做不同的处理

            HashSet<ISymbol> taintedSymbols = [];

            // 先把方法的参数拉进来
            foreach (var parameter in methodDeclaration.ParameterList.Parameters)
            {
                var symbol = semanticModel.GetDeclaredSymbol(parameter);
                if (symbol != null)
                {
                    taintedSymbols.Add(symbol);
                }
            }

            var methodDeclSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration)!;
            var methodBody = methodDeclaration.Body;
            if (methodBody == null)
                return;
            var assignments = methodBody.DescendantNodes().OfType<AssignmentExpressionSyntax>().ToList();
            var invocations = methodBody.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();

            // 赋值和方法调用按顺序遍历
            foreach (var node in assignments.Cast<SyntaxNode>().Concat(invocations.Cast<SyntaxNode>()).OrderBy(n => n.SpanStart))
            {
                /*
                // 有个问题！如果参数是直接计算、赋值就行，但如果包了层函数就不行了
                if (node is AssignmentExpressionSyntax assignment)
                {
                    // 处理赋值语句
                    // 如果说做其他静态分析的话，可以在处理右表达式时加上自定义的找 source 逻辑，比如定义 Request.QueryString["input"] 为 taint。。。
                    var rhsSymbols = assignment.Right.DescendantNodesAndSelf()
                        .OfType<IdentifierNameSyntax>()
                        .Select(id => semanticModel.GetSymbolInfo(id).Symbol)
                        .Where(s => s != null);

                    if (taintedSymbols.Count > 0 && rhsSymbols.Any(s => taintedSymbols.Contains(s!)))
                    {
                        var lhsSymbol = semanticModel.GetSymbolInfo(assignment.Left).Symbol;
                        if (lhsSymbol != null)
                            taintedSymbols.Add(lhsSymbol);
                    }
                }
                */
                if (node is VariableDeclaratorSyntax declarator && declarator.Initializer != null)
                {
                    var rhsSymbols = declarator.Initializer.Value
                        .DescendantNodesAndSelf()
                        .OfType<IdentifierNameSyntax>()
                        .Select(id => semanticModel.GetSymbolInfo(id).Symbol)
                        .Where(s => s != null);

                    if (rhsSymbols.Any(s => taintedSymbols.Contains(s!)))
                    {
                        var lhsSymbol = semanticModel.GetDeclaredSymbol(declarator);
                        if (lhsSymbol != null)
                            taintedSymbols.Add(lhsSymbol);
                    }
                }

                else if (node is InvocationExpressionSyntax invocation)
                {
                    // invocation 可能不包括构造函数
                    if (semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol symbol)
                    {
                        // 处理方法调用
                        var arguments = invocation.ArgumentList.Arguments;
                        foreach (var argument in arguments)
                        {
                            var symbolsUsedInArgument = argument.Expression.DescendantNodesAndSelf()
                                .OfType<IdentifierNameSyntax>()
                                .Select(id => semanticModel.GetSymbolInfo(id).Symbol)
                                .Where(s => s != null); ;
                            if (symbolsUsedInArgument.Any(s => taintedSymbols.Contains(s!)))
                            {
                                CallGraph.Instance.AddTainted(symbol, methodDeclSymbol);
                            }
                        }
                    }
                    else
                    {
                        Log.Warning(methodDeclSymbol.ToDisplayString());
                        Log.Warning("    -> [Unresolved Symbol] " + invocation.ToString());
                        var diagnostics = semanticModel.GetDiagnostics(invocation.Span);
                        foreach (var diag in diagnostics)
                        {
                            Log.Warning($"    [Diag] {diag.Id}: {diag.GetMessage()}");
                        }
                    }
                }
            }
        }

        public static bool InheritsFromPublicGadget(INamedTypeSymbol classSymbol)
        {
            var current = classSymbol.BaseType;
            while (current != null)
            {
                if (PublicGadget.Contains(current.ToString()))
                    return true;
                current = current.BaseType;
            }
            return false;
        }

        private static readonly string[] PublicGadget =
        [
            "Microsoft.Build.Evaluation.Project",
            "Microsoft.Data.Schema.SchemaModel.ModelStore",
            "Microsoft.Diagnostics.Runtime.DbgEngDataReader",
            "Microsoft.Diagnostics.Runtime.DumpDataReader",
            "Microsoft.Diagnostics.Runtime.Utilities.Command",
            "Microsoft.Diagnostics.Runtime.Utilities.DumpReader",
            "Microsoft.Diagnostics.Runtime.Utilities.PEFile",
            "Microsoft.Diagnostics.Runtime.Utilities.Pdb.PdbReader",
            "Microsoft.FailoverClusters.NotificationViewer.ConfigStore",
            "Microsoft.Forefront.Monitoring.ActiveMonitoring.OrganizationInitializationDefinition",
            "Microsoft.Forefront.Monitoring.ActiveMonitoring.RecipientProvisioningDefinition",
            "Microsoft.IdentityModel.Claims.WindowsClaimsIdentity",
            "Microsoft.Management.UI.FilterRuleExtensions",
            "Microsoft.Management.UI.Internal.FilterRuleExtensions",
            "Microsoft.Practices.EnterpriseLibrary.Caching.Expirations.FileDependency",
            "Microsoft.Practices.EnterpriseLibrary.Common.Configuration.FileConfigurationSource",
            "Microsoft.Practices.EnterpriseLibrary.Logging.TraceListeners.FlatFileTraceListener",
            "Microsoft.Practices.EnterpriseLibrary.Logging.TraceListeners.FormattedTextWriterTraceListener",
            "Microsoft.Reporting.RdlCompile.ReadStateFile",
            "Microsoft.TeamFoundation.VersionControl.Client.PolicyEnvelope",
            "Microsoft.VisualStudio.DebuggerVisualizers.VisualizerObjectSource",
            "Microsoft.VisualStudio.Editors.PropPageDesigner.PropertyPageSerializationService+PropertyPageSerializationStore",
            "Microsoft.VisualStudio.EnterpriseTools.Shell.ModelingPackage",
            "Microsoft.VisualStudio.Modeling.Diagnostics.XmlSerialization",
            "Microsoft.VisualStudio.Publish.BaseProvider.Util",
            "Microsoft.VisualStudio.Text.Formatting.TextFormattingRunProperties",
            "Microsoft.VisualStudio.Web.WebForms.ControlDesignerStateCache",
            "Microsoft.Web.Design.Remote.ProxyObject",
            "MicrosoftResearch.Infer.Utils.MatlabWriter",
            "System.Activities.Presentation.Internal.ManifestImages+XamlImageInfo",
            "System.Activities.Presentation.WorkflowDesigner",
            "System.AddIn.Hosting.AddInStore",
            "System.AddIn.Hosting.Utils",
            "System.CodeDom.Compiler.CodeDomProvider",
            "System.CodeDom.Compiler.TempFileCollection",
            "System.Collections.Generic.SortedSet`1",
            "System.Collections.Hashtable",
            "System.Collections.IEqualityComparer",
            "System.ComponentModel.Design.DesigntimeLicenseContextSerializer",
            "System.ComponentModel.ISupportInitialize",
            "System.Configuration.Install.AssemblyInstaller",
            "System.Configuration.SettingsPropertyValue",
            "System.Data.DataSet",
            "System.Data.DataTable",
            "System.Data.DataViewManager",
            "System.Data.Design.MethodSignatureGenerator",
            "System.Data.Design.TypedDataSetGenerator",
            "System.Data.Design.TypedDataSetSchemaImporterExtension",
            "System.Data.SerializationFormat",
            "System.Delegate",
            "System.DelegateSerializationHolder",
            "System.Drawing.Design.ToolboxItemContainer",
            "System.Drawing.Design.ToolboxItemContainer+ToolboxItemSerializer",
            "System.IdentityModel.Services.Tokens.MachineKeySessionSecurityTokenHandler",
            "System.IdentityModel.Tokens.SessionSecurityToken",
            "System.IdentityModel.Tokens.SessionSecurityTokenHandler",
            "System.IntPtr",
            "System.IO.DirectoryInfo",
            "System.IO.FileInfo",
            "System.IO.FileSystemInfo",
            "System.Management.Automation.ErrorRecord",
            "System.Management.Automation.PSObject",
            "System.Management.IWbemClassObjectFreeThreaded",
            "System.Messaging.BinaryMessageFormatter",
            "System.MulticastDelegate",
            "System.Resources.ResourceReader",
            "System.Resources.ResourceSet",
            "System.Resources.ResXFileRef.Converter",
            "System.Resources.ResXResourceReader",
            "System.Resources.ResXResourceSet",
            "System.Runtime.Remoting.Channels.BinaryClientFormatterSink",
            "System.Runtime.Remoting.Channels.BinaryClientFormatterSinkProvider",
            "System.Runtime.Remoting.Channels.BinaryServerFormatterSink",
            "System.Runtime.Remoting.Channels.BinaryServerFormatterSinkProvider",
            "System.Runtime.Remoting.Channels.CrossAppDomainSerializer",
            "System.Runtime.Remoting.Channels.SoapClientFormatterSink",
            "System.Runtime.Remoting.Channels.SoapClientFormatterSinkProvider",
            "System.Runtime.Remoting.Channels.SoapServerFormatterSink",
            "System.Runtime.Remoting.Channels.SoapServerFormatterSinkProvider",
            "System.Runtime.Remoting.ObjRef",
            "System.Runtime.Serialization.Formatters.Binary.BinaryFormatter",
            "System.Runtime.Serialization.Formatters.Soap.SoapFormatter",
            "System.Runtime.Serialization.IDeserializationCallback",
            "System.Runtime.Serialization.IObjectReference",
            "System.Runtime.Serialization.ISerializable",
            "System.Runtime.Serialization.NetDataContractSerializer",
            "System.Security.Claims.ClaimsIdentity",
            "System.Security.Claims.ClaimsPrincipal",
            "System.Security.Policy.EvidenceBase",
            "System.Security.Principal.WindowsIdentity",
            "System.Security.Principal.WindowsPrincipal",
            "System.Security.SecurityException",
            "System.UIntPtr",
            "System.Web.Script.Serialization.JavaScriptSerializer",
            "System.Web.Script.Serialization.SimpleTypeResolver",
            "System.Web.Security.RolePrincipal",
            "System.Web.UI.LosFormatter",
            "System.Web.UI.MobileControls.SessionViewState+SessionViewStateHistoryItem",
            "System.Web.UI.ObjectStateFormatter",
            "System.Windows.Data.ObjectDataProvider",
            "System.Windows.Forms.AxHost+State",
            "System.Windows.Forms.BindingSource",
            "System.Windows.Markup.XamlReader",
            "System.Windows.ResourceDictionary",
            "System.Workflow.ComponentModel.Activity",
            "System.Workflow.ComponentModel.Serialization.ActivitySurrogateSelector",
            "System.Xml.XmlDataDocument",
            "System.Xml.XmlDocument",
            "System.Xaml.XamlServices"


        ];
    }

}
