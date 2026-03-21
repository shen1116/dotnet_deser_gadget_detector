
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Sinks.Async;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;


namespace Project;
internal class Program
{
    static void Main(string[] args)
    {
        // # 0: Config
        LogConfigurator.Configure();

        MSBuildLocator.RegisterDefaults();
        var solutionPath = @"C:\Users\Administrator\Desktop\TestCode\aws-sdk-net-main\sdk\AWSSDK.Net45.sln";

        // # 1: Load project
        Log.Information("Load " + solutionPath);
        var compilations = ProjectLoader.Load(solutionPath);

        // # 2: Create call graph
        foreach (var compilation in compilations)
        {
            CallGraph.Instance.Build(compilation);
        }

        // # 3: Debug or analysis
        SinkDetector.GetGadgetChain();

        var p = CallGraph.Instance.GetInheritedFromPublicGadget();
        if (p.Count != 0) 
        {
            Log.Information("Have Public Gadget");
            foreach (var pg in p)
                Log.Information(pg.ToDisplayString());
        }
        else
        {
            Log.Information("No Public Gadget");
        }


        Log.CloseAndFlush();
    }
}
