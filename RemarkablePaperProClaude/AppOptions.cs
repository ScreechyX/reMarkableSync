namespace RemarkablePaperProClaude
{
    /// <summary>
    /// Runtime configuration for a single "ask Claude" run, populated from
    /// command-line arguments and environment variables.
    /// </summary>
    public class AppOptions
    {
        public string Host;
        public string Password;
        public string ApiKey;
        public string Model = "claude-opus-4-8";
        public string NotebookName = "Claude";
        public bool WriteBackToDevice = true;
        public string OutputDirectory = ".";

        /// <summary>Forced task name. When null, the task is auto-detected from a
        /// keyword written at the top of the page (default = "answer").</summary>
        public string Task;

        /// <summary>Target language for the "translate" task.</summary>
        public string Language = "English";
    }
}
