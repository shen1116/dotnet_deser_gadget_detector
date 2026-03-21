using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Xml.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Serilog;

namespace Project;
public static class ProjectLoader
{
    public static List<Compilation> Load(string path)
    {
        List<Compilation> compilations = [];
        if (path.EndsWith(".sln"))
            compilations = LoadFromSolutionAsync(path);
        else if (path.EndsWith(".csproj"))
            compilations.Add(LoadFromCsprojAsync(path));
        else if (Directory.Exists(path))
            compilations.Add(LoadFromSourceCode(path));
        else
            Log.Error("Path not exists: " + path);
        return compilations;
    }

    public static List<Compilation> LoadFromSolutionAsync(string solutionPath)
    { 
        var workspace = MSBuildWorkspace.Create();
        var solution = workspace.OpenSolutionAsync(solutionPath).GetAwaiter().GetResult();

        var compilations = new List<Compilation>();
        foreach (var project in solution.Projects)
        {
            var compilation = project.GetCompilationAsync().GetAwaiter().GetResult();
            if (compilation != null)
            {
                compilations.Add(compilation);
            }
        }

        return compilations;
    }

    public static Compilation LoadFromCsprojAsync(string csprojPath)
    {
        var workspace = MSBuildWorkspace.Create();
        var project = workspace.OpenProjectAsync(csprojPath).GetAwaiter().GetResult(); ;

        return project.GetCompilationAsync().Result!;
    }

    public static Compilation LoadFromSourceCode(string codePath)
    {
        var csFiles = Directory.GetFiles(codePath, "*.cs", SearchOption.AllDirectories);

        var syntaxTrees = csFiles
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file))
            .ToList();

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            "compilation",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication)
        );

        return compilation;
    }

}
