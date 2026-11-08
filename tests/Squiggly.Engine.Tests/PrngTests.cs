namespace Squiggly.Engine.Tests;

public class PrngTests
{
    [Fact]
    public void SplitMix64_matches_the_reference_output_for_state_zero()
    {
        // Published first output of SplitMix64 seeded with 0.
        var random = new Prng(0);
        Assert.Equal(0xE220A8397B1DCDAFUL, random.NextUInt64());
    }

    [Fact]
    public void Known_seed_produces_a_pinned_sequence()
    {
        // Pinned values: if these change, every saved game changes. Do not update them casually.
        var random = Prng.For(8812, 37);
        Assert.Equal(PinnedSequence, new[] { random.NextUInt64(), random.NextUInt64(), random.NextUInt64() });
    }

    [Fact]
    public void Streams_are_independent()
    {
        Assert.NotEqual(Prng.For(1, 1, stream: 0).NextUInt64(), Prng.For(1, 1, stream: 1).NextUInt64());
    }

    [Fact]
    public void NextBelow_stays_in_range_and_covers_it()
    {
        var random = new Prng(42);
        var seen = new bool[7];
        for (var i = 0; i < 1000; i++)
        {
            var value = random.NextBelow(7);
            Assert.InRange(value, 0, 6);
            seen[value] = true;
        }

        Assert.All(seen, Assert.True);
    }

    private static readonly ulong[] PinnedSequence = { 15579844762293677951UL, 15196802056222229913UL, 3425988615809556666UL };
}
