namespace LogAlyzer.Services.Parsing
{
    using LogAlyzer.Models;

    public interface ILogParser
    {
        bool TryParse(string line, out LogFileEntry entry);
    }
}
