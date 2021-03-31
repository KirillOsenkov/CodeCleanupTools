using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Time
{
    public class Time
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return 0;
            }

            int repeat = 1;

            var firstArg = args[0];
            if (firstArg.StartsWith("-") || firstArg.StartsWith("/"))
            {
                firstArg = firstArg.Substring(1);
                if (int.TryParse(firstArg, out repeat) && repeat > 0 && repeat < 1000000)
                {
                    args = args.Skip(1).ToArray();
                    if (args.Length == 0)
                    {
                        PrintHelp();
                        return 3;
                    }
                }
                else
                {
                    Console.Error.WriteLine("Unknown first argument: " + firstArg);
                    return 1;
                }
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
                        return 2;
                    }
                }
            }

            processFilePath = Path.GetFullPath(processFilePath);

            var results = new List<ProcessRunResult>();
            var totalDuration = TimeSpan.Zero;

            for (int i = 0; i < repeat; i++)
            {
                var result = RunProcess(processFilePath, arguments);
                results.Add(result);

                string output = ToDisplayString(result.Elapsed);
                if (repeat > 1)
                {
                    output = $"Iteration {i + 1}: {output}";
                }

                Log(output, ConsoleColor.Green);

                totalDuration += result.Elapsed;
                if (result.ExitCode != 0)
                {
                    Log($"Exit code: {result.ExitCode}", ConsoleColor.Yellow);
                }
            }

            if (repeat > 1)
            {
                var average = TimeSpan.FromMilliseconds(totalDuration.TotalMilliseconds / repeat);
                Log($"Average: {ToDisplayString(average)}", ConsoleColor.Cyan);
            }

            return 0;
        }

        private static void PrintHelp()
        {
            Console.WriteLine(@"Usage: timing [-10] <process> <arguments>
    Prints the duration of a process invocation.
    Optionally repeats the command -10 times (or any other number like -42).");
        }

        public static void Log(string text, ConsoleColor color = ConsoleColor.Gray)
        {
            var originalColor = Console.ForegroundColor;
            if (originalColor != color)
            {
                Console.ForegroundColor = color;
            }

            Console.WriteLine(text);

            if (originalColor != color)
            {
                Console.ForegroundColor = originalColor;
            }
        }

        public class ProcessRunResult
        {
            public TimeSpan Elapsed;
            public int ExitCode;
        }

        private static ProcessRunResult RunProcess(string processFilePath, string arguments)
        {
            var processStartInfo = new ProcessStartInfo(processFilePath, arguments);
            processStartInfo.UseShellExecute = false;
            processStartInfo.CreateNoWindow = false;
            processStartInfo.WorkingDirectory = Environment.CurrentDirectory;

            var stopwatch = Stopwatch.StartNew();

            var process = Process.Start(processStartInfo);
            process.WaitForExit();

            var result = new ProcessRunResult
            {
                Elapsed = stopwatch.Elapsed,
                ExitCode = process.ExitCode
            };

            return result;
        }

        public static string ToDisplayString(TimeSpan span, bool highPrecision = true)
        {
            if (span.TotalMilliseconds < 1)
            {
                return "";
            }

            if (span.Days > 0)
            {
                return $"{span.Days} days {span:h\\:mm\\:ss}";
            }

            if (span.TotalSeconds > 3600)
            {
                return span.ToString(@"h\:mm\:ss");
            }

            if (span.TotalSeconds > 60)
            {
                if (highPrecision && span.Milliseconds != 0)
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
