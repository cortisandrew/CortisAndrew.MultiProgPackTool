// Copyright (c) 2022 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

using MultiProjPackTool.ParseProjects;
using MultiProjPackTool.SettingHandling;
using System.IO;
using System.Linq;
using MultiProjPackTool.BuildNuspec;
using Test.Helpers;
using Test.Stubs;
using TestSupport.Helpers;
using Xunit;
using Xunit.Extensions.AssertExtensions;

namespace Test.UnitTests
{
    // see https://stackoverflow.com/questions/1408175/execute-unit-tests-serially-rather-than-in-parallel
    [Collection("Sequential")]
    public class TestMultipleTargetFrameworks
    {
        private readonly ITestOutputHelper _output;

        public TestMultipleTargetFrameworks(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void NuspecBuilder_MultiFrameworks_Works()
        {
            //SETUP
            var stubWriter = new StubWriteToConsole(_output);
            var settings = SettingHelpers.GetMinimalSettings();
            // The hydrated graph includes legacy Projects1-3, CPM Projects4-5, and
            // the Group3 projects referenced by Projects4-5.
            settings.toolSettings.NamespacePrefix = string.Empty;
            using var fixture = CentralPackageManagementFixture.Create(
                includeLegacyMultiFrameworkProjects: true);
            var dirToScan = fixture.RootPath;

            var appInfo = dirToScan.ScanForProjects(settings, stubWriter);
            var argsDecoded = new ArgsDecoded(new[] { "D" }, dirToScan, stubWriter);

            //ATTEMPT
            var builder = new NuspecBuilder(settings, argsDecoded, appInfo, stubWriter);
            builder.BuildNuspecFile(dirToScan);

            //VERIFY
            dirToScan.NuspecFileExists().ShouldBeTrue();
        }

        [Fact]
        public void ParseModularMonolithApp_MultiFrameworks()
        {
            //SETUP
            var stubWriter = new StubWriteToConsole(_output);
            var settings = SettingHelpers.GetMinimalSettings();
            settings.toolSettings.NamespacePrefix = string.Empty;
            using var fixture = CentralPackageManagementFixture.Create(
                includeLegacyMultiFrameworkProjects: true);

            //ATTEMPT
            var pathToProjects = fixture.RootPath;
            var appInfo = pathToProjects.ScanForProjects(settings, stubWriter);

            //VERIFY
            foreach (var project in appInfo.AllProjects)
            {
                _output.WriteLine($"Project: {project.ProjectName}");
                foreach (var targetFramework in project.TargetFrameworks)
                {
                    _output.WriteLine($"  TargetFramework {targetFramework}");
                    foreach (var nuGet in project.NuGetPackagesByFramework[targetFramework])
                    {
                        _output.WriteLine($"       {nuGet.NuGetId}, {nuGet.Version}");
                    }
                }
            }
            // should match projects being loaded
            appInfo.AllProjects
                .Select(x => x.ProjectName)
                .OrderBy(x => x)
                .ToArray()
                .ShouldEqual(new[]
                {
                    "Group3.Project1",
                    "Group3.Project2",
                    "Group3.Project3",
                    "MultiFrameworks.Project1",
                    "MultiFrameworks.Project2",
                    "MultiFrameworks.Project3",
                    "MultiFrameworks.Project4",
                    "MultiFrameworks.Project5"
                });
            // This should be updated to match the project's .net frameworks in the .proj file of each of the projects
            appInfo.NuGetInfosDistinctByFramework.Keys.OrderBy(x => x).ToArray()
                .ShouldEqual(new[] { "net10.0", "net9.0", "netstandard2.1" });
        }
    }
}