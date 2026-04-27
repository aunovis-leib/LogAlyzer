using LogAnalyzer.Behaviors;
using System.Reflection;
using Xunit;

namespace LogAnalyzer.Tests.Behaviors;

public class TimeInputBehaviorTests
{
    [Fact]
    public void TryNormalizeToHMS_IfHourAndMinuteProvided_DoesNotAppendSeconds()
    {
        var method = typeof(TimeInputBehavior).GetMethod(
            "TryNormalizeToHMS",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = method!.Invoke(null, ["05:33"]) as string;

        Assert.Equal("05:33", result);
    }
}
