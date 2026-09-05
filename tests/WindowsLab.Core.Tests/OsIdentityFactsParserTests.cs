using WindowsLab.Core;

namespace WindowsLab.Core.Tests;

public sealed class OsIdentityFactsParserTests
{
    [Fact]
    public void Parse_integer_build_string()
    {
        var facts = OsIdentityFactsParser.Parse(
            "26200",
            10,
            0,
            9168,
            "25H2",
            "Professional",
            "Windows 10 Pro",
            "Enterprise");

        Assert.Equal(26200, facts.Build);
        Assert.Equal("Enterprise", facts.CompositionEditionId);
    }

    [Fact]
    public void Parse_rejects_non_integer_build()
    {
        Assert.Throws<FormatException>(() =>
            OsIdentityFactsParser.Parse("not-a-build", 10, 0, 0, "25H2", "Professional", "Windows 10 Pro"));
    }

    [Fact]
    public void Parse_rejects_negative_build()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OsIdentityFactsParser.Parse("-1", 10, 0, 0, "25H2", "Professional", "Windows 10 Pro"));
    }
}
