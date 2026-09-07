# 傍ら

生成AI開発で、AIが書いた **Markdown** と **HTML** だけを参照するビューアです。VS Code の横に置く **Windows デスクトップアプリ** です。HTTP ポートは開きません。同じネットワークの別 PC から番号指定で見ることはできません。

## 実行

Go も Node.js も不要です。配布 ZIP を展開し、次のどちらかをダブルクリックします。

| 起動 | 内容 |
|---|---|
| `start.bat` または `起動.bat` | **対策あり**（推奨）。プレビューのスクリプトを隔離します |
| `start-open.bat` または `起動-制限なし.bat` | **対策なし**。HTML の CDN や、Markdown 内の HTML をそのまま動かします |

特定のフォルダから始める場合:

```bat
start.bat C:\path\to\your-project
start-open.bat C:\path\to\your-project
```

黒いコンソールは出ません。窓を閉じると終了します。

ソースから自分で exe を作る場合は [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) を入れたあと:

```bat
dotnet test
dotnet publish src\SobaDesk\SobaDesk.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
```

`dist\soba-desk.exe` ができます。初回起動には WebView2（通常は Edge 同梱）が必要です。

## 表示するフォルダ

左の一覧は Markdown と HTML だけです。作業用ディレクトリは最初から出しません。

既定で隠すもの（抜粋）:

- Node.js: `node_modules`, `.next`, `dist`, `build`, `coverage` など
- Python: `.venv`, `venv`, `__pycache__`, `.pytest_cache` など
- Java: `target`, `.gradle`, `bin`, `classes` など
- その他: `.git`, `.idea`, `.vscode` と、ドットから始まるファイル/フォルダ

プロジェクト直下（または配下）に `.sobaignore` を置くと、[gitignore](https://git-scm.com/docs/gitignore) と同じ書き方で足したり、打ち消したりできます。

```
# 生成した HTML を dist から見たいとき
!dist/
```

見本は `.sobaignore.example` です。

## セキュリティ上の懸念

このアプリは、選んだフォルダの中身を **自分の PC 上の窓** に出します。待受ポートはありません。

### 対策あり（`start.bat`）

- Markdown から危険なタグを取り除く
- HTML を、アプリ本体と権限を共有しない iframe で表示する
- プレビューから外部サイトへ通信できないようにする（CSP）

それでも残りうる点:

- 完全なサンドボックスではない
- Tailwind など CDN のスクリプトが必要な HTML は、アプリ内では動かないことがある
- 「ブラウザで開く」は OS の既定アプリでファイルを開く（このアプリの外）

### 対策なし（`start-open.bat`）

プレビュー上のスクリプトはより自由に動きます。信用できない HTML / Markdown を開かないでください。

**自分や、自分が動かしている生成AIが書いたプロジェクト専用** です。

## ソース

公開リポジトリは個人アカウントです。

https://github.com/kumaboarder/md-web-preview

```bat
git clone https://github.com/kumaboarder/md-web-preview.git
```
