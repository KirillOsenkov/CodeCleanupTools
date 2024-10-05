using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

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
            CodeFixIds = ["VSTHRD111"]
        };

        var invocations = CompilerInvocationsReader.ReadInvocations(binlog);
        foreach (var invocation in invocations)
        {
            FormatCompilation(invocation, context);
        }
    }

    private static void FormatCompilation(CompilerInvocation invocation, Context context)
    {
        var workspace = new AdhocWorkspace();

        string projectFilePath = invocation.ProjectFilePath;

        string arguments = invocation.CommandLineArguments;

        arguments = AppendAnalyzers(arguments, context);

        string language = projectFilePath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ?
                LanguageNames.VisualBasic : LanguageNames.CSharp;

        var projectInfo = CommandLineProject.CreateProjectInfo(
            projectFilePath,
            language,
            invocation.CommandLineArguments,
            invocation.ProjectDirectory,
            workspace);

        var solution = workspace.CurrentSolution.AddProject(projectInfo);

        var project = solution.GetProject(projectInfo.Id);
        var compilation = project.GetCompilationAsync().Result;

        var relevantAnalyzerReferences = project.AnalyzerReferences.OfType<AnalyzerFileReference>().Where(a => context.AnalyzerFilePaths.Contains(a.FullPath)).ToArray();

        var assemblies = relevantAnalyzerReferences.Select(a => a.GetAssembly()).ToArray();

        var analyzers = relevantAnalyzerReferences.SelectMany(a => a.GetAnalyzers(language));

        var fixers = FixerLoader.LoadFixers(assemblies, language);

        var codeFixIdSet = context.CodeFixIds.ToHashSet();

        var analyzersAndFixersPerId = context.CodeFixIds.Select(id =>
        (
            id,
            analyzer: analyzers.FirstOrDefault(a => a.SupportedDiagnostics.Any(d => d.Id == id)),
            fixer: fixers.FirstOrDefault(f => f.FixableDiagnosticIds.Contains(id))
        )).Where(t => t.analyzer != null && t.fixer != null).ToArray();

        var applicableAnalyzers = analyzersAndFixersPerId.Select(t => t.analyzer).ToImmutableArray();

        var analyzerOptions = new CompilationWithAnalyzersOptions(
            project.AnalyzerOptions,
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false);
        var analyzerCompilation = compilation.WithAnalyzers(applicableAnalyzers, analyzerOptions);

        var diagnostics = analyzerCompilation.GetAnalyzerDiagnosticsAsync().Result;

        var newSolution = solution;

        foreach (var kvp in analyzersAndFixersPerId)
        {
            Fix(kvp.id, kvp.analyzer, kvp.fixer);
        }

        var newProject = newSolution.GetProject(project.Id);

        foreach (var docId in project.DocumentIds)
        {
            var oldDoc = project.GetDocument(docId);
            var newDoc = newProject.GetDocument(docId);

            var oldText = oldDoc.GetTextAsync().Result;
            var newText = newDoc.GetTextAsync().Result;

            if (oldText != newText)
            {
                WriteChanges(newDoc.FilePath, newText);
            }
        }

        void Fix(string id, DiagnosticAnalyzer analyzer, CodeFixProvider fixer)
        {
            var diagnosticsPerFile = diagnostics
                .Where(d => d.Id == id && d.Location.IsInSource)
                .GroupBy(d => d.Location.SourceTree);

            foreach (var kvp in diagnosticsPerFile)
            {
                var tree = kvp.Key;
                var diagnosticsInFile = kvp;

                var document = solution.GetDocument(tree);
                if (document == null)
                {
                    continue;
                }

                document = newSolution.GetDocument(document.Id);

                foreach (var diag in diagnosticsInFile)
                {
                    var context = new CodeFixContext(document, diag, (codeAction, diags) =>
                    {
                        var op = codeAction.GetOperationsAsync(CancellationToken.None).Result.OfType<ApplyChangesOperation>().FirstOrDefault();
                        newSolution = op.ChangedSolution;
                    }, CancellationToken.None);
                    fixer.RegisterCodeFixesAsync(context);
                }
            }
        }
    }

    private static void WriteChanges(string filePath, SourceText newText)
    {
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