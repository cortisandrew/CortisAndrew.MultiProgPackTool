# `MultiProjPack` Tool

This `MultiProjPack` tool is designed to create a NuGet package that has multiple projects (assemblies) in it. I created this tool because (currently) you can't do that via NuGet settings in a .csproj file. The documenation for this `dotnet tool` are in this README file and the [Release Notes file](https://github.com/JonPSmith/MultiProgPackTool/blob/main/ReleaseNotes.md) tells you what has happened in each version.

This is a open-source project under the MIT licence.

## Why was this dotnet tool created

This was created initially to allow you to break up a large application into sub-application based on the Domain-Driven Design (DDD)'s [bounded context](https://martinfowler.com/bliki/BoundedContext.html) approach, and turn them into NuGet packages. But now I use it for the library [AuthPermissions.AspNetCore](https://github.com/JonPSmith/AuthPermissions.AspNetCore) which has multiple projects. Version 2.0.0 supports multiple frameworks, e.g `<TargetFrameworks>netstandard2.1;net6.0;net7.0</TargetFrameworks>`

This tool will scan your code to create a .nuspec file, from which it will create a NuGet package containing multiple projects. The tool also has a number of features than improve the development process by using a local NuGet package source.

*NOTE: I cover this bounded context sub-application approach in [part 2 of my evolving modular monolith](#) series.*

This modular approach has three benefits:

- It breaks up a complex application into bounded context sub-applications, which are managed via NuGet packages.
- It allows a development team to work on a bounded context sub-application separately for the main application.
- If you are adding a new feature an old application which is hard to extend, then you can build a separate solution using a more modern design and inset that package into your existing application via a NuGet package.

I build the `MultiProjPack` tool because the normal NuGet tools don't handle certain things, for instance:

1. The normal tools assume a main project, with references to sub-projects. But in the bounded context case you have multiple projects and not all of them are linked to a single, main project. In fact, some projects may not be referenced directly by any project beacause they are only accessed via dependency injection.
2. The normal tools don't concatenate the NuGet packages referenced by the projects.
3. The the `MultiProjPack` tool can add some features that speeds up the local testing of the created NuGet package - see [part 4 of my evolving modular monolith](#) series, which is about testing when using this bounded context/NuGet package approach.


See [Release Notes](https://github.com/JonPSmith/MultiProgPackTool/blob/main/ReleaseNotes.md) file for changes to this tool.

## Overview of how to use the `MultiProjPack` tool

The `MultiProjPack` tool is what is known as a [.NET tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools) and is run from the command line

### 1. Install the `MultiProjPack` tool

```console
dotnet tool install JonPSmith.MultiProjPack --global
```

### 2. Organize the your code by namespaces

The `MultiProjPack` tool finds the projects to combine by the projects' name/namespace. It will find all the projects with a name staring with a given string and combine them into a single NuGet package.

 For instance, in [part 1 of my evolving modular monolith](#) I describe a naming convention where the namespace is `<NameOfApp>.<BoundedContextName>.<other-namespaces...>`, and below is an example of an application called `BookApp` and a bounded context of `Books`. These would

```c#
//Bounded context called "Books" in the application "BookApp"
BookApp.Books.Domain
BookApp.Books.Infrastructure.CachedValues
BookApp.Books.Infrastructure.Seeding
BookApp.Books.Persistence
BookApp.Books.ServiceLayer.Cached
BookApp.Books.ServiceLayer.Common
//etc...
//New bounded context called "Orders"
BookApp.Orders.Domain       
//etc...
```

By telling the `MultiProjPack` tool to look for namespaces starting with "`BookApp.Books.`" it would create a NuGet packages that contains all the projects with names starting with "`BookApp.Books.`", and leaving out any other projects, such as the `BookApp.Orders...` projects.

### 3. Create a file containing the NuGet package info

To create a NuGet package you need to define some NuGet information, such as the name (id) of the created NuGet package, its version, its authors and so on.

The `MultiProjPack` tool looks for a settings file called `MultiProjPack.xml`, which contains NuGet info and settings for the `MultiProjPack` tool. Here is a link to a [example MultiProjPack.xml settings file](https://github.com/JonPSmith/MultiProgPackTool/blob/main/MultiProjPackTool/SettingHandling/TypicalMultiProjPack.xml), but I give more information on the `MultiProjPack.xml` file later (LINK!!!)

*NOTE: The `MultiProjPack` tool will create an `MultiProjPack.xml` settings file for you, with the typical settings that you should fill in. See !!!LINK on how to do that.*

The `MultiProjPack.xml` setting file should be placed in the directory containing the projects in your solution, for instance `C:\Users\YourUser\source\repos\BookApp.Books>`

### 4. Run the `MultiProjPack` tool

You run the `MultiProjPack` tool from a command line in the directory containing the projects in your solution.

*NOTE: If you want to create a new version of the NuGet package you must update the NuGet `version` and most likely the `releaseNotes` in the `MultiProjPack.xml` before you call the `MultiProjPack` tool.*

```console
MultiProjPack <what to do>
```

Where `<what to do>` is either:

- `D`(ebug): This creates a NuGet package using the `Debug` version of the code.
- `R`(elease): This creates a NuGet package using the `Release` version of the code.
- `U`(pdate): This builds a NuGet package using the `Debug` code, but also updates the `.dll`s in the NuGet cache - See section [???](???) for more of this.

The `MultiProjPack` tool does the following:

1. Finds all the projects to go into the NuGet package
2. Creates/updates a .nuspec file with the latests information> The .nuspec will contain 
    - The `.dll` of each project.
    - The xml documentation file if present.
    - If the settings says a symbol output is requires, then any symbol file (.pdb) will be added.
    - If the `MultiProjPack`'s settings says a symbol output is required, then any symbol file (.pdb) will be added.
3. Calls `dotnet pack` to create the NuGet package
4. Optionally it copies it to a local NuGet package server directory.

*NOTE: You can override settings in the `MultiProjPack.xml` file via the `-m:name=value` (NuGet metadata settings) or `-t:name=value` (toolSettings). See section ??? for more*

### 5. Use the generated NuGet package

At this point you have a new or updated NuGet package. You can use the package locally to check it works with the main application, or push it to a private NuGet server for others to use.

---

# the `MultiProjPack`'s settings and options

Now we look at the settings and options that MultiProjPack tool has.

1. The `MultiProjPack.xml` settings file, which holds the NuGet information and extra setting for the `MultiProjPack` tool.
2. The MultiProjPack tool parameters and options used when you run the `MultiProjPack` tool.

## 1. The `MultiProjPack.xml` settings file content

The `MultiProjPack.xml` settings file has two sections

```xml
<allsettings>
  <metadata>
    <!-- this contains the NuGet settings -->
  </metadata>
  <toolSettings>
    <!-- this contains the tool settings -->
  </toolSettings>
</allsettings>
```

### 1. The `<metadata>` section

I assume you know how to define a NuGet package. If you don't then see this [create a NuGet package document](https://docs.microsoft.com/en-us/nuget/create-packages/creating-a-package-msbuild).

This holds the the information that NuGet needs when building a package. This follows the [nuspec metadata format](https://docs.microsoft.com/en-us/nuget/reference/nuspec) because the `MultiProjPack` tool create a `.nuspec` file.

*NOTE: The `<metadata>` section uses same commands as you would add to a .csproj file to create a NuGet package, but the xml names are camelCase, while the .csproj version is PascalCase.**

The [this file](https://github.com/JonPSmith/MultiProgPackTool/blob/main/MultiProjPackTool/SettingHandling/EveryPropertyFilledIn.xml) contains ALL the possible settings. 

### 2. The `<toolSettings>` section

This contains options that are particular to the `MultiProjPack` tool and control the the tool works. Here is each list of the the options, with the most useful first.

*NOTE: unlike the NuGet settings every tool setting has a default value.*

#### `<NamespacePrefix>` - defines the namespace to look for (optional)

The `MultiProjPack` tool will look for projects who's name starts with `$"{NamespacePrefix}."`. An example would be `<NamespacePrefix>BookApp.Books</NamespacePrefix>`.

*NOTE: If you don't fill this in, or leave the setting out it will use the NuGet `<id>` from the `<metadata>` section.*

#### `<ExcludeProjects>` - excluding certain projects

If you have projects starting with the `<NamespacePrefix>`, but you don't want them included in the NuGet package, then you can define them in this setting as a comma delimited list of the ending of their name.

For example, assuming the `<id>` or `<NamespacePrefix>` is set to "`BookApp.Books`" then a setting of `<ExcludeProjects>Test,AnotherProject</ExcludeProjects>` would exclude the `BookApp.Books.Test` and `BookApp.Books.AnotherProject` projects from the NuGet package.

#### `<CopyNuGetTo>` - used to auto-copy new NuGet to local NuGet source

The NuGet package is created in the directory the the `MultiProjPack` tool was run in. That's OK, but its useful to have your new NuGet available on your dev computer for local testing, and (via Visual Studio's NuGet Package Manager ) you can have a local NuGet package source so that you can immediately add it to your test application.

You need to set up a local NuGet package source (which I explain in [part 4 of my evolving modular monolith](#) series on testing or this [useful article](https://spin.atomicobject.com/2021/01/05/local-nuget-package/)) and then fill the `<CopyNuGetTo>` setting with the local NuGet package source directory. When you run the `MultiProjPack` tool it will automatically copy your new NeGet package to your local NuGet package source.

For example, as  settings of ` <CopyNuGetTo>{USERPROFILE}\LocalNuGet</CopyNuGetTo>` would refer to a directory called `LocalNuGet` in the user's directory.

NOTE: The `{USERPROFILE}` is optional, but makes this setting work for any user. `USERPROFILE` is an environment value that contains your user directory - for me that's `C:\User\JonPSmith`.

#### `<AddSymbols>` - will add a symbols package

When you are unit testing its useful to have the full information for unit testing. Personally I prefer embedding the code into the `.dll` file when in Debug mode (I explain why in [part 4 of my evolving modular monolith](#) series on testing).

But if you want NuGet to create a symbol package then you need to a) add `<IncludeSymbols>true</IncludeSymbols>` in all of your projects and b) set the correct setting for the `<AddSymbols>` setting. The possible values for the `<AddSymbols>` setting are: "None", "Debug", "Release" or "Always".

If you don't provide a value it defaults to "None".

#### NoAutoPack - Turn off the call to `dotnet pack`

By default the tool will create a .nuspec file and then call `dotnet pack` to create a NuGet package. If you don't want the tool to call `dotnet pack`, then set this to `false`.

This can be useful if you want to hand-edit the created .nuspec file before you pack it.

#### `<NuGetCachePath>` - override default cache path settings

The U(pdate) command will replace all the `.dll`s/etc. files in the NuGet cache in the computer you are developing on. It uses the environment variables to get the default path to the NuGet cache (Windows/Linux/Mac). But if your system has a different path, then you should set this value.

*NOTE: THis does NOT use the `NUGET_PACKAGES` enviroment value. If you have changes the default cache path, then you must set this setting.*

## 2. The `MultiProjPack` tool options

### Help: i.e. `-h | --help`

This is pretty obvious - it outputs a list of the commands and options.

### Create `MultiProjPack.xml` settings file

the `--CreateSettings` command will create an example `MultiProjPack.xml` setting file in the current directory. This xml file contains the typical settings you should need to set, but you can add other settings as required.

### Override NuGet metadata settings

The `-m:` command allows you to override any NuGet setting in the `<metadata>` part of the `MultiProjPack.xml` settings file, e.g.

`-m:version=1.0.0.1-preview001`

NOTE: Any NuGet settings that contain spaces must be wrapped in a string, e.g. `-m:releaseNotes="This release fixes issues #1 and #2"`

### Override tool settings

The `-t:` command allows you to override any tools setting in the `<toolSettings>` part of the `MultiProjPack.xml` settings file, e.g.

`-t:ExcludeProjects=Test,AnotherProject`


# Central package management with Directory.Packages.props

The project parser supports NuGet Central Package Management (CPM). This section is the detailed implementation and review guide for that update.

## What changed

When `MultiProjPack` scans the solution directory for projects, it now also looks for `Directory.Packages.props`. Every parsed `PackageReference` is assigned a concrete version before the generated nuspec dependency groups are built. The selected version and its source are retained in `NuGetInfo` as `Version` and `VersionSource`.

This behavior is backward compatible for solutions that do not use central package management: a project-local `Version` continues to be used.

## Directory.Packages.props discovery

Discovery starts in the directory passed to `ScanForProjects` (the solution/project-group scan directory) and walks upward one directory at a time. The first `Directory.Packages.props` found is used, matching the nearest-file convention used by .NET central package management.

- A file in the scan directory takes precedence over a file in its parent.
- Parent directories continue to be searched until the filesystem root.
- Only the nearest file is parsed automatically. Parent files can still be incorporated by an explicit MSBuild import, but this parser does not execute imports.
- The selected absolute path is exposed as `AppStructureInfo.DirectoryPackagesPropsPath`.
- If no file is found, `DirectoryPackagesPropsPath` is `null` and normal project-local version handling continues.

## Package version precedence

Versions are resolved independently for every package reference and active target framework, in this order:

| Priority | Source | Behavior |
| --- | --- | --- |
| 1 | `PackageReference VersionOverride` | Always wins when it contains a non-empty value, whether or not the package also exists in `Directory.Packages.props`. |
| 2 | Matching `PackageVersion` in `Directory.Packages.props` | Wins over a normal project `Version`. Package IDs are matched case-insensitively. A target-framework condition must apply to the active framework. |
| 3 | `PackageReference Version` | Used when no applicable central entry exists. This also preserves behavior when no central file exists. |
| 4 | No version source | Throws `PackageVersionResolutionException` and stops parsing before nuspec creation or packing can continue. |

For example:

```xml
<!-- Directory.Packages.props -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <JsonVersion>13.0.3</JsonVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Newtonsoft.Json" Version="$(JsonVersion)" />
  </ItemGroup>
</Project>
```

```xml
<!-- Uses 13.0.3 from Directory.Packages.props -->
<PackageReference Include="Newtonsoft.Json" />

<!-- Uses 13.0.4 because VersionOverride has highest priority -->
<PackageReference Include="Newtonsoft.Json" VersionOverride="13.0.4" />

<!-- Uses 1.2.3 only when No.Central.Entry has no applicable PackageVersion -->
<PackageReference Include="No.Central.Entry" Version="1.2.3" />
```

## Supported XML forms

The parser handles the following forms:

- `PackageVersion Include="Id" Version="1.2.3"`.
- `PackageVersion Update="Id" Version="1.2.3"`.
- A child `<Version>1.2.3</Version>` element instead of a `Version` attribute.
- Simple `$(PropertyName)` references and chained property references declared in `PropertyGroup` elements in the same file.
- `PackageReference Version` as either an attribute or child element.
- `PackageReference VersionOverride` as either an attribute or child element.
- Package IDs with different casing in the project and central file.
- Target-framework equality conditions on either the parent `ItemGroup` or `PackageVersion` itself. Multiple equality clauses joined with `or` are treated as alternatives.

Central declarations are retained in file order and the last applicable declaration wins, mirroring MSBuild item evaluation order. Repeating the same package and condition with different versions produces a non-blocking warning explaining that the last declaration was selected.

This is an XML parser, not a complete MSBuild evaluation engine. It intentionally does not evaluate arbitrary MSBuild functions, imports, `and` expressions involving unrelated properties, or conditional property assignment. An unresolved version property is treated as unusable and produces a resolution exception rather than writing an invalid version into the nuspec.

## Multi-target framework behavior

The active target framework is passed into central version resolution. This allows one package to use different centrally managed versions for different targets:

```xml
<ItemGroup Condition=" '$(TargetFramework)' == 'net9.0'">
  <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
</ItemGroup>
<ItemGroup Condition=" '$(TargetFramework)' == 'net10.0'">
  <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
</ItemGroup>
```

An unconditional project package reference resolves to `9.0.0` while parsing `net9.0` and `10.0.0` while parsing `net10.0`. If a central declaration does not apply to the active framework, the normal project `Version` fallback is considered.

## Missing or unusable versions

`PackageVersionResolutionException` is thrown in each of these cases:

- no `VersionOverride`, no applicable central `PackageVersion`, and no project `Version`;
- a matching central entry exists but has an empty version;
- a matching central entry contains a property reference that cannot be resolved from the same props file;
- a parsed `PackageReference` does not contain an `Include` package ID.

The exception identifies the package, project, target framework when available, selected `Directory.Packages.props` path or search directory, and why no usable version could be selected. This exception stops processing before generated dependency metadata or compilation/packing can proceed.

## Multiple-version warnings

After every project and framework has been parsed, the resolved reference set is partitioned by target framework and then grouped by package ID using case-insensitive comparison. A `Warning` is emitted only when one package ID has more than one distinct version inside the same target-framework dependency group. The warning contains:

- the package ID;
- every resolved version;
- the target framework being checked;
- every project that selected each version.

These warnings are informational/non-blocking and do not increment the blocking warning count. Within one target-framework nuspec dependency group, the first parsed occurrence of a duplicate package ID remains the emitted dependency; the warning makes any disagreement in that group visible for review. Different target frameworks are separate compilation/dependency groups and are never compared with each other, so net9.0 using one version and net10.0 using another does not warn.

## Group3 and MultiFrameworks.Project4 test topology

The solution contains a buildable `Central Package Management Group`. Its central configuration is checked in as the inert `CentralPackageManagement/Directory.Packages.props.template`, so normal restore/build operations do not activate CPM for the repository. Each integration test creates a unique temporary directory, copies the five fixture project files into it, and copies the template there as the real `Directory.Packages.props` before invoking the parser. Test disposal removes that hydrated workspace.

```text
MultiFrameworks.Project4 (net9.0; net10.0)
??? Group3.Project3 (net9.0; net10.0)
    ??? Group3.Project2 (net9.0; net10.0)
        ??? Group3.Project1 (net9.0; net10.0)

MultiFrameworks.Project5 (net9.0; net10.0)
??? Group3.Project2
```

The projects intentionally cover these real solution behaviors:

| Project | Central-package behavior |
| --- | --- |
| `Group3.Project1` | Uses central `Newtonsoft.Json 13.0.3` through a property reference. |
| `Group3.Project2` | Overrides that package with `VersionOverride="13.0.4"`. |
| `Group3.Project3` | Uses target-specific central Logging Abstractions versions. |
| `MultiFrameworks.Project4` | Manages/references the complete Group3 chain through Project3 and consumes target-specific central versions. |
| `MultiFrameworks.Project5` | Manages/references Group3 through Project2 and exercises the override path independently. |

When hydrated in the isolated test workspace, this graph produces intentional multi-version diagnostics for both an override conflict and a target-framework-specific dependency. Every checked-in fixture `PackageReference` deliberately omits normal `Version` metadata; only the two intentional `VersionOverride` values remain. `CentralPackageManagement/Directory.Build.targets` supplies scoped fallback versions during ordinary solution restore/build, but only when no active `Directory.Packages.props` exists beside it. The parser tests copy only the versionless project files and hydrate the central template, so their resolved versions can come only from `Directory.Packages.props` or `VersionOverride`.

## Test coverage added

`TestDirectoryPackagesProps` covers:

1. central version selection and `VersionSource` tracking;
2. central precedence over a project `Version`;
3. case-insensitive package IDs;
4. `VersionOverride` precedence with and without a central entry;
5. attribute and child-element forms of `Version` and `VersionOverride`;
6. project `Version` fallback with a non-matching or absent central file;
7. discovery in the scan directory and a parent directory;
8. nearest-file precedence;
9. `PackageVersion Include` and `Update`;
10. central version child elements and chained properties;
11. target conditions on `ItemGroup` and `PackageVersion`;
12. target-specific fallback to a project version;
13. missing versions with and without a central file;
14. empty and unresolved central versions;
15. duplicate central declarations and last-applicable selection;
16. no warning when all resolved versions agree;
17. a warning when projects resolve one package to multiple versions inside the same target framework;
18. no warning when versions differ only between separate target frameworks;
19. the full Group3 graph managed by `MultiFrameworks.Project4` and `Project5`;
20. the checked-in fixture remains inert, while a temporary test copy contains a hydrated `Directory.Packages.props` and all five project files;
21. every checked-in fixture `PackageReference` lacks ordinary `Version` metadata, while the two intended `VersionOverride` entries remain;
22. fixture discovery works for both repository-root and arbitrarily nested template/project layouts;
23. the legacy multi-framework parser and nuspec tests run Projects1-5 together with the three required Group3 projects under hydrated CPM.

The console test stub now retains every warning message so tests assert diagnostic content, not only a warning count.

## Files changed by this feature

- `CentralPackageVersions.cs`: nearest-file discovery, XML parsing, property expansion, conditional central entry selection, and duplicate-definition diagnostics.
- `NuGetInfo.cs` and `PackageVersionSource.cs`: precedence resolution and source tracking.
- `PackageVersionResolutionException.cs`: explicit stop condition for unresolved package versions.
- `ProjectXmlFormat.cs`: `VersionOverride` plus attribute/element version shapes.
- `FilterNuGetsByCondition.cs` and `ProjectsParser.cs`: pass the central map and active target framework through parsing.
- `AppStructureInfo.cs`: final per-target-framework multiple-version analysis and selected props path.
- `TestDirectoryPackagesProps.cs` and `StubWriteToConsole.cs`: edge-case tests and warning capture.
- `CentralPackageManagement/*` and `MultiProgPackTool.sln`: versionless Group3/Project4/Project5 integration topology, an inert props template hydrated only in temporary tests, and a scoped `Directory.Build.targets` fallback that keeps ordinary solution builds valid.

## Reproducing verification

The test project builds as a test-library assembly. Build the solution, then run that assembly through VSTest:

```console
dotnet build MultiProgPackTool.sln --configuration Debug
dotnet vstest Test/bin/Debug/net10.0/Test.dll
```

On .NET 10, the repository's current test-platform configuration does not support the legacy VSTest-based `dotnet test MultiProgPackTool.sln` path; running the test project is the supported equivalent.


# Updating the MultiProjPack dotnet tool

The MultiProjPack dotnet tool uses .NET target framework, which means at some time the MultiProjPack won't work when the MultiProjPack's current .NET isn't supported. This happened in 2025 because the current MultiProjPack uses .NET 6 and once .NET 10 came out you get an error and you can’t use it.

It turned out that dotnet tool works a different way to the normal NuGet, and because I hadn’t look at for four years and in this time, I got Alzheimer's disease too. It took a long time to work out what, and I only got there from help from other clever people to point in the right way.

So, there is the steps you must do to update MultiProjPack dotnet tool, and recommend that you update on ever even .NET, .NET 12, NET 14, and on. The example below shows how to update to .NET 12

## 1. Update the MultiProjPackTool.csprog

### 1.1 Change the TargetFramework to the new .NET, e.g. <TargetFramework>net12.0</TargetFramework> 

### 1.2 Update the NuGet's version(s), e.g.

```
<Version>12.0.0</Version>
<AssemblyVersion>12.0.0</AssemblyVersion>
<FileVersion>12.0.0</FileVersion>
```

### 1.3 Update the <PackageReleaseNotes>, e.g,

```
<PackageReleaseNotes>
    - Updated to .NET 12
</PackageReleaseNotes>
```

## 2. Build the JonPSmith.MultiProjPack in release mode
This will create a NuGet called "JonPSmith.MultiProjPack.12.0.0" in the ```...\MultiProgPackTool\MultiProjPackTool\DotNetTool``` folder.

## 3. Upload the JonPSmith.MultiProjPack.12.0.0 file to the https://www.nuget.org/packages/manage/upload
 
NOTE: Unit tests for multi-frameworks projects, e.g. net9.0;net10.0, don't work and I can't get it to work at this time.

[END]
