using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Test.Helpers;

internal sealed class CentralPackageManagementFixture : IDisposable
{
    internal static readonly string[] RequiredProjectNames =
    {
        "Group3.Project1",
        "Group3.Project2",
        "Group3.Project3",
        "MultiFrameworks.Project4",
        "MultiFrameworks.Project5"
    };

    private static readonly string[] LegacyMultiFrameworkProjectNames =
    {
        "MultiFrameworks.Project1",
        "MultiFrameworks.Project2",
        "MultiFrameworks.Project3"
    };

    private CentralPackageManagementFixture(string sourceRoot, string rootPath)
    {
        SourceRoot = sourceRoot;
        RootPath = rootPath;
    }

    internal string SourceRoot { get; }

    internal string RootPath { get; }

    internal static CentralPackageManagementFixture Create(
        bool includeLegacyMultiFrameworkProjects = false)
    {
        var sourceRoot = FindSourceRoot();
        var templatePath = Path.Combine(sourceRoot, "Directory.Packages.props.template");
        var projectNames = includeLegacyMultiFrameworkProjects
            ? RequiredProjectNames.Concat(LegacyMultiFrameworkProjectNames).ToArray()
            : RequiredProjectNames;
        var projectFiles = GetProjectFiles(sourceRoot, projectNames);

        var hydratedRoot = Path.Combine(
            Path.GetTempPath(),
            "MultiProjPackTool.DirectoryPackagesProps.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(hydratedRoot);

        var hydratedPropsPath = Path.Combine(hydratedRoot, "Directory.Packages.props");
        File.Copy(templatePath, hydratedPropsPath);
        if (!File.Exists(hydratedPropsPath))
            throw new IOException(
                $"Directory.Packages.props was not created at the hydrated test root '{hydratedRoot}'.");

        // Do not copy Directory.Build.targets: parser tests must resolve from CPM/override only.
        foreach (var projectFile in projectFiles)
        {
            // ScanForProjects reads project directories directly below the scan root. Flattening
            // the named fixtures makes hydration independent of their checked-in subfolder layout.
            var projectName = Path.GetFileNameWithoutExtension(projectFile);
            var projectDirectory = Path.Combine(hydratedRoot, projectName);
            Directory.CreateDirectory(projectDirectory);
            File.Copy(projectFile, Path.Combine(projectDirectory, Path.GetFileName(projectFile)));

            // The augmented legacy NuspecBuilder test still validates real compiled outputs.
            // Mirror outputs from the already-built source fixture into its temporary location.
            if (includeLegacyMultiFrameworkProjects)
                CopyDirectoryIfPresent(
                    Path.Combine(Path.GetDirectoryName(projectFile)!, "bin"),
                    Path.Combine(projectDirectory, "bin"));
        }

        return new CentralPackageManagementFixture(sourceRoot, hydratedRoot);
    }

    internal static string FindSourceRoot()
    {
        return FindSourceRoot(new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() });
    }

    internal static string FindSourceRoot(IEnumerable<string> searchRoots)
    {
        var normalizedSearchRoots = searchRoots
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var searchRoot in normalizedSearchRoots)
        {
            var currentDirectory = new DirectoryInfo(searchRoot);
            while (currentDirectory != null)
            {
                foreach (var fixtureRoot in FindFixtureRootsBelow(currentDirectory.FullName))
                {
                    var templatePath = Path.Combine(
                        fixtureRoot,
                        "Directory.Packages.props.template");
                    if (File.Exists(templatePath) && ContainsAllRequiredProjects(fixtureRoot))
                        return Path.GetFullPath(fixtureRoot);
                }

                currentDirectory = currentDirectory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate Directory.Packages.props.template together with all five " +
            "central-package fixture projects at an ancestor or below one of its subdirectories. " +
            $"Searched upward from: {string.Join("; ", normalizedSearchRoots)}.");
    }

    internal static string[] GetRequiredProjectFiles(string fixtureRoot)
    {
        return GetProjectFiles(fixtureRoot, RequiredProjectNames);
    }

    private static IEnumerable<string> FindFixtureRootsBelow(string ancestorDirectory)
    {
        yield return ancestorDirectory;

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false
        };
        string[] nestedTemplates;
        try
        {
            nestedTemplates = Directory.GetFiles(
                    ancestorDirectory,
                    "Directory.Packages.props.template",
                    enumerationOptions)
                .OrderBy(path => path.Count(character =>
                    character == Path.DirectorySeparatorChar ||
                    character == Path.AltDirectorySeparatorChar))
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (DirectoryNotFoundException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var templatePath in nestedTemplates)
        {
            var fixtureRoot = Path.GetDirectoryName(templatePath);
            if (fixtureRoot != null &&
                !Path.GetFullPath(fixtureRoot).Equals(
                    Path.GetFullPath(ancestorDirectory),
                    StringComparison.OrdinalIgnoreCase))
                yield return fixtureRoot;
        }
    }

    private static bool ContainsAllRequiredProjects(string fixtureRoot)
    {
        return Directory.Exists(fixtureRoot) &&
               RequiredProjectNames.All(projectName =>
                   Directory.GetFiles(
                       fixtureRoot,
                       projectName + ".csproj",
                       SearchOption.AllDirectories).Length == 1);
    }

    private static string[] GetProjectFiles(
        string fixtureRoot,
        IEnumerable<string> projectNames)
    {
        var projectFiles = new List<string>();
        foreach (var projectName in projectNames)
        {
            var matches = Directory.GetFiles(
                fixtureRoot,
                projectName + ".csproj",
                SearchOption.AllDirectories);
            if (matches.Length != 1)
                throw new InvalidOperationException(
                    $"Expected exactly one {projectName}.csproj below '{fixtureRoot}', but found " +
                    $"{matches.Length}.");

            projectFiles.Add(matches[0]);
        }

        return projectFiles.ToArray();
    }

    private static void CopyDirectoryIfPresent(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
            return;

        foreach (var sourceFile in Directory.GetFiles(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
            var destinationFile = Path.Combine(destinationDirectory, relativePath);
            var destinationParent = Path.GetDirectoryName(destinationFile);
            if (destinationParent != null)
                Directory.CreateDirectory(destinationParent);

            File.Copy(sourceFile, destinationFile, overwrite: true);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, true);
        }
        catch (IOException)
        {
            // A failed cleanup should not hide the behavior under test.
        }
        catch (UnauthorizedAccessException)
        {
            // A failed cleanup should not hide the behavior under test.
        }
    }
}