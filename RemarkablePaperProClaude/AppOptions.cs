namespace RemarkablePaperProClaude
{
    /// <summary>
    /// Runtime configuration for a single "ask Claude" run, populated from
    /// command-line arguments and environment variables.
    /// </summary>
    public class AppOptions
    {
        public string ApiKey;
        public string Model = "claude-opus-4-8";

        /// <summary>rmapi path of the trigger notebook (e.g. "Claude" or "/Folder/Claude").</summary>
        public string NotebookName = "Claude";

        public string OutputDirectory = ".";

        /// <summary>Forced task name. When null, the task is auto-detected from a
        /// keyword written at the top of the page (default = "answer").</summary>
        public string Task;

        /// <summary>Target language for the "translate" task.</summary>
        public string Language = "English";

        // --- reMarkable connection via the external rmapi binary ---

        /// <summary>Path to the rmapi executable. Null = "rmapi" on PATH.</summary>
        public string RmapiPath;

        /// <summary>Cloud folder to upload the answer PDF into ("/" = top level).</summary>
        public string RmapiDest = "/";
    }
}
