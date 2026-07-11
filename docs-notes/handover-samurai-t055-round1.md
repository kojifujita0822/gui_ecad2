# 侍・臨時引き継ぎ（T-055増分1: 末尾行加減算、§5離脱による）

最終更新: 2026-07-10 14:50頃(JST)、侍記す（出力破損の同種2回目検知=§5発動により離脱）。
次の侍はこのファイルだけで再開できるよう、`long-horizon-discipline`スキル§6の5点セットで記す。

---

## 1. 目的とDoD（家老采配の原文要旨、task_id=T-055増分1）

殿裁可済み。T-055増分1=末尾行の追加・削除（ツールバー行±ボタン＋Ctrl+Shift+Up/Downショートカット）。

- DoD: ツールバー「行＋/行－」・キーボードショートカット(Ctrl+Shift+Up/Down)経由でGrid.Rowsが
  1増減し、上限60・下限1で正しくクランプされることをテストで実測確認（RED→GREEN）。全件テスト合格。
- 殿裁定の反映事項（増分1に適用）:
  1. 行＋/行－ボタンは**ツールバー大型**（`ToolBarButtonStyle`同型、`元に戻す`等と同じ見た目）
  2. **要素の存在する行の削除は拒否（警告）**——増分1・増分3共通の掟
- 参照: `docs/archive/ecad2-t055-implementation-plan-samurai.md`（起草済み計画書、増分1〜3の全体像）、
  `docs/todo.md` T-055節（殿裁定原文）

## 2. 現在の状態（三区分）

### 検証済み（根拠あり、実装への着手前の事前調査のみ）

すべてExplore委譲＋直接Readで確認済み。**src/tests/への変更は一切未着手**。

- **ツールバーボタンの実装パターン**: `MainWindow.xaml:137-181`（Band=0）。
  `Button Style="{StaticResource ToolBarButtonStyle}" ToolTip="..." AutomationProperties.Name="..." Click="XxxButton_Click"`
  ＋`StackPanel`内`Path`(アイコン)＋`TextBlock`(キー凡例)の構成。行＋/行－もこの型で追加する
  （殿裁定1「ツールバー大型」に合致）。Band=0の「元に戻す/やり直し/PDF出力」グループの後
  （`MainWindow.xaml:174`のSeparator後）に新設するのが自然（新規Separatorで区切る）。
- **Command bindingとRelayCommand**: `src/Ecad2.App/Commands/RelayCommand.cs`は
  `Action<object?>`+`Func<object?,bool>?`の単純なラッパ。`CanExecuteChanged`は
  `CommandManager.RequerySuggested`に委譲（自動再評価、手動`RaiseCanExecuteChanged`不要）。
  既存の自動IsEnabled連動例=`MainWindow.xaml:363`の`Command="{Binding SheetNavigation.DeleteCommand}"`
  （`Sheets.Count > 1`でCanExecute）。行＋/行－も同様にICommand化し、CanExecuteで上限/下限を
  表現すれば、ボタンのIsEnabled連動を個別配線せず済む。
- **ICommandの置き場所**: 既存のICommandは全て子ViewModel（`SheetNavigationViewModel`の
  Add/Delete/RenameCommand、`OutputPanelViewModel`のRunDrcCommand）にあり、`MainWindowViewModel`
  自体はICommandを1つも持たない（メソッド直接呼び出しのみ、例:`NewDocument()`）。ただし
  Grid.Rows・CurrentSheetは`MainWindowViewModel`が直接所有するため、行＋/行－コマンドは
  **`MainWindowViewModel`に新設するのが自然**（先例が無いだけで規約違反ではない）。
- **CurrentSheet・再描画の仕組み**: `MainWindowViewModel.cs:148-149`（`CurrentSheet`プロパティ）、
  `:191`（`NotifyCurrentSheetChanged()`、`OnPropertyChanged(nameof(CurrentSheet))`等を発火）。
  `MainWindow.xaml.cs:68-78`の`ViewModel_PropertyChanged`がCurrentSheetのPropertyChangedを
  購読し`RedrawCanvas()`を呼ぶ（オブジェクト参照の同一性は問わない、プロパティ名一致のみで
  発火）。**Grid.Rowsを変更した後`NotifyCurrentSheetChanged()`を呼べば、既存の仕組みで
  再描画が自動的にトリガーされる**（新規の再描画配線は不要）。
- **MarkDirty()流儀**: Undo機構は未実装確定済み（`MainWindowViewModel.cs:82-86`コメントで
  再確認）。行＋/行－も他操作同様「直接プロパティ変更＋`MarkDirty()`」でよい。
- **警告表示の手段（殿裁定2の実装先）**: `MainWindowViewModel.cs:1285-1292`の`StatusMessage`
  プロパティ（ステータスバー案内文言、既存はセル未選択時の配置操作案内等に使用）。
  削除拒否時はここへ警告文言をセットする方針でよいと思われる（他に確立されたダイアログ/
  警告表示の仕組みは見当たらず、`MessageBox.Show`の使用例もsrc内に無い）。
- **既存GridSpec既定値の2箇所**（隠密調査書時点の行番号、要再確認——ファイル編集で
  ズレている可能性あり）: `SheetNavigationViewModel.cs`（AddCommand内）・
  `MainWindowViewModel.cs`（NewDocument内）、いずれも`new GridSpec{Rows=10,Columns=20}`。
  殿裁定3「既定10」なので**この2箇所は変更不要**（既に10）。

### 実施したが未検証

なし（コード変更は一切未着手）。

### 未着手・スキップ

- 上限/下限クランプの定数化（置き場所は要検討——後述「次の1手」参照）
- `AddRowCommand`/`DeleteRowCommand`（仮称）の実装
- ツールバーボタン・キーボードショートカットの実配線
- RED先行証明・全件テスト・コミット・家老報告

## 3. 試して失敗したアプローチと結果

コード面の試行はゼロ（失敗アプローチ無し）。離脱理由は**出力破損の同種2回目検知**（Grepツール
結果への`//`→`\ `化け、`docs-notes/output-corruption-log.md` #6・#7参照）:

- #6: `MainWindow.xaml.cs`のボタンハンドラ名grep（`-A 8`）→6箇所(190,1065,1317,1461,1475,1480行)
  で`//`が`\ `に化けて表示
- #7: `StatusMessage = `の狭域grep（`-B 2`）→`MainWindowViewModel.cs:1546`で同型の化け
- **両件とも実ファイルは無傷**（Read直読で確認済み）。破損は常にGrep結果側のみ、行頭の
  コメントトークン(`//`)に限局
- **次の侍への教訓**: Grepの使用そのものが誘因になっている疑いが強い（前セッション#3・#4も
  同型）。本ファイル（引き継ぎ書）に必要な情報は極力転記済みゆえ、**再開後はGrepを避け、
  必要箇所はRead（offset/limit指定）で直接読むこと**。やむを得ずGrepを使う場合、結果のコメント
  行は鵜呑みにせずRead直読で裏取りする。同種2回目が出たら本件同様に即離脱。

## 4. スコープ境界

- 触ってよい: `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（新規コマンド・定数）、
  `src/Ecad2.App/MainWindow.xaml`（ツールバーボタン）、`src/Ecad2.App/MainWindow.xaml.cs`
  （キーボードショートカット、`Window_PreviewKeyDown`）、対応する`tests/Ecad2.App.Tests/`配下
- 触らぬ: 増分2（シート設定ダイアログ）・増分3（任意位置挿入/削除・コンテキストメニュー）は
  今回の増分1範囲外。他の無関係な機能・ファイルへの波及は便乗拡大禁止

## 5. 次の1手（具体的な実施計画）

### 実装方針（次の侍は妥当性を自分でも確認の上で採用可否を判断せよ）

1. **上限/下限定数の置き場所**: `src/Ecad2.Core/Model/Sheet.cs`の`GridSpec`クラスへ
   `public const int MinRows = 1;`・`public const int MaxRows = 60;`として新設することを推奨
   （増分2のダイアログ側からも同じ定数を参照でき、二重定義を避けられる）。要検討・決定は次の侍に委ねる。
2. **`MainWindowViewModel`へ新規コマンド追加**（構成要素）:
   ```csharp
   public ICommand AddRowCommand { get; }
   public ICommand DeleteRowCommand { get; }
   ```
   コンストラクタ内で初期化（他のフィールド初期化と同じ箇所）:
   - `AddRowCommand`: `CurrentSheet is Sheet sheet && sheet.Grid.Rows < GridSpec.MaxRows`のとき実行可能。
     実行時: `sheet.Grid.Rows++`→`MarkDirty()`→`NotifyCurrentSheetChanged()`
   - `DeleteRowCommand`: `CurrentSheet is Sheet sheet && sheet.Grid.Rows > GridSpec.MinRows`のとき実行可能。
     実行時: **最終行(`sheet.Grid.Rows - 1`)に要素が存在するかを判定**（要判断、下記「要確認」参照）
     →存在すれば`StatusMessage`へ警告文言をセットして中断→存在しなければ`sheet.Grid.Rows--`→
     `MarkDirty()`→`NotifyCurrentSheetChanged()`
3. **ツールバーボタン**（`MainWindow.xaml:174`のSeparator後、Band=0）:
   ```xml
   <Separator/>
   <Button Style="{StaticResource ToolBarButtonStyle}" ToolTip="行を追加 (Ctrl+Shift+Up)"
           AutomationProperties.Name="行を追加 (Ctrl+Shift+Up)"
           Command="{Binding AddRowCommand}">
       <StackPanel>
           <Path Style="{StaticResource ToolBarIconStyle}" Data="(要デザイン)"/>
           <TextBlock Style="{StaticResource ToolBarKeyLabelStyle}" Text="Ctrl+Shift+↑"/>
       </StackPanel>
   </Button>
   <Button Style="{StaticResource ToolBarButtonStyle}" ToolTip="行を削除 (Ctrl+Shift+Down)"
           AutomationProperties.Name="行を削除 (Ctrl+Shift+Down)"
           Command="{Binding DeleteRowCommand}">
       ...
   </Button>
   ```
   アイコン(Path Data)は新規デザインが要る（他の`ToolBarIconStyle`アイコンの座標系(0-16/0-18程度の
   グリッド)に合わせる、既存アイコンのpath dataを参考に）。
4. **キーボードショートカット**（`MainWindow.xaml.cs:589`の`Window_PreviewKeyDown`、
   既存Ctrl+S/O/N=865-877行付近に倣い追加）:
   ```csharp
   case Key.Up when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
       _viewModel.AddRowCommand.Execute(null);
       e.Handled = true;
       break;
   case Key.Down when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
       _viewModel.DeleteRowCommand.Execute(null);
       e.Handled = true;
       break;
   ```

### 【裁定済み・2026-07-10殿裁定＝広義】「要素の存在する行」の判定範囲

**殿裁定が下りた：広義=5種すべて**（ElementInstance・VerticalConnector・WireBreak・GroupFrame・
RungCommentのいずれかが最終行にあれば削除拒否。詳細は`docs/todo.md` T-055節の補足裁定）。
以下は裁定前の論点整理（経緯として残置）。

### （裁定前の論点整理・経緯）「要素の存在する行」の判定範囲

殿裁定2「要素の存在する行の削除は拒否」の「要素」が何を指すか、計画書・todo.mdに明記が無い。
ecad2の用語では「要素」=`ElementInstance`（配置済み部品、`sheet.Elements`）を指すのが通常の語感
（`IsSelectedCellOccupied()`等の既存コードでも「要素」はElementInstanceの意）。一方、最終行には
`VerticalConnector.TopRow/BottomRow`・`WireBreak.Row`・`GroupFrame`(範囲)・`RungComment.Row`も
存在しうる。**狭義（ElementInstanceのみ）で実装するか、広義（5種類全て）にするかは家老へ一声
確認してから実装するのが安全**（曖昧な仕様のまま実装すると往復の原因になる、CLAUDE.md
「不明な点は必ず質問する」の原則）。RED証明・テスト設計もこの判定に依存するため、着手前に
確定させること。

### RED先行証明の手順（想定、上記論点確定後に確定）

1. 上限クランプ: Rows=60でAddRowCommand実行→Rows=60のまま（CanExecuteでボタン自体無効化される
   ため、CanExecute自体のテストと、仮に強制実行された場合の内部ガードの両方を検討）
2. 下限クランプ: Rows=1でDeleteRowCommand実行→Rows=1のまま
3. 最終行に要素がある場合の削除拒否（論点確定後）
4. 通常加減算: Rows=10→追加→11、削除→10
5. 全件テスト（現行268件＋新規分）が合格することを確認
6. パス限定コミット→家老へ三区分報告

### 家老・隠密への申し送り

- 家老へ: 本離脱の事実・引き継ぎ書の所在を報告すること（次の侍再開時、または家老自身が
  引き継ぎ内容を把握しておくため）
- 隠密へ: T-050 Stryker棚卸しのビルド専有は問題なし（侍はsrc/testsへの変更を一切行っておらず
  ビルドも実行していないため、ビルド競合の懸念は無い旨を伝えること）
