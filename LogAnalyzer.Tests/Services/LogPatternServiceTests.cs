using System;
using System.IO;
using System.Threading.Tasks;
using LogAnalyzer.Models;
using LogAnalyzer.Services;
using Xunit;

namespace LogAnalyzer.Tests.Services
{
    public class LogPatternServiceTests
    {
        private readonly string _testPatternDir;

        public LogPatternServiceTests()
        {
            _testPatternDir = Path.Combine(Path.GetTempPath(), $"LogPatterns_{Guid.NewGuid()}");
        }

        [Fact]
        public async Task SavePatternAsync_CreatesValidYamlFile()
        {
            // Arrange
            var service = new LogPatternService(_testPatternDir);
            var pattern = new LogPattern
            {
                Id = "test_pattern",
                Name = "Test Pattern",
                Description = "A test pattern",
                RegexPattern = @"(?<timestamp>\d{4}-\d{2}-\d{2}) (?<message>.*)",
                Severity = "error",
                Priority = 50
            };
            pattern.Tags.Add("test");
            pattern.Fields.Add("timestamp");
            pattern.Fields.Add("message");

            // Act
            await service.SavePatternAsync(pattern);

            // Assert
            var filePath = Path.Combine(_testPatternDir, "test_pattern.yaml");
            Assert.True(File.Exists(filePath), "YAML file should be created");

            var content = await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken);
            Assert.Contains("Id: test_pattern", content);
            Assert.Contains("Name: Test Pattern", content);
            Assert.Contains("Severity: error", content);
        }

        [Fact]
        public async Task MatchLine_ReturnsMatchesForValidPattern()
        {
            // Arrange
            var service = new LogPatternService(_testPatternDir);
            var pattern = new LogPattern
            {
                Id = "exception_pattern",
                Name = "Exception Pattern",
                RegexPattern = @"(?<timestamp>\d{2}:\d{2}:\d{2}) .* (?<exception>\w+Exception): (?<message>.*)",
                Severity = "error",
                Priority = 100
            };
            pattern.Fields.Add("timestamp");
            pattern.Fields.Add("exception");
            pattern.Fields.Add("message");

            await service.SavePatternAsync(pattern);

            var logEntry = new LogFileEntry
            {
                Date = DateTime.Now,
                Type = LogType.Error,
                Text = "14:35:22 [ERROR] NullReferenceException: Object reference not set to an instance of an object."
            };

            // Act
            var matches = service.MatchLine(logEntry);

            // Assert
            Assert.Single(matches);
            Assert.Equal("exception_pattern", matches[0].Pattern.Id);
            Assert.Contains("timestamp", matches[0].ExtractedFields);
            Assert.Equal("NullReferenceException", matches[0].ExtractedFields["exception"]);
        }

        [Fact]
        public async Task FilterByTags_ReturnsOnlyMatchingPatterns()
        {
            // Arrange
            var service = new LogPatternService(_testPatternDir);

            var pattern1 = new LogPattern
            {
                Id = "pattern1",
                Name = "Pattern 1",
                RegexPattern = @"test1"
            };
            pattern1.Tags.Add("exception");

            var pattern2 = new LogPattern
            {
                Id = "pattern2",
                Name = "Pattern 2",
                RegexPattern = @"test2"
            };
            pattern2.Tags.Add("http");
            pattern2.Tags.Add("error");

            await service.SavePatternAsync(pattern1);
            await service.SavePatternAsync(pattern2);

            // Act
            var filtered = service.FilterByTags("http");

            // Assert
            var result = filtered.ToList();
            Assert.Single(result);
            Assert.Equal("pattern2", result[0].Id);
        }

        [Fact]
        public async Task FilterBySeverity_ReturnsOnlyMatchingSeverity()
        {
            // Arrange
            var service = new LogPatternService(_testPatternDir);

            var pattern1 = new LogPattern
            {
                Id = "error_pattern",
                Name = "Error Pattern",
                RegexPattern = @"error",
                Severity = "error"
            };

            var pattern2 = new LogPattern
            {
                Id = "warning_pattern",
                Name = "Warning Pattern",
                RegexPattern = @"warning",
                Severity = "warning"
            };

            await service.SavePatternAsync(pattern1);
            await service.SavePatternAsync(pattern2);

            // Act
            var errors = service.FilterBySeverity("error").ToList();
            var warnings = service.FilterBySeverity("warning").ToList();

            // Assert
            Assert.Single(errors);
            Assert.Equal("error_pattern", errors[0].Id);
            Assert.Single(warnings);
            Assert.Equal("warning_pattern", warnings[0].Id);
        }

        [Fact]
        public async Task DeletePatternAsync_RemovesPatternFile()
        {
            // Arrange
            var service = new LogPatternService(_testPatternDir);
            var pattern = new LogPattern
            {
                Id = "delete_test",
                Name = "Delete Test",
                RegexPattern = @"test"
            };

            await service.SavePatternAsync(pattern);
            var filePath = Path.Combine(_testPatternDir, "delete_test.yaml");
            Assert.True(File.Exists(filePath));

            // Act
            await service.DeletePatternAsync("delete_test");

            // Assert
            Assert.False(File.Exists(filePath));
        }

        [Fact]
        public async Task LoadPatternsAsync_SkipsDisabledPatterns()
        {
            // Arrange
            var service = new LogPatternService(_testPatternDir);

            var enabledPattern = new LogPattern
            {
                Id = "enabled",
                Name = "Enabled",
                RegexPattern = @"test",
                IsDisabled = false
            };

            var disabledPattern = new LogPattern
            {
                Id = "disabled",
                Name = "Disabled",
                RegexPattern = @"test",
                IsDisabled = true
            };

            await service.SavePatternAsync(enabledPattern);
            await service.SavePatternAsync(disabledPattern);

            // Act
            await service.LoadPatternsAsync();
            var patterns = service.GetPatterns();

            // Assert
            Assert.Single(patterns);
            Assert.Equal("enabled", patterns[0].Id);
        }
    }
}
