using WindowsLab.Core;

namespace WindowsLab.Core.Tests;

public sealed class OsIdentityMapperTests
{
    [Fact]
    public void Build_19045_is_windows_10_not_11()
    {
        Assert.False(OsIdentityMapper.IsWindows11(19045));
        Assert.Equal("Windows 10", OsIdentityMapper.FamilyLabel(19045));
    }

    [Fact]
    public void Build_22000_is_windows_11()
    {
        Assert.True(OsIdentityMapper.IsWindows11(22000));
        Assert.Equal("Windows 11", OsIdentityMapper.FamilyLabel(22000));
    }

    [Fact]
    public void ProductName_Windows_10_Pro_with_build_26200_is_still_windows_11()
    {
        var facts = new OsIdentityFacts(
            Major: 10,
            Minor: 0,
            Build: 26200,
            Ubr: 9168,
            DisplayVersion: "25H2",
            EditionId: "Professional",
            ProductName: "Windows 10 Pro",
            CompositionEditionId: "Enterprise");

        var os = OsIdentityMapper.Map(facts);

        Assert.True(os.IsWindows11);
        Assert.Equal("Windows 11", os.FamilyLabel);
        Assert.Equal(26200, os.Build);
        Assert.Equal("25H2", os.DisplayVersion);
        Assert.Equal("Professional", os.EditionId);
        Assert.Equal("Windows 10 Pro", os.ProductName);
        Assert.False(string.IsNullOrWhiteSpace(os.ProductNameWarning));
    }

    [Fact]
    public void Negative_build_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OsIdentityMapper.IsWindows11(-1));
    }

    [Fact]
    public void FamilyLabel_below_windows_10_min_build_is_generic_windows()
    {
        Assert.Equal("Windows", OsIdentityMapper.FamilyLabel(7601));
    }

    [Fact]
    public void Map_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => OsIdentityMapper.Map(null!));
    }
}
