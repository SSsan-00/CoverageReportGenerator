# CoverageReportGenerator

C# / WinForms で作成した、JetBrains dotCover DetailedXML からオフライン閲覧できるHTMLカバレッジレポートを生成するツールです。

主な対象は Razor Pages プロジェクトです。`.csproj` を読み込み、Roslyn AST解析でソース上のメソッドや行範囲を把握したうえで、dotCover XMLのStatement情報をHTMLに変換します。
プロジェクト解析は必ず `.csproj` ファイルの選択を起点に行います。

## 機能

- `.csproj` と dotCover DetailedXML を選択してHTMLレポートを生成します。
- プロジェクト全体、フォルダ配下、単一ファイルを解析対象にできます。
- フォルダを選択した場合は、配下のファイルを再帰的に対象にします。
- セミコロン区切りのワイルドカードでファイルをフィルタリングできます。
- `.csproj` のソース検出結果と Roslyn のメンバー解析結果を `%LOCALAPPDATA%\CoverageReportGenerator\cache` にキャッシュします。
- CSS/JavaScriptを埋め込んだ単一HTMLファイルを生成します。
- Summary、Rankings、Coverage Tree、Members、Files、Source、Raw dotCover method names を表示します。
- メンバー名やTree行をクリックすると、対象ソース行へジャンプします。
- 行状態は Covered、Uncovered、No Data の3種類のみを表示します。

## 対応XML

最初に対応する入力は dotCover DetailedXML です。実質的に次のような構造を想定しています。

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

XML内の要素順には依存しません。`FileIndices/File` と `Statement` はXMLツリーから探索します。

必須属性:

- `File Index`
- `File Name`
- `Statement FileIndex`
- `Statement Line`
- `Statement Covered`

`Assembly`、`Namespace`、`Type`、`Method` は存在する場合に利用します。階層情報が欠けている場合は `Unknown` として扱います。

## 開発

```powershell
dotnet restore
dotnet build
dotnet test
```

## 単一EXEの作成

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

実行ファイルは次のフォルダに出力されます。

```text
src/CoverageReportGenerator.WinForms/bin/Release/net9.0-windows/win-x64/publish/
```

出力ファイル名は `CoverageReportGenerator.exe` です。

## 単一PowerShellファイルからのBootstrap

リポジトリをcloneできないユーザー向けに、`bootstrap/CoverageReportGenerator.Bootstrap.ps1` を用意しています。

このファイルはpublicリポジトリのzipをダウンロードし、WinFormsアプリをpublishします。テストプロジェクトはビルド対象に含めません。

.NET 9 SDK が入っていれば、コンソールプロジェクトを作らずに実行できます。

```powershell
.\bootstrap\CoverageReportGenerator.Bootstrap.ps1 -Output .\dist
```

ローカルソースを指定する場合:

```powershell
.\bootstrap\CoverageReportGenerator.Bootstrap.ps1 -Source C:\work\CoverageReportGenerator -Output .\dist
```

## プロジェクト構成

```text
src/
  CoverageReportGenerator.Core/
  CoverageReportGenerator.WinForms/
tests/
  CoverageReportGenerator.Core.Tests/
bootstrap/
  CoverageReportGenerator.Bootstrap.ps1
  CoverageReportGenerator.Bootstrap.cs
```

## ライセンス

MIT
