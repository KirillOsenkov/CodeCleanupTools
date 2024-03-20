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
                SortProjectItems(file);
            }
        }
        else
        {
            if (File.Exists(args[0]))
            {
                SortProjectItems(args[0]);
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
        Console.WriteLine(@"Usage: SortProjectItems.exe [<project file>|/r]
       Sorts the ItemGroup contents of an MSBuild project file alphabetically.
       If the project file is not specified sorts all files in the current
       directory.

SortProjectItems.exe /r
       Recursively sorts all *.csproj && *.vbproj files in the current directory and all
       subdirectories.");
    }

    static void SortProjectItems(string filePath)
    {
        XDocument document = XDocument.Load(filePath, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        XNamespace msBuildNamespace = document.Root.GetDefaultNamespace();
        XName itemGroupName = XName.Get("ItemGroup", msBuildNamespace.NamespaceName);

        // only consider the top-level item groups, otherwise stuff inside Choose, Targets etc. will be broken
        var itemGroups = document.Root.Elements(itemGroupName).ToArray();

        var processedItemGroups = new List<XElement>();

        CombineCompatibleItemGroups(itemGroups, processedItemGroups);

        foreach (XElement itemGroup in processedItemGroups)
        {
            SortItemGroup(itemGroup);
        }

        var originalBytes = File.ReadAllBytes(filePath);
        byte[] newBytes = null;

        using (var memoryStream = new MemoryStream())
        using (var textWriter = new StreamWriter(memoryStream, Encoding.UTF8))
        {
            document.Save(textWriter, SaveOptions.None);
            newBytes = memoryStream.ToArray();
        }

        if (!AreEqual(originalBytes, newBytes))
        {
            Console.WriteLine("Changed: " + filePath);
            File.WriteAllBytes(filePath, newBytes);
        }

        foreach (XElement itemGroup in processedItemGroups)
        {
            foreach (XElement item in itemGroup.Elements())
            {
                var include = item.Attribute(XName.Get("Include"))?.Value;
                if (include == null)
                {
                    continue;
                }

                var path = Path.Combine(Path.GetDirectoryName(filePath), include);
                var name = Path.GetFileName(path);

                if (string.Equals(".licx", Path.GetExtension(path), StringComparison.OrdinalIgnoreCase))
                {
                    SortLicenseFile(path);
                }
                else if (string.Equals("packages.config", name, StringComparison.OrdinalIgnoreCase))
                {
                    SortPackageConfigFile(path);
                }
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

    private static void CombineCompatibleItemGroups(XElement[] itemGroups, List<XElement> processedItemGroups)
    {
        var itemTypeLookup = itemGroups.ToDictionary(i => i, i => GetItemTypesFromItemGroup(i));
        foreach (var itemGroup in itemGroups)
        {
            if (!itemGroup.HasElements)
            {
                RemoveItemGroup(itemGroup);
                continue;
            }

            var suitableExistingItemGroup = FindSuitableItemGroup(processedItemGroups, itemGroup, itemTypeLookup);
            if (suitableExistingItemGroup != null)
            {
                ReplantAllItems(from: itemGroup, to: suitableExistingItemGroup);

                RemoveItemGroup(itemGroup);
            }
            else
            {
                processedItemGroups.Add(itemGroup);
            }
        }
    }

    private static void RemoveItemGroup(XElement itemGroup)
    {
        var leadingTrivia = itemGroup.PreviousNode;
        if (leadingTrivia is XText)
        {
            leadingTrivia.Remove();
        }

        itemGroup.Remove();
    }

    private static void ReplantAllItems(XElement from, XElement to)
    {
        if (to.LastNode is XText)
        {
            to.LastNode.Remove();
        }

        var fromNodes = from.Nodes().ToArray();
        from.RemoveNodes();
        foreach (var element in fromNodes)
        {
            to.Add(element);
        }
    }

    private static XElement FindSuitableItemGroup(
        List<XElement> existingItemGroups,
        XElement itemGroup,
        Dictionary<XElement, HashSet<string>> itemTypeLookup)
    {
        foreach (var existing in existingItemGroups)
        {
            var itemTypesInExisting = itemTypeLookup[existing];
            var itemTypesInCurrent = itemTypeLookup[itemGroup];
            if (itemTypesInCurrent.IsSubsetOf(itemTypesInExisting) && AreItemGroupsMergeable(itemGroup, existing))
            {
                return existing;
            }
        }

        return null;
    }

    private static bool AreItemGroupsMergeable(XElement left, XElement right)
    {
        if (!AttributeMissingOrSame(left, right, "Label"))
        {
            return false;
        }

        if (!AttributeMissingOrSame(left, right, "Condition"))
        {
            return false;
        }

        return true;
    }

    private static bool AttributeMissingOrSame(XElement left, XElement right, string attributeName)
    {
        var leftAttribute = left.Attribute(attributeName);
        var rightAttribute = right.Attribute(attributeName);
        if (leftAttribute == null && rightAttribute == null)
        {
            return true;
        }
        else if (leftAttribute != null && rightAttribute != null)
        {
            return leftAttribute.Value == rightAttribute.Value;
        }

        return false;
    }

    private static HashSet<string> GetItemTypesFromItemGroup(XElement itemGroup)
    {
        var set = new HashSet<string>();
        foreach (var item in itemGroup.Elements())
        {
            set.Add(item.Name.LocalName);
        }

        return set;
    }

    private static void SortItemGroup(XElement itemGroup)
    {
        var original = itemGroup.Elements().ToArray();
        var sorted = original
            .OrderBy(i => i.Name.LocalName)
            .ThenBy(i => (i.Attribute("Include") ?? i.Attribute("Remove") ?? i.Attribute("Update")).Value)
            .ToArray();

        for (int i = 0; i < original.Length; i++)
        {
            original[i].ReplaceWith(sorted[i]);
        }
    }

    private static void SortLicenseFile(string file)
    {
        var text = File.ReadAllText(file);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var split = text.Split(new[] { (char)13, (char)10 }, StringSplitOptions.RemoveEmptyEntries).ToList();
        split.Sort(StringComparer.OrdinalIgnoreCase);

        var newText = string.Join(Environment.NewLine, split);
        if (!string.Equals(text, newText))
        {
            Console.WriteLine("Changed: " + file);
            File.WriteAllText(file, newText);
        }
    }

    private static void SortPackageConfigFile(string file)
    {
        XDocument document = XDocument.Load(file, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        XNamespace msBuildNamespace = document.Root.GetDefaultNamespace();
        XName packageName = XName.Get("package", msBuildNamespace.NamespaceName);
        var packages = document.Root.Elements(packageName).ToArray();

        var original = packages.ToArray();
        var sorted = original
            .OrderBy(i => i.Attribute("id")?.Value)
            .ToArray();

        var changed = false;

        for (var i = 0; i < original.Length; i++)
        {
            if (!ReferenceEquals(original[i], sorted[i]))
            {
                original[i].ReplaceWith(sorted[i]);
                changed = true;
            }
        }

        if (changed)
        {
            Console.WriteLine("Changed: " + file);

            byte[] newBytes;
            using (var memoryStream = new MemoryStream())
            using (var textWriter = new StreamWriter(memoryStream, Encoding.UTF8))
            {
                document.Save(textWriter, SaveOptions.None);
                newBytes = memoryStream.ToArray();
            }

            File.WriteAllBytes(file, newBytes);
        }
    }
}