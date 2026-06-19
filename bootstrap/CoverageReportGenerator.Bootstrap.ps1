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

    Write-Host "CoverageReportGenerator was published to: $outputPath"
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
