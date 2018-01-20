using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

internal class FindDuplicateFiles
{
    private static readonly Dictionary<string, HashSet<string>> filesByHash = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    private static void Main(string[] args)
    {
        var pattern = "*.*";
        if (args.Length == 1)
        {
            pattern = args[0];
        }

        var allFiles = Directory.GetFiles(Environment.CurrentDirectory, pattern, SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            var hash = Utilities.SHA1Hash(file);
            if (!filesByHash.TryGetValue(hash, out HashSet<string> bucket))
            {
                bucket = new HashSet<string>();
                filesByHash[hash] = bucket;
            }

            bucket.Add(file);
        }

        var sb = new StringBuilder();
        foreach (var currentBucket in filesByHash)
        {
            if (currentBucket.Value.Count > 1)
            {
                foreach (var dupe in currentBucket.Value)
                {
                    sb.AppendLine(dupe);
                }

                sb.AppendLine();
            }
        }

        Console.WriteLine(sb.ToString());
    }
}