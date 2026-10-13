# 使い方の説明図の生成

`docs/usage/images/*.png`（使い方ウィンドウに出る、番号付きの説明図）を、定義ファイルから作り直す道具。

## 使い方

先に `dotnet build src/Ecad2.App` を済ませる（撮るのは Debug 版）。

```
pwsh tools/usage-screenshots/New-UsageShots.ps1                          # キー・マウスを借りぬ絵だけを全部
pwsh tools/usage-screenshots/New-UsageShots.ps1 -AllowIntrusive          # 借りる絵も含めて全部
pwsh tools/usage-screenshots/New-UsageShots.ps1 -Only overview           # 1枚だけ（名は1つずつ）
pwsh tools/usage-screenshots/New-UsageShots.ps1 -Only overview -ListAnchors   # 注釈の的に使える要素の一覧
```

- 撮るアプリは自ら別プロセスで起動し、セカンダリモニターへ置く。殿のインストール版には触れず、
  終了は強制終了のみ（正規終了は `main-layout.xml` を上書きするため）。終了後にハッシュで確かめる。
- 絵はアプリへ埋め込まれる。撮り直したら再ビルドし、使い方ウィンドウで見え方を確かめる。

## shots.json

絵1枚の定義。`name`（出力名）／`document`（`demo.gcad`）／`steps`／`annotations`／`crop`／`capture`／`intrusive`。

| 項目 | 意味 |
|---|---|
| `steps` の `invoke` | UIA でボタンを押す（トグルも可）。前面化もマウス移動も伴わぬ |
| `steps` の `menu` | `path`: `["パーツ(P)", "新規作成(N)..."]`。メニューの項目名は `(P)` のように括弧つき |
| `steps` の `click`／`move`／`keys` | 実マウス・キー送出（SendKeys 書式）。`intrusive: true` と `-AllowIntrusive` が要る |
| `capture: "dialog"` | メイン窓でなく、開いた別窓（モーダルダイアログ）を撮る |
| `annotations` | `number`／`target`／`box`／`badgeOffset`。`target` は `automationId`・`name`・`nameLike`・`xy`、位置は `at`・`nudge` |
| `crop` | 要素の矩形で切り抜く。注釈の座標は切る前の絵のまま書く |

`xy` と座標は、見えておる枠の左上を原点とする絵の座標。要素の座標は `-ListAnchors` で採る。

## 注意

- 要素の名前は `-ListAnchors` を端末で見ると文字化けして見えることがある。ファイルへ出して読むこと。
- `intrusive` の絵は殿の画面・マウスを借りる。終了後にカーソルは元へ戻すが、撮影中は触らぬこと。
- 撮影は殿の保存済みレイアウトで起動した姿になる（出荷時の配置と異なる場合は、撮影前に既定へ戻す手当てが要る。未着手）。
- 右クリックメニューは別窓ゆえ、メイン窓の撮影には写らぬ。
