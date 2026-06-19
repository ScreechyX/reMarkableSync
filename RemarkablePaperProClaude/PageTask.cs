namespace RemarkablePaperProClaude
{
    /// <summary>
    /// A task Claude can perform on a handwritten page. A task is selected either
    /// by a keyword written at the top of the page (auto-detect mode) or forced
    /// with the --task option.
    /// </summary>
    public class PageTask
    {
        public string Name;
        public string[] Keywords;
        public string Description;   // short phrase shown in the keyword routing list
        public string Instruction;   // imperative instruction used when this task runs

        public PageTask(string name, string[] keywords, string description, string instruction)
        {
            Name = name;
            Keywords = keywords;
            Description = description;
            Instruction = instruction;
        }
    }
}
