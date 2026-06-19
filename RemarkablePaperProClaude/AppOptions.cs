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
        public string NotebookName = "Claude";
        public string OutputDirectory = ".";

        /// <summary>Forced task name. When null, the task is auto-detected from a
        /// keyword written at the top of the page (default = "answer").</summary>
        public string Task;

        /// <summary>Target language for the "translate" task.</summary>
        public string Language = "English";

        // --- reMarkable cloud connection (connect-code flow, like the OneNote add-in) ---

        /// <summary>One-time connect code to link this tool to your reMarkable cloud
        /// account. Only needed once; afterwards the saved token is reused.</summary>
        public string ConnectCode;

        /// <summary>Where the saved cloud device token lives.</summary>
        public string ConfigPath;

        // --- Optional: push the answer back onto the device over SSH ---
        // (the reMarkable cloud API is read-only in this tool, so device write-back
        //  needs SSH; without it the answer is just saved locally as a PDF.)

        public string SshHost;
        public string SshPassword;
    }
}
