using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Time
{
    public class Time
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(@"Usage: time <process> <arguments>
    Prints the duration of a process invocation.");
            }

            var processFilePath = args[0];
            string arguments = "";
            if (args.Length > 1)
            {
                arguments = string.Join(" ", args.Skip(1).ToArray());
            }

            processFilePath = processFilePath.TrimStart('"').TrimEnd('"');

            processFilePath = Path.GetFullPath(processFilePath);
            if (!File.Exists(processFilePath))
            {
                Console.Error.WriteLine($"Application {processFilePath} doesn't exist");
                return;
            }

            var processStartInfo = new ProcessStartInfo(processFilePath, arguments);
            processStartInfo.UseShellExecute = false;
            processStartInfo.CreateNoWindow = false;
            processStartInfo.WorkingDirectory = Environment.CurrentDirectory;

            var stopwatch = Stopwatch.StartNew();

            var process = Process.Start(processStartInfo);
            process.WaitForExit();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(stopwatch.Elapsed.ToString("mm':'ss'.'fff"));
            Console.ForegroundColor = ConsoleColor.Gray;
        }
    }
}
