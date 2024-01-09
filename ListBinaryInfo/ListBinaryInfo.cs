using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Cecil;

class ListBinaryInfo
{
    private static void PrintUsage()
    {
        Console.WriteLine(@"Usage:
lbi.exe [<pattern>]
        [-l[:<out.txt>]]
        [-d:<path>]*
        [-ed:<path>]*
        [-ef:<substring>]*
        [-nr]
        [-sn]
        [-p]
        [-v]
        [-fv]
        [-iv]
        [-tf]
        [@response.rsp]

    -l:     List full directory contents (optionally output to a file, e.g. out.txt)
            If not specified, files are grouped by hash, then version.
    -d:     Specify root directory to start in (defaults to current directory).
            Maybe be specified more than once to scan multiple directories.
    -ed:    Exclude directory from search. May be specified more than once.
    -ef:    Exclude files with substring. May be specified more than once.
    -nr:    Non-recursive (current directory only). Recursive by default.

    -sn     Print whether the assembly is signed.
    -p      Print assembly platform.
    -v      Print assembly version.
    -fv     Print assembly file version.
    -iv     Print assembly informational version.
    -tf     Print assembly target framework.

    @r:     Specify a response file (each file line treated as argument).

Examples: 
    lbi foo.dll
    lbi *.exe -nr
    lbi
    lbi -d:sub\directory -d:sub\dir2 -ed:sub\dir2\obj -l:out.txt");
    }

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

    private static string corflagsExe;
    private static string snExe;
    private static bool checkSn;
    private static bool checkPlatform;
    private static bool printVersion;
    private static bool printFileVersion;
    private static bool printInformationalVersion;
    private static bool printTargetFramework;

    private static bool ShouldReadModule => printFileVersion || printInformationalVersion || printTargetFramework;

    static void Main(string[] args)
    {
        FindCorflagsAndSn();

        List<string> roots = new();
        List<string> excludeDirectories = new();
        List<string> excludeFileSubstrings = new();

        string patternList = "*.dll;*.exe";
        bool recursive = true;
        bool directoryListing = false;
        string outputFile = null;

        var arguments = new HashSet<string>(args, StringComparer.OrdinalIgnoreCase);

        while (arguments.FirstOrDefault(a => a.StartsWith("@")) is string responseFile)
        {
            arguments.Remove(responseFile);
            responseFile = responseFile.Substring(1);
            if (File.Exists(responseFile))
            {
                var lines = File.ReadAllLines(responseFile);
                foreach (var line in lines)
                {
                    arguments.Add(line);
                }
            }
            else
            {
                Error("Response file doesn't exist: " + responseFile);
                return;
            }
        }

        var helpArgument = arguments.FirstOrDefault(a => a == "/?" || a == "-?" || a == "-h" || a == "/h" || a == "-help" || a == "/help");
        if (helpArgument != null)
        {
            PrintUsage();
            return;
        }

        var nonRecursiveArgument = arguments.FirstOrDefault(a => a == "/nr" || a == "-nr");
        if (nonRecursiveArgument != null)
        {
            arguments.Remove(nonRecursiveArgument);
            recursive = false;
        }

        var listArgument = arguments.FirstOrDefault(a => a.StartsWith("-l"));
        if (listArgument != null)
        {
            arguments.Remove(listArgument);
            directoryListing = true;
            patternList = "*";

            if (listArgument.StartsWith("-l:"))
            {
                string output = listArgument.Substring(3);
                output = output.Trim('"');
                outputFile = Path.GetFullPath(output);
            }
        }

        var signArgument = arguments.FirstOrDefault(a => a == "-sn");
        if (signArgument != null)
        {
            arguments.Remove(signArgument);
            checkSn = true;
        }

        var platformArgument = arguments.FirstOrDefault(a => a == "-p");
        if (platformArgument != null)
        {
            arguments.Remove(platformArgument);
            checkPlatform = true;
        }

        var versionArgument = arguments.FirstOrDefault(a => a == "-v");
        if (versionArgument != null)
        {
            arguments.Remove(versionArgument);
            printVersion = true;
        }

        var fileVersionArgument = arguments.FirstOrDefault(a => a == "-fv");
        if (fileVersionArgument != null)
        {
            arguments.Remove(fileVersionArgument);
            printFileVersion = true;
        }

        var informationalVersionArgument = arguments.FirstOrDefault(a => a == "-iv");
        if (informationalVersionArgument != null)
        {
            arguments.Remove(informationalVersionArgument);
            printInformationalVersion = true;
        }

        var targetFrameworkArgument = arguments.FirstOrDefault(a => a == "-tf");
        if (targetFrameworkArgument != null)
        {
            arguments.Remove(targetFrameworkArgument);
            printTargetFramework = true;
        }

        while (arguments.FirstOrDefault(a => a.StartsWith("-d:")) is string directoryArgument)
        {
            arguments.Remove(directoryArgument);
            string path = directoryArgument.Substring(3).Trim('"');
            path = Path.GetFullPath(path);
            if (Directory.Exists(path))
            {
                roots.Add(path);
            }
            else
            {
                Error($"Directory {path} doesn't exist");
                return;
            }
        }

        while (arguments.FirstOrDefault(a => a.StartsWith("-ed:")) is string directoryArgument)
        {
            arguments.Remove(directoryArgument);
            string path = directoryArgument.Substring(4).Trim('"');
            path = path.TrimEnd('\\');
            path = Path.GetFullPath(path);
            if (Directory.Exists(path))
            {
                excludeDirectories.Add(path);
            }
        }

        while (arguments.FirstOrDefault(a => a.StartsWith("-ef:")) is string excludeFileArgument)
        {
            arguments.Remove(excludeFileArgument);
            string substring = excludeFileArgument.Substring(4).Trim('"');
            excludeFileSubstrings.Add(substring);
        }

        if (arguments.Count > 0)
        {
            if (arguments.Count == 1)
            {
                string firstArgument = arguments.First().Trim('"');
                patternList = firstArgument;
            }
            else
            {
                PrintUsage();
                return;
            }
        }

        if (roots.Count == 0)
        {
            roots.Add(Environment.CurrentDirectory);
        }

        var files = new List<string>();
        if (File.Exists(patternList))
        {
            var file = Path.GetFullPath(patternList);
            files.Add(file);
            roots.Clear();
        }
        else
        {
            var patterns = patternList.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            Func<string, bool> exclude = null;
            if (excludeDirectories.Count > 0)
            {
                var hashset = new HashSet<string>(excludeDirectories, StringComparer.OrdinalIgnoreCase);
                exclude = hashset.Contains;
            }

            foreach (var root in roots)
            {
                AddFiles(
                    root,
                    patterns,
                    files,
                    recursive,
                    exclude,
                    excludeFileSubstrings);
            }
        }

        if (directoryListing)
        {
            for (int i = 0; i < roots.Count; i++)
            {
                var root = roots[i];
                if (!root.EndsWith("\\"))
                {
                    roots[i] = root + "\\";
                }
            }

            PrintFiles(roots, files, outputFile);
            return;
        }

        PrintGroupedFiles(files);
    }

    private static void AddFiles(
        string directory,
        string[] patterns,
        List<string> list,
        bool recursive,
        Func<string, bool> excludeDirectory,
        List<string> excludeFileSubstrings)
    {
        if (excludeDirectory != null && excludeDirectory(directory))
        {
            return;
        }

        if (recursive)
        {
            try
            {
                var directories = Directory.GetDirectories(directory);
                foreach (var subdirectory in directories)
                {
                    AddFiles(subdirectory,
                        patterns,
                        list,
                        recursive,
                        excludeDirectory,
                        excludeFileSubstrings);
                }
            }
            catch
            {
            }
        }

        foreach (var pattern in patterns)
        {
            try
            {
                var files = Directory.GetFiles(directory, pattern);
                foreach (var file in files)
                {
                    string name = Path.GetFileName(file);

                    if (ShouldExcludeFile(name, excludeFileSubstrings))
                    {
                        continue;
                    }

                    list.Add(file);
                }
            }
            catch
            {
            }
        }
    }

    private static bool ShouldExcludeFile(string name, List<string> excludeFileSubstrings)
    {
        foreach (var substring in excludeFileSubstrings)
        {
            if (name.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void PrintFiles(IList<string> rootDirectories, List<string> files, string outputFile)
    {
        var sb = new StringBuilder();

        foreach (var file in files)
        {
            string line = file;

            // make the path relative to the current root directory, if we have any root directories
            if (rootDirectories != null && rootDirectories.Count > 0)
            {
                string rootDirectory = rootDirectories[0];
                while (!line.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    rootDirectories.RemoveAt(0);

                    if (rootDirectories.Count > 0)
                    {
                        rootDirectory = rootDirectories[0];
                        sb.AppendLine();
                    }
                    else
                    {
                        rootDirectories = null;
                        rootDirectory = null;
                    }
                }

                if (rootDirectory != null)
                {
                    line = line.Substring(rootDirectory.Length);
                }
            }

            if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                bool isManagedAssembly = true;

                if (printVersion)
                {
                    var assemblyName = GetAssemblyName(file);
                    if (assemblyName != null)
                    {
                        if (assemblyName?.Version is Version version)
                        {
                            line += $", {version}";
                        }
                    }
                    else
                    {
                        isManagedAssembly = false;
                    }
                }

                if (isManagedAssembly)
                {
                    var fileInfo = FileInfo.Get(file, ShouldReadModule);

                    RunSnAndCorflags(fileInfo);

                    string signedText = fileInfo.SignedText;
                    if (!string.IsNullOrWhiteSpace(signedText))
                    {
                        line += $", {signedText}";
                    }

                    string platformText = fileInfo.PlatformText;
                    if (!string.IsNullOrWhiteSpace(platformText))
                    {
                        line += $", {platformText}";
                    }

                    if (printFileVersion && !string.IsNullOrWhiteSpace(fileInfo.FileVersion))
                    {
                        line += $", {fileInfo.FileVersion}";
                    }

                    if (printTargetFramework && !string.IsNullOrWhiteSpace(fileInfo.TargetFramework))
                    {
                        line += $", {fileInfo.TargetFramework}";
                    }

                    if (printInformationalVersion && !string.IsNullOrWhiteSpace(fileInfo.InformationalVersion))
                    {
                        line += $", {fileInfo.InformationalVersion}";
                    }
                }
            }

            sb.AppendLine(line);
        }

        string text = sb.ToString();

        if (!string.IsNullOrEmpty(outputFile))
        {
            File.WriteAllText(outputFile, text);
        }
        else
        {
            Console.Write(text);
        }
    }

    private static void PrintGroupedFiles(List<string> files)
    {
        foreach (var assemblyNameGroup in files.Select(f => FileInfo.Get(f, ShouldReadModule)).GroupBy(f => f.AssemblyName ?? NotAManagedAssembly).OrderBy(g => g.Key))
        {
            Highlight(assemblyNameGroup.Key, ConsoleColor.Cyan);
            foreach (var shaGroup in assemblyNameGroup.GroupBy(f => f.Sha))
            {
                var fileInfo = shaGroup.First();
                Highlight("    SHA1: " + shaGroup.Key, ConsoleColor.DarkGray, newLineAtEnd: false);

                Highlight(" " + shaGroup.First().FileSize.ToString("N0"), ConsoleColor.Gray, newLineAtEnd: false);

                if (fileInfo.AssemblyName != null)
                {
                    RunSnAndCorflags(fileInfo);

                    var signedText = fileInfo.SignedText;
                    if (!string.IsNullOrEmpty(signedText))
                    {
                        Highlight($" {signedText}", ConsoleColor.DarkGray, newLineAtEnd: false);
                    }

                    var platformText = fileInfo.PlatformText;
                    if (!string.IsNullOrEmpty(platformText))
                    {
                        Highlight(" " + platformText, ConsoleColor.DarkMagenta, newLineAtEnd: false);
                    }

                    if (printFileVersion && !string.IsNullOrWhiteSpace(fileInfo.FileVersion))
                    {
                        Highlight(" " + fileInfo.FileVersion, ConsoleColor.DarkYellow, newLineAtEnd: false);
                    }

                    if (printTargetFramework && !string.IsNullOrWhiteSpace(fileInfo.TargetFramework))
                    {
                        Highlight(" " + fileInfo.TargetFramework, ConsoleColor.Blue, newLineAtEnd: false);
                    }

                    if (printInformationalVersion && !string.IsNullOrWhiteSpace(fileInfo.InformationalVersion))
                    {
                        Highlight(" " + fileInfo.InformationalVersion, ConsoleColor.DarkGreen, newLineAtEnd: false);
                    }
                }

                Console.WriteLine();

                foreach (var file in shaGroup.OrderBy(f => f.FilePath))
                {
                    Highlight("        " + file.FilePath, ConsoleColor.White);
                }
            }
        }
    }

    public static void ReadModuleInfo(FileInfo fileInfo)
    {
        try
        {
            string filePath = fileInfo.FilePath;

            using (var module = ModuleDefinition.ReadModule(filePath))
            {
                var customAttributes = module.GetCustomAttributes().ToArray();

                var targetFrameworkAttribute = customAttributes.FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute");
                if (targetFrameworkAttribute != null)
                {
                    var value = targetFrameworkAttribute.ConstructorArguments[0].Value;
                    string targetFramework = ShortenTargetFramework(value.ToString());
                    fileInfo.TargetFramework = targetFramework;
                }

                var assemblyFileVersion = customAttributes.FirstOrDefault(a => a.AttributeType.FullName ==
                    "System.Reflection.AssemblyFileVersionAttribute");
                if (assemblyFileVersion != null)
                {
                    var value = assemblyFileVersion.ConstructorArguments[0].Value;
                    string fileVersion = value.ToString();
                    fileInfo.FileVersion = fileVersion;
                }

                var assemblyInformationalVersion = customAttributes.FirstOrDefault(a => a.AttributeType.FullName ==
                    "System.Reflection.AssemblyInformationalVersionAttribute");
                if (assemblyInformationalVersion != null)
                {
                    var value = assemblyInformationalVersion.ConstructorArguments[0].Value;
                    string informationalVersion = value.ToString();
                    fileInfo.InformationalVersion = informationalVersion;
                }
            }
        }
        catch
        {
        }
    }

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
        public string TargetFramework { get; set; }
        public string Architecture { get; set; }
        public string Signed { get; set; }
        public string FileVersion { get; set; }
        public string InformationalVersion { get; set; }
        public long FileSize { get; set; }

        public string SignedText
        {
            get
            {
                var signedText = FullSigned;
                if (Signed != "Signed" && Signed != null)
                {
                    signedText += "(" + Signed + ")";
                }

                return signedText;
            }
        }

        public string PlatformText
        {
            get
            {
                var platformText = Architecture;
                if (Platform != "32BITPREF : 0" && Platform != null)
                {
                    platformText += "(" + Platform + ")";
                }

                return platformText;
            }
        }

        public static FileInfo Get(string filePath, bool readModule = false)
        {
            var fileInfo = new FileInfo
            {
                FilePath = filePath,
                AssemblyName = GetAssemblyNameText(filePath),
                Sha = Utilities.SHA1Hash(filePath),
                FileSize = new System.IO.FileInfo(filePath).Length
            };

            if (readModule && fileInfo.AssemblyName != null)
            {
                ReadModuleInfo(fileInfo);
            }

            return fileInfo;
        }
    }

    private static AssemblyName GetAssemblyName(string file)
    {
        try
        {
            var name = AssemblyName.GetAssemblyName(file);
            return name;
        }
        catch
        {
            return null;
        }
    }

    private static string GetAssemblyNameText(string file)
    {
        var name = GetAssemblyName(file);
        return name?.ToString();
    }

    private static void RunSnAndCorflags(FileInfo fileInfo)
    {
        CheckPlatform(fileInfo);
        CheckSigned(fileInfo);
    }

    private static void CheckPlatform(FileInfo fileInfo)
    {
        if (!checkPlatform || corflagsExe == null)
        {
            return;
        }

        StartProcess(corflagsExe, "/nologo " + fileInfo.FilePath, fileInfo);
    }

    private static void CheckSigned(FileInfo fileInfo)
    {
        if (!checkSn || snExe == null)
        {
            return;
        }

        StartProcess(snExe, "-vf " + fileInfo.FilePath, fileInfo);
    }

    private static void StartProcess(string executableFilePath, string arguments, FileInfo fileInfo)
    {
        if (!File.Exists(executableFilePath))
        {
            return;
        }

        var processResult = ProcessRunner.Run(executableFilePath, arguments);
        var text = processResult.Output;
        var lines = text.GetLines();
        foreach (var line in lines)
        {
            ProcessLine(fileInfo, line);
        }
    }

    private static void ProcessLine(FileInfo fileInfo, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (text.Contains("32BITPREF"))
        {
            fileInfo.Platform = text;
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
            fileInfo.AssemblyName = "Native";
            return;
        }

        if (text.Contains("is valid"))
        {
            fileInfo.FullSigned = "Full-signed";
            return;
        }

        if (text.Contains("is a delay-signed or test-signed"))
        {
            fileInfo.FullSigned = "Delay-signed or test-signed";
            return;
        }

        if (text.Contains("32BITREQ  : 1"))
        {
            fileInfo.Architecture = "x86";
            return;
        }

        if (text.Contains("32BITREQ  : 0"))
        {
            fileInfo.Architecture = "Any CPU";
            return;
        }

        if (text.Contains("Signed    : 1"))
        {
            fileInfo.Signed = "Signed";
            return;
        }

        if (text.Contains("Signed    : 0"))
        {
            fileInfo.Signed = "Unsigned";
            return;
        }

        if (text.Contains("Failed to verify assembly -- Strong name validation failed."))
        {
            fileInfo.FullSigned = "Strong name validation failed";
            return;
        }

        Console.WriteLine(text);
    }

    private static void Error(string text)
    {
        Highlight(text, ConsoleColor.Red, writer: Console.Error);
    }

    private static void Highlight(
        string message,
        ConsoleColor color = ConsoleColor.Cyan,
        bool newLineAtEnd = true,
        TextWriter writer = null)
    {
        writer ??= Console.Out;

        lock (typeof(Console))
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            writer.Write(message);
            if (newLineAtEnd)
            {
                writer.WriteLine();
            }

            Console.ForegroundColor = oldColor;
        }
    }
}