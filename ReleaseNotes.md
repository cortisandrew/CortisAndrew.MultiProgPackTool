# Release Notes

## 10.0.2 Directory.Packages.props support

- Added nearest-ancestor Directory.Packages.props discovery and parsing.
- Added package version precedence: VersionOverride, applicable central PackageVersion, then project Version.
- Added target-framework-aware central versions, simple property expansion, Include/Update support, and attribute/element version forms.
- Added PackageVersionResolutionException when no usable version can be resolved.
- Added final non-blocking warnings when package references/dependencies resolve to multiple versions within the same target-framework dependency group; versions in different target frameworks are not treated as conflicts.
- Added the Group3 integration graph managed by MultiFrameworks.Project4 and Project5, plus comprehensive edge-case tests.
- Kept central package management inert during normal solution builds by checking in a props template and hydrating it only in isolated temporary integration-test workspaces.
- Kept all fixture PackageReference items free of normal Version metadata; the root Directory.Build.targets supplies build-only fallbacks when the test props file is absent.
- Made test fixture discovery support both repository-root and nested template/project layouts.
- Added a shared hydrator that selects the five CPM projects by name, flattens them under the temporary scan root, and can augment legacy tests with MultiFrameworks Projects1-3.
- Updated both legacy multi-framework tests to execute the complete hydrated eight-project graph.
- Fixed target-framework forwarding for unconditional PackageReferences in multi-target projects.
- Fixed nuspec test paths so callers do not need a trailing directory separator.
- Added a detailed behavior, compatibility, topology, and verification guide to README.md.



## 10.0.0

- Updated to .NET 10 because .NET 6 is not supported now

## 2.1.0

- Add code to gives a warning of different versions of the same NuGet package.

## 2.0.0

- Updated to handle projects that have multiple frameworks, e.g. <TargetFrameworks>net6.0;net7.0</TargetFrameworks>
- Updated to NET 6

## 1.1.1 (bug fix)

- Updated to the new `<icon>images\someicon.png</icon>` format
- Updated to the new `<license type="expression">MIT</license>` format
- NOTE: does not support `<license type="file">LICENSE.txt</license>` format

## 1.1.0

- Updated to the new `<icon>someicon.png</icon>` format
- Updated to the new `<license type="expression">MIT</license>` format
- NOTE: does not support `<license type="file">LICENSE.txt</license>` format

## 1.0.1

- Added symbol files to NuGet package if there (doesn't need AddSymbols to be on)
- Added copy of symbol files to NuGet cache when using the U(pdate) command.

## 1.0.0

- First release