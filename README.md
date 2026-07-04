# CoverageReportGenerator

C# / WinForms で作成した、JetBrains dotCover DetailedXML から HTML / Excel カバレッジレポートを生成するツールです。

主な対象は Razor Pages プロジェクトです。`.csproj` を読み込み、Roslyn AST 解析でソース上のメンバー定義位置を把握したうえで、dotCover XML の `Statement` 情報をレポートへ変換します。

## ドキュメント

- [ユーザー向けガイド](docs/USER_GUIDE.md)
  - アプリの使い方
  - 対象範囲の選び方
  - HTML / Excel レポートの見方
  - Bootstrap での導入
- [開発者向けガイド](docs/DEVELOPER_GUIDE.md)
  - プロジェクト構成
  - レポート生成フロー
  - テストと検証
  - publish / bootstrap の扱い

## 主な機能

- `.csproj` と dotCover DetailedXML を指定して HTML レポートを生成
- プロジェクト全体、フォルダ配下、単一ファイルを解析対象に指定
- フォルダツリーから対象フォルダを直感的に選択
- ファイル一覧プレビューを入力文字列で絞り込み
- ファイル一覧の行クリックで単一ファイル対象へ切り替え
- 前回入力内容の保存と初期化
- プロジェクト解析結果のキャッシュ
- UTF-8 / UTF-16 / Shift_JIS / CP932 の XML とソース読み取り
- 視覚的な HTML カバレッジ表示
- メンバー名クリックによるソース行ジャンプ
- 単一ファイルの Excel レポート出力
- Excel 上での未カバー行 `※` 表示とメンバー定義行リンク

## 動作環境

- Windows
- .NET 8 SDK
- dotCover DetailedXML を出力できる環境

通常利用では `CoverageReportGenerator.exe` を起動します。開発や bootstrap 実行には .NET 8 SDK が必要です。

## 開発コマンド

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

## 単一 EXE の作成

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

出力ファイル名は `CoverageReportGenerator.exe` です。

## Bootstrap

リポジトリを clone できないユーザー向けに、単一 PowerShell ファイルの bootstrap を用意しています。

```powershell
.\bootstrap\CoverageReportGenerator.Bootstrap.ps1 -Output .\dist
```

bootstrap は public リポジトリの zip をダウンロードし、WinForms アプリを publish します。出力先には `CoverageReportGenerator.exe` と、テストコードを含む `source/` フォルダを作成します。

bootstrap の成果物には `bootstrap/`、`docs/`、`README.md` は含めません。bootstrap に関する説明は、このリポジトリ側のドキュメントだけに残します。

## ライセンス

MIT
