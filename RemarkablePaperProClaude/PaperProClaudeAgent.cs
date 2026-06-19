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
    /// device, render its latest page, have Claude read and answer the handwriting,
    /// then write the answer back to the device as a PDF.
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
            Console.WriteLine($"Connecting to reMarkable at {_options.Host} ...");
            using (var dataSource = new RmSftpDataSource(_options.Host, _options.Password))
            {
                List<RmItem> hierarchy = await dataSource.GetItemHierarchy(ct, new Progress<string>());
                RmItem notebook = FindNotebook(hierarchy, _options.NotebookName);
                if (notebook == null)
                {
                    throw new Exception(
                        $"Could not find a notebook named \"{_options.NotebookName}\" on the device. " +
                        "Create one with that exact name (or pass --notebook).");
                }

                Console.WriteLine($"Found notebook \"{notebook.VissibleName}\" ({notebook.ID}). Downloading ...");
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

                    Console.WriteLine($"Asking {_options.Model} to read and answer the page ...");
                    string answer;
                    using (var claude = new ClaudeClient(_options.ApiKey, _options.Model))
                        answer = await claude.AnswerHandwrittenPageAsync(png, SystemPrompt, UserInstruction, ct);

                    Console.WriteLine();
                    Console.WriteLine("----- Claude's answer -----");
                    Console.WriteLine(answer);
                    Console.WriteLine("---------------------------");
                    Console.WriteLine();

                    byte[] pdf = PdfTextWriter.Create(BuildAnswerDocument(answer));
                    string pdfPath = Path.Combine(_options.OutputDirectory, "claude-answer.pdf");
                    File.WriteAllBytes(pdfPath, pdf);
                    File.WriteAllText(Path.Combine(_options.OutputDirectory, "claude-answer.txt"), answer);
                    Console.WriteLine($"Saved answer to {pdfPath}");

                    if (_options.WriteBackToDevice)
                    {
                        Console.WriteLine("Writing answer back to the device ...");
                        using (var writer = new RmDeviceWriter(_options.Host, _options.Password))
                        {
                            string docName = $"Claude answer - {DateTime.Now:yyyy-MM-dd HH:mm}";
                            writer.UploadPdfDocument(pdf, docName, notebook.Parent);
                            writer.RestartUi();
                        }
                        Console.WriteLine("Done. The answer should appear on your reMarkable shortly.");
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

        private static string BuildAnswerDocument(string answer)
        {
            var sb = new StringBuilder();
            sb.Append("Claude's answer\n");
            sb.Append(DateTime.Now.ToString("f"));
            sb.Append("\n\n");
            sb.Append(answer);
            return sb.ToString();
        }

        private const string SystemPrompt =
            "You are an assistant embedded in a reMarkable Paper Pro workflow. " +
            "The user writes a question by hand on the tablet and you receive a picture of that page. " +
            "Read the handwriting carefully and answer the question(s) directly and concisely. " +
            "If the page contains several questions, answer each in order. " +
            "If part of the handwriting is illegible, say which part you could not read rather than guessing. " +
            "Write plain prose suitable for an e-ink display: short paragraphs, no markdown, no tables.";

        private const string UserInstruction =
            "Here is a photo of my handwritten page. Please read it and answer.";
    }
}
