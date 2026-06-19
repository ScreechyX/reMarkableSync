using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RemarkableSync;

namespace RemarkablePaperProClaude
{
    /// <summary>
    /// Orchestrates one "ask Claude" cycle, using the external rmapi binary for both
    /// directions of the reMarkable cloud: download the trigger notebook, render its
    /// latest page, have Claude read and answer the handwriting, then upload the
    /// answer back as a PDF (which syncs to the device).
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
            var rmapi = new RmapiClient(_options.RmapiPath);
            Console.WriteLine("Checking rmapi ...");
            rmapi.EnsureReady();

            string workDir = Path.Combine(Path.GetTempPath(), "rmpp-claude-" + Guid.NewGuid().ToString("N"));
            try
            {
                Console.WriteLine($"Downloading notebook \"{_options.NotebookName}\" via rmapi ...");
                string contentFolder = rmapi.DownloadToFolder(_options.NotebookName, workDir);

                string contentFile = Directory.GetFiles(contentFolder, "*.content").FirstOrDefault();
                if (contentFile == null)
                    throw new Exception("Downloaded notebook is missing its .content file.");
                string uuid = Path.GetFileNameWithoutExtension(contentFile);

                using (var localSource = new LocalFolderDataSource(contentFolder))
                using (RmDocument doc = await localSource.DownloadDocument(uuid, ct, new Progress<string>()))
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
                    string pdfName = $"Claude {taskLabel} - {DateTime.Now:yyyy-MM-dd HH-mm}.pdf";
                    string pdfPath = Path.Combine(_options.OutputDirectory, pdfName);
                    File.WriteAllBytes(pdfPath, pdf);
                    File.WriteAllText(Path.Combine(_options.OutputDirectory, "claude-answer.txt"), answer);
                    Console.WriteLine($"Saved answer to {pdfPath}");

                    Console.WriteLine($"Uploading answer to \"{_options.RmapiDest}\" via rmapi ...");
                    rmapi.Upload(pdfPath, _options.RmapiDest);
                    Console.WriteLine("Done. The answer was uploaded and will sync to your reMarkable shortly.");
                }
            }
            finally
            {
                TryDeleteDirectory(workDir);
            }
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

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
                // Temp folder cleanup is best-effort.
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
