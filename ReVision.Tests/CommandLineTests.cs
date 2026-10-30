namespace ReVision.Tests;

public sealed class CommandLineTests
{
    [Fact]
    public void DefaultRunHasNoDeadline()
    {
        var options = CommandLineOptions.Parse([]);
        Assert.Null(options.DurationSeconds);
        Assert.Equal(4442, options.Stream.Port);
    }

    [Theory]
    [InlineData("--duration", "0")]
    [InlineData("--duration", "-1")]
    [InlineData("--port", "80")]
    [InlineData("--jpeg-quality", "101")]
    [InlineData("--max-fps", "0")]
    [InlineData("--unknown", "42")]
    public void InvalidArgumentsFail(string key, string value) => Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse([key, value]));

    [Fact]
    public void MissingAndRepeatedArgumentsFail()
    {
        Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(["--port"]));
        Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(["--port", "4442", "--port", "4443"]));
    }
}
