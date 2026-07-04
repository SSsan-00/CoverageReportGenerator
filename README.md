# CoverageReportGenerator

C# / WinForms で作成した、JetBrains dotCover DetailedXML からオフライン閲覧できるカバレッジレポートを生成するツールです。

主な対象は Razor Pages プロジェクトです。`.csproj` を読み込み、Roslyn AST解析でソース上のメソッドや行範囲を把握したうえで、dotCover XML の Statement 情報を HTML または Excel に変換します。プロジェクト解析は必ず `.csproj` ファイルの選択を起点に行います。

## ドキュメント

- [利用者向けガイド](docs/USER_GUIDE.md)
  - アプリの使い方
  - dotCover XML の用意
  - HTML / Excel レポートの見方
  - よくあるトラブル
- [開発者向けガイド](docs/DEVELOPER_GUIDE.md)
  - プロジェクト構成
  - レポート生成の内部フロー
  - テストと検証方針
  - publish / bootstrap の扱い

## 主な機能

- `.csproj` と dotCover DetailedXML を選択して HTML レポートを生成
- プロジェクト全体、フォルダ配下、単一ファイルを解析対象に指定
- フォルダ配下の再帰解析
- セミコロン区切りのワイルドカードによる対象ファイルフィルタ
- 前回入力内容の保存と復元
- 初期化ボタンによる設定リセット
- プロジェクト解析結果のキャッシュ
- UTF-8、UTF-16、Shift_JIS / CP932 の XML とソース読み取り
- HTML レポートの視覚的なカバレッジバー表示
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

出力ファイル名は `CoverageReportGenerator.exe` です。

## 単一PowerShellファイルからのBootstrap

リポジトリを clone できないユーザー向けに、`bootstrap/CoverageReportGenerator.Bootstrap.ps1` を用意しています。

```powershell
.\bootstrap\CoverageReportGenerator.Bootstrap.ps1 -Output .\dist
```

このスクリプトは public リポジトリの zip をダウンロードし、WinForms アプリを publish します。出力先には `CoverageReportGenerator.exe` と、テストコードを含む `source/` フォルダを作成します。

## ライセンス

MIT
