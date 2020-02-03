using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

#if ShowTargetFramework
using Mono.Cecil;
#endif

class ListBinaryInfo
{
    private static string[] netfxToolsPaths =
    {
        @"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools",
        @"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7.2 Tools",
        @"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7.1 Tools",
        @"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7 Tools",
        @"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.2 Tools",
        @"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools",
        @"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6 Tools",
        @"Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools",
        @"Microsoft SDKs\Windows\v7.0A\bin",
    };

    private static readonly string[] excludedLocations =
    {
        "Contents/Resources/lib/monodevelop/AddIns/designer/Xamarin.iOSDesigner/MonoTouchDesignServer.app",
        "Contents/Resources/lib/monodevelop/AddIns/designer/Xamarin.iOSDesigner/MonoTouchDesignServerTVOS.app",
        "Contents/Resources/lib/monodevelop/AddIns/designer/Xamarin.iOSDesigner/MonoTouchDesignServerUnified.app",
        "Contents/Resources/lib/monodevelop/AddIns/designer/Xamarin.iOSDesigner/XamarinFormsPrevieweriOS.app",
        "Contents/Resources/lib/monodevelop/AddIns/docker/MonoDevelop.Docker/MSBuild/Sdks/Microsoft.Docker.Sdk",
        "Contents/Resources/lib/monodevelop/AddIns/docker/MonoDevelop.Docker/mscorlib.dll",
        "Contents/Resources/lib/monodevelop/AddIns/DotNetCore.Debugger/Adapter",
        "Contents/Resources/lib/monodevelop/AddIns/MonoDevelop.AzureFunctions/azure-functions-cli",
        "Contents/Resources/lib/monodevelop/AddIns/MonoDevelop.UnitTesting/NUnit3/Mono.Cecil.dll",
        "Contents/Resources/lib/monodevelop/AddIns/MonoDevelop.UnitTesting/VsTestConsole",
        "Contents/Resources/lib/monodevelop/AddIns/MonoDevelop.Unity/Editor",
        "Contents/Resources/lib/monodevelop/AddIns/Xamarin.Ide.Identity",
        "Contents/Resources/lib/monodevelop/AddIns/Xamarin.Interactive.XS/Xamarin Inspector.app",
        "Contents/Resources/lib/monodevelop/bin/Microsoft.VisualStudio.Imaging.dll",
        "Contents/Resources/lib/monodevelop/bin/ServiceHub",
        "Contents/Resources/lib/monodevelop/AddIns/MonoDevelop.MonoDroid/Microsoft.CodeAnalysis.Workspaces.MSBuild.dll",
    };

    private static string corflagsExe;
    private static string snExe;

    static void Main(string[] args)
    {
        FindCorflagsAndSn();

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

        var filtered = files.Where(f => ShouldIncludeFile(f)).ToArray();

        var targetFrameworks = new Dictionary<string, List<string>>();

        foreach (var assemblyNameGroup in filtered.Select(f => FileInfo.Get(f)).GroupBy(f => f.AssemblyName).OrderBy(g => g.Key))
        {
            Highlight(assemblyNameGroup.Key, ConsoleColor.Cyan);
            foreach (var shaGroup in assemblyNameGroup.GroupBy(f => f.Sha))
            {
                if (shaGroup.Count() > 1)
                {
                }

                var first = shaGroup.First();
                Highlight("    SHA: " + shaGroup.Key, ConsoleColor.DarkGray, newLineAtEnd: false);

                Highlight(" " + shaGroup.First().FileSize.ToString("N0"), ConsoleColor.Gray, newLineAtEnd: false);

                if (first.AssemblyName != NotAManagedAssembly)
                {
                    current = first;
                    CheckSigned(first.FilePath);
                    CheckPlatform(first.FilePath);

                    var signedText = first.FullSigned;
                    if (first.Signed != "Signed" && first.Signed != null)
                    {
                        signedText += "(" + first.Signed + ")";
                    }

                    if (!string.IsNullOrEmpty(signedText))
                    {
                        Highlight($" {signedText}", ConsoleColor.DarkGray, newLineAtEnd: false);
                    }

                    var platformText = first.Architecture;
                    if (first.Platform != "32BITPREF : 0" && first.Platform != null)
                    {
                        platformText += "(" + first.Platform + ")";
                    }

                    if (!string.IsNullOrEmpty(platformText))
                    {
                        Highlight(" " + platformText, ConsoleColor.Gray, newLineAtEnd: false);
                    }

#if ShowTargetFramework
                    var targetFramework = GetTargetFramework(first.FilePath);
                    if (!string.IsNullOrEmpty(targetFramework))
                    {
                        Highlight(" " + targetFramework, ConsoleColor.Blue, newLineAtEnd: false);
                        AddTargetFramework(targetFramework, first.FilePath);
                    }

                    void AddTargetFramework(string tf, string filePath)
                    {
                        if (!targetFrameworks.TryGetValue(tf, out var bucket))
                        {
                            bucket = new List<string>();
                            targetFrameworks[tf] = bucket;
                        }

                        bucket.Add(filePath);
                    }
#endif
                }

                Console.WriteLine();

                foreach (var file in shaGroup.OrderBy(f => f.FilePath))
                {
                    Highlight("        " + file.FilePath, ConsoleColor.White);
                }
            }
        }

        foreach (var tf in targetFrameworks.OrderBy(s => s.Key))
        {
            Highlight(tf.Key, ConsoleColor.Yellow);
            foreach (var item in tf.Value.OrderBy(s => s))
            {
                Highlight("    " + item, ConsoleColor.Gray);
            }

            Console.WriteLine();
        }
    }

    private static bool ShouldIncludeFile(string filePath)
    {
        if (filePath.EndsWith(".resources.dll"))
        {
            return false;
        }

        foreach (var excludePattern in excludedLocations)
        {
            if (filePath.IndexOf(excludePattern.Replace('/', '\\')) != -1)
            {
                return false;
            }
        }

        return true;
    }

#if ShowTargetFramework
    private static string GetTargetFramework(string filePath)
    {
        try
        {
            using (var module = ModuleDefinition.ReadModule(filePath))
            {
                var targetFrameworkAttribute = module.GetCustomAttributes().FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute");
                if (targetFrameworkAttribute != null)
                {
                    var value = targetFrameworkAttribute.ConstructorArguments[0].Value;
                    return ShortenTargetFramework(value.ToString());
                }
            }
        }
        catch
        {
        }

        return null;
    }
#endif

    private static readonly Dictionary<string, string> targetFrameworkNames = new Dictionary<string, string>()
    {
        { ".NETFramework,Version=v", "net" },
        { ".NETCoreApp,Version=v", "netcoreapp" },
        { ".NETStandard,Version=v", "netstandard" }
    };

    private static string ShortenTargetFramework(string name)
    {
        foreach (var kvp in targetFrameworkNames)
        {
            if (name.StartsWith(kvp.Key))
            {
                var shortened = name.Substring(kvp.Key.Length);
                if (kvp.Value == "net")
                {
                    shortened = shortened.Replace(".", "");
                }

                return kvp.Value + shortened;
            }
        }

        return name;
    }

    private static void FindCorflagsAndSn()
    {
        foreach (var netfxToolsPath in netfxToolsPaths)
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                netfxToolsPath,
                "corflags.exe");
            if (corflagsExe == null && File.Exists(path))
            {
                corflagsExe = path;
            }

            path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                netfxToolsPath,
                @"sn.exe");
            if (snExe == null && File.Exists(path))
            {
                snExe = path;
            }

            if (corflagsExe != null && snExe != null)
            {
                break;
            }
        }
    }

    public const string NotAManagedAssembly = "Not a managed assembly";

    public class FileInfo
    {
        public string FilePath { get; set; }
        public string Sha { get; set; }
        public string AssemblyName { get; set; }
        public string FullSigned { get; set; }
        public string Platform { get; set; }
        public string Architecture { get; set; }
        public string Signed { get; set; }
        public long FileSize { get; set; }

        public static FileInfo Get(string filePath)
        {
            var fileInfo = new FileInfo
            {
                FilePath = filePath,
                AssemblyName = GetAssemblyName(filePath),
                Sha = Utilities.SHA1Hash(filePath),
                FileSize = new System.IO.FileInfo(filePath).Length
            };

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
            return NotAManagedAssembly;
        }
    }

    private static void CheckPlatform(string file)
    {
        if (!File.Exists(corflagsExe))
        {
            return;
        }

        file = QuoteIfNecessary(file);
        StartProcess(corflagsExe, "/nologo " + file);
    }

    private static void CheckSigned(string file)
    {
        if (!File.Exists(snExe))
        {
            return;
        }

        file = QuoteIfNecessary(file);
        StartProcess(snExe, "-vf " + file);
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

        if (text.Contains("Copyright") ||
            text.Contains("(R)") ||
            text.Contains("Version ") ||
            text.Contains("CLR Header") ||
            text.Contains("PE  ") ||
            text.Contains("ILONLY  ") ||
            text.Contains("CorFlags") ||
            text.Contains("does not represent") ||
            text.Contains("is verified with a key other than the identity key"))
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

        if (text.Contains("is a delay-signed or test-signed"))
        {
            current.FullSigned = "Delay-signed or test-signed";
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

        if (text.Contains("Failed to verify assembly -- Strong name validation failed."))
        {
            current.FullSigned = "Strong name validation failed";
            return;
        }

        Console.WriteLine(text);
    }

    private static void Highlight(string message, ConsoleColor color = ConsoleColor.Cyan, bool newLineAtEnd = true)
    {
        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(message);
        if (newLineAtEnd)
        {
            Console.WriteLine();
        }

        Console.ForegroundColor = oldColor;
    }
}