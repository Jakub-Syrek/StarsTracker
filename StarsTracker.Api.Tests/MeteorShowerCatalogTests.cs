using StarsTracker.Api.Services;

namespace StarsTracker.Api.Tests;

public sealed class MeteorShowerCatalogTests
{
    private readonly MeteorShowerCatalog _catalog = new();

    [Fact]
    public void PerseidsAreActive_AroundAugustTwelfth()
    {
        var aug12 = new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);
        var active = _catalog.ActiveAround(aug12);
        active.Should().Contain(s => s.Code == "PER");
        var per = active.Single(s => s.Code == "PER");
        Math.Abs(per.DaysUntilPeak).Should().BeLessThanOrEqualTo(7);
    }

    [Fact]
    public void GeminidsAreActive_AroundDecemberFourteenth()
    {
        var dec14 = new DateTime(2026, 12, 14, 0, 0, 0, DateTimeKind.Utc);
        _catalog.ActiveAround(dec14).Should().Contain(s => s.Code == "GEM");
    }

    [Fact]
    public void RandomMidsummerDay_ReturnsNothing()
    {
        // mid-June has no major shower peak within ±7 days
        var jun15 = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        _catalog.ActiveAround(jun15).Should().BeEmpty();
    }

    [Fact]
    public void QuadrantidsRolledOver_FromLastYear_AreFound()
    {
        // The Quadrantids peak on Jan 4. Querying Dec 30 should still resolve
        // to the upcoming January peak via the candidate-set rollover logic.
        var dec30 = new DateTime(2026, 12, 30, 0, 0, 0, DateTimeKind.Utc);
        _catalog.ActiveAround(dec30).Should().Contain(s => s.Code == "QUA");
    }

    [Fact]
    public void AllReturnedEntries_HaveCanonicalShape()
    {
        var aug12 = new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);
        _catalog.ActiveAround(aug12).Should().AllSatisfy(s =>
        {
            s.Code.Should().HaveLength(3);
            s.Name.Should().NotBeNullOrWhiteSpace();
            s.RadiantRightAscensionDeg.Should().BeInRange(0, 360);
            s.RadiantDeclinationDeg.Should().BeInRange(-90, 90);
            s.ZenithalHourlyRate.Should().BeGreaterThan(0);
        });
    }
}
