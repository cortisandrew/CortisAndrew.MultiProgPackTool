using System;
using System.Collections.Generic;
using System.Text;

namespace MultiProjPackTool.ParseProjects;

/// <summary>
/// Identifies which supported package-version source supplied a resolved dependency version.
/// </summary>
public enum PackageVersionSource
{
    /// <summary>The normal Version metadata on the project PackageReference.</summary>
    Project,

    /// <summary>A matching PackageVersion from the nearest Directory.Packages.props.</summary>
    DirectoryPackagesProps,

    /// <summary>The VersionOverride metadata on the project PackageReference.</summary>
    VersionOverride
}
