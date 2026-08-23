using System;
using System.Collections.Generic;
using System.Text;

namespace MultiProjPackTool.ParseProjects;

/// <summary>
/// Raised when a PackageReference cannot be assigned a concrete version from any supported source.
/// </summary>
public sealed class PackageVersionResolutionException : InvalidOperationException
{
    /// <summary>Creates an exception containing the package resolution conflict details.</summary>
    public PackageVersionResolutionException(string message)
        : base(message)
    {
    }
}