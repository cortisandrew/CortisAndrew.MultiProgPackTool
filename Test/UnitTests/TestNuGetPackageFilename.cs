using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using MultiProjPackTool.HelperExtensions;

namespace Test.UnitTests;

public sealed class TestNuGetPackageFilename
{
    private const string PackageId = "CortisAndrew.AuthPermissions.AspNetCore";

    [Theory]
    [InlineData("10.0-CortisAndrew.1", "10.0.0-CortisAndrew.1")]
    [InlineData("10.0", "10.0.0")]
    [InlineData("10.0.0", "10.0.0")]
    [InlineData("10.0.0.0", "10.0.0")]
    [InlineData("01.02.003", "1.2.3")]
    [InlineData("1.2.3+Build.5", "1.2.3")]
    public void FormNupkgFilenameUsesNuGetNormalizedVersion(
        string configuredVersion,
        string normalizedVersion)
    {
        var settings = Settings(configuredVersion);
        // Technically, we should not reuse the same code from the code
        // However, it is simple enough that it can be reviewed manually
        // Future work should
        var filename = settings.FormNupkgFilename();

        Assert.Equal($"{PackageId}.{normalizedVersion}.nupkg", filename);
    }

    [Fact]
    public void FormNupkgFilenameRejectsInvalidNuGetVersion()
    {
        var settings = Settings("not-a-version");

        var exception = Assert.Throws<ArgumentException>(() => settings.FormNupkgFilename());

        Assert.Contains("not-a-version", exception.Message);
    }

    private static allsettings Settings(string version)
    {
        return new allsettings
        {
            metadata = new allsettingsMetadata
            {
                id = PackageId,
                version = version
            }
        };
    }
}
