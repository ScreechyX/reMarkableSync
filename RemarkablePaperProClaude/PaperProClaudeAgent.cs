using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RemarkableSync;
using RemarkableSync.document;

namespace RemarkablePaperProClaude
{
    /// <summary>
    /// Orchestrates one "ask Claude" cycle: pull the trigger notebook from the
    /// reMarkable cloud (linked with a one-time connect code, like the OneNote
    /// add-in), render its latest page, have Claude read and answer the
    /// handwriting, then save the answer as a PDF (and optionally push it back to
    /// the device over SSH).
    /// </summary>
    public class PaperProClaudeAgent
    {
        private readonly AppOptions _options;

        public PaperProClaudeAgent(AppOptions options)
        {
            _options = options;
        }

        public async Task RunOnceAsync(CancellationToken ct)
        {
            using (var configStore = new FileConfigStore(_options.ConfigPath))
            using (var dataSource = new RmCloudDataSource(configStore))
            {
                if (!string.IsNullOrWhiteSpace(_options.ConnectCode))
                {
                    Console.WriteLine("Linking to your reMarkable cloud account with the one-time code ...");
                    bool registered = await dataSource.RegisterWithOneTimeCode(_options.ConnectCode.Trim());
                    if (!registered)
                    {
                        throw new Exception(
                            "Could not register with that connect code. Codes expire quickly - get a fresh " +
                            "one from https://my.remarkable.com/device/desktop/connect and try again.");
                    }
                    Console.WriteLine(
                        $"Linked. The token is saved to {_options.ConfigPath}; you won't need a code next time.");
                }

                Console.WriteLine("Connecting to the reMarkable cloud ...");
                List<RmItem> hierarchy;
                try
                {
                    hierarchy = await dataSource.GetItemHierarchy(ct, new Progress<string>());
                }
                catch (Exception err)
                {
                    throw new Exception(
                        "Could not read from the reMarkable cloud. If this is the first run, link your account " +
                        "with --connect-code <code> (get one at https://my.remarkable.com/device/desktop/connect). " +
                        "Underlying error: " + err.Message);
                }

                RmItem notebook = FindNotebook(hierarchy, _options.NotebookName);
                if (notebook == null)
                {
                    throw new Exception(
                        $"Could not find a notebook named \"{_options.NotebookName}\" in your reMarkable cloud. " +
                        "Create one with that exact name (or pass --notebook). The cloud syncs on a delay, so " +
                        "make sure your latest page has finished syncing.");
                }

                Console.WriteLine($"Found notebook \"{notebook.VissibleName}\". Downloading ...");
                using (RmDocument doc = await dataSource.DownloadDocument(notebook.ID, ct, new Progress<string>()))
                {
                    if (doc.PageCount == 0)
                        throw new Exception("The notebook has no pages.");

                    int pageIndex = doc.PageCount - 1;
                    Console.WriteLine($"Rendering page {pageIndex + 1} of {doc.PageCount} ...");

                    byte[] png;
                    using (Bitmap bmp = doc.GetPageAsImage(pageIndex))
                        png = ToPng(bmp);

                    SaveArtifact(png, "question.png");

                    PageTask forcedTask = TaskLibrary.Find(_options.Task);
                    string systemPrompt = BuildSystemPrompt(forcedTask, _options.Language);
                    string userInstruction = forcedTask != null
                        ? "Here is a photo of my handwritten page."
                        : "Here is a photo of my handwritten page. The first line may be a command keyword.";
                    string taskLabel = forcedTask != null ? forcedTask.Name : "response";

                    Console.WriteLine(forcedTask != null
                        ? $"Task: {forcedTask.Name}"
                        : "Task: auto-detect from keyword (default: answer)");
                    Console.WriteLine($"Asking {_options.Model} to process the page ...");
                    string answer;
                    using (var claude = new ClaudeClient(_options.ApiKey, _options.Model))
                        answer = await claude.ProcessPageAsync(png, systemPrompt, userInstruction, ct);

                    Console.WriteLine();
                    Console.WriteLine("----- Claude's answer -----");
                    Console.WriteLine(answer);
                    Console.WriteLine("---------------------------");
                    Console.WriteLine();

                    byte[] pdf = PdfTextWriter.Create(BuildAnswerDocument(answer, taskLabel));
                    string pdfPath = Path.Combine(_options.OutputDirectory, "claude-answer.pdf");
                    File.WriteAllBytes(pdfPath, pdf);
                    File.WriteAllText(Path.Combine(_options.OutputDirectory, "claude-answer.txt"), answer);
                    Console.WriteLine($"Saved answer to {pdfPath}");

                    bool canPushOverSsh =
                        !string.IsNullOrWhiteSpace(_options.SshHost) &&
                        !string.IsNullOrWhiteSpace(_options.SshPassword);

                    if (canPushOverSsh)
                    {
                        Console.WriteLine("Pushing the answer onto the device over SSH ...");
                        using (var writer = new RmDeviceWriter(_options.SshHost, _options.SshPassword))
                        {
                            string docName = $"Claude {taskLabel} - {DateTime.Now:yyyy-MM-dd HH:mm}";
                            writer.UploadPdfDocument(pdf, docName, notebook.Parent);
                            writer.RestartUi();
                        }
                        Console.WriteLine("Done. The answer should appear on your reMarkable shortly.");
                    }
                    else
                    {
                        // The reMarkable cloud API is read-only in this tool, so we can't upload
                        // through the cloud. Leave the PDF locally for the user to import.
                        Console.WriteLine(
                            $"Answer saved to {pdfPath}. To get it onto the device, import that PDF via the " +
                            "reMarkable app, or re-run with --ssh-host/--ssh-password to push it over SSH.");
                    }
                }
            }
        }

        private static RmItem FindNotebook(List<RmItem> roots, string name)
        {
            var stack = new Stack<RmItem>(roots);
            while (stack.Count > 0)
            {
                RmItem item = stack.Pop();
                if (item.Type == RmItem.DocumentType &&
                    string.Equals(item.VissibleName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
                foreach (RmItem child in item.Children)
                    stack.Push(child);
            }
            return null;
        }

        private static byte[] ToPng(Bitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        private void SaveArtifact(byte[] data, string filename)
        {
            try
            {
                File.WriteAllBytes(Path.Combine(_options.OutputDirectory, filename), data);
            }
            catch
            {
                // Debug artifact only — never fail the run because we couldn't save it.
            }
        }

        private static string BuildAnswerDocument(string answer, string taskLabel)
        {
            var sb = new StringBuilder();
            sb.Append("Claude - ").Append(taskLabel).Append('\n');
            sb.Append(DateTime.Now.ToString("f"));
            sb.Append("\n\n");
            sb.Append(answer);
            return sb.ToString();
        }

        private const string BasePersona =
            "You are an assistant embedded in a reMarkable Paper Pro workflow. " +
            "The user writes on the tablet by hand and you receive a picture of one page. " +
            "Read the handwriting carefully. " +
            "If part of the handwriting is illegible, say which part you could not read rather than guessing. " +
            "Write plain prose suitable for an e-ink display: short paragraphs, no markdown, no tables.";

        /// <summary>
        /// Builds the system prompt. When a task is forced, Claude is told to run
        /// exactly that task; otherwise Claude is given the keyword routing table
        /// and reads the first line of the page to decide which task to run.
        /// </summary>
        private static string BuildSystemPrompt(PageTask forcedTask, string language)
        {
            if (forcedTask != null)
            {
                string instruction = forcedTask.Instruction.Replace(TaskLibrary.LanguagePlaceholder, language);
                return BasePersona + "\n\nYour task for this page: " + instruction;
            }

            var sb = new StringBuilder();
            sb.Append(BasePersona);
            sb.Append("\n\nThe first line of the page may be a command keyword that selects what to do ");
            sb.Append("with the rest of the page. Supported commands:\n");
            foreach (PageTask task in TaskLibrary.Tasks)
            {
                string description = task.Description.Replace(TaskLibrary.LanguagePlaceholder, language);
                sb.Append("- ").Append(string.Join("/", task.Keywords)).Append(": ").Append(description).Append('\n');
            }
            sb.Append("If the first line matches one of these commands (ignoring case and punctuation), ");
            sb.Append("perform that command on the rest of the page and do not treat the keyword itself as content. ");
            sb.Append("If the first line is not one of these commands, treat the whole page as a question and answer it.");
            return sb.ToString();
        }
    }
}
