---
name: ecad2-ui-automation
description: Launch and drive the Ecad2 WPF desktop app (dotnet run --project src/Ecad2.App) for real-machine UI verification using Windows UI Automation — invoke toolbar/palette buttons by Name, read status bar text directly, send keyboard shortcuts (WPF accepts SendKeys, unlike GuiEcad/WinUI3), capture window screenshots. ecad2（WPFラダー図CAD）の実機確認・忍者役の検証作業で使う。座標クリックの試行錯誤を避け、UI Automation経由で確実に操作する。
---

# Ecad2 実機確認スキル

WPF デスクトップアプリ ecad2 を実機操作して検証するための手順。
**固定UI（ツールバー・メニュー等）の座標ベースクリックはウィンドウ位置・DPIでズレるため使わない。**
Name/AutomationId 経由で直接呼び出すこと。キャンバス内セルなど座標が本質的に必要な操作のみ、
ウィンドウ左上からの相対座標を使う。

姉妹スキルとして GuiEcad（WinUI3、旧アプリ）用の `guiecad-ui-automation`
（`C:\Users\kojif\Desktop\生産物\gui_ecad\.claude\skills\guiecad-ui-automation\`）が存在する。
本スキルは同じ設計思想を踏襲しつつ、WPFとWinUI3のフレームワーク差分に合わせて調整している
（差分は「0. 前提・既知の制約」参照）。

## 0. 前提・既知の制約

- **`SendKeys` によるキーボード送信はこのアプリに正常に届く**（GuiEcad/WinUI3では届かなかったのと対照的、
  T-002/T-006 PoCで実証済み）。Esc・Ctrl+Tab等のグローバルショートカット検証は
  `Send-Ecad2Keys` を使ってよい。
- ツールバーボタンには `AutomationProperties.Name` が付与済み（例: `"a接点配置 (F5)"`）。
  GuiEcadのように「RadioButton自体はName空、Text子要素から親を辿る」フォールバックは基本不要だが、
  念のため `Invoke-Ecad2Button` はButton→ListItemの順で探すようにしてある。
- **ウィンドウの起動位置はモニタ構成によって変わる**（実測: マルチモニタ環境で `2240,116` のような
  値になったことがある）。絶対座標のハードコードはしない。`Get-Ecad2WindowRect` を基準にすること。
- ステータスバー（`AutomationId=StatusBarArea`）配下のテキストは、GuiEcadのように個別
  AutomationId（`StatusPos`等）を持たず、`"ツール: Select"` `"ズーム: 100%"` のような文字列が
  並んでいるだけ。`Get-Ecad2StatusText -Prefix "ツール:"` のように前方一致で取り出す。
- **リサイズ操作は要注意**（2026-07-03実測）: `MoveWindow` でのリサイズ直後にプロセスが消失する
  事象を2回確認した。ただし同条件での再テストでは再現せず、原因は未特定（UI Automation越しの
  他操作との組み合わせが引き金だった可能性もある）。`Resize-Ecad2Window` は呼び出し後に自動で
  プロセス生存確認を行い、消失していれば例外を投げる。リサイズ確認をする際は、その前後で
  必ず他の検証観点と切り離して単独実行し、クラッシュした場合は再現条件を丁寧に記録すること。
- **Ctrl+ホイールでのズームは`Send-Ecad2Keys`では送れない**（キーボードショートカットではなくマウス
  ホイール+修飾キーの組み合わせのため）。`Invoke-Ecad2CtrlScroll`（`keybd_event`でCtrl押下→
  `mouse_event`でホイール送信→Ctrl解放、を合成）を使うこと（2026-07-05、T-021ズーム検証で実証・スキル化）。
- **UI Automation経由の操作（Invoke等）がボタンのClickハンドラを経由せず内部状態を不安定にする
  ケースが実際にあった**（2026-07-03、T-016検証: 複数ツールボタンが同時にハイライトされたまま
  ツール切替不能になるバグが発生。原因はアプリ側のToolState等価性判定の実装不備だったが、UI
  Automation経由の連続呼び出しが誘発した可能性も否定できない）。同一要素に対する連続invoke後に
  不審な挙動が出た場合は、一度スクリーンショットで実際のボタン選択状態（ハイライト）とステータス
  バーのテキストに矛盾がないか確認すること。

## 1. 起動

```powershell
. "C:\ECAD2\.claude\skills\ecad2-ui-automation\helpers.ps1"
dotnet build src/Ecad2.App   # 事前ビルド確認（任意、警告0件・エラー0件を確認してから起動する）
Start-Ecad2App                # コンソール出力を $env:TEMP\ecad2-ui-automation-std{out,err}.log にリダイレクトして起動、MainWindowHandle確定まで待機

# セカンダリモニタがある環境では、ユーザーの作業画面を占有しないよう右画面に起動できる
Start-Ecad2App -Screen Secondary
```

`Start-Ecad2App` は `dotnet run --project src/Ecad2.App` を起動し、`Ecad2.App` プロセス
（`dotnet` ラッパープロセスとは別、`MainWindowHandle` を持つ方）が確立するまで待機する。
`-Screen Secondary` を指定すると起動直後にセカンダリモニタへウィンドウを移動する（モニタが
1台のみの環境で指定すると例外）。起動後に画面を切り替えたい場合は `Move-Ecad2WindowToScreen
-Screen Secondary` を単独で呼んでもよい。マウス操作自体は依然として実カーソル移動を伴うため
（0節参照）、セカンダリモニタに置いても「完全にバックグラウンド」にはならないが、少なくとも
ユーザーが作業しているプライマリモニタ上にウィンドウが出現しなくなる。

## 2. ヘルパーの読み込み

**PowerShell 呼び出しごとに冒頭で dot-source する**（シェル状態は呼び出し間で持続しないため）:

```powershell
. "C:\ECAD2\.claude\skills\ecad2-ui-automation\helpers.ps1"
```

## 3. 基本操作パターン

```powershell
. "...\helpers.ps1"

# ツールバーボタンを Name で押す（Name はボタンの AutomationProperties.Name、括弧内はショートカット表記）
Invoke-Ecad2Button -Name "a接点配置 (F5)"
Invoke-Ecad2Button -Name "選択ツール (Esc)"

# 左パーツパレットのリスト項目（Name は完全一致が必要。表示ラベルだけでなく
# "PartFolderEntry { Category = ..., FilePath = ..., Definition = ... }" という完全な文字列になっている点に注意。
# 部分一致で探したい場合は Find-Ecad2Element -All の結果を Where-Object で絞り込む）
$items = Find-Ecad2Element -AutomationId "PartPaletteList" | ForEach-Object {
    $_.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
}
($items | Where-Object { $_.Current.Name -like "*a接点.gcadpart*" } | Select-Object -First 1) | ForEach-Object { Invoke-Ecad2Element -Element $_ }

# ステータスバーの値を直接取得（画像を撮らずに検証できる）
Get-Ecad2StatusText -Prefix "ツール:"   # 例: "ツール: PlaceElement"
Get-Ecad2StatusText -Prefix "ズーム:"   # 例: "ズーム: 100%"
Get-Ecad2StatusText                     # 全件配列で取得

# キー送信（Escでツールキャンセル、Ctrl+Tabでタブ切替ブロック確認など。WPFなので正常に届く）
Send-Ecad2Keys "{ESC}"
Send-Ecad2Keys "^{TAB}"

# Ctrl+ホイールでのズーム操作（通常のSend-Ecad2Keysでは送れないため専用ヘルパーを使う。
# 座標はスクリーン絶対座標、対象要素の中心を渡す。$Clicks正=ズームイン、負=ズームアウト）
$canvas = Find-Ecad2Element -AutomationId "CanvasArea"
$b = $canvas.Current.BoundingRectangle
$cx = [int]($b.Left + $b.Width / 2)
$cy = [int]($b.Top + $b.Height / 2)
Invoke-Ecad2CtrlScroll -ScreenX $cx -ScreenY $cy -Clicks 5   # 100%→150%（1クリック=10%相当、実測）
Invoke-Ecad2CtrlScroll -ScreenX $cx -ScreenY $cy -Clicks -5  # 150%→100%

# キャンバス内のセルをクリック（座標が必要な唯一のケース。ウィンドウ左上からの相対座標で指定）
$canvas = Find-Ecad2Element -AutomationId "CanvasArea"
Write-Output $canvas.Current.BoundingRectangle   # まずキャンバス範囲を確認してから相対座標を決める
Invoke-Ecad2CanvasClick -RelativeX 700 -RelativeY 370

# 見た目そのものを確認したい時だけスクリーンショット
Save-Ecad2Screenshot -Path "$env:TEMP\claude\...\scratchpad\check1.png"
# → 保存後は Read ツールで画像を開いて目視確認する

# ウィンドウリサイズ確認（0節の注意点を踏まえ、単独で行い直後に生存確認する。関数内で自動チェック済み）
Resize-Ecad2Window -Width 900 -Height 500

# 検証後のクリーンアップ
Stop-Ecad2App
```

## 4. 既知の AutomationId・要素構成（2026-07-03 T-009/T-016実測、変更されうるため都度 FindAll で確認推奨）

| AutomationId / Name | 内容 |
|---|---|
| `StatusBarArea` | ステータスバー全体。配下に `"ツール: X"` `"ズーム: N%"` のText 2件（個別IDなし、Prefixで判別） |
| `PartPaletteList` | 左パーツパレットのListBox。ListItemのNameは `PartFolderEntry { Category = ..., FilePath = ..., Definition = ... }` という完全文字列 |
| `CanvasArea` | 中央キャンバス（Pane、単一ビジュアルとしてUI Automationツリーに現れる。内部の図形要素は個別に走査できない可能性が高い＝GuiEcadのWin2D Canvasと同様の制約） |
| `DeviceTableGrid` | 右パネル機器表（DataGrid、`DataItem`単位で行、列は機器名/種別/型式） |
| ツールバーボタン | `"新規作成 (Ctrl+N)"` `"開く (Ctrl+O)"` `"上書き保存 (Ctrl+S)"` `"元に戻す (Ctrl+Z)"` `"やり直し (Ctrl+Y)"` `"PDF出力 (Ctrl+P)"`（1段目）、`"選択ツール (Esc)"` `"a接点配置 (F5)"` `"b接点配置 (F6)"` `"コイル配置 (F7)"` `"端子台配置 (F8)"`（2段目） |

要素一覧を再取得したい場合:
```powershell
. "...\helpers.ps1"
$root = Get-Ecad2Root
$root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
  ForEach-Object { "$($_.Current.ControlType.ProgrammaticName) Name='$($_.Current.Name)' Id='$($_.Current.AutomationId)' Bounds=$($_.Current.BoundingRectangle)" }
```

## 5. 見た目そのものの確認（レイアウト崩れ・配色・アイコン形状）

UI Automationのテキスト情報だけでは色・アイコン形状・重なりは分からない。`Save-Ecad2Screenshot` で
撮影し、Readツールで画像として開いて目視確認する。**撮影前に必ず `Set-Ecad2Foreground` 相当の
フォアグラウンド化を経由すること**（`Invoke-Ecad2*` 系関数は内部で自動的に行うが、
`Save-Ecad2Screenshot` 単体では行わないため、直前に他ウィンドウを操作した場合は別ウィンドウが
写り込む。実際に2026-07-03、リサイズ直後の撮影でターミナルウィンドウが写り込む事故があった）。

## 6. トラブルシュート

- 要素が見つからない (`throw "... not found"`) → `Get-Ecad2Process` でアプリが起動しているか確認。
  ダイアログ（確認メッセージ等）が前面に出て要素ツリーが変わっている可能性もある。
- クリック・キー送信しても反応がない → `Set-Ecad2Foreground` を挟んでウィンドウをアクティブに
  してから再試行（`Invoke-Ecad2ScreenClick`/`Send-Ecad2Keys` は内部で自動的に行う）。
- ツール状態とツールバーのハイライト表示が食い違う（複数ボタンが同時に選択状態に見える等）→
  実装側のバグの可能性が高い。`Get-Ecad2StatusText` の値とスクリーンショットの両方を証跡として
  残し、実装担当（侍）へ再現手順とともに報告する。
- リサイズ直後にプロセスが消えた → `$env:TEMP\ecad2-ui-automation-std{out,err}.log` と
  `$env:TEMP\ecad2-crash.log`（未処理例外ハンドラが記録）を確認する。
