namespace LogAnalyzer.Models
{
    public class ParserProfile
    {
        public string Name { get; set; } = string.Empty;
        public string DateFormat { get; set; } = "dd.MM.yyyy HH:mm:ss.fff";
        public string Splitter { get; set; } = "|"; // can be multi-character
    }
}
