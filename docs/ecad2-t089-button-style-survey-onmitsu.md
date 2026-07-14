# T-089着手前調査: 全ボタンのスタイル定義箇所洗い出し(隠密)

調査日: 2026-07-14　調査者: 隠密　依頼元: 家老（T-089着手前調査、
「共通スタイルへの`IsPressed`トリガー追加方式が妥当か」の判断材料）

## 調査方法（事実）

`src/Ecad2.App`配下の全XAML（`App.xaml`, `MainWindow.xaml`, `Views/*.xaml`計6ファイル）を対象に
Style定義・Button/ToggleButton/RadioButton要素・`IsPressed`・`ControlTemplate`を全文検索し、
該当箇所を全てRead確認した。決定的な箇所（App.xamlのResources定義、MainWindow.xamlの3スタイル、
ダイアログ1件のResources不在）は隠密自身が実ファイルで再確認済み。

## 結論（要約）

**「共通スタイル1箇所への`IsPressed`トリガー追加で全ボタンに反映」は不可。反映されるのは
一部のみ**。理由は3点。

1. 名前付き共通スタイルは`MainWindow.xaml`の`Window.Resources`（1ファイルにスコープが閉じる
   ローカルリソース）にしか存在せず、`App.xaml`の`Application.Resources`（`MainWindow.xaml:6-19`
   確認済み）にはButton系スタイルが1つもない。
2. ダイアログ6ファイル（`Views/*.xaml`）はいずれも`Window.Resources`ブロック自体が存在せず
   （`AddSheetDialog.xaml`で確認済み）、OK/キャンセル等のボタンはWPF既定スタイルのまま。
3. WPFの型制約上、ButtonとToggleButtonはBasedOnで型混在継承できないため、ボタン用スタイルと
   トグルボタン用スタイルは別々に追加が要る。

## 事実: スタイル定義の内訳

### (A) `App.xaml`（アプリ全体スコープ）
`Application.Resources`（`App.xaml:6-19`）にはSolidColorBrush 4個のみ定義。Button系の
暗黙・名前付きスタイルは0件。

### (B) `MainWindow.xaml`の`Window.Resources`（MainWindow限定スコープ、15〜92行）
| スタイル | 行 | TargetType | BasedOn | 既存Trigger |
|---|---|---|---|---|
| `ToolBarButtonStyle` | 27 | `Button` | なし | `IsEnabled=False`→`Opacity=0.35` |
| `TestModeToolBarButtonStyle` | 60 | `ToggleButton` | なし(独立定義) | `IsEnabled=False`→`Opacity=0.35`、`IsChecked=True`→`Background=TestModeActiveBrush` |
| `PlacementToolBarButtonStyle` | 77 | `Button` | `ToolBarButtonStyle` | 継承のみ(Width/Heightのみ上書き) |

`MainWindow.xaml:57-59`のコメントに「ButtonとToggleButtonはButtonBaseの兄弟クラスでBasedOn
による型混在継承ができない」と明記あり(事実)。

### (C) ダイアログ`Views/*.xaml`（6ファイル）
`AboutDialog.xaml`・`AddSheetDialog.xaml`・`DocumentInfoDialog.xaml`・`RenameDialog.xaml`・
`SheetSettingsDialog.xaml`・`PdfPreviewDialog.xaml`いずれも`Window.Resources`ブロックが
存在せず（`AddSheetDialog.xaml`で確認済み）、Button要素にStyle属性の指定もなし。WPF組み込み
既定Buttonスタイルがそのまま適用されている。

## 事実: ボタンとスタイルの対応（集計）

- `ToolBarButtonStyle`使用（1段目8個）: 新規作成・開く・上書き保存・元に戻す・やり直し・
  PDF出力・行を追加・行を削除
- `TestModeToolBarButtonStyle`使用（ToggleButton 2個）: テストモード、タイマ一時停止
- `PlacementToolBarButtonStyle`使用（2段目・配置系13個）: 選択ツール、a接点(F5)、OR a接点、
  b接点(F6)、OR b接点、コイル(F7)、端子台(F8)、自由線横/縦(F9)、縦分岐線、接続点/配線分断(F10)、
  自作パーツ
- スタイル指定なし（MainWindow内14個）: シートの＋/－/名前変更/設定、検索バー5個
  （前/次/置換/全置換/閉じる）、未定アイコン2個、ElementPlacementBarのOK/キャンセル、拡張表示
- スタイル指定なし（ダイアログ6ファイル16個）: 各種OK/キャンセル、PdfPreviewDialogの
  前/次/ズーム/PDF出力/閉じる等
- RadioButton 2個（`AddSheetDialog.xaml`）: 対象スタイル外

合計: 名前付きスタイル使用23個 / スタイル指定なし30個 / RadioButton 2個。

## 事実: 既存の`IsPressed`トリガー

リポジトリ全体で`IsPressed`という文字列は`MainWindow.xaml.cs:2077`のコメント1箇所のみに出現
（Click発火をButtonBase標準に委ねる設計判断の説明）。`<Trigger Property="IsPressed">`等の
実装はXAML上どこにも存在しない。

## 事実: ControlTemplateの独自定義

`<ControlTemplate`のgrep結果は0件。全ボタンがControlTemplateを差し替えず、Style内のSetterの
みで外観調整している。テーマ系NuGet参照・`ThemeMode`設定もなし。

## 個別対応が必要/衝突しそうな箇所（指摘）

1. **ToggleButton 2個への別途追加が必須**（型制約、事実）。
2. **`TestModeToolBarButtonStyle`内の既存`IsChecked`トリガーとの競合リスク**:
   `MainWindow.xaml:69-71`に既に`IsChecked=True`→背景色変化がある。ここへ
   `IsPressed=True`→背景変化を単純追加すると、テストモードON中に押下した瞬間、
   Style.Triggers内の宣言順で後勝ちとなり、押下フィードバック色とテストモード色が競合する。
   `MultiTrigger`等の個別対応が必要（トリガー競合の仕組みはWPF仕様上の事実、対応要否の判断は
   家老・侍・殿）。
3. **ダイアログ側30個は`MainWindow.xaml`側のStyle変更の影響範囲外**（スコープが閉じているため、
   事実）。T-089が例示する「OK/キャンセル系」はこちらに該当するため、この調査を踏まえないと
   見落としうる。
4. **機能未定プレースホルダボタン**（`MainWindow.xaml:752-761`未定アイコン1/2、`:811-812`拡張表示）
   は`KeyboardNavigation.IsTabStop="False"`かつClick未配線。押下フィードバックを付けると
   「押せそうに見えて実際は無反応」という誤解を助長する恐れがある（推測、T-089スコープからの
   除外検討の余地ありとして家老へ申し送り）。

## 実装方針についての所感（推測、判断は家老・侍・殿に委ねる）

全ボタン（ダイアログ含む）へ1箇所の変更で波及させたい場合、`App.xaml`の
`Application.Resources`に`TargetType="Button"`の暗黙（x:Keyなし）スタイルを新設し、既存3
スタイルをそこへ`BasedOn`させる構成変更が必要になる。ToggleButton側は
`TargetType="ToggleButton"`の暗黙スタイルを別途`App.xaml`に用意し、上記2の`IsChecked`競合は
個別に手当てする。この方式変更自体はUI/UXの見た目分岐ではなく実装構造の話だが、規模は
「Style 1箇所追加」より大きくなる点は家老の采配・侍の実装見積もりに反映されたい。

押下時の具体的な視覚表現（色変化・枠線・影・スケール等）自体はUI/UX分岐のため、本調査の
対象外。着手時に殿確認が必要（`docs/todo.md` T-089節に既記載のとおり）。
