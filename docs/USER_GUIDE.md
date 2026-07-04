# 利用者向けガイド

CoverageReportGenerator は、dotCover が出力した DetailedXML と対象プロジェクトの `.csproj` を読み込み、見やすい HTML / Excel カバレッジレポートを作る Windows アプリです。

## できること

- プロジェクト全体のカバレッジ確認
- Namespace / Type / メンバー / ファイル単位のカバレッジ確認
- カバレッジが低い箇所のランキング表示
- メンバー名クリックによる対象ソース行へのジャンプ
- 特定ファイルだけの Excel レポート出力
- Excel で未カバー行を `※` 付きで確認
- Shift_JIS / CP932 ソースの日本語表示

## 入力に必要なもの

### `.csproj`

解析対象の C# プロジェクトファイルです。Razor Pages プロジェクトを主対象にしています。

アプリは `.csproj` を起点にソースファイル一覧を取得します。フォルダだけを直接読み込むのではなく、必ず先に `.csproj` を選択してください。

### dotCover DetailedXML

dotCover で生成した詳細 XML レポートです。HTML レポートではなく、`DetailedXML` 形式を指定してください。

このツールは XML 内の `Statement`、`FileIndices`、`Assembly`、`Namespace`、`Type`、`Method` 情報を使います。

## dotCover XML の用意

dotCover CLI を使う場合は、概ね次の流れです。

```powershell
dotCover cover-dotnet `
  --Output coverage.dcvr `
  -- test path\to\Tests.csproj --configuration Release

dotCover report `
  --Source coverage.dcvr `
  --Output coverage.xml `
  --ReportType DetailedXML
```

Visual Studio / ReSharper の GUI で取得した coverage snapshot から DetailedXML を出力しても構いません。

## HTML レポートの作り方

1. アプリを起動します。
2. `プロジェクト` の `選択` から `.csproj` を選択します。
3. `DotCover XML` の `選択` から DetailedXML を選択します。
4. `対象範囲` を選択します。
5. 必要に応じて `Include` / `Exclude` を調整します。
6. `出力先` と `レポート名` を確認します。
7. `HTML生成` を押します。

生成後に開く設定が有効な場合、作成された HTML が自動で開きます。

## 対象範囲

### プロジェクト

`.csproj` 配下の対象ソース全体をレポートします。

Namespace / Type / メンバー / ファイル別に全体像を見たい場合に使います。

### フォルダ

指定したフォルダ配下を再帰的に対象にします。

特定の機能領域や Razor Pages 配下だけを見たい場合に使います。

### ファイル

指定した単一ファイルだけを対象にします。

1ファイルのカバレッジを細かく確認したい場合に使います。

## Include / Exclude

セミコロン区切りのワイルドカードで対象を絞り込みます。

既定値:

```text
Include: *.cs;*.cshtml
Exclude: *.g.cs;*.generated.cs;*.Designer.cs;bin;obj
```

例:

```text
Include: *.cs
Exclude: bin;obj;Migrations;*.Designer.cs
```

## HTML レポートの見方

### 概要

レポート名、プロジェクト、対象範囲、全体カバレッジを表示します。

カバレッジ率はバーでも表示されるため、数値だけでなく視覚的に状態を把握できます。

### ランキング

低カバレッジの Namespace / Type / メンバーと、未カバーが多いファイルを表示します。

まず改善箇所を探したい場合はこのタブから確認すると効率的です。

### メンバー

メソッド、コンストラクタ、プロパティ、アクセサ、ローカル関数、ラムダなどの単位で一覧表示します。

メンバー名をクリックすると、`ソース` タブの該当行へジャンプします。

### ファイル

ファイル単位のカバレッジを一覧表示します。

ファイル名で絞り込めるため、自分が知りたいファイルのカバレッジ率を素早く確認できます。カバレッジバーの色は次の目安です。

- 緑: 80%以上
- 黄: 50%以上80%未満
- 赤: 50%未満

### ソース

ソース行をカバレッジ状態付きで表示します。

- `C`: Covered
- `U`: Uncovered
- 空欄: No Data

dotCover 公式 HTML と行ハイライトが一致するよう、複数行 Statement や record 主コンストラクタの扱いを調整しています。

## Excel レポートの作り方

1. `.csproj` と DotCover XML を選択します。
2. プロジェクトが読み込まれていることを確認します。
3. `Excel出力` を押します。
4. ファイル選択ダイアログで対象ファイルを選びます。
5. 保存先を指定します。

ファイル選択ダイアログでは、ファイル名、フォルダ名、拡張子で絞り込みできます。

## Excel レポートの見方

Excel レポートは1シートです。

上部には対象ファイル、カバレッジ率、Statement 数を表示します。

メンバー一覧では、各メンバーのカバレッジ率を色とバーで確認できます。メンバー名をクリックすると、同じシート内の該当ソース定義行へジャンプします。呼び出し行ではなく、Roslyn で解析したメンバー定義行へリンクします。

ソース一覧は B 列から表示します。未カバー行の A 列には `※` を表示します。Excel の `Ctrl + ↑` / `Ctrl + ↓` で `※` のある行を移動しやすくするためです。

## 保存される設定

前回入力した内容は次回起動時に復元されます。

保存対象:

- プロジェクトパス
- DotCover XML パス
- 対象範囲
- 対象パス
- Include / Exclude
- 出力先
- レポート名
- 生成後に開く
- 既存ファイル上書き

初期状態に戻したい場合は `初期化` ボタンを押します。

## キャッシュ

`.csproj` のソース検出結果と Roslyn のメンバー解析結果は、次の場所にキャッシュされます。

```text
%LOCALAPPDATA%\CoverageReportGenerator\cache
```

ソースが変更された場合は必要な分だけ更新します。表示がおかしい場合は、アプリの初期化またはキャッシュ削除後に再読み込みしてください。

## よくあるトラブル

### Target Preview の件数が少ない

次を確認してください。

- `.csproj` が正しいか
- Include / Exclude で対象が除外されていないか
- 対象範囲がフォルダや単一ファイルに絞られていないか
- Razor Pages の `.cshtml.cs` がプロジェクトに含まれているか

### 文字化けする

HTML は UTF-8 BOM 付きで出力します。ソースファイルは UTF-8、UTF-16、Shift_JIS / CP932 を読み取ります。

それでも文字化けする場合は、対象 XML またはソースファイルの実際のエンコーディングを確認してください。

### HTML のカバレッジ行が dotCover と違う

このツールは dotCover DetailedXML の Statement 情報を元に行表示します。公式 HTML と一致するよう調整していますが、dotCover のバージョンや XML 形式が変わった場合は差が出る可能性があります。

差分がある場合は、dotCover の HTML レポート、DetailedXML、対象ソースをセットで確認してください。

### Excel のリンクが呼び出し行に飛ばないか心配

Excel のメンバーリンクは、メンバー名の文字列検索ではなく Roslyn 解析で得た定義開始行を使います。同じ名前の呼び出しがソース内にあっても、定義行へジャンプします。

### レポート生成に失敗する

次を確認してください。

- DotCover XML が DetailedXML 形式か
- XML 内に `FileIndices/File` と `Statement` が含まれているか
- XML のファイルパスと `.csproj` 配下のソースパスが対応しているか
- 出力先フォルダに書き込み権限があるか

## Bootstrap で導入する

リポジトリを clone できない場合は、単一 PowerShell ファイルで導入できます。

```powershell
.\bootstrap\CoverageReportGenerator.Bootstrap.ps1 -Output .\dist
```

出力先には `CoverageReportGenerator.exe` と `source/` が作成されます。`source/` にはテストコードも含まれます。
