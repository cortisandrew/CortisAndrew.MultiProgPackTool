// Copyright (c) 2026 Andrew Cortis, GitHub: CortisAndrew
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;
using MultiProjPackTool.HelperExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MultiProjPackTool.ParseProjects;

/// <summary>
/// Finds and parses the nearest Directory.Packages.props file used while scanning a solution directory.
/// </summary>
public sealed class CentralPackageVersions
{
    private static readonly Regex PropertyReferenceRegex =
        new(@"\$\((?<name>[^)]+)\)", RegexOptions.Compiled);

    private static readonly Regex TargetFrameworkEqualityRegex =
        new(@"['""]?\$\(TargetFramework\)['""]?\s*==\s*['""](?<framework>[^'""]+)['""]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly Dictionary<string, List<PackageVersionDefinition>> _versions;

    private CentralPackageVersions(
        string searchDirectory,
        string filePath,
        Dictionary<string, List<PackageVersionDefinition>> versions)
    {
        SearchDirectory = searchDirectory;
        FilePath = filePath;
        _versions = versions;
    }

    /// <summary>The absolute directory where discovery started.</summary>
    public string SearchDirectory { get; }

    /// <summary>The selected props path, or null when no file was found.</summary>
    public string FilePath { get; }

    /// <summary>True when a Directory.Packages.props file was found and parsed.</summary>
    public bool WasFound => FilePath != null;

    /// <summary>Finds the nearest props file at or above the scan directory and parses its package versions.</summary>
    public static CentralPackageVersions FindAndParse(string solutionDirectory, IWriteToConsole consoleOut)
    {
        if (string.IsNullOrWhiteSpace(solutionDirectory))
            throw new ArgumentException("A solution directory is required.", nameof(solutionDirectory));
        if (consoleOut == null)
            throw new ArgumentNullException(nameof(consoleOut));

        var fullSearchDirectory = Path.GetFullPath(solutionDirectory);
        consoleOut.LogMessage(
            $"Searching for Directory.Packages.props starting at '{fullSearchDirectory}'.",
            LogLevel.Debug);
        var currentDirectory = new DirectoryInfo(fullSearchDirectory);
        while (currentDirectory != null)
        {
            var candidatePath = Path.Combine(currentDirectory.FullName, "Directory.Packages.props");
            if (File.Exists(candidatePath))
                return Parse(fullSearchDirectory, candidatePath, consoleOut);

            currentDirectory = currentDirectory.Parent;
        }

        consoleOut.LogMessage(
            $"No Directory.Packages.props was found at or above '{fullSearchDirectory}'. " +
            "Project-local package versions remain available.",
            LogLevel.Debug);

        return new CentralPackageVersions(
            fullSearchDirectory,
            null,
            new Dictionary<string, List<PackageVersionDefinition>>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Gets the last central version applicable to a package and target framework.</summary>
    public bool TryGetVersion(string packageId, string targetFramework, out string version)
    {
        version = null;
        if (packageId == null || !_versions.TryGetValue(packageId, out var definitions))
            return false;

        var applicableDefinitions = definitions
            .Where(x => ConditionMatches(x.ItemGroupCondition, targetFramework)
                        && ConditionMatches(x.PackageCondition, targetFramework))
            .ToList();
        if (!applicableDefinitions.Any())
            return false;

        version = applicableDefinitions.Last().Version;
        return true;
    }

    private static CentralPackageVersions Parse(
        string searchDirectory,
        string filePath,
        IWriteToConsole consoleOut)
    {
        var document = XDocument.Load(filePath, LoadOptions.SetLineInfo);
        var properties = ReadProperties(document);
        var versions =
            new Dictionary<string, List<PackageVersionDefinition>>(StringComparer.OrdinalIgnoreCase);

        foreach (var packageVersion in document.Descendants()
                     .Where(x => x.Name.LocalName == "PackageVersion"))
        {
            var packageId = GetAttributeValue(packageVersion, "Include")
                            ?? GetAttributeValue(packageVersion, "Update");
            if (string.IsNullOrWhiteSpace(packageId))
            {
                consoleOut.LogMessage(
                    $"Ignored a PackageVersion without Include or Update in '{filePath}'.",
                    LogLevel.Warning,
                    true);
                continue;
            }

            var version = GetAttributeValue(packageVersion, "Version")
                          ?? packageVersion.Elements()
                              .SingleOrDefault(x => x.Name.LocalName == "Version")?.Value;
            version = ExpandProperties(version?.Trim(), properties);

            var definition = new PackageVersionDefinition(
                version,
                GetAttributeValue(packageVersion.Parent, "Condition"),
                GetAttributeValue(packageVersion, "Condition"));

            if (!versions.TryGetValue(packageId, out var packageDefinitions))
            {
                packageDefinitions = new List<PackageVersionDefinition>();
                versions[packageId] = packageDefinitions;
            }

            var previousEquivalent = packageDefinitions.LastOrDefault(x =>
                string.Equals(x.ItemGroupCondition, definition.ItemGroupCondition, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.PackageCondition, definition.PackageCondition, StringComparison.OrdinalIgnoreCase));
            if (previousEquivalent != null
                && !string.Equals(previousEquivalent.Version, version, StringComparison.OrdinalIgnoreCase))
            {
                consoleOut.LogMessage(
                    $"Directory.Packages.props defines package '{packageId}' more than once with different " +
                    $"versions ('{previousEquivalent.Version}' and '{version}') for the same condition. " +
                    "The last definition is used.",
                    LogLevel.Warning,
                    true);
            }

            // MSBuild items are evaluated in document order. The last applicable declaration therefore wins.
            packageDefinitions.Add(definition);
        }

        consoleOut.LogMessage(
            $"Loaded {versions.Count} central package version(s) from '{filePath}'.",
            LogLevel.Debug);

        return new CentralPackageVersions(searchDirectory, Path.GetFullPath(filePath), versions);
    }

    private static Dictionary<string, string> ReadProperties(XDocument document)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.Descendants()
                     .Where(x => x.Name.LocalName == "PropertyGroup")
                     .SelectMany(x => x.Elements()))
        {
            properties[property.Name.LocalName] = property.Value.Trim();
        }

        // Resolve simple property-to-property chains before PackageVersion values are read.
        foreach (var propertyName in properties.Keys.ToList())
            properties[propertyName] = ExpandProperties(properties[propertyName], properties);

        return properties;
    }

    private static string ExpandProperties(string value, IReadOnlyDictionary<string, string> properties)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var result = value;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var expanded = PropertyReferenceRegex.Replace(result, match =>
                properties.TryGetValue(match.Groups["name"].Value, out var propertyValue)
                    ? propertyValue
                    : match.Value);
            if (expanded == result)
                break;

            result = expanded;
        }

        return result;
    }

    private static bool ConditionMatches(string condition, string targetFramework)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true;
        if (string.IsNullOrWhiteSpace(targetFramework))
            return false;

        var frameworkMatches = TargetFrameworkEqualityRegex.Matches(condition);
        return frameworkMatches.Any(match =>
            string.Equals(
                match.Groups["framework"].Value,
                targetFramework,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string GetAttributeValue(XElement element, string localName)
    {
        return element?.Attributes()
            .SingleOrDefault(x => x.Name.LocalName == localName)?.Value;
    }

    private sealed class PackageVersionDefinition
    {
        public PackageVersionDefinition(
            string version,
            string itemGroupCondition,
            string packageCondition)
        {
            Version = version;
            ItemGroupCondition = itemGroupCondition;
            PackageCondition = packageCondition;
        }

        public string Version { get; }

        public string ItemGroupCondition { get; }

        public string PackageCondition { get; }
    }
}