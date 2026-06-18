# CoverageReportGenerator

C# WinForms tool that reads JetBrains dotCover DetailedXML and generates an offline HTML coverage report for Razor Pages projects.

## Features

- Select a `.csproj` and a dotCover DetailedXML file.
- Analyze project, folder, or single-file scopes.
- Recursively include files under a selected folder.
- Filter files with semicolon-separated wildcard patterns.
- Cache `.csproj` source discovery and Roslyn member analysis under `%LOCALAPPDATA%\CoverageReportGenerator\cache`.
- Generate a single self-contained HTML report with embedded CSS and JavaScript.
- Show Summary, Rankings, Coverage Tree, Members, Files, Source, and Raw dotCover method names.
- Jump from a member or tree row to the matching source line.
- Display only Covered, Uncovered, and No Data line states.

## Current XML Scope

The first supported input is dotCover DetailedXML with this effective shape:

```text
Root
  FileIndices
    File
  Assembly
    Namespace
      Type
        Method
          Statement
```

Element order is not significant. `FileIndices/File` and `Statement` are discovered from the XML tree.

Required attributes:

- `File Index`
- `File Name`
- `Statement FileIndex`
- `Statement Line`
- `Statement Covered`

`Assembly`, `Namespace`, `Type`, and `Method` are used when available. Missing optional hierarchy values are shown as `Unknown`.

## Development

```powershell
dotnet restore
dotnet build
dotnet test
```

## Publish Single EXE

```powershell
dotnet publish src\CoverageReportGenerator.WinForms\CoverageReportGenerator.WinForms.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:PublishTrimmed=false `
  /p:EnableCompressionInSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true
```

The executable is written under:

```text
src/CoverageReportGenerator.WinForms/bin/Release/net8.0-windows/win-x64/publish/
```

The published file is `CoverageReportGenerator.exe`.

## Bootstrap From One C# File

For users who cannot clone the repository, `bootstrap/CoverageReportGenerator.Bootstrap.cs` can download the public repository archive and publish the WinForms app. It does not build or include test projects.

With .NET 10 SDK:

```powershell
dotnet run bootstrap\CoverageReportGenerator.Bootstrap.cs -- --output .\dist
```

Optional local source mode:

```powershell
dotnet run bootstrap\CoverageReportGenerator.Bootstrap.cs -- --source C:\work\CoverageReportGenerator --output .\dist
```

## Project Layout

```text
src/
  CoverageReportGenerator.Core/
  CoverageReportGenerator.WinForms/
tests/
  CoverageReportGenerator.Core.Tests/
bootstrap/
  CoverageReportGenerator.Bootstrap.cs
```

## License

MIT
