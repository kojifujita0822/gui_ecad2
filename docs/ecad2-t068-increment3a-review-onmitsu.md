# T-068 増分3-a 静的レビュー（隠密、1周目・軽量既定）

対象コミット：`de00c03`（`feat(app): T-068増分3-a - パーツエディタを単一画面構成へ再設計`）
対象ファイル：`src/Ecad2.App/Views/PartEditorDialog.xaml`（+86/-54相当）・`PartEditorDialog.xaml.cs`（コメント2箇所のみ）
参照：`docs/ecad2-t068-increment3-design-onmitsu.md`（隠密設計書§3.1・分岐A/H）、`docs/todo.md` T-068節（1927-1943行、DoD5点・家老仮決定1）

## 結論：要修正なし

台帳DoD5点全て充足、既知トラップ2件（SetProperty早期return罠・PR-13型CanEditDiagramガード漏れ）とも
該当なし。家老が最大リスクと見た「レイアウト全面組み替えによる結線切れ」は、本ダイアログの実装方式上
原理的に発生し得ないと判定した。軽微な気づき2件（経過観察）を申し送る。

## 台帳DoD5点との整合確認

`docs/todo.md`1935-1943行に記載の増分3-a DoD5点を、diffと突き合わせて確認した。

| # | DoD | 確認結果 |
|---|---|---|
| 1 | タブ廃止・原本Row0-3相当の単一画面へ再設計 | OK。`TabControl`削除、`Grid`5行構成（Row0=プロパティ／Row1=ツールバー領域／Row2=キャンバス+端子リスト／Row3=エラー表示／Row4=OK・キャンセル） |
| 2 | 440x420固定/NoResize→800x600・リサイズ可能 | OK。`Height="600" Width="800" MinHeight="420" MinWidth="640" ResizeMode="CanResize"` |
| 3 | プロパティ編集は`x:Name`不変ゆえコードビハインド無変更で機能保持 | OK。`PartEditorDialog.xaml.cs`の差分はクラスコメント2箇所のみ、ロジック本体（`OkButton_Click`等）は無変更 |
| 4 | キャンバス領域はプレースホルダで確保 | OK。Row2左`Border`に「形状編集キャンバス(実装予定)」のプレースホルダTextBlock、`MinHeight="160"` |
| 5 | 端子リストはRow2右列へ暫定残置、3-cで`ColumnDefinition`と`PortsPanel`を削るだけで除去できる構成 | OK。`DockPanel x:Name="PortsPanel"`として独立した列（`ColumnDefinition Width="Auto"`）に分離済み |

## 最大懸念点（結線切れ）の検証

家老采配の重点観点「レイアウトの全面組み替えでバインディング・イベントハンドラの結線が実は切れていない
か（`x:Name`が同じでも親要素の入れ替えで`FindName`スコープや`DataContext`の継承が変わりうる）」について。

**結論：原理的に発生し得ない。**

根拠：
1. `PartEditorDialog`はDataContext非依存・コードビハインド直接`x:Name`参照方式（`RenameDialog`等の
   既存モーダルダイアログと同型）。XAML全編（差分後の全体）を確認したが`{Binding ...}`構文は一切使用
   されていない
2. `NameBox`/`WidthBox`/`HeightBox`/`RoleCombo`/`ErrorText`/`PortsGrid`はいずれも`x:Name`で宣言され、
   コードビハインド（`OkButton_Click`・コンストラクタ等）から直接プロパティアクセスされている
   （`NameBox.Text`・`RoleCombo.SelectedItem`・`PortsGrid.CommitEdit`等）
3. WPFの`x:Name`解決は`InitializeComponent()`が呼ぶ`Connect(int connectionId, object target)`
   メソッド内でBAMLのconnectionIdに基づき直接フィールドへキャストする仕組みであり、`FindName`の
   実行時スコープ探索とは異なる。Visual Treeの親子構造（`TabControl`配下→`WrapPanel`/`Grid`/`Border`/
   `DockPanel`配下への変更）に依存せず、`x:Name`の一意性のみが要件（本ファイル内で重複なし、ビルド
   成功もこれを裏付ける）
4. コンストラクタ内`RoleCombo.Items.Add(...)`（47行目）等の参照も`InitializeComponent()`完了後に
   実行されるため、レイアウト変更の影響を受けない
5. 懸念が現実になり得るのはMVVMパターン（`DataContext`継承＋`{Binding}`）で、かつ`DataTemplate`/
   `ItemsControl`のitem container等、暗黙に新しい`DataContext`スコープが生まれる要素を挟むケース。
   本ダイアログはこの型に該当しない

## スコープ境界の確認

- **Core層無改変**：diff対象は`src/Ecad2.App/Views/`配下2ファイルのみ、`src/Ecad2.Core/`への変更なし
- **形状編集ロジック未着手**：Row1（ツールバー）・Row2左（キャンバス）はいずれも
  「(実装予定)」のプレースホルダTextBlockのみ、`PartPrimitive`関連の操作コードは追加されていない
- **接続点ツール未着手**：`AddPortButton_Click`/`DeletePortButton_Click`/`PortsGrid`はいずれも増分2の
  実装のまま無変更（Row2右列へ位置移動のみ）

## 侍自己申告の範囲外変更2点の確認

侍の自己申告「着手時に範囲外の変更2点（プロパティ欄ラベルのToolTip移動・DataGrid列ヘッダ短縮）が
無意識に紛れていたのを報告前の自己点検で発見・是正、最終差分には不含」について、diffを直接確認：
- `ToolTip`属性：差分全体をgrepしたが該当箇所なし
- DataGrid列ヘッダ（"名前"／"行オフセット"／"境界オフセット"）：増分2コミット時点の文字列と完全一致、
  短縮なし

**申告通り、最終差分には含まれていないことを確認した。**

## 新規DynamicResourceキーの実在確認

Row1・Row2で新規に参照される`ToolBarBackgroundBrush`・`ToolBarForegroundBrush`・
`WorkAreaBackgroundBrush`（`DynamicResource`は実行時解決のためビルド成功だけでは未定義を検出できない）
について、`Theme.Dark.xaml`・`Theme.Light.xaml`両方に定義済みであることを確認した：
- `WorkAreaBackgroundBrush`：Dark `#FF202224`／Light `White`
- `ToolBarBackgroundBrush`：Dark `#FF2D2D30`／Light `#F0F0F0`
- `ToolBarForegroundBrush`：Dark `#FFF0F0F0`／Light `#000000`
- `DialogBorderBrush`（Border枠線に使用）：Dark `#FF565656`定義確認済み

いずれも既存の確立済みリソース（他パネルで既に使用中）を流用しており、未定義リソース参照の懸念なし。

## 既知トラップの狙い撃ち

- **PR-03（SetProperty早期return罠）**：該当なし。本コミットの変更はXAMLレイアウトの組み替えと
  クラスコメント変更のみ、SetPropertyパターンを一切含まない
- **PR-13型（CanEditDiagramガード漏れ）**：該当なし。呼び出し元（`MainWindow.xaml`のメニュー導線）は
  無変更。本ダイアログ自体には元々CanEditDiagram概念が存在しない（増分2レビュー時と同じ結論）

## 気づき2点（経過観察、要修正ではなし）

a. `MinHeight="420" MinWidth="640"`の数値根拠がコード上明示されていない。3-b（ツールバー実装）・
   3-c（接続点統合）完了後、実際にプロパティ欄・ツールバー・キャンバスが窮屈にならないか実機確認で
   確認する価値がある
b. Row2キャンバスプレースホルダの`MinHeight="160"`が、増分0 PoC実機（ウィンドウ900x650相当）と比べて
   十分な作業領域か未知数。3-b実装時の検証観点として申し送る

## 不明点

なし。

## 派生提案

なし。
