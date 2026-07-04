# 開発者向けガイド

このドキュメントは CoverageReportGenerator を修正、拡張、検証する開発者向けのメモです。

## 開発環境

- Windows
- .NET 8 SDK
- PowerShell
- dotCover DetailedXML を生成できる環境

テストフレームワークは MSTest です。

```powershell
dotnet restore
dotnet build
dotnet test
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
docs/
  USER_GUIDE.md
  DEVELOPER_GUIDE.md
```

### CoverageReportGenerator.Core

レポート生成の中心です。

主な責務:

- `.csproj` からソースファイルを解決
- Roslyn AST からメンバー情報を抽出
- dotCover DetailedXML を解析
- dotCover の Statement とソースファイルを対応付け
- HTML / Excel レポートモデルを生成
- HTML / Excel ファイルへ描画

主な依存パッケージ:

- `Microsoft.CodeAnalysis.CSharp`
- `ClosedXML`
- `System.Text.Encoding.CodePages`

### CoverageReportGenerator.WinForms

Windows Forms UI です。

主な責務:

- `.csproj` 選択
- DotCover XML 選択
- 対象範囲とフィルタ入力
- 対象プレビュー表示
- HTML 生成
- Excel 出力対象ファイル選択
- 入力設定の保存と復元

### CoverageReportGenerator.Core.Tests

Core のユニットテストです。

主な検証対象:

- dotCover XML パース
- Shift_JIS / CP932 XML とソースの読み取り
- レポートモデル生成
- dotCover 公式 HTML に合わせた行ハイライト
- HTML 出力
- Excel 出力
- メンバーリンクが定義行へ向くこと

## レポート生成の流れ

```text
.csproj
  -> ProjectAnalyzer
  -> SourceFile / SourceMember

dotCover DetailedXML
  -> DotCoverDetailedXmlParser
  -> DotCoverReport / DotCoverStatement

ProjectAnalysis + DotCoverReport + CoverageSelection
  -> CoverageReportBuilder
  -> CoverageReport

CoverageReport
  -> HtmlReportRenderer
  -> HTML

CoverageReport
  -> ExcelFileCoverageReportRenderer
  -> XLSX
```

## プロジェクト解析

`ProjectAnalyzer` は `.csproj` を起点にソースファイルを解決します。

解析結果は `%LOCALAPPDATA%\CoverageReportGenerator\cache` に保存します。ファイルの更新状態を見て、変更がないファイルの Roslyn 解析結果を再利用します。

メンバー抽出は `RoslynSourceMemberParser` が担当します。

抽出対象:

- メソッド
- コンストラクタ
- プロパティ
- アクセサ
- ローカル関数
- ラムダ

`SourceMember.StartLine` は定義ジャンプに使われます。文字列検索でメンバーを探すのではなく、Roslyn の SyntaxNode の位置情報を使います。

## dotCover XML 解析

`DotCoverDetailedXmlParser` は DetailedXML を解析します。

対応方針:

- XML 要素順には依存しない
- `FileIndices/File` は XML 全体から探索する
- `Statement` は XML 全体から探索する
- `Root` / `Assembly` / `Namespace` / `Type` / `Method` の集計属性は存在する場合に利用する
- 欠けている階層情報は `Unknown` として扱う
- `Column` / `EndLine` / `EndColumn` がある場合は行範囲に反映する
- Method の `CoveredStatements` / `TotalStatements` は重複 Statement の補正に使う

読み取りエンコーディング:

- XML 宣言の encoding を優先
- UTF 系を自動判定
- 宣言がない Shift_JIS / CP932 XML もフォールバックで読む

## CoverageReportBuilder

`CoverageReportBuilder` は `.csproj` 解析結果と dotCover XML を結合します。

重要な設計:

- dotCover の FileIndex をプロジェクト内ソースへ対応付ける
- 対象範囲は `CoverageSelection` で絞り込む
- フォルダ選択時は配下ファイルを再帰的に含める
- 行表示は Covered / Uncovered / NoData の3種類のみ
- 同一行に Covered / Uncovered が混在する場合、公式 HTML と同じく Uncovered を優先する
- 複数行 Statement は対象行すべてへ展開する
- record class の主コンストラクタ Statement は公式 HTML に合わせてソース行ハイライトから除外する
- Root 集計が使えるプロジェクト全体レポートでは Root の CoveragePercent を優先する

## HTML レンダリング

`HtmlReportRenderer` は単一 HTML を出力します。

特徴:

- CSS / JavaScript を埋め込む
- UTF-8 BOM 付きで保存する
- 概要、ランキング、メンバー、ファイル、ソースのタブを持つ
- カバレッジ率を数値とバーで表示する
- メンバー名クリックでソース行へジャンプする
- ファイル名やメンバー名で絞り込みできる

UI ラベルは日本語を基本にしています。ただし `Namespace`、`Type`、`Statement` など開発者に馴染みのある語は無理に日本語化しません。

## Excel レンダリング

`ExcelReportGenerationService` と `ExcelFileCoverageReportRenderer` が Excel 出力を担当します。

設計上の制約:

- Excel 出力は単一ファイル対象のみ
- シートは1枚
- ソースは B 列から下に表示する
- 未カバー行の A 列には `※` を表示する
- メンバー名セルには同一シート内リンクを設定する
- リンク先は Roslyn で取得した定義開始行

カバレッジの見た目:

- 80%以上: 緑
- 50%以上80%未満: 黄
- 50%未満: 赤
- Statement なし: グレー

メンバー名リンクは呼び出し行ではなく定義行へ向けます。テストでは、同じファイル内に `OnGet();` の呼び出しがあっても `public void OnGet()` の定義行へ飛ぶことを確認しています。

## WinForms UI

`MainForm` がメイン画面です。

主要操作:

- `.csproj` 選択
- プロジェクト再読込
- DotCover XML 選択
- 対象範囲選択
- Include / Exclude 入力
- 出力先指定
- HTML生成
- Excel出力
- 初期化

Excel 出力では `FileSelectionDialog` を使います。`.csproj` から検出したファイル一覧を表示し、入力文字列で絞り込みできます。

## 設定保存

`AppSettingsService` が前回入力内容を保存します。

保存対象:

- ProjectPath
- DotCoverXmlPath
- ScopeType
- ScopePath
- IncludePatterns
- ExcludePatterns
- OutputDirectory
- ReportTitle
- OpenAfterGeneration
- OverwriteExisting

初期化時は保存済み設定と画面状態を両方リセットします。

## テスト方針

基本は TDD で進めます。

追加や修正時は、まず対象の仕様を MSTest で固定します。

よく使うコマンド:

```powershell
dotnet test --configuration Release
dotnet build --configuration Release
```

テスト観点:

- 入力 XML の構造差異
- エンコーディング差異
- dotCover Statement の範囲
- Method 集計値と Statement 要素数のズレ
- HTML タブとリンク
- Excel の未カバー印
- Excel のメンバーリンク

## dotCover 公式 HTML との比較

行ハイライトは公式 HTML と一致させる方針です。

比較時は次の3点を揃えます。

- 同じテスト実行で生成した coverage snapshot
- dotCover 公式 HTML
- dotCover DetailedXML

確認観点:

- Covered / Uncovered / NoData の行状態
- 複数行 Statement
- switch 式やラムダでの同一行混在
- record 主コンストラクタ
- Razor Pages の PageModel

現在の実装では、公式 HTML の文字範囲ハイライトを行単位に畳み込むとき、未カバー範囲を含む行は Uncovered として扱います。

## 単一EXE publish

WinForms プロジェクトは単一 EXE publish 用のプロパティを持っています。

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

出力先:

```text
src/CoverageReportGenerator.WinForms/bin/Release/net8.0-windows/win-x64/publish/
```

## Bootstrap

`bootstrap/CoverageReportGenerator.Bootstrap.ps1` は、リポジトリを clone できないユーザー向けです。

動作:

1. public リポジトリの zip を取得する
2. WinForms アプリを publish する
3. 出力先へ `CoverageReportGenerator.exe` を配置する
4. テストコードを含む `source/` を展開する
5. `bin`、`obj`、`.git`、`artifacts` などは除外する

ローカルソースで確認する場合:

```powershell
.\bootstrap\CoverageReportGenerator.Bootstrap.ps1 -Source C:\work\CoverageReportGenerator -Output .\dist
```

## 変更時の注意

### dotCover XML まわり

dotCover の XML はバージョンや出力条件で形が変わる可能性があります。新しい構造を見つけた場合は、文字列処理ではなく `XDocument` ベースの探索を維持してください。

### ソース行ハイライト

公式 HTML との一致を崩さないようにしてください。行状態を変更する場合は、必ず公式 HTML との比較ケースを増やします。

### Excel 出力

ソース表示は B 列開始、未カバー印は A 列という利用者操作に直結する仕様です。列構成を変える場合は、利用者向けガイドとテストも更新してください。

### UI ラベル

基本は日本語です。ただし技術用語として自然なものは英語のまま残します。

### キャッシュ

解析キャッシュは起動速度に影響します。メンバー解析のモデルを変更した場合は、キャッシュ互換性に注意してください。

## リリース前チェックリスト

- `dotnet test --configuration Release`
- `dotnet build --configuration Release`
- HTML レポート生成
- Excel レポート生成
- dotCover 公式 HTML とのハイライト比較
- Bootstrap 実行
- README / docs 更新
