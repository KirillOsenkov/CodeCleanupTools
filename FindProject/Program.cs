using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        List<string> lines = new();

        if (Console.IsInputRedirected)
        {
            while (Console.In.ReadLine() is string line && !string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        if (args.Length == 1)
        {
            var arg = args[0];
            if (arg.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && File.Exists(arg))
            {
                arg = Path.GetFullPath(arg);
                lines.Add(arg);
            }
        }

        var projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var csFilePath in lines)
        {
            if (!File.Exists(csFilePath))
            {
                continue;
            }

            var project = FindNearestProject(csFilePath);
            if (File.Exists(project))
            {
                projects.Add(project);
            }
        }

        if (lines.Count == 0)
        {
            var project = GetPathOfFileAbove(Environment.CurrentDirectory, "*.csproj", includeStartingDirectory: true);
            if (File.Exists(project))
            {
                projects.Add(project);
            }
        }

        foreach (var project in projects.OrderBy(f => f))
        {
            Console.WriteLine(project);
        }
    }

    private static string FindNearestProject(string filePath)
    {
        var found = GetPathOfFileAbove(Path.GetDirectoryName(filePath), "*.csproj", includeStartingDirectory: true);
        return found;
    }

    public static string GetPathOfFileAbove(string directory, string fileName, bool includeStartingDirectory = false)
    {
        if (string.IsNullOrEmpty(directory) || !Path.IsPathRooted(directory))
        {
            return null;
        }

        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        if (!Directory.Exists(directory))
        {
            return null;
        }

        bool hasWildcards = fileName.Contains('*') || fileName.Contains('?');

        if (includeStartingDirectory && TryGetFile(directory, fileName, hasWildcards) is string inCurrent)
        {
            return inCurrent;
        }

        while (true)
        {
            directory = Path.GetDirectoryName(directory);
            if (string.IsNullOrEmpty(directory))
            {
                return null;
            }

            if (TryGetFile(directory, fileName, hasWildcards) is string found)
            {
                return found;
            }
        }

        static string TryGetFile(string directory, string fileName, bool hasWildcards)
        {
            if (hasWildcards)
            {
                var candidates = Directory.GetFiles(directory, fileName);
                if (candidates.Length == 1)
                {
                    return candidates[0];
                }
            }
            else
            {
                var candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }

}