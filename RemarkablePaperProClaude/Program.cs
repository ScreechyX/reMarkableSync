using System;
using System.IO;
using System.Linq;
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
                if (args.Any(a => a == "-h" || a == "--help"))
                {
                    PrintUsage();
                    return 0;
                }
                if (args.Any(a => a == "--list-tasks"))
                {
                    PrintTasks();
                    return 0;
                }

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

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                switch (a)
                {
                    case "--api-key": o.ApiKey = Next(args, ref i); break;
                    case "--model": o.Model = Next(args, ref i); break;
                    case "--notebook": o.NotebookName = Next(args, ref i); break;
                    case "--task": o.Task = Next(args, ref i); break;
                    case "--language": o.Language = Next(args, ref i); break;
                    case "--out": o.OutputDirectory = Next(args, ref i); break;
                    case "--rmapi": o.RmapiPath = Next(args, ref i); break;
                    case "--rmapi-dest": o.RmapiDest = Next(args, ref i); break;
                    case "-h":
                    case "--help":
                        return null;
                    default:
                        Console.Error.WriteLine($"Unknown argument: {a}");
                        return null;
                }
            }

            if (string.IsNullOrWhiteSpace(o.ApiKey))
            {
                Console.Error.WriteLine("Missing --api-key (or set ANTHROPIC_API_KEY).");
                return null;
            }
            if (o.Task != null && TaskLibrary.Find(o.Task) == null)
            {
                Console.Error.WriteLine($"Unknown task \"{o.Task}\". Valid tasks: {TaskLibrary.NamesList()}");
                Console.Error.WriteLine("Run with --list-tasks to see what each one does.");
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
            Console.WriteLine("RemarkablePaperProClaude - ask Claude about a handwritten page on your reMarkable.");
            Console.WriteLine();
            Console.WriteLine("Connects to your reMarkable cloud through the rmapi tool");
            Console.WriteLine("(https://github.com/ddvk/rmapi), which handles both download and upload. Link");
            Console.WriteLine("rmapi to your account once (run 'rmapi' and enter a one-time code), then:");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  RemarkablePaperProClaude --api-key <anthropic-key> [options]");
            Console.WriteLine();
            Console.WriteLine("Get a connect code (for rmapi) at https://my.remarkable.com/device/desktop/connect");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --api-key <key>        Anthropic API key. Falls back to ANTHROPIC_API_KEY.");
            Console.WriteLine("  --model <id>           Claude model id (default: claude-opus-4-8).");
            Console.WriteLine("  --notebook <path>      rmapi path of the trigger notebook (default: \"Claude\").");
            Console.WriteLine("                         Use a full path like \"/Folder/Claude\" if it's nested.");
            Console.WriteLine("  --task <name>          Force a task instead of detecting a keyword.");
            Console.WriteLine($"                         One of: {TaskLibrary.NamesList()}.");
            Console.WriteLine("  --language <lang>      Target language for the translate task (default: English).");
            Console.WriteLine("  --out <dir>            Where to save artifacts (default: current directory).");
            Console.WriteLine("  --rmapi <path>         Path to the rmapi executable (default: \"rmapi\" on PATH).");
            Console.WriteLine("  --rmapi-dest <dir>     Cloud folder to upload the answer into (default: \"/\").");
            Console.WriteLine("  --list-tasks           Show the available tasks and their keywords.");
            Console.WriteLine("  -h, --help             Show this help.");
            Console.WriteLine();
            Console.WriteLine("By default the task is chosen from a keyword written on the first line of the");
            Console.WriteLine("page (e.g. \"summarize\", \"translate\"); with no recognised keyword it answers");
            Console.WriteLine("the page as a question. The answer is uploaded back to your reMarkable as a PDF.");
            Console.WriteLine();
        }

        private static void PrintTasks()
        {
            Console.WriteLine();
            Console.WriteLine("Available tasks (write the keyword on the first line of the page, or use --task):");
            Console.WriteLine();
            foreach (PageTask task in TaskLibrary.Tasks)
            {
                string defaultMark = task == TaskLibrary.Default ? "  (default)" : string.Empty;
                Console.WriteLine($"  {task.Name}{defaultMark}");
                Console.WriteLine($"      keywords: {string.Join(", ", task.Keywords)}");
                Console.WriteLine($"      {task.Description}");
                Console.WriteLine();
            }
        }
    }
}
