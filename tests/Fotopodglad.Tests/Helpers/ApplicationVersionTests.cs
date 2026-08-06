using Fotopodglad.Helpers;
using Xunit;

namespace Fotopodglad.Tests.Helpers;

public sealed class ApplicationVersionTests
{
    [Fact]
    public void ResolveDisplayVersion_RemovesBuildMetadata()
    {
        var result = ApplicationVersion.ResolveDisplayVersion("1.2.3+abcdef", new Version(9, 9, 9, 9));

        Assert.Equal("1.2.3", result);
    }

    [Fact]
    public void ResolveDisplayVersion_PreservesPrereleaseName()
    {
        var result = ApplicationVersion.ResolveDisplayVersion("2.0.0-beta.1+abcdef", null);

        Assert.Equal("2.0.0-beta.1", result);
    }

    [Fact]
    public void ResolveDisplayVersion_UsesAssemblyVersionAsFallback()
    {
        var result = ApplicationVersion.ResolveDisplayVersion(null, new Version(3, 4, 5, 6));

        Assert.Equal("3.4.5", result);
    }
}
