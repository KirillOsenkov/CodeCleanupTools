using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

class Context
{
    public IReadOnlyList<string> AnalyzerFilePaths { get; set; }
    public IReadOnlyList<string> CodeFixIds { get; set; }
}

class Program
{
    static void Main(string[] args)
    {
        string binlog = @"C:\temp\formattee\msbuild.binlog";

        string analyzersDirectory = @"C:\Users\kirillo\.nuget\packages\microsoft.visualstudio.threading.analyzers\17.11.20\analyzers\cs";

        var context = new Context
        {
            AnalyzerFilePaths =
            [
                $@"{analyzersDirectory}\Microsoft.VisualStudio.Threading.Analyzers.dll",
                $@"{analyzersDirectory}\Microsoft.VisualStudio.Threading.Analyzers.CSharp.dll",
                $@"{analyzersDirectory}\Microsoft.VisualStudio.Threading.Analyzers.CodeFixes.dll",
            ],
            CodeFixIds = ["VSTRHD111"]
        };

        var invocations = CompilerInvocationsReader.ReadInvocations(binlog);
        foreach (var invocation in invocations)
        {
            FormatCompilation(invocation, context);
        }
    }

    private static void FormatCompilation(CompilerInvocation invocation, Context context)
    {
        var workspace = MSBuildWorkspace.Create();

        string projectFilePath = invocation.ProjectFilePath;

        string arguments = invocation.CommandLineArguments;

        arguments = AppendAnalyzers(arguments, context);

        string language = projectFilePath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ?
                LanguageNames.VisualBasic : LanguageNames.CSharp;

        var projectInfo = CommandLineProject.CreateProjectInfo(
            projectFilePath,
            LanguageNames.CSharp,
            invocation.CommandLineArguments,
            invocation.ProjectDirectory,
            workspace);

        var solution = workspace.CurrentSolution.AddProject(projectInfo);

        var project = solution.GetProject(projectInfo.Id);
        var compilation = project.GetCompilationAsync().Result;

        var diagnostics = compilation.GetDiagnostics();
    }

    private static string AppendAnalyzers(string arguments, Context context)
    {
        foreach (var analyzerFilePath in context.AnalyzerFilePaths)
        {
            string analyzerAssemblyName = Path.GetFileNameWithoutExtension(analyzerFilePath);

            if (!arguments.Contains(analyzerAssemblyName, StringComparison.OrdinalIgnoreCase))
            {
                string quoted = analyzerFilePath;
                if (quoted.Contains(" "))
                {
                    quoted = $"\"{quoted}\"";
                }

                arguments = $"{arguments} -analyzer:{quoted}";
            }
        }

        return arguments;
    }
}