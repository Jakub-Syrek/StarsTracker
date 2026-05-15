using StarsTracker.Api.Services;

namespace StarsTracker.Api.Tests;

public sealed class DeepSkyCatalogTests
{
    private readonly DeepSkyCatalog _catalog = new();

    [Fact]
    public void Catalog_ContainsCanonicalShowpieces()
    {
        var ids = _catalog.All.Select(d => d.Id).ToHashSet();
        ids.Should().Contain(["M31", "M42", "M45", "M44", "M13", "M51"]);
    }

    [Fact]
    public void Catalog_AllEntries_HaveSaneCoordinates()
    {
        _catalog.All.Should().AllSatisfy(d =>
        {
            d.RightAscensionDeg.Should().BeInRange(0, 360);
            d.DeclinationDeg.Should().BeInRange(-90, 90);
            d.Magnitude.Should().BeInRange(-30, 30);
            d.ApparentSizeArcmin.Should().BeGreaterThan(0);
            d.Name.Should().NotBeNullOrWhiteSpace();
            d.Type.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public void M31_AndromedaGalaxy_IsLargestObject()
    {
        var sorted = _catalog.All.OrderByDescending(d => d.ApparentSizeArcmin).Take(3).ToList();
        sorted.Should().Contain(d => d.Id == "M31");
    }

    [Fact]
    public void M45_Pleiades_AreOpenCluster()
    {
        var m45 = _catalog.All.Single(d => d.Id == "M45");
        m45.Type.Should().Be("Open Cluster");
    }
}
