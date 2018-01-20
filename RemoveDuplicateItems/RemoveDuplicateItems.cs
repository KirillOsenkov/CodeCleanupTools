using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length > 1)
        {
            PrintHelp();
            return -1;
        }

        if (args.Any(a => a == "/?" || a == "-h" || a == "help"))
        {
            PrintHelp();
            return 0;
        }

        if (args.Length == 0 || args.Length == 1 && args[0] == "/r")
        {
            var searchOption = args.Length == 0 ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;

            var files = Directory.GetFiles(Environment.CurrentDirectory, "*.csproj", searchOption)
                .Concat(Directory.GetFiles(Environment.CurrentDirectory, "*.vbproj", searchOption));
            foreach (var file in files)
            {
                RemoveDuplicateItems(file);
            }
        }
        else
        {
            if (File.Exists(args[0]))
            {
                RemoveDuplicateItems(args[0]);
            }
            else
            {
                Console.WriteLine("File not found: " + args[0]);
                return -1;
            }
        }

        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"Usage: RemoveDuplicateItems.exe [<project file>|/r]
       Remove duplicate project items from an MSBuild project file alphabetically.
       If the project file is not specified sorts all files in the current
       directory.

RemoveDuplicateItems.exe /r
       Recursively processes all *.csproj && *.vbproj files in the current directory and all
       subdirectories.");
    }

    static void RemoveDuplicateItems(string filePath)
    {
        XDocument document = XDocument.Load(filePath, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        XNamespace msBuildNamespace = document.Root.GetDefaultNamespace();
        XName itemGroupName = XName.Get("ItemGroup", msBuildNamespace.NamespaceName);

        // only consider the top-level item groups, otherwise stuff inside Choose, Targets etc. will be broken
        var itemGroups = document.Root.Elements(itemGroupName).ToArray();

        foreach (XElement itemGroup in itemGroups)
        {
            ProcessItemGroup(itemGroup);
        }

        var originalBytes = File.ReadAllBytes(filePath);
        byte[] newBytes = null;

        using (var memoryStream = new MemoryStream())
        using (var textWriter = new StreamWriter(memoryStream, Encoding.UTF8))
        {
            document.Save(textWriter, SaveOptions.None);
            newBytes = memoryStream.ToArray();
        }

        newBytes = SyncBOM(originalBytes, newBytes);

        if (!AreEqual(originalBytes, newBytes))
        {
            File.WriteAllBytes(filePath, newBytes);
        }
    }

    private static byte[] SyncBOM(byte[] originalBytes, byte[] newBytes)
    {
        bool originalHasBOM = HasBOM(originalBytes);
        bool newHasBOM = HasBOM(newBytes);

        if (originalHasBOM && !newHasBOM)
        {
            var extended = new byte[newBytes.Length + 3];
            newBytes.CopyTo(extended, 3);
            BOM.CopyTo(extended, 0);
            newBytes = extended;
        }

        if (!originalHasBOM && newHasBOM)
        {
            var trimmed = new byte[newBytes.Length - 3];
            Array.Copy(newBytes, 3, trimmed, 0, trimmed.Length);
            newBytes = trimmed;
        }

        return newBytes;
    }

    private static byte[] BOM = { 0xEF, 0xBB, 0xBF };

    private static bool HasBOM(byte[] bytes)
    {
        if (bytes.Length >= 3 &&
            bytes[0] == BOM[0] &&
            bytes[1] == BOM[1] &&
            bytes[2] == BOM[2])
        {
            return true;
        }

        return false;
    }

    private static void ProcessItemGroup(XElement itemGroup)
    {
        var original = itemGroup.Elements().ToArray();
        var visited = new HashSet<string>();
        foreach (var item in original)
        {
            // if we've seen this node before, remove it
            if (!visited.Add(item.ToString(SaveOptions.DisableFormatting)))
            {
                if (item.PreviousNode is XText previousTrivia)
                {
                    previousTrivia.Remove();
                }

                item.Remove();
            }
        }
    }

    private static bool AreEqual(byte[] left, byte[] right)
    {
        if (left == null)
        {
            return right == null;
        }

        if (right == null)
        {
            return false;
        }

        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }
}
