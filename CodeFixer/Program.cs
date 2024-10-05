﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
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
        var workspace = MSBuildWorkspace.Create();

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

        var fixers = LoadFixers(assemblies, language);

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

        foreach (var kvp in analyzersAndFixersPerId)
        {
            Fix(kvp.id, kvp.analyzer, kvp.fixer);
        }

        void Fix(string id, DiagnosticAnalyzer analyzer, CodeFixProvider fixer)
        {
            var fixAllProvider = fixer.GetFixAllProvider();
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

    public static ImmutableArray<CodeFixProvider> LoadFixers(IEnumerable<Assembly> assemblies, string language)
    {
        return assemblies
            .SelectMany(GetConcreteTypes)
            .Where(t => typeof(CodeFixProvider).IsAssignableFrom(t))
            .Where(t => IsExportedForLanguage(t, language))
            .Select(CreateInstanceOfCodeFix)
            .OfType<CodeFixProvider>()
            .ToImmutableArray();
    }

    private static bool IsExportedForLanguage(Type codeFixProvider, string language)
    {
        var exportAttribute = codeFixProvider.GetCustomAttribute<ExportCodeFixProviderAttribute>(inherit: false);
        return exportAttribute is not null && exportAttribute.Languages.Contains(language);
    }

    private static CodeFixProvider CreateInstanceOfCodeFix(Type codeFixProvider)
    {
        try
        {
            return (CodeFixProvider)Activator.CreateInstance(codeFixProvider);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<Type> GetConcreteTypes(Assembly assembly)
    {
        try
        {
            var concreteTypes = assembly
                .GetTypes()
                .Where(type => !type.GetTypeInfo().IsInterface
                    && !type.GetTypeInfo().IsAbstract
                    && !type.GetTypeInfo().ContainsGenericParameters);

            // Realize the collection to ensure exceptions are caught
            return concreteTypes.ToList();
        }
        catch
        {
            return Type.EmptyTypes;
        }
    }
}