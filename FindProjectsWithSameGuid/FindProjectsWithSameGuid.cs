using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            PrintHelp();
            return 0;
        }

        var projects = Directory.GetFiles(Environment.CurrentDirectory, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Environment.CurrentDirectory, "*.vbproj", SearchOption.AllDirectories));
        var projectsByGuid = new Dictionary<Guid, List<string>>();
        foreach (var projectFile in projects)
        {
            var guid = GetGuid(projectFile);
            if (guid != Guid.Empty)
            {
                List<string> bucket = null;
                if (!projectsByGuid.TryGetValue(guid, out bucket))
                {
                    bucket = new List<string>();
                    projectsByGuid.Add(guid, bucket);
                }

                bucket.Add(projectFile);
            }
        }

        foreach (var bucket in projectsByGuid.Where(kvp => kvp.Value.Count > 1))
        {
            Console.WriteLine("Projects with Guid {0}:", bucket.Key);
            foreach (var project in bucket.Value)
            {
                Console.WriteLine("  " + project);
            }
        }

        return 0;
    }

    static Guid GetGuid(string projectFilePath)
    {
        try
        {
            Guid result;
            XDocument document = XDocument.Load(projectFilePath, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            XNamespace msBuildNamespace = document.Root.GetDefaultNamespace();
            XName projectGuidName = XName.Get("ProjectGuid", msBuildNamespace.NamespaceName);
            var projectGuidElement = document.Root.Descendants(projectGuidName).FirstOrDefault();
            var value = projectGuidElement.Value;
            Guid.TryParse(value, out result);
            return result;
        }
        catch (Exception)
        {
            return Guid.Empty;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"Usage: FindProjectsWithSameGuid.exe
       Finds *.proj files that have the same GUID (usually when projects are
       copy-pasted. GUID conflicts aren't good for the VS project system.");
    }
}
