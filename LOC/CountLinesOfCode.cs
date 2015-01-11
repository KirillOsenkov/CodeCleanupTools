using System;
using System.IO;
using System.Linq;

class CountLinesOfCode
{
    static void Main(string[] args)
    {
        string pattern = "*.cs";
        if (args.Length != 0)
        {
            pattern = args[0];
        }

        Console.WriteLine(
            Directory.GetFiles(
                Environment.CurrentDirectory,
                pattern,
                SearchOption.AllDirectories)
            .Select(f => File.ReadAllLines(f).Length)
            .Sum());
    }
}