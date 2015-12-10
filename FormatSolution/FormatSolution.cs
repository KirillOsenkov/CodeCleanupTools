using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MSBuild;

class FormatSolution
{
    static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("Usage: formatsolution.exe <path-to-solution>.sln");
            return;
        }

        var slnPath = args[0];
        if (!File.Exists(slnPath))
        {
            Console.WriteLine("Solution file not found: " + slnPath);
            return;
        }

        var workspace = MSBuildWorkspace.Create();
        var solution = workspace.OpenSolutionAsync(slnPath).GetAwaiter().GetResult();
        var projectIds = solution.ProjectIds;
        foreach (var projectId in projectIds)
        {
            var project = solution.GetProject(projectId);
            solution = ProcessProject(project);
        }

        workspace.TryApplyChanges(solution);
    }

    private static Solution ProcessProject(Project project)
    {
        var solution = project.Solution;
        foreach (var documentId in project.DocumentIds)
        {
            var document = project.GetDocument(documentId);

            if (document.Name.EndsWith(".Designer.cs") || document.Name.EndsWith(".Designer.vb"))
            {
                continue;
            }

            var newDocument = Formatter.FormatAsync(document).GetAwaiter().GetResult();
            if (newDocument != document)
            {
                Write("Formatting: " + document.FilePath);
            }

            newDocument = RemoveConsecutiveEmptyLinesWorker.Process(newDocument);

            project = newDocument.Project;
            solution = project.Solution;
        }

        return solution;
    }

    public static void Write(string message, ConsoleColor color = ConsoleColor.Gray)
    {
        lock (typeof(Console))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(DateTime.Now.ToString("HH:mm:ss") + " ");
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            if (color != ConsoleColor.Gray)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }
    }
}