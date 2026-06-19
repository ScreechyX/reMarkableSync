using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RemarkablePaperProClaude
{
    /// <summary>
    /// The set of tasks Claude can run on a page, plus keyword lookup. The
    /// <see cref="LanguagePlaceholder"/> token in an instruction/description is
    /// replaced with the configured target language at prompt-build time.
    /// </summary>
    public static class TaskLibrary
    {
        public const string LanguagePlaceholder = "{language}";

        public static readonly PageTask Default;
        public static readonly List<PageTask> Tasks;

        static TaskLibrary()
        {
            var answer = new PageTask(
                "answer",
                new[] { "answer", "question", "ask", "q" },
                "answer the question(s) on the page",
                "Read the question(s) on the page and answer them directly and concisely, in order.");

            var summarize = new PageTask(
                "summarize",
                new[] { "summarize", "summarise", "summary", "tldr" },
                "summarize the page",
                "Summarize the content of the page in a few concise sentences, capturing the key points.");

            var cleanup = new PageTask(
                "cleanup",
                new[] { "cleanup", "clean", "tidy", "transcribe", "rewrite" },
                "transcribe and tidy the notes",
                "Transcribe the handwritten notes and rewrite them as clean, well-structured prose, preserving the original meaning.");

            var expand = new PageTask(
                "expand",
                new[] { "expand", "outline", "flesh" },
                "expand the outline into prose",
                "Treat the page as an outline or rough notes and expand it into clear, complete prose.");

            var translate = new PageTask(
                "translate",
                new[] { "translate", "translation" },
                "translate the page into " + LanguagePlaceholder,
                "Translate the text on the page into " + LanguagePlaceholder + ". " +
                "If it is already in " + LanguagePlaceholder + ", translate it into the language it was most likely meant to be in and say which language you chose.");

            var explain = new PageTask(
                "explain",
                new[] { "explain", "eli5" },
                "explain the concept on the page",
                "Explain the concept, term, or problem written on the page clearly and simply.");

            var todo = new PageTask(
                "todo",
                new[] { "todo", "tasks", "actions", "actionitems" },
                "extract action items",
                "Extract the concrete action items / to-dos from the page and present them as a simple numbered list.");

            Default = answer;
            Tasks = new List<PageTask> { answer, summarize, cleanup, expand, translate, explain, todo };
        }

        /// <summary>
        /// Finds a task by its name or any of its keywords (case- and
        /// punctuation-insensitive). Returns null if nothing matches.
        /// </summary>
        public static PageTask Find(string nameOrKeyword)
        {
            if (string.IsNullOrWhiteSpace(nameOrKeyword))
                return null;

            string key = Normalize(nameOrKeyword);
            foreach (PageTask task in Tasks)
            {
                if (Normalize(task.Name) == key)
                    return task;
                foreach (string kw in task.Keywords)
                    if (Normalize(kw) == key)
                        return task;
            }
            return null;
        }

        public static string Normalize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s.Trim().ToLowerInvariant())
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
            return sb.ToString();
        }

        public static string NamesList()
        {
            return string.Join(", ", Tasks.Select(t => t.Name));
        }
    }
}
