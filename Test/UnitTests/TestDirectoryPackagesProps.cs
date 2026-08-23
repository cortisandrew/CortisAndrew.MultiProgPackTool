using MultiProjPackTool.ParseProjects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using Test.Helpers;
using Test.Stubs;
using TestSupport.Helpers;
using Xunit;

namespace Test.UnitTests;

public sealed class TestDirectoryPackagesProps : IDisposable
{
    private const string PackageId = "CentralTest.Package";
    private readonly List<string> _temporaryDirectories = new();

    [Fact]
    public void Group3_UsesCentralAndOverrideVersions_AndWarnsAboutTheConflict()
    {
        var stubWriter = new StubWriteToConsole();
        var solutionRoot = HydrateCentralPackageManagementFixture();

        var appInfo = Scan(solutionRoot, "Group3", stubWriter);

        Assert.Equal(
            Path.Combine(solutionRoot, "Directory.Packages.props"),
            appInfo.DirectoryPackagesPropsPath);

        var project1Package = SinglePackage(appInfo, "Group3.Project1", "net10.0");
        Assert.Equal("13.0.3", project1Package.Version);
        Assert.Equal(PackageVersionSource.DirectoryPackagesProps, project1Package.VersionSource);

        var project2Package = SinglePackage(appInfo, "Group3.Project2", "net10.0");
        Assert.Equal("13.0.4", project2Package.Version);
        Assert.Equal(PackageVersionSource.VersionOverride, project2Package.VersionSource);

        var project3Package = SinglePackage(appInfo, "Group3.Project3", "net10.0");
        Assert.Equal("10.0.0", project3Package.Version);
        Assert.Equal(PackageVersionSource.DirectoryPackagesProps, project3Package.VersionSource);

        Assert.Contains(stubWriter.WarningMessages, message =>
            message.Contains("Newtonsoft.Json")
            && message.Contains("13.0.3")
            && message.Contains("13.0.4")
            && message.Contains("multiple versions"));
    }

    [Fact]
    public void MultiFrameworksProject4And5_ManageGroup3AcrossBothFrameworks()
    {
        var stubWriter = new StubWriteToConsole();
        var solutionRoot = HydrateCentralPackageManagementFixture();

        // An empty prefix deliberately scans the complete fixture graph so the manager-to-Group3 links are retained.
        var appInfo = Scan(solutionRoot, string.Empty, stubWriter);

        Assert.Equal(5, appInfo.AllProjects.Count);

        var project4 = appInfo.AllProjects.Single(x => x.ProjectName == "MultiFrameworks.Project4");
        Assert.Equal(new[] { "net9.0", "net10.0" }, project4.TargetFrameworks);
        Assert.Equal("Group3.Project3", project4.ChildProjects.Single().ProjectName);
        Assert.Equal(
            PackageVersionSource.DirectoryPackagesProps,
            SinglePackage(appInfo, "MultiFrameworks.Project4", "net9.0").VersionSource);
        Assert.Equal(
            "9.0.0",
            SinglePackage(appInfo, "MultiFrameworks.Project4", "net9.0").Version);
        Assert.Equal(
            "10.0.0",
            SinglePackage(appInfo, "MultiFrameworks.Project4", "net10.0").Version);

        var project5 = appInfo.AllProjects.Single(x => x.ProjectName == "MultiFrameworks.Project5");
        Assert.Equal(new[] { "net9.0", "net10.0" }, project5.TargetFrameworks);
        Assert.Equal("Group3.Project2", project5.ChildProjects.Single().ProjectName);
        Assert.Equal(
            PackageVersionSource.VersionOverride,
            SinglePackage(appInfo, "MultiFrameworks.Project5", "net9.0").VersionSource);
        Assert.Equal(
            "13.0.4",
            SinglePackage(appInfo, "MultiFrameworks.Project5", "net10.0").Version);

        Assert.Equal(
            new[] { "MultiFrameworks.Project4", "MultiFrameworks.Project5" },
            appInfo.RootProjects.Select(x => x.ProjectName).OrderBy(x => x).ToArray());
        Assert.Contains(stubWriter.WarningMessages, message =>
            message.Contains("Newtonsoft.Json")
            && message.Contains("13.0.3")
            && message.Contains("13.0.4"));
        Assert.Contains(stubWriter.WarningMessages, message =>
            message.Contains("Microsoft.Extensions.Logging.Abstractions")
            && message.Contains("9.0.0")
            && message.Contains("10.0.0")
            && message.Contains("net9.0")
            && message.Contains("net10.0"));
    }

    [Fact]
    public void CentralPackageManagementFixtureIsInertUntilHydratedForATest()
    {
        var sourceRoot = GetCentralPackageManagementFixtureRoot();

        Assert.False(File.Exists(Path.Combine(sourceRoot, "Directory.Packages.props")));
        Assert.True(File.Exists(Path.Combine(sourceRoot, "Directory.Packages.props.template")));

        var hydratedRoot = HydrateCentralPackageManagementFixture();

        var hydratedPropsPath = Path.Combine(hydratedRoot, "Directory.Packages.props");
        Assert.True(File.Exists(hydratedPropsPath));
        Assert.False(File.Exists(Path.Combine(hydratedRoot, "Directory.Build.targets")));
        Assert.Equal(5, Directory.GetFiles(hydratedRoot, "*.csproj", SearchOption.AllDirectories).Length);
        Assert.False(File.Exists(Path.Combine(sourceRoot, "Directory.Packages.props")));

        var centralVersions = CentralPackageVersions.FindAndParse(
            hydratedRoot,
            new StubWriteToConsole());
        Assert.Equal(Path.GetFullPath(hydratedRoot), centralVersions.SearchDirectory);
        Assert.Equal(Path.GetFullPath(hydratedPropsPath), centralVersions.FilePath);
    }

    [Fact]
    public void CheckedInCentralPackageManagementProjectsHaveNoNormalPackageVersions()
    {
        var sourceRoot = GetCentralPackageManagementFixtureRoot();
        var packageReferences = Directory.GetFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(projectPath => XDocument.Load(projectPath).Descendants()
                .Where(element => element.Name.LocalName == "PackageReference"))
            .ToList();

        Assert.Equal(5, packageReferences.Count);
        Assert.All(packageReferences, packageReference =>
        {
            Assert.Null(packageReference.Attributes()
                .SingleOrDefault(attribute => attribute.Name.LocalName == "Version"));
            Assert.DoesNotContain(packageReference.Elements(),
                element => element.Name.LocalName == "Version");
        });
        Assert.Equal(2, packageReferences.Count(packageReference => packageReference.Attributes()
            .Any(attribute => attribute.Name.LocalName == "VersionOverride")));
    }

    [Fact]
    public void CentralVersionWinsOverProjectVersion_AndPackageIdsAreCaseInsensitive()
    {
        var root = CreateSolutionRoot(Props(
            "<PackageVersion Include=\"centraltest.package\" Version=\"2.0.0\" />"));
        AddProject(root, Project(
            "<PackageReference Include=\"CentralTest.Package\" Version=\"1.0.0\" />"));

        var package = SinglePackage(Scan(root));

        Assert.Equal("2.0.0", package.Version);
        Assert.Equal(PackageVersionSource.DirectoryPackagesProps, package.VersionSource);
    }

    [Fact]
    public void VersionOverrideIsUsedEvenWhenCentralFileHasNoEquivalent()
    {
        var root = CreateSolutionRoot(Props(
            "<PackageVersion Include=\"Different.Package\" Version=\"2.0.0\" />"));
        AddProject(root, Project(
            "<PackageReference Include=\"CentralTest.Package\" VersionOverride=\"3.0.0\" />"));

        var package = SinglePackage(Scan(root));

        Assert.Equal("3.0.0", package.Version);
        Assert.Equal(PackageVersionSource.VersionOverride, package.VersionSource);
    }

    [Fact]
    public void ConditionalCentralVersionFallsBackToProjectVersionForOtherFramework()
    {
        var stubWriter = new StubWriteToConsole();
        var root = CreateSolutionRoot(
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="CentralTest.Package"
                                Version="10.0.0"
                                Condition=" '$(TargetFramework)' == 'net10.0'" />
              </ItemGroup>
            </Project>
            """);
        AddProject(
            root,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net9.0;net10.0</TargetFrameworks>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="CentralTest.Package" Version="9.0.0" />
              </ItemGroup>
            </Project>
            """);

        var appInfo = Scan(root, consoleOut: stubWriter);

        Assert.Equal(
            PackageVersionSource.Project,
            appInfo.NuGetInfosDistinctByFramework["net9.0"].Single().VersionSource);
        Assert.Equal("9.0.0", appInfo.NuGetInfosDistinctByFramework["net9.0"].Single().Version);
        Assert.Equal(
            PackageVersionSource.DirectoryPackagesProps,
            appInfo.NuGetInfosDistinctByFramework["net10.0"].Single().VersionSource);
        Assert.Equal("10.0.0", appInfo.NuGetInfosDistinctByFramework["net10.0"].Single().Version);
        Assert.Contains(stubWriter.WarningMessages,
            message => message.Contains("resolves to multiple versions"));
    }

    [Fact]
    public void VersionOverrideAttributeWinsOverCentralAndProjectVersions()
    {
        var root = CreateSolutionRoot(Props(
            "<PackageVersion Include=\"CentralTest.Package\" Version=\"2.0.0\" />"));
        AddProject(root, Project(
            "<PackageReference Include=\"CentralTest.Package\" Version=\"1.0.0\" VersionOverride=\"3.0.0\" />"));

        var package = SinglePackage(Scan(root));

        Assert.Equal("3.0.0", package.Version);
        Assert.Equal(PackageVersionSource.VersionOverride, package.VersionSource);
    }

    [Fact]
    public void VersionOverrideElementWinsAndProjectVersionElementIsParsed()
    {
        var root = CreateSolutionRoot(Props(
            "<PackageVersion Include=\"CentralTest.Package\" Version=\"2.0.0\" />"));
        AddProject(root, Project(
            """
            <PackageReference Include="CentralTest.Package">
              <Version>1.0.0</Version>
              <VersionOverride>3.1.0</VersionOverride>
            </PackageReference>
            """));

        var package = SinglePackage(Scan(root));

        Assert.Equal("3.1.0", package.Version);
        Assert.Equal(PackageVersionSource.VersionOverride, package.VersionSource);
    }

    [Fact]
    public void ProjectVersionIsUsedWhenCentralFileHasNoEquivalent()
    {
        var root = CreateSolutionRoot(Props(
            "<PackageVersion Include=\"Different.Package\" Version=\"8.0.0\" />"));
        AddProject(root, Project(
            "<PackageReference Include=\"CentralTest.Package\" Version=\"4.0.0\" />"));

        var package = SinglePackage(Scan(root));

        Assert.Equal("4.0.0", package.Version);
        Assert.Equal(PackageVersionSource.Project, package.VersionSource);
    }

    [Fact]
    public void ProjectVersionElementIsUsedWhenNoCentralFileExists()
    {
        var root = CreateSolutionRoot();
        AddProject(root, Project(
            """
            <PackageReference Include="CentralTest.Package">
              <Version>4.1.0</Version>
            </PackageReference>
            """));

        var appInfo = Scan(root);
        var package = SinglePackage(appInfo);

        Assert.Null(appInfo.DirectoryPackagesPropsPath);
        Assert.Equal("4.1.0", package.Version);
        Assert.Equal(PackageVersionSource.Project, package.VersionSource);
    }

    [Fact]
    public void DirectoryPackagesPropsIsFoundInParentDirectory()
    {
        var parent = CreateSolutionRoot(Props(
            "<PackageVersion Include=\"CentralTest.Package\" Version=\"5.0.0\" />"));
        var solutionRoot = Path.Combine(parent, "Solution");
        Directory.CreateDirectory(solutionRoot);
        AddProject(solutionRoot, Project(
            "<PackageReference Include=\"CentralTest.Package\" />"));

        var appInfo = Scan(solutionRoot);

        Assert.Equal(Path.Combine(parent, "Directory.Packages.props"), appInfo.DirectoryPackagesPropsPath);
        Assert.Equal("5.0.0", SinglePackage(appInfo).Version);
    }

    [Fact]
    public void NearestDirectoryPackagesPropsWinsOverParentFile()
    {
        var parent = CreateSolutionRoot(Props(
            "<PackageVersion Include=\"CentralTest.Package\" Version=\"5.0.0\" />"));
        var solutionRoot = Path.Combine(parent, "Solution");
        Directory.CreateDirectory(solutionRoot);
        File.WriteAllText(
            Path.Combine(solutionRoot, "Directory.Packages.props"),
            Props("<PackageVersion Include=\"CentralTest.Package\" Version=\"6.0.0\" />"));
        AddProject(solutionRoot, Project(
            "<PackageReference Include=\"CentralTest.Package\" />"));

        var package = SinglePackage(Scan(solutionRoot));

        Assert.Equal("6.0.0", package.Version);
    }

    [Fact]
    public void PackageVersionUpdate_ElementVersion_AndPropertyChainsAreSupported()
    {
        var root = CreateSolutionRoot(
            """
            <Project>
              <PropertyGroup>
                <VersionMajorMinor>7.2</VersionMajorMinor>
                <VersionPatch>.1</VersionPatch>
                <CombinedVersion>$(VersionMajorMinor)$(VersionPatch)</CombinedVersion>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Update="CentralTest.Package">
                  <Version>$(CombinedVersion)</Version>
                </PackageVersion>
              </ItemGroup>
            </Project>
            """);
        AddProject(root, Project(
            "<PackageReference Include=\"CentralTest.Package\" />"));

        var package = SinglePackage(Scan(root));

        Assert.Equal("7.2.1", package.Version);
        Assert.Equal(PackageVersionSource.DirectoryPackagesProps, package.VersionSource);
    }

    [Fact]
    public void MissingVersionInBothCentralFileAndProjectThrows()
    {
        var root = CreateSolutionRoot(Props(
            "<PackageVersion Include=\"Different.Package\" Version=\"8.0.0\" />"));
        AddProject(root, Project(
            "<PackageReference Include=\"CentralTest.Package\" />"));

        var exception = Assert.Throws<PackageVersionResolutionException>(() => Scan(root));

        Assert.Contains("CentralTest.Package", exception.Message);
        Assert.Contains("CentralTest.Project", exception.Message);
        Assert.Contains("no VersionOverride or Version", exception.Message);
        Assert.Contains("no matching PackageVersion", exception.Message);
        Assert.Contains("compilation can continue", exception.Message);
    }

    [Fact]
    public void MissingVersionWithoutCentralFileThrows()
    {
        var root = CreateSolutionRoot();
        AddProject(root, Project(
            "<PackageReference Include=\"CentralTest.Package\" />"));

        var exception = Assert.Throws<PackageVersionResolutionException>(() => Scan(root));

        Assert.Contains("CentralTest.Package", exception.Message);
        Assert.Contains("no Directory.Packages.props was found", exception.Message);
        Assert.Contains("compilation can continue", exception.Message);
    }

    [Fact]
    public void EmptyCentralVersionThrowsInsteadOfUsingProjectVersion()
    {
        var root = CreateSolutionRoot(Props(
            "<PackageVersion Include=\"CentralTest.Package\" />"));
        AddProject(root, Project(
            "<PackageReference Include=\"CentralTest.Package\" Version=\"9.0.0\" />"));

        var exception = Assert.Throws<PackageVersionResolutionException>(() => Scan(root));

        Assert.Contains("matches a PackageVersion", exception.Message);
        Assert.Contains("does not provide a usable Version", exception.Message);
    }

    [Fact]
    public void UnresolvedCentralPropertyThrowsAUsefulException()
    {
        var root = CreateSolutionRoot(Props(
            "<PackageVersion Include=\"CentralTest.Package\" Version=\"$(UnknownVersion)\" />"));
        AddProject(root, Project(
            "<PackageReference Include=\"CentralTest.Package\" />"));

        var exception = Assert.Throws<PackageVersionResolutionException>(() => Scan(root));

        Assert.Contains("does not provide a usable Version", exception.Message);
    }

    [Fact]
    public void DuplicateCentralDefinitionsUseLastValueAndWarn()
    {
        var stubWriter = new StubWriteToConsole();
        var root = CreateSolutionRoot(Props(
            """
            <PackageVersion Include="CentralTest.Package" Version="10.0.0" />
            <PackageVersion Update="centraltest.package" Version="10.1.0" />
            """));
        AddProject(root, Project(
            "<PackageReference Include=\"CentralTest.Package\" />"));

        var package = SinglePackage(Scan(root, consoleOut: stubWriter));

        Assert.Equal("10.1.0", package.Version);
        Assert.Contains(stubWriter.WarningMessages, message =>
            message.Contains("more than once")
            && message.Contains("10.0.0")
            && message.Contains("10.1.0"));
    }

    [Fact]
    public void SameResolvedVersionAcrossProjectsDoesNotWarn()
    {
        var stubWriter = new StubWriteToConsole();
        var root = CreateSolutionRoot(Props(
            "<PackageVersion Include=\"CentralTest.Package\" Version=\"11.0.0\" />"));
        AddProject(root, Project(
            "<PackageReference Include=\"CentralTest.Package\" />"),
            "CentralTest.Project1");
        AddProject(root, Project(
            "<PackageReference Include=\"centraltest.package\" VersionOverride=\"11.0.0\" />"),
            "CentralTest.Project2");

        Scan(root, consoleOut: stubWriter);

        Assert.DoesNotContain(stubWriter.WarningMessages,
            message => message.Contains("resolves to multiple versions"));
    }

    [Fact]
    public void MultipleVersionsAcrossTargetFrameworkDependenciesWarnAtEndOfParsing()
    {
        var stubWriter = new StubWriteToConsole();
        var root = CreateSolutionRoot();
        AddProject(
            root,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net9.0;net10.0</TargetFrameworks>
              </PropertyGroup>
              <ItemGroup Condition=" '$(TargetFramework)' == 'net9.0'">
                <PackageReference Include="CentralTest.Package" VersionOverride="9.0.0" />
              </ItemGroup>
              <ItemGroup Condition=" '$(TargetFramework)' == 'net10.0'">
                <PackageReference Include="CentralTest.Package" VersionOverride="10.0.0" />
              </ItemGroup>
            </Project>
            """);

        var appInfo = Scan(root, consoleOut: stubWriter);

        Assert.Equal("9.0.0", appInfo.NuGetInfosDistinctByFramework["net9.0"].Single().Version);
        Assert.Equal("10.0.0", appInfo.NuGetInfosDistinctByFramework["net10.0"].Single().Version);
        Assert.Contains(stubWriter.WarningMessages, message =>
            message.Contains("resolves to multiple versions")
            && message.Contains("net9.0")
            && message.Contains("net10.0"));
        Assert.Equal(0, stubWriter.NumWarnings);
    }

    public void Dispose()
    {
        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            try
            {
                Directory.Delete(directory, true);
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

    private string CreateSolutionRoot(string directoryPackagesProps = null)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "MultiProjPackTool.DirectoryPackagesProps.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        _temporaryDirectories.Add(root);

        if (directoryPackagesProps != null)
        {
            File.WriteAllText(
                Path.Combine(root, "Directory.Packages.props"),
                directoryPackagesProps);
        }

        return root;
    }

    private string HydrateCentralPackageManagementFixture()
    {
        var sourceRoot = GetCentralPackageManagementFixtureRoot();
        var templatePath = Path.Combine(sourceRoot, "Directory.Packages.props.template");
        if (!File.Exists(templatePath))
            throw new FileNotFoundException(
                $"Cannot hydrate the central-package fixture because its template was not found at '{templatePath}'.",
                templatePath);

        var sourceProjectFiles = Directory.GetFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories);
        if (sourceProjectFiles.Length != 5)
            throw new InvalidOperationException(
                $"Expected five central-package fixture projects below '{sourceRoot}', but found " +
                $"{sourceProjectFiles.Length}.");

        var hydratedRoot = CreateSolutionRoot();
        var hydratedPropsPath = Path.Combine(hydratedRoot, "Directory.Packages.props");
        File.Copy(templatePath, hydratedPropsPath);
        if (!File.Exists(hydratedPropsPath))
            throw new IOException(
                $"Directory.Packages.props was not created at the hydrated test root '{hydratedRoot}'.");

        // Do not copy Directory.Build.targets: parser integration tests must resolve from CPM/override only.
        foreach (var sourceProjectFile in sourceProjectFiles)
        {
            var relativePath = Path.GetRelativePath(sourceRoot, sourceProjectFile);
            var destination = Path.Combine(hydratedRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(sourceProjectFile, destination);
        }

        return hydratedRoot;
    }

    private static string GetCentralPackageManagementFixtureRoot()
    {
        var searchRoots = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var searchRoot in searchRoots)
        {
            var currentDirectory = new DirectoryInfo(searchRoot);
            while (currentDirectory != null)
            {
                var fixtureRoot = Path.Combine(currentDirectory.FullName, "CentralPackageManagement");
                var templatePath = Path.Combine(fixtureRoot, "Directory.Packages.props.template");
                if (File.Exists(templatePath))
                    return Path.GetFullPath(fixtureRoot);

                currentDirectory = currentDirectory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate CentralPackageManagement/Directory.Packages.props.template. " +
            $"Searched upward from: {string.Join("; ", searchRoots)}.");
    }

    private static void AddProject(
        string solutionRoot,
        string projectXml,
        string projectName = "CentralTest.Project")
    {
        var projectDirectory = Path.Combine(solutionRoot, projectName);
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(
            Path.Combine(projectDirectory, projectName + ".csproj"),
            projectXml);
    }

    private static AppStructureInfo Scan(
        string solutionRoot,
        string namespacePrefix = "CentralTest",
        StubWriteToConsole consoleOut = null)
    {
        var settings = SettingHelpers.GetMinimalSettings();
        settings.toolSettings.NamespacePrefix = namespacePrefix;
        return solutionRoot.ScanForProjects(settings, consoleOut ?? new StubWriteToConsole());
    }

    private static NuGetInfo SinglePackage(AppStructureInfo appInfo)
    {
        return appInfo.AllProjects.Single()
            .NuGetPackagesByFramework.Values.Single()
            .Single();
    }

    private static NuGetInfo SinglePackage(
        AppStructureInfo appInfo,
        string projectName,
        string targetFramework)
    {
        return appInfo.AllProjects.Single(x => x.ProjectName == projectName)
            .NuGetPackagesByFramework[targetFramework]
            .Single();
    }

    private static string Props(string packageVersions)
    {
        return $"""
                <Project>
                  <ItemGroup>
                    {packageVersions}
                  </ItemGroup>
                </Project>
                """;
    }

    private static string Project(string packageReferences)
    {
        return $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    {packageReferences}
                  </ItemGroup>
                </Project>
                """;
    }
}