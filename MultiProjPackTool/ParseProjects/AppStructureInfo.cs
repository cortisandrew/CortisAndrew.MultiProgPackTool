// Copyright (c) 2021 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using MultiProjPackTool.HelperExtensions;

namespace MultiProjPackTool.ParseProjects
{
    public class AppStructureInfo
    {
        public AppStructureInfo(
            string namespacePrefix, 
            Dictionary<string, ProjectInfo> allProjects, 
            IWriteToConsole writeToConsoleOut,
            string directoryPackagesPropsPath = null)
        {
            NamespacePrefix = namespacePrefix;
            DirectoryPackagesPropsPath = directoryPackagesPropsPath;
            AllProjects = allProjects.Values.ToList();
            SetupAllNuGetInfosDistinct();

            foreach (var project in allProjects.Values)
            {
                foreach (var lookForLinks in allProjects.Values
                    .Where(lookForLinks => lookForLinks.ChildProjects.Select(x => x.ProjectName).Contains(project.ProjectName)))
                {
                    project.ParentProjects.Add(lookForLinks);
                }
            }

            RootProjects = allProjects.Values.Where(x => !x.ParentProjects.Any())
                .OrderBy(x => x.ProjectName.Length).ToList();

            WarnIfPackageReferencesHaveMultipleVersions(writeToConsoleOut);
        }

        public string NamespacePrefix { get; private set; }

        public List<ProjectInfo> RootProjects { get; private set; }

        public List<ProjectInfo> AllProjects { get; private set; }

        /// <summary>Gets the selected Directory.Packages.props path, or null when none was found.</summary>
        public string DirectoryPackagesPropsPath { get; }

        /// <summary>
        /// This dictionary key is the name of the TargetFramework, and the value is all the projects for that TargetFramework
        /// </summary>
        public Dictionary<string, List<NuGetInfo>> NuGetInfosDistinctByFramework { get; private set; }

        /// <summary>
        /// This fills in the <see cref="NuGetInfosDistinctByFramework"/> with the first <see cref="NuGetInfo"/>
        /// for each package ID and TargetFramework. A separate final pass reports every multi-version package.
        /// </summary>
        private void SetupAllNuGetInfosDistinct()
        {
            NuGetInfosDistinctByFramework = new Dictionary<string, List<NuGetInfo>>();
            foreach (var projectInfo in AllProjects)
            {
                foreach (var targetFramework in projectInfo.TargetFrameworks)
                {
                    if (!NuGetInfosDistinctByFramework.ContainsKey(targetFramework))
                        NuGetInfosDistinctByFramework[targetFramework] = new List<NuGetInfo>();

                    foreach (var nuGetInfo in projectInfo.NuGetPackagesByFramework[targetFramework])
                    {
                        var existingNuget = NuGetInfosDistinctByFramework[targetFramework]
                            .SingleOrDefault(x => string.Equals(
                                x.NuGetId, nuGetInfo.NuGetId, System.StringComparison.OrdinalIgnoreCase));
                        if (existingNuget == null)
                            NuGetInfosDistinctByFramework[targetFramework].Add(nuGetInfo);

                        /* else
                           already in 
                        */

                        /* Removed check if same version

                            if (existingNuget.Version != nuGetInfo.Version)
                            {
                                writeToConsoleOut.LogMessage(
                                    $"The NuGet '{nuGetInfo.NuGetId}' in framework '{targetFramework}' " +
                                    $"has an existing version of {existingNuget.Version}, which is different " +
                                    $"from the same NuGet in the {projectInfo.ProjectName} which has version {nuGetInfo.Version}.",
                                    LogLevel.Warning, true);
                            }
                        */
                    }
                }
            }
        }

        private void WarnIfPackageReferencesHaveMultipleVersions(IWriteToConsole writeToConsoleOut)
        {
            var references = AllProjects
                .SelectMany(project => project.NuGetPackagesByFramework.SelectMany(packagesByFramework =>
                    packagesByFramework.Value.Select(package => new
                    {
                        Package = package,
                        ProjectName = project.ProjectName,
                        TargetFramework = packagesByFramework.Key
                    })))
                .ToList();

            foreach (var packageGroup in references
                         .GroupBy(x => x.Package.NuGetId, System.StringComparer.OrdinalIgnoreCase))
            {
                var versions = packageGroup
                    .GroupBy(x => x.Package.Version, System.StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (versions.Count <= 1)
                    continue;

                var versionDetails = versions.Select(versionGroup =>
                    $"{versionGroup.Key} ({string.Join(", ", versionGroup
                        .Select(x => $"{x.ProjectName}/{x.TargetFramework}")
                        .Distinct())})");
                writeToConsoleOut.LogMessage(
                    $"Package reference/dependency '{packageGroup.First().Package.NuGetId}' resolves to multiple " +
                    $"versions after parsing: {string.Join("; ", versionDetails)}.",
                    LogLevel.Warning,
                    true);
            }
        }

        public override string ToString()
        {
            return
                $"Found {AllProjects.Count} projects starting with {NamespacePrefix}, with {NuGetInfosDistinctByFramework.Values.Sum(x => x.Count)} NuGet packages.";
        }
    }
}