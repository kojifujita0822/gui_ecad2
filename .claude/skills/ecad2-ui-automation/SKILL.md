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

- **【MUST】フォーカス非占有を優先する（殿指示、2026-07-10）**: `Invoke-Ecad2Button`/
  `Invoke-Ecad2Element`（InvokePattern/SelectionItemPattern）・`ValuePattern.SetValue`
  （テキスト入力）・`Save-Ecad2Screenshot`（PrintWindow方式）は、いずれも
  `SetForegroundWindow`/`SetCursorPos`等のグローバル入力を使わず実機で機能することを実証済み
  （ボタン押下・シート追加/削除/改名・ダイアログ操作・スクリーンショット撮影、殿の他ウィンドウ
  操作中でも正しく動く）。ゆえに**ボタン操作・リスト選択・テキスト入力・見た目確認で完結する
  検証は、フォーカスを奪わずに実行できる**。一方 `Send-Ecad2Keys`（キーボードショートカット自体の
  検証）・`Invoke-Ecad2ScreenClick`/`Invoke-Ecad2CanvasClick`/`Invoke-Ecad2CtrlScroll`
  （キャンバス内セルクリック等、UI Automationツリーで要素を辿れず座標指定が本質的に必要な操作）は
  グローバルなキーボード/マウス入力を要し、殿の他ウィンドウ操作と衝突する。**代替できる観点は
  極力前者で済ませ、後者は代替手段が無い場面に限定する**（詳細経緯: 2026-07-10、実機確認中に
  フォアグラウンド化・キー送出が殿の並列セッション操作へ意図せず割り込む事故が発生し、殿裁定で
  本方針へ改修）。
- **`SendKeys` によるキーボード送信はこのアプリに正常に届く**（GuiEcad/WinUI3では届かなかったのと対照的、
  T-002/T-006 PoCで実証済み）。Esc・Ctrl+Tab等のグローバルショートカット検証は
  `Send-Ecad2Keys` を使ってよい。**モーダルダイアログ表示中の使用は特に注意**（21文字以上は既定で
  例外になる長文字列ガード付き、詳細は6節トラブルシュート末尾の項目参照）。
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
```

**【MUST】実機確認は必ずセカンダリモニタ上で行う（殿の明示指示、2026-07-07）。**
ユーザーはプライマリモニタで作業しており、検証ウィンドウがプライマリに出現するたびに作業を妨げるため。

- `Start-Ecad2App` は既定（`-Screen Auto`）で、セカンダリモニタが存在すれば起動直後に
  ウィンドウを自動でセカンダリへ移動する（存在しなければ何もしない）。
  **`-Screen None`/`Primary` で既定を打ち消さないこと**（プライマリでしか再現しない事象の
  検証等、正当な理由があり殿の了承を得た場合のみ例外）。
- `Start-Ecad2App` を経由せず起動した場合（`dotnet run` 直叩き等）や、検証中にウィンドウ位置が
  プライマリへ戻ってしまった場合は、操作を始める前に `Move-Ecad2WindowToScreen -Screen Secondary`
  を呼んで移動させる。
- スクリーンショット・キャンバスクリックはウィンドウ左上からの相対座標基準（`Get-Ecad2WindowRect`）
  のため、セカンダリ配置でもそのまま動く。

`Start-Ecad2App` は `dotnet run --project src/Ecad2.App` を起動し、`Ecad2.App` プロセス
（`dotnet` ラッパープロセスとは別、`MainWindowHandle` を持つ方）が確立するまで待機する。
マウス操作自体は依然として実カーソル移動を伴うため（0節参照）、セカンダリモニタに置いても
「完全にバックグラウンド」にはならないが、少なくともユーザーが作業しているプライマリモニタ上に
ウィンドウが出現しなくなる。

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

# キャンバス内で右クリック（ContextMenu表示等、右クリック固有の検証用。T-055増分3で新設）
Invoke-Ecad2CanvasRightClick -RelativeX 700 -RelativeY 370
# → メニュー項目の取得・実行は6節「ダイアログ・ポップアップの検出」を参照（通常のFindAllでは拾えない）

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
撮影し、Readツールで画像として開いて目視確認する。**`Save-Ecad2Screenshot` は内部で`PrintWindow`
（`PW_RENDERFULLCONTENT`）を使い対象ウィンドウの内容を直接描画するため、フォアグラウンド化不要
（2026-07-10改修）。Ecad2.Appが他ウィンドウの背後に完全に隠れていても、他ウィンドウが写り込まず
Ecad2.Appの内容だけが正しく撮れることを実証済み**（旧実装は`CopyFromScreen`＝画面表示の
キャプチャのみで、撮影前のフォアグラウンド化が前提だった。フォアグラウンド化を挟まず撮影すると
別ウィンドウが写り込む事故が実際に発生し、それを機に本方式へ切替）。

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
- **ダイアログ・ポップアップ（OpenFileDialog、コードビハインド生成のContextMenu、カスタム
  ダイアログ等）が `[System.Windows.Automation.AutomationElement]::RootElement.FindAll(Children,
  Window条件)` では検出できないことがある**（2026-07-10、T-055増分3検証で実証。`Invoke-Ecad2Button`
  でダイアログを開くボタンをInvokeしても、直後のFindAllでウィンドウが1件も増えず「開かれて
  いない」ように見えるが、実際は正しく開いている）。代わりに **Win32 `EnumWindows` API で
  対象プロセスの可視ウィンドウを直接列挙する**と確実に検出できる:
  ```powershell
  Add-Type @"
  using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
  public class WinEnumHelper {
      public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
      [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc p, IntPtr l);
      [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int pid);
      [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
      public static List<IntPtr> GetVisible(int pid) {
          var r = new List<IntPtr>();
          EnumWindows((h, l) => { int p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) r.Add(h); return true; }, IntPtr.Zero);
          return r;
      }
  }
  "@
  $handles = [WinEnumHelper]::GetVisible((Get-Ecad2Process).Id)
  # 各ハンドルを [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$h) で取得し中身を探索
  ```
- **ContextMenu表示中は `(Get-Ecad2Process).MainWindowHandle` がメインウィンドウではなく
  メニュー自身のハンドルを指すことがある**（2026-07-10実証。.NETの`Process.MainWindowHandle`は
  呼び出し時点で再検出されるため、ポップアップがメインウィンドウ候補に化けることがある）。
  この状態で `Get-Ecad2WindowRect`/`Save-Ecad2Screenshot` 等メインウィンドウ前提のヘルパーを
  呼ぶと、小さいメニューの矩形・メニューだけが写ったスクリーンショットが返り誤診断しやすい。
  ContextMenu操作中は「直前に確定したメインウィンドウハンドル」を変数にキャッシュして使うか、
  逆にこの現象自体を「メニューが正しく開いた証拠」として積極的に利用してよい（本スキルの
  `Invoke-Ecad2CanvasRightClick`使用時はメニュー操作直後に`(Get-Ecad2Process).MainWindowHandle`
  でメニュー要素を取得し、メニュー項目のInvoke後に再度呼べばメインウィンドウに戻る）。
- **DataGrid（出力パネル等）はUI仮想化により、スクロール範囲外（非表示）の行がUIA `FindAll`で
  一切取得できない**（2026-07-11、T-052検証で実証。ウィンドウ・パネルが小さい状態でDRC結果を
  `FindAll`で数えたところ実際より少ない件数しか返らず、「一部の診断が出ていない」と誤診断しかけた。
  殿がウィンドウを拡大すると全件表示された）。DataGridの全件を確認する際は、パネルを十分な高さに
  拡大するかスクロールしてから`FindAll`すること。件数の食い違いに気づいたら即バグと断定せず、
  まず仮想化を疑う。
- **プロパティパネルのテキストボックス（`DeviceNameBox`等）への`ValuePattern.SetValue`は、値を
  設定できても背後のモデルへの反映（機器表更新等）がされないことがある**（2026-07-11、T-055増分3
  検証で実証。WPFの`Binding`が`LostFocus`等の実イベントを要求するため、UIA経由の値設定だけでは
  トリガーされないと見られる）。一方、要素配置時のインラインダイアログのテキストボックスは
  `Invoke-Ecad2CanvasClick`での物理クリック＋`Send-Ecad2Keys`実入力なら正しく反映される。モデルへの
  反映有無を確認したい検証では後者を使うこと。
- **`Send-Ecad2Keys`直後にスクリーンショットで見た目を確認すると、実際の物理操作とは異なる結果が
  観測されることがある**（2026-07-11、T-056のCtrl+Gグリッド表示切替検証で発生。UIA経由ではメニューの
  ToggleStateは切り替わるのにキャンバス描画が反映されていないように見えたが、殿の実機操作では
  正常に切り替わった。`Start-Sleep`を600ms挟んでも再現し、原因未特定）。キーボードショートカットの
  見た目検証で疑わしい結果が出た場合は、待機時間を増やす・複数回スクリーンショットを取るなどの
  対照実験を行うか、殿代行操作（0節参照）での再確認を検討すること。
- **モーダルダイアログを開くボタンを`InvokePattern.Invoke()`で連続Invokeすると、モーダル制約を
  無視して背後のボタンが実行され続け、同一ダイアログが複数枚重なって開くことがある**
  （2026-07-11、T-051検証で実証。`AddSheetButton`はClick=コードビハインドで`AddSheetDialog`を
  `ShowDialog()`表示する実装だが、ダイアログが開いた状態を知らずに同じボタン（や他のボタン）へ
  `Invoke()`を重ねたところ、通常のマウスクリックなら受けるはずのモーダルブロックが効かず、
  「シート追加」ダイアログが3枚重なって開いた。この状態では他要素へのInvokeが「認識できない
  エラーです」という例外で断続的に失敗したり、`FindAll`が空を返したりと原因不明の不安定挙動が
  連鎖する。ダイアログの存在自体は上記のEnumWindows手法でしか検出できない（`Save-Ecad2Screenshot`
  はメインウィンドウしか描画しないため写らない）。**対策**：ダイアログを開く可能性のあるボタン
  （Click=コードビハインドで`ShowDialog()`するもの。`Command="{Binding ...}"`のボタンは通常
  対象外）をInvokeした直後は、次の操作に進む前に必ずEnumWindowsでダイアログの出現枚数を確認する
  （1枚のみであることを確認してから、そのダイアログ内の要素を操作する）。原因不明の
  「認識できないエラー」例外や`FindAll`の空振りに遭遇したら、まずこの罠（ダイアログの多重化）を
  疑うこと。
- **PDFプレビューウィンドウのPrintWindow撮影は色情報が信頼できない**（2026-07-12、T-080検証で
  実証。PDFプレビューウィンドウ（メインとは別ウィンドウ、ネイティブPDFレンダラーを内包）を
  PrintWindow方式でスクリーンショット撮影したところ、行コメントの文字色が行によって黒/赤/青に
  ばらついて写ったが、殿の実画面確認では**全て黒色**だった。撮影画像側の異常であり、実描画は
  正常）。PDFプレビューに対する**色・配色系の所見はPrintWindow撮影画像だけを根拠に報告しない**こと。
  色異常らしきものを見つけたら、まず撮影手法起因を疑い、殿の実画面確認（0節の殿代行操作）で
  裏取りしてから判定する（レイアウト・文字内容・ページ構成の確認には引き続き使ってよい）。
- **`Send-Ecad2Keys`でダイアログへ文字列（ファイルパス等）を送るのは避ける**（2026-07-11、
  P-056で実証）。`Set-Ecad2Foreground`は元々モーダル状態を考慮せず無条件にメインウィンドウを
  アクティブ化する実装だったため、`ShowDialog`内部の`EnableWindow(FALSE)`と矛盾する
  「無効化されているのにアクティブ」という状態を作り、フォーカス誤爆→意図しないコマンド
  誘発（実機ではCtrl+O相当の再トリガーによるダイアログ多重化）につながった実例がある
  （殿PCのキーボード入力不通フリーズ＝P-056の契機ともなった）。**対策済み**：
  `Set-Ecad2Foreground`は対象ウィンドウが`IsWindowEnabled`で無効化されている間は
  `SetForegroundWindow`自体をスキップするよう改修済み（モーダル表示中は何もしない）。加えて
  `Send-Ecad2Keys`は21文字以上の文字列を既定で拒否する（`-Force`で明示解除しない限り例外）。
  これらの安全策があっても、**ダイアログへのテキスト入力は`Send-Ecad2Keys`ではなく、最近使った
  ファイル一覧のボタン操作・物理クリック+短いキー入力など別手段を優先すること**。
