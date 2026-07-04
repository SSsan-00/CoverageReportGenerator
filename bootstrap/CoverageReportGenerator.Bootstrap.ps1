[CmdletBinding()]
param(
    [string]$Source,
    [string]$RepoZip = "https://github.com/SSsan-00/CoverageReportGenerator/archive/refs/heads/main.zip",
    [string]$Output = (Join-Path (Get-Location) "dist"),
    [switch]$KeepTemp
)

$ErrorActionPreference = "Stop"
try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
}
catch {
    # Encoding setup is best-effort for older hosts.
}

$workRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("CoverageReportGenerator.Bootstrap." + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $workRoot | Out-Null

function Invoke-Dotnet {
    param([string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE."
    }
}

function Get-SourceRoot {
    if (-not [string]::IsNullOrWhiteSpace($Source)) {
        return (Resolve-Path -LiteralPath $Source).Path
    }

    $zipPath = Join-Path $workRoot "source.zip"
    $extractPath = Join-Path $workRoot "source"
    Invoke-WebRequest -Uri $RepoZip -OutFile $zipPath
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractPath -Force

    $roots = Get-ChildItem -LiteralPath $extractPath -Directory
    if ($roots.Count -eq 1) {
        return $roots[0].FullName
    }

    return $extractPath
}

function Test-IsChildPath {
    param(
        [string]$ChildPath,
        [string]$ParentPath
    )

    $childFullPath = [System.IO.Path]::GetFullPath($ChildPath)
    $parentFullPath = [System.IO.Path]::GetFullPath($ParentPath)
    if (-not $parentFullPath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $parentFullPath += [System.IO.Path]::DirectorySeparatorChar
    }

    return $childFullPath.StartsWith($parentFullPath, [System.StringComparison]::OrdinalIgnoreCase)
}

function Copy-SourceTree {
    param(
        [string]$SourceRoot,
        [string]$TargetRoot,
        [string]$OutputRoot
    )

    $sourceFullPath = (Resolve-Path -LiteralPath $SourceRoot).Path
    $targetFullPath = [System.IO.Path]::GetFullPath($TargetRoot)
    $outputFullPath = [System.IO.Path]::GetFullPath($OutputRoot)
    if (-not (Test-IsChildPath -ChildPath $targetFullPath -ParentPath $outputFullPath)) {
        throw "Source target must be inside the output directory: $targetFullPath"
    }

    if (Test-Path -LiteralPath $targetFullPath) {
        Remove-Item -LiteralPath $targetFullPath -Recurse -Force
    }

    $excludedDirectoryNames = @(".git", ".vs", "bin", "obj", "artifacts", "bootstrap", "docs")
    $excludedFileNames = @("README.md")
    New-Item -ItemType Directory -Force -Path $targetFullPath | Out-Null

    function Copy-DirectoryContent {
        param(
            [string]$CurrentSource,
            [string]$CurrentTarget
        )

        foreach ($item in Get-ChildItem -LiteralPath $CurrentSource -Force) {
            if ($item.PSIsContainer) {
                $itemFullPath = [System.IO.Path]::GetFullPath($item.FullName)
                if ($excludedDirectoryNames -contains $item.Name) {
                    continue
                }

                if ($itemFullPath.Equals($outputFullPath, [System.StringComparison]::OrdinalIgnoreCase) -or
                    $itemFullPath.Equals($targetFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                    continue
                }

                $nextTarget = Join-Path $CurrentTarget $item.Name
                New-Item -ItemType Directory -Force -Path $nextTarget | Out-Null
                Copy-DirectoryContent -CurrentSource $item.FullName -CurrentTarget $nextTarget
                continue
            }

            if ($excludedFileNames -contains $item.Name) {
                continue
            }

            Copy-Item -LiteralPath $item.FullName -Destination (Join-Path $CurrentTarget $item.Name) -Force
        }
    }

    Copy-DirectoryContent -CurrentSource $sourceFullPath -CurrentTarget $targetFullPath
}

try {
    $sourceRoot = Get-SourceRoot
    $projectPath = Join-Path $sourceRoot "src\CoverageReportGenerator.WinForms\CoverageReportGenerator.WinForms.csproj"
    if (-not (Test-Path -LiteralPath $projectPath)) {
        throw "WinForms project was not found: $projectPath"
    }

    $outputPath = [System.IO.Path]::GetFullPath($Output)
    New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

    Invoke-Dotnet @(
        "publish",
        $projectPath,
        "--configuration", "Release",
        "--runtime", "win-x64",
        "--self-contained", "true",
        "/p:PublishSingleFile=true",
        "/p:PublishTrimmed=false",
        "/p:EnableCompressionInSingleFile=true",
        "/p:IncludeNativeLibrariesForSelfExtract=true",
        "/p:DebugType=None",
        "/p:DebugSymbols=false",
        "-o", $outputPath
    )

    $sourceOutputPath = Join-Path $outputPath "source"
    Copy-SourceTree -SourceRoot $sourceRoot -TargetRoot $sourceOutputPath -OutputRoot $outputPath

    Write-Host "CoverageReportGenerator was published to: $outputPath"
    Write-Host "Source files were copied to: $sourceOutputPath"
}
finally {
    if (-not $KeepTemp -and (Test-Path -LiteralPath $workRoot)) {
        $resolvedWorkRoot = (Resolve-Path -LiteralPath $workRoot).Path
        $resolvedTemp = (Resolve-Path -LiteralPath ([System.IO.Path]::GetTempPath())).Path
        if ($resolvedWorkRoot.StartsWith($resolvedTemp, [System.StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolvedWorkRoot -Recurse -Force
        }
    }
}
