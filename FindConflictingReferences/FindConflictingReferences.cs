using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

public class Program
{
    public static void Main(string[] args)
    {
        new Program().DoWork();
    }

    private void DoWork()
    {
        var assemblies = GetAllAssemblies();
        var references = GetReferencesFromAllAssemblies(assemblies);

        var groupsOfConflicts = GroupReferences(references);

        foreach (var group in groupsOfConflicts)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(group.Key);
            Console.ResetColor();

            foreach (var subGroup in group)
            {
                Console.Write("  ");
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine(subGroup.Key);
                Console.ResetColor();

                foreach (var reference in subGroup)
                {
                    Console.Write("    ");
                    Console.WriteLine(reference.Assembly.Name);
                }
            }

            Console.WriteLine();
            Console.WriteLine();
        }
    }

    private IEnumerable<IGrouping<string, IGrouping<string, Reference>>> GroupReferences(IEnumerable<Reference> references)
    {
        var query = references.GroupBy(r => r.ReferencedAssembly.FullName);
        var result = query.GroupBy(q => GetShortName(q.Key)).Where(g => g.Count() > 1);

        return result;
    }

    private static string GetShortName(string fullName)
    {
        int comma = fullName.IndexOf(',');
        if (comma == -1)
        {
            return fullName;
        }

        return fullName.Substring(0, comma);
    }

    private List<Reference> GetReferencesFromAllAssemblies(IEnumerable<Assembly> assemblies)
    {
        var references = new List<Reference>();
        foreach (var assembly in assemblies)
        {
            foreach (var referencedAssembly in assembly.GetReferencedAssemblies())
            {
                references.Add(new Reference(assembly.GetName(), referencedAssembly));
            }
        }

        return references;
    }

    private IEnumerable<Assembly> GetAllAssemblies(string path = null)
    {
        if (string.IsNullOrEmpty(path))
        {
            path = Environment.CurrentDirectory;
        }

        var files = new List<FileInfo>();
        var directoryToSearch = new DirectoryInfo(path);
        files.AddRange(directoryToSearch.GetFiles("*.dll", SearchOption.AllDirectories));
        files.AddRange(directoryToSearch.GetFiles("*.exe", SearchOption.AllDirectories));
        return files.Select(file => Load(file)).Where(f => f != null);
    }

    private static Assembly Load(FileInfo file)
    {
        Assembly result = null;
        try
        {
            result = Assembly.LoadFile(file.FullName);
        }
        catch
        {
        }

        return result;
    }

    private class Reference
    {
        public Reference(AssemblyName assembly, AssemblyName referencedAssembly)
        {
            this.Assembly = assembly;
            this.ReferencedAssembly = referencedAssembly;
        }

        public AssemblyName Assembly { get; set; }
        public AssemblyName ReferencedAssembly { get; set; }
    }
}
