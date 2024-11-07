using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    public IReadOnlyList<string> FixerEquivalenceKeys { get; set; }
    public bool Suppress { get; set; } = true;
}

class Program
{
    static void Main(string[] args)
    {
        string binlog = @"C:\temp\formattee\msbuild.binlog";

        //string analyzersDirectory = @"C:\Users\kirillo\.nuget\packages\microsoft.visualstudio.threading.analyzers\17.11.20\analyzers\cs";

        var context = new Context
        {
            //AnalyzerFilePaths =
            //[
            //    $@"{analyzersDirectory}\Microsoft.VisualStudio.Threading.Analyzers.dll",
            //    $@"{analyzersDirectory}\Microsoft.VisualStudio.Threading.Analyzers.CSharp.dll",
            //    $@"{analyzersDirectory}\Microsoft.VisualStudio.Threading.Analyzers.CodeFixes.dll",
            //],
            //CodeFixIds = ["VSTHRD111"],
            //FixerEquivalenceKeys = ["False"]
        };

        // Reuse the workspace to share services across all projects. Specifically this will reuse
        // metadata and analyzer references across projects, which are very expensive.
        var workspace = new AdhocWorkspace();

        Write($"Reading {binlog}");
        var invocations = CompilerInvocationsReader.ReadInvocations(binlog);
        foreach (var invocation in invocations)
        {
            if (invocation.ProjectFilePath.Contains("wpftmp.csproj", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                FormatCompilation(invocation, context, workspace);
            }
            catch
            {
            }
        }
    }

    private static void FormatCompilation(CompilerInvocation invocation, Context context, AdhocWorkspace workspace)
    {
        string projectFilePath = invocation.ProjectFilePath;

        Write($"{projectFilePath}");

        string arguments = invocation.CommandLineArguments;

        if (context.AnalyzerFilePaths != null)
        {
            arguments = AppendAnalyzers(arguments, context);
        }

        string language = projectFilePath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ?
                LanguageNames.VisualBasic : LanguageNames.CSharp;

        var projectInfo = CommandLineProject.CreateProjectInfo(
            projectFilePath,
            language,
            arguments,
            invocation.ProjectDirectory,
            workspace);
        projectInfo = projectInfo.WithFilePath(projectFilePath);

        var solution = workspace.CurrentSolution.AddProject(projectInfo);

        var project = solution.GetProject(projectInfo.Id);
        var compilation = project.GetCompilationAsync().Result;

        var relevantAnalyzerReferences = project.AnalyzerReferences.OfType<AnalyzerFileReference>().ToArray();
        if (context.AnalyzerFilePaths != null)
        {
            relevantAnalyzerReferences = relevantAnalyzerReferences.Where(a => context.AnalyzerFilePaths.Contains(a.FullPath)).ToArray();
        }

        if (relevantAnalyzerReferences.Length == 0)
        {
            return;
        }

        var assemblies = relevantAnalyzerReferences.Select(a => a.GetAssembly()).ToArray();

        var analyzers = relevantAnalyzerReferences.SelectMany(a => a.GetAnalyzers(language));

        var fixers = FixerLoader.LoadFixers(assemblies, language);

        (string id, DiagnosticAnalyzer analyzer, CodeFixProvider fixer)[] analyzersAndFixersPerId = null;

        if (context.CodeFixIds != null)
        {
            analyzersAndFixersPerId = context.CodeFixIds.Select(id =>
            (
                id,
                analyzer: analyzers.FirstOrDefault(a => a.SupportedDiagnostics.Any(d => d.Id == id)),
                fixer: fixers.FirstOrDefault(f => f.FixableDiagnosticIds.Contains(id))
            )).Where(t => t.analyzer != null && t.fixer != null).ToArray();
            if (analyzersAndFixersPerId.Length == 0)
            {
                return;
            }

            analyzers = analyzersAndFixersPerId.Select(t => t.analyzer);
        }

        var analyzerOptions = new CompilationWithAnalyzersOptions(
            project.AnalyzerOptions,
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false);
        var analyzerCompilation = compilation.WithAnalyzers(analyzers.ToImmutableArray(), analyzerOptions);

        var diagnostics = analyzerCompilation.GetAnalyzerDiagnosticsAsync().Result;

        var newSolution = solution;

        if (context.Suppress)
        {
            var csharpFeatures = Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features");
            var suppressionCodeFixProviderType = csharpFeatures.GetType("Microsoft.CodeAnalysis.CSharp.CodeFixes.Suppression.CSharpSuppressionCodeFixProvider");
            var csharpSuppressionCodeFixProvider = Activator.CreateInstance(suppressionCodeFixProviderType);

            var workspacesAssembly = typeof(Workspace).Assembly;

            var diagnosticsPerFile = diagnostics
                .Where(d => d.Location.IsInSource)
                .GroupBy(d => d.Location.SourceTree)
                .OrderBy(d => d.Key.FilePath)
                .ToArray();

            var codeActionOptionsType = workspacesAssembly.GetType("Microsoft.CodeAnalysis.CodeActions.CodeActionOptions");
            var codeActionOptionsProvider = codeActionOptionsType.GetField("DefaultProvider", BindingFlags.Public | BindingFlags.Static).GetValue(null);

            var getFixesAsyncMethodInfo = suppressionCodeFixProviderType
                .GetMethod(
                    "GetFixesAsync",
                    [
                        typeof(TextDocument),
                        typeof(TextSpan),
                        typeof(IEnumerable<Diagnostic>),
                        workspacesAssembly.GetType("Microsoft.CodeAnalysis.CodeActions.CodeActionOptionsProvider"),
                        typeof(CancellationToken)
                    ]);

            foreach (var kvp in diagnosticsPerFile)
            {
                var tree = kvp.Key;
                var diagnosticsInFile = kvp.OrderByDescending(d => d.Location.SourceSpan.Start).ToArray();

                var document = newSolution.GetDocument(tree);
                if (document == null)
                {
                    continue;
                }

                Write($"    {tree.FilePath}", ConsoleColor.DarkGray);

                foreach (var diag in diagnosticsInFile)
                {
                    document = newSolution.GetDocument(document.Id);

                    var task = getFixesAsyncMethodInfo.Invoke(csharpSuppressionCodeFixProvider,
                        [
                            document,
                            diag.Location.SourceSpan,
                            (IEnumerable<Diagnostic>)[diag],
                            codeActionOptionsProvider,
                            CancellationToken.None
                        ]);
                    var suppressionFixes = task.GetType().GetProperty("Result").GetValue(task) as IList;
                    if (suppressionFixes.Count == 0)
                    {
                        continue;
                    }

                    var codeFixContext = new CodeFixContext(document, diag, (codeAction, diags) =>
                    {
                        var globalSuppression = codeAction.NestedActions.FirstOrDefault(n => n.GetType().Name == "GlobalSuppressMessageCodeAction");
                        var ops = globalSuppression.GetOperationsAsync(newSolution, null, CancellationToken.None).Result;
                        var op = ops.OfType<ApplyChangesOperation>().FirstOrDefault();
                        var candidateSolution = op.ChangedSolution;

                        // Roslyn creates a new GlobalSuppressions document with FilePath null, and then
                        // fails to detect it here:
                        // https://github.com/dotnet/roslyn/blob/b0b8e0fe16f29a602422fa93e6366521437a4188/src/Features/Core/Portable/CodeFixes/Suppression/AbstractSuppressionCodeFixProvider.AbstractGlobalSuppressMessageCodeAction.cs#L76
                        if (candidateSolution.GetProject(project.Id).DocumentIds.Except(project.DocumentIds).ToArray() is { Length: 1 } newDocIds)
                        {
                            var globalSuppressionsDocument = candidateSolution.GetDocument(newDocIds[0]);
                            globalSuppressionsDocument = globalSuppressionsDocument.WithFilePath(
                                Path.Combine(invocation.ProjectDirectory, globalSuppressionsDocument.Name));
                            candidateSolution = globalSuppressionsDocument.Project.Solution;
                        }

                        newSolution = candidateSolution;
                    }, CancellationToken.None);

                    foreach (var suppressionFix in suppressionFixes)
                    {
                        var type = suppressionFix.GetType();
                        var action = type.GetField("Action", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(suppressionFix) as CodeAction;
                        var diagnostic = (ImmutableArray<Diagnostic>) type.GetField("Diagnostics", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(suppressionFix);
                        codeFixContext.RegisterCodeFix(action, diagnostic);
                    }
                }
            }
        }
        else if (analyzersAndFixersPerId != null)
        {
            foreach (var kvp in analyzersAndFixersPerId)
            {
                Fix(kvp.id, kvp.analyzer, kvp.fixer);
            }
        }
        else
        {
            foreach (var diag in diagnostics)
            {
                Console.WriteLine(diag.ToString());
            }
        }

        void Fix(string id, DiagnosticAnalyzer analyzer, CodeFixProvider fixer)
        {
            var diagnosticsPerFile = diagnostics
                .Where(d => d.Id == id && d.Location.IsInSource)
                .GroupBy(d => d.Location.SourceTree)
                .OrderBy(d => d.Key.FilePath)
                .ToArray();

            foreach (var kvp in diagnosticsPerFile)
            {
                var tree = kvp.Key;
                var diagnosticsInFile = kvp.OrderByDescending(d => d.Location.SourceSpan.Start).ToArray();

                var document = newSolution.GetDocument(tree);
                if (document == null)
                {
                    continue;
                }

                Write($"    {tree.FilePath}", ConsoleColor.DarkGray);

                foreach (var diag in diagnosticsInFile)
                {
                    document = newSolution.GetDocument(document.Id);

                    var codeFixContext = new CodeFixContext(document, diag, (codeAction, diags) =>
                    {
                        var fixerNames = context.FixerEquivalenceKeys;
                        if (fixerNames == null ||
                            fixerNames.Count == 0 ||
                            codeAction.EquivalenceKey == null ||
                            fixerNames.Contains(codeAction.EquivalenceKey))
                        {
                            var op = codeAction.GetOperationsAsync(CancellationToken.None).Result.OfType<ApplyChangesOperation>().FirstOrDefault();
                            newSolution = op.ChangedSolution;
                        }
                    }, CancellationToken.None);
                    fixer.RegisterCodeFixesAsync(codeFixContext);
                }
            }
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
                WriteText(newDoc.FilePath, newText.ToString(), newText.Encoding);
            }
        }

        foreach (var newDocId in newProject.DocumentIds.Except(project.DocumentIds))
        {
            var newDoc = newProject.GetDocument(newDocId);
            var newText = newDoc.GetTextAsync().Result;
            WriteText(newDoc.FilePath, newText.ToString(), Encoding.UTF8);
        }
    }

    private static void WriteText(string filePath, string text, Encoding encoding = null)
    {
        try
        {
            if (encoding != null)
            {
                File.WriteAllText(filePath, text, encoding);
            }
            else
            {
                File.WriteAllText(filePath, text);
            }
        }
        catch
        {
        }
    }

    public static void Write(
        string message,
        ConsoleColor color = ConsoleColor.Gray,
        bool newLineAtEnd = true,
        TextWriter writer = null)
    {
        writer ??= Console.Out;

        lock (writer)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            if (newLineAtEnd)
            {
                writer.WriteLine(message);
            }
            else
            {
                writer.Write(message);
            }

            Console.ForegroundColor = oldColor;
        }
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