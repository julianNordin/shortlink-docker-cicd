using ShortLink.Api.Services;

namespace ShortLink.Api.Tests;

public class ShortCodeGeneratorTests
{
    private readonly ShortCodeGenerator _generator = new();

    [Fact]
    public void Generate_returns_a_code_of_the_configured_length()
    {
        Assert.Equal(ShortCodeGenerator.CodeLength, _generator.Generate().Length);
    }

    [Fact]
    public void Generate_uses_only_base62_characters()
    {
        // Anything outside this set would be a problem in a URL path, which is the only
        // place these codes are ever used.
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        for (var i = 0; i < 500; i++)
        {
            var code = _generator.Generate();
            Assert.All(code, c => Assert.Contains(c, alphabet));
        }
    }

    [Fact]
    public void Generate_does_not_repeat_across_many_draws()
    {
        // Not a proof of uniqueness — nothing at this level could be. With 62^7 possible
        // codes the chance of any collision in 10,000 draws is about 1 in 70,000, so a
        // failure here means the generator has collapsed to a small range, not bad luck.
        const int draws = 10_000;

        var seen = new HashSet<string>();
        for (var i = 0; i < draws; i++)
        {
            seen.Add(_generator.Generate());
        }

        Assert.Equal(draws, seen.Count);
    }

    [Fact]
    public void Generate_varies_every_position()
    {
        // A generator that seeded once and reused the value, or that got a position wrong,
        // would show up as a column that never changes.
        var codes = Enumerable.Range(0, 200).Select(_ => _generator.Generate()).ToList();

        for (var position = 0; position < ShortCodeGenerator.CodeLength; position++)
        {
            var distinct = codes.Select(c => c[position]).Distinct().Count();
            Assert.True(distinct > 1, $"Position {position} produced the same character every time.");
        }
    }
}
