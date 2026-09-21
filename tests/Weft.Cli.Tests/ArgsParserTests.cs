using Weft.Cli;
using Xunit;

namespace Weft.Cli.Tests;

public class ArgsParserTests
{
    private static ParsedArgs Parse(params string[] args) => ArgsParser.Parse(args);

    [Fact]
    public void Verb_and_operands()
    {
        var a = Parse("rollback", "Bella", "v2");
        Assert.Equal("rollback", a.Verb);
        Assert.Equal(["Bella", "v2"], a.Operands);
    }

    [Fact]
    public void Short_valued_option_takes_next_token()
    {
        var a = Parse("commit", "-m", "firstrig");
        Assert.Equal("firstrig", a.Get("-m"));
    }

    [Fact]
    public void Long_valued_option_with_equals()
    {
        var a = Parse("edit", "Bella", "--samples=256");
        Assert.Equal("256", a.Get("--samples"));
    }

    [Fact]
    public void Long_valued_option_takes_next_token()
    {
        var a = Parse("add", "Bella", "--source", "//characters/bella.blend");
        Assert.Equal("//characters/bella.blend", a.Get("--source"));
    }

    [Fact]
    public void Boolean_flags()
    {
        var a = Parse("commit", "-a", "--yes");
        Assert.True(a.Flag("-a"));
        Assert.True(a.Flag("--yes"));
    }

    [Fact]
    public void Help_flags()
    {
        Assert.True(Parse("commit", "--help").Help);
        Assert.True(Parse("commit", "-h").Help);
        Assert.True(Parse("--help", "status").Help);
    }

    [Fact]
    public void Unknown_long_options_are_flags_not_values()
    {
        var a = Parse("rm", "Bella", "--yes");
        Assert.True(a.Flag("--yes"));
        Assert.Equal("", a.Get("--yes")); // flags read as empty values
    }

    [Fact]
    public void Trailing_option_without_value_is_empty_string()
    {
        var a = Parse("edit", "Bella", "--description");
        Assert.Equal("", a.Get("--description"));
    }
}
