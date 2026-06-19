using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RemarkablePaperProClaude
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                AppOptions options = ParseArgs(args);
                if (options == null)
                {
                    PrintUsage();
                    return 1;
                }

                Directory.CreateDirectory(options.OutputDirectory);

                var agent = new PaperProClaudeAgent(options);
                await agent.RunOnceAsync(CancellationToken.None);
                return 0;
            }
            catch (Exception err)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Error: " + err.Message);
                return 2;
            }
        }

        private static AppOptions ParseArgs(string[] args)
        {
            var o = new AppOptions();
            o.ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            o.Host = Environment.GetEnvironmentVariable("REMARKABLE_SSH_HOST");
            o.Password = Environment.GetEnvironmentVariable("REMARKABLE_SSH_PASSWORD");

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                switch (a)
                {
                    case "--host": o.Host = Next(args, ref i); break;
                    case "--password": o.Password = Next(args, ref i); break;
                    case "--api-key": o.ApiKey = Next(args, ref i); break;
                    case "--model": o.Model = Next(args, ref i); break;
                    case "--notebook": o.NotebookName = Next(args, ref i); break;
                    case "--out": o.OutputDirectory = Next(args, ref i); break;
                    case "--no-write-back": o.WriteBackToDevice = false; break;
                    case "-h":
                    case "--help":
                        return null;
                    default:
                        Console.Error.WriteLine($"Unknown argument: {a}");
                        return null;
                }
            }

            if (string.IsNullOrWhiteSpace(o.Host))
            {
                Console.Error.WriteLine("Missing --host (or set REMARKABLE_SSH_HOST).");
                return null;
            }
            if (string.IsNullOrWhiteSpace(o.Password))
            {
                Console.Error.WriteLine("Missing --password (or set REMARKABLE_SSH_PASSWORD).");
                return null;
            }
            if (string.IsNullOrWhiteSpace(o.ApiKey))
            {
                Console.Error.WriteLine("Missing --api-key (or set ANTHROPIC_API_KEY).");
                return null;
            }

            return o;
        }

        private static string Next(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
                throw new ArgumentException($"Missing value after {args[i]}");
            return args[++i];
        }

        private static void PrintUsage()
        {
            Console.WriteLine();
            Console.WriteLine("RemarkablePaperProClaude - ask Claude about a handwritten page on your reMarkable Paper Pro.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  RemarkablePaperProClaude --host <ip> --password <ssh-password> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --host <ip>            reMarkable IP address (e.g. 10.11.99.1 over USB).");
            Console.WriteLine("                         Falls back to REMARKABLE_SSH_HOST.");
            Console.WriteLine("  --password <pw>        Device SSH password (Settings > Help > About).");
            Console.WriteLine("                         Falls back to REMARKABLE_SSH_PASSWORD.");
            Console.WriteLine("  --api-key <key>        Anthropic API key. Falls back to ANTHROPIC_API_KEY.");
            Console.WriteLine("  --model <id>           Claude model id (default: claude-opus-4-8).");
            Console.WriteLine("  --notebook <name>      Trigger notebook name (default: \"Claude\").");
            Console.WriteLine("  --out <dir>            Where to save artifacts (default: current directory).");
            Console.WriteLine("  --no-write-back        Don't upload the answer back to the device.");
            Console.WriteLine("  -h, --help             Show this help.");
            Console.WriteLine();
        }
    }
}
