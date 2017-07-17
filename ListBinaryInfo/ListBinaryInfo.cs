using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

class ListBinaryInfo
{
    private static readonly string netfxTools = @"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools";

    static void Main(string[] args)
    {
        string patternList = "*.dll;*.exe";
        bool recursive = true;

        var arguments = new HashSet<string>(args, StringComparer.OrdinalIgnoreCase);
        if (arguments.Contains("/nr"))
        {
            arguments.Remove("/nr");
            recursive = false;
        }

        if (arguments.Count > 0)
        {
            if (arguments.Count == 1)
            {
                patternList = arguments.First();
                if (patternList == "/?" || patternList == "-h" || patternList == "-help" || patternList == "help")
                {
                    PrintUsage();
                    return;
                }
            }
            else
            {
                PrintUsage();
                return;
            }
        }

        var files = new List<string>();
        if (File.Exists(patternList))
        {
            files.Add(Path.GetFullPath(patternList));
        }
        else
        {
            var root = Environment.CurrentDirectory;
            var patterns = patternList.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pattern in patterns)
            {
                files.AddRange(Directory.GetFiles(root, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly));
            }
        }

        foreach (var assemblyNameGroup in files.Select(f => FileInfo.Get(f)).GroupBy(f => f.AssemblyName).OrderBy(g => g.Key))
        {
            Highlight(assemblyNameGroup.Key, ConsoleColor.Cyan);
            foreach (var shaGroup in assemblyNameGroup.GroupBy(f => f.Sha))
            {
                Highlight("    SHA: " + shaGroup.Key, ConsoleColor.DarkCyan);
                foreach (var file in shaGroup.OrderBy(f => f.FilePath))
                {
                    Highlight("    " + file.FilePath, ConsoleColor.Gray);
                }

                var first = shaGroup.First();
                Highlight("        " + first.FullSigned, ConsoleColor.Green);
                Highlight("        " + first.Architecture, ConsoleColor.Blue);
                Highlight("        " + first.Platform, ConsoleColor.DarkYellow);
                Highlight("        " + first.Signed, ConsoleColor.DarkGreen);
            }
        }
    }

    public class FileInfo
    {
        public string FilePath { get; set; }
        public string Sha { get; set; }
        public string AssemblyName { get; set; }
        public string FullSigned { get; set; }
        public string Platform { get; set; }
        public string Architecture { get; set; }
        public string Signed { get; set; }

        public static FileInfo Get(string filePath)
        {
            var fileInfo = new FileInfo
            {
                FilePath = filePath,
                AssemblyName = GetAssemblyName(filePath),
                Sha = Utilities.SHA1Hash(filePath)
            };
            current = fileInfo;
            CheckSigned(filePath);
            CheckPlatform(filePath);
            return fileInfo;
        }
    }

    private static FileInfo current; 

    private static void PrintUsage()
    {
        Console.WriteLine(@"Usage: ListBinaryInfo.exe [<pattern>] [/nr]
        /nr: non-recursive (current directory only). Recursive by default.

  Examples: 
    ListBinaryInfo foo.dll
    ListBinaryInfo *.exe /nr
    ListBinaryInfo");
    }

    private static string GetAssemblyName(string file)
    {
        try
        {
            var name = AssemblyName.GetAssemblyName(file);
            return name.ToString();
        }
        catch
        {
            return "Native";
        }
    }

    private static void CheckPlatform(string file)
    {
        file = QuoteIfNecessary(file);
        var corflags = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            netfxTools,
            "corflags.exe");
        StartProcess(corflags, file);
    }

    private static void CheckSigned(string file)
    {
        file = QuoteIfNecessary(file);
        var sn = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            netfxTools,
            @"sn.exe");
        StartProcess(sn, "-vf " + file);
    }

    private static void StartProcess(string executableFilePath, string arguments)
    {
        if (!File.Exists(executableFilePath))
        {
            return;
        }

        executableFilePath = QuoteIfNecessary(executableFilePath);

        var psi = new ProcessStartInfo(executableFilePath, arguments);
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        var process = Process.Start(psi);
        process.OutputDataReceived += Process_DataReceived;
        process.ErrorDataReceived += Process_DataReceived;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
    }

    private static string QuoteIfNecessary(string filePath)
    {
        if (filePath.Contains(' '))
        {
            filePath = "\"" + filePath + "\"";
        }

        return filePath;
    }

    private static void Process_DataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e == null || string.IsNullOrEmpty(e.Data))
        {
            return;
        }

        string text = e.Data;

        if (text.Contains("32BITPREF"))
        {
            current.Platform = text;
            return;
        }

        if (text.Contains("delay") ||
            text.Contains("Copyright") ||
            text.Contains("(R)") ||
            text.Contains("Version ") ||
            text.Contains("CLR Header") ||
            text.Contains("PE  ") ||
            text.Contains("ILONLY  ") ||
            text.Contains("CorFlags") ||
            text.Contains("32BITPREF") ||
            text.Contains("does not represent") ||
            text.Contains("is verified with a key other than the identity key") ||
            text.Contains("Utility"))
        {
            return;
        }

        if (text.Contains("The specified file does not have a valid managed header"))
        {
            current.AssemblyName = "Native";
            return;
        }

        if (text.Contains("is valid"))
        {
            current.FullSigned = "Full-signed";
            return;
        }

        if (text.Contains("32BITREQ  : 1"))
        {
            current.Architecture = "x86";
            return;
        }

        if (text.Contains("32BITREQ  : 0"))
        {
            current.Architecture = "Any CPU";
            return;
        }

        if (text.Contains("Signed    : 1"))
        {
            current.Signed = "Signed";
            return;
        }

        if (text.Contains("Signed    : 0"))
        {
            current.Signed = "Unsigned";
            return;
        }

        Console.WriteLine(text);
    }

    private static void Highlight(string message, ConsoleColor color = ConsoleColor.Cyan)
    {
        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = oldColor;
    }
}