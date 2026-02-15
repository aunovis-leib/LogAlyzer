namespace LogAnalyzer.Services.Parsing
{
    using LogAnalyzer.Models;

    public interface ILogParser
    {
        bool TryParse(string line, out LogFileEntry entry);
    }
}
