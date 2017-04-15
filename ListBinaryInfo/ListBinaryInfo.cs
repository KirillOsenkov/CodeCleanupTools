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

        if (args.Length > 0)
        {
            if (args.Length == 1)
            {
                if (args[0] == "/?" || args[0] == "-h" || args[0] == "-help" || args[0] == "help")
                {
                    PrintUsage();
                    return;
                }

                patternList = args[0];
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
                files.AddRange(Directory.GetFiles(root, pattern, SearchOption.AllDirectories));
            }
        }

        foreach (var file in files)
        {
            Console.WriteLine(file);
            WriteVersion(file);
            CheckSigned(file);
            CheckPlatform(file);
            Console.WriteLine();
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"Usage: ListBinaryInfo.exe [<pattern>]
  Examples: 
    ListBinaryInfo foo.dll
    ListBinaryInfo *.exe
    ListBinaryInfo");
    }

    private static void WriteVersion(string file)
    {
        try
        {
            var name = AssemblyName.GetAssemblyName(file);
            Highlight(name.ToString(), ConsoleColor.DarkGray);
        }
        catch
        {
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
        if (e == null ||
            string.IsNullOrEmpty(e.Data))
        {
            return;
        }

        string text = e.Data;
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
            Highlight("    Native", ConsoleColor.DarkRed);
            return;
        }

        if (text.Contains("is valid"))
        {
            Highlight("    Full-signed", ConsoleColor.White);
            return;
        }

        if (text.Contains("32BITREQ  : 1"))
        {
            Highlight("    x86", ConsoleColor.Green);
            return;
        }

        if (text.Contains("32BITREQ  : 0"))
        {
            Highlight("    AnyCPU");
            return;
        }

        if (text.Contains("Signed    : 1"))
        {
            Highlight("    Signed");
            return;
        }

        if (text.Contains("Signed    : 0"))
        {
            Highlight("    Unsigned", ConsoleColor.DarkCyan);
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