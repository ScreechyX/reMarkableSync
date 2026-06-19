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
    }
}
