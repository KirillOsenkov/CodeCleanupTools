using System;
using System.Collections.Generic;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        var folder = Environment.CurrentDirectory;
        var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
        HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            set.Add(extension);
        }

        foreach (var extension in set)
        {
            Console.WriteLine(extension);
        }
    }
}