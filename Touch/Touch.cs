using System;
using System.IO;

class Touch
{
    static void Main(string[] args)
    {
        var folder = Environment.CurrentDirectory;
        bool recursive = false;
        string pattern = null;

        foreach (var arg in args)
        {
            if (arg.ToLowerInvariant() == "/s" || arg.ToLowerInvariant() == "-s")
            {
                recursive = true;
                continue;
            }

            if (pattern == null)
            {
                pattern = arg;
            }
            else
            {
                Console.WriteLine("Usage: touch [/s] [<pattern>]");
                return;
            }
        }

        if (File.Exists(pattern))
        {
            TouchFile(pattern);
        }
        else
        {
            var files = Directory.GetFiles(folder, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                TouchFile(file);
            }
        }
    }

    private static void TouchFile(string file)
    {
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow);
    }
}