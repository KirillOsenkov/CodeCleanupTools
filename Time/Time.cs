using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

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
                return;
            }

            var processFilePath = args[0];
            string arguments = "";
            if (args.Length > 1)
            {
                arguments = string.Join(" ", args.Skip(1).ToArray());
            }

            processFilePath = processFilePath.TrimStart('"').TrimEnd('"');

            if (!File.Exists(processFilePath))
            {
                var missingExtension = GuessMissingExtension(processFilePath);
                if (missingExtension != null)
                {
                    processFilePath += missingExtension;
                }
                else
                {
                    var resolved = ResolveFromPath(processFilePath);
                    if (resolved != null)
                    {
                        processFilePath = resolved;
                    }
                    else
                    {
                        Console.Error.WriteLine($"Application {processFilePath} doesn't exist");
                        return;
                    }
                }
            }

            processFilePath = Path.GetFullPath(processFilePath);

            var processStartInfo = new ProcessStartInfo(processFilePath, arguments);
            processStartInfo.UseShellExecute = false;
            processStartInfo.CreateNoWindow = false;
            processStartInfo.WorkingDirectory = Environment.CurrentDirectory;

            var stopwatch = Stopwatch.StartNew();

            var process = Process.Start(processStartInfo);
            process.WaitForExit();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(ToDisplayString(stopwatch.Elapsed));
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static string ToDisplayString(TimeSpan span, bool highPrecision = true)
        {
            if (span.TotalMilliseconds < 1)
            {
                return "";
            }

            string prefix = "";

            if (span.TotalDays > 0)
            {
                prefix = $"{span.TotalDays} days ";
            }

            if (span.TotalSeconds > 3600)
            {
                return prefix + span.ToString(@"h\:mm\:ss");
            }

            if (span.TotalSeconds > 60)
            {
                if (highPrecision)
                {
                    return span.ToString(@"m\:ss\.fff");
                }
                else
                {
                    return span.ToString(@"m\:ss");
                }
            }

            if (span.TotalMilliseconds > 1000)
            {
                if (highPrecision)
                {
                    return span.ToString(@"s\.fff") + " s";
                }
                else
                {
                    return span.Seconds + " s";
                }
            }

            return span.Milliseconds + " ms";
        }

        private static string ResolveFromPath(string processFilePath)
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var parts = path.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var candidate = Path.Combine(part, processFilePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                var missingExtension = GuessMissingExtension(candidate);
                if (missingExtension != null)
                {
                    return candidate + missingExtension;
                }
            }

            return null;
        }

        private static readonly string[] executableExtensions = { ".exe", ".cmd", ".bat" };

        private static string GuessMissingExtension(string filePath)
        {
            if (executableExtensions.Any(e => filePath.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var extension = executableExtensions.FirstOrDefault(e => File.Exists(filePath + e));
            if (extension != null)
            {
                return extension;
            }

            return null;
        }
    }
}
