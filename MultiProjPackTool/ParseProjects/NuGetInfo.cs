// Copyright (c) 2021 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MultiProjPackTool.ParseProjects
{
    public class NuGetInfo
    {
        public string NuGetId { get; set; }

        public string Version { get; set; }

        /// <summary>Gets the source selected by the package version precedence rules.</summary>
        public PackageVersionSource VersionSource { get; }

        /// <summary>Gets the project that declared this package reference.</summary>
        public string ProjectName { get; }

        /// <summary>Gets the target framework for which this package reference was evaluated.</summary>
        public string TargetFramework { get; }

        /// <summary>
        /// Gets every usable version applicable to this reference before precedence selects <see cref="Version"/>.
        /// </summary>
        public IReadOnlyList<string> ApplicableVersions { get; }


        public NuGetInfo(
            ProjectItemGroupPackageReference xml,
            CentralPackageVersions centralPackageVersions,
            string projectName,
            string targetFramework)

        {
            if (xml == null)
                throw new ArgumentNullException(nameof(xml));
            if (centralPackageVersions == null)
                throw new ArgumentNullException(nameof(centralPackageVersions));

            ProjectName = projectName;
            TargetFramework = targetFramework;

            NuGetId = xml.Include?.Trim();
            if (string.IsNullOrWhiteSpace(NuGetId))
                throw new PackageVersionResolutionException(
                    $"A PackageReference in project '{projectName}' does not contain an Include package ID.");

            var targetFrameworkContext = string.IsNullOrWhiteSpace(targetFramework)
                ? string.Empty
                : $" for target framework '{targetFramework}'";

            var versionOverride = FirstPopulated(xml.VersionOverride, xml.VersionOverrideElement);
            var hasCentralVersion = centralPackageVersions.TryGetVersion(
                NuGetId,
                targetFramework,
                out var centralVersion);
            var projectVersion = FirstPopulated(xml.Version, xml.VersionElement);
            ApplicableVersions = new[] { versionOverride, centralVersion, projectVersion }
                .Where(IsUsableVersion)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            
            if (versionOverride != null)
            {
                Version = versionOverride;
                VersionSource = PackageVersionSource.VersionOverride;
                return;
            }

            if (hasCentralVersion)
            {
                if (!IsUsableVersion(centralVersion))
                {
                    throw new PackageVersionResolutionException(
                        $"PackageReference '{NuGetId}' in project '{projectName}'{targetFrameworkContext} " +
                        $"matches a PackageVersion in " +
                        $"'{centralPackageVersions.FilePath}', but that central entry does not provide a usable Version.");
                }

                Version = centralVersion;
                VersionSource = PackageVersionSource.DirectoryPackagesProps;
                return;
            }

            if (projectVersion != null)
            {
                Version = projectVersion;
                VersionSource = PackageVersionSource.Project;
                return;
            }

            var centralVersionContext = centralPackageVersions.WasFound
                ? $"no matching PackageVersion exists in '{centralPackageVersions.FilePath}'"
                : $"no Directory.Packages.props was found at or above '{centralPackageVersions.SearchDirectory}'";
            throw new PackageVersionResolutionException(
                $"PackageReference '{NuGetId}' in project '{projectName}'{targetFrameworkContext} has no " +
                $"VersionOverride or Version, and " +
                $"{centralVersionContext}. A package version is required before compilation can continue.");
        }

        private static string FirstPopulated(params string[] candidates)
        {
            return candidates.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
        }

        private static bool IsUsableVersion(string version)
        {
            return !string.IsNullOrWhiteSpace(version)
                   && !version.Contains("$(", StringComparison.Ordinal);
        }
    }
}