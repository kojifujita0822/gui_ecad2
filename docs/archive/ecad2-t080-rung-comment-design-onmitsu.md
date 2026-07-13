# T-080 行コメント機能 着手前設計調査（隠密）

- 依頼元: 家老（2026-07-12、殿直接指示・起票済みコミット`4e57767`）
- 前提: 右母線の右側に記入する行コメント。GuiEcadでは記入・印刷とも対応、ecad2では記入手段が
  App層に一切無く印刷にも出ない（データが空のため）。殿指定トリガー＝右母線の右側クリック。
- 既存関連調査: `docs/ecad2-t069-context-menu-design-onmitsu2.md`（RungCommentモデルはCore
  完備・編集UIはApp層皆無、と本調査でも同じ結論。ただし今回はGuiEcad原本の入力UI詳細を新規調査）
- 調査のみ・実装せず。出典は全てファイルパス:行。事実と推測は明示区分。

## DoD(1): GuiEcad原本の実物挙動

`C:/Users/kojif/Desktop/生産物/gui_ecad/`を読解（隠密2相当の別セッションへ委譲・査読済み）。

### 入力トリガー
- 右母線の右側領域を**ダブルクリック**（`GuiEcad.App/MainPage.Pointer.cs:181-194`）。
  ヒット領域は`xMm > rightBusEdge`（`rightBusEdge = 右母線X + セル幅の半分`）かつ行が
  描画範囲内（`Grid.Rows`と要素最大行+1の大きい方）。描画モード時のみ（テストモード中は不可）。
- ダブルクリック自体は自前実装（タイムスタンプ差分＋8pxトレランス、`MainPage.Pointer.cs:162-171`）。
  **ecad2はWPFネイティブの`MouseButtonEventArgs.ClickCount==2`が使えるため、この自前実装は不要**
  （実装が単純化できる）。
- ヘルプテキストにも明記（`MainPage.Menu.cs:512`）：「右母線の右側をダブルクリック→行コメントを入力」。
- 【注意】F2キーの「コメント」機能（`ShowCommentEditor`、`MainPage.Pointer.cs:898`）は**要素単位の
  別プロパティ**であり、行コメント（`RungComment`）とは無関係。混同注意。

### エディタUIの形
- キャンバス上インラインTextBoxオーバーレイ（ダイアログではない）。`MainPage.xaml:592-604`、
  幅140px・PlaceholderText「行コメント」、初期`Visibility=Collapsed`。
- `ShowRungCommentEditor`（`MainPage.Pointer.cs:948-960`）：クリック位置からMargin計算で位置決め
  （右母線端+4px、行Y位置-14px）、Text設定→Visible→Focus→SelectAll。
- 【重要な癖】既存コメントが無い行を初めてダブルクリックした瞬間、**編集完了を待たず即座に
  空のRungCommentがUndoコマンド経由でSheet.RungCommentsへ追加される**（`MainPage.Pointer.cs:191`）。
  直後にキャンセルしても空エントリがモデルに残留する（実害は表示上ゼロだが、モデル上は
  ノイズになる。DoD(4)論点5参照）。

### 確定/キャンセル
- Enter/Tab＝確定、Escape＝キャンセル（`OnRungCommentBoxKeyDown`、`MainPage.Pointer.cs:979-985`）。
- フォーカスロスト＝確定扱い（キャンセルではない、`OnRungCommentBoxLostFocus`、
  `MainPage.Pointer.cs:987-990`）。
- 確定時、テキストをTrim（`MainPage.Pointer.cs:972`）、変化があればUndoコマンド
  （`SetRungCommentCommand`）で反映。**空文字列は削除ではなく空文字列として保存**（モデルから
  RungCommentエントリ自体は消えない、`DiagramRenderer.cs:947`側の`!string.IsNullOrEmpty`
  判定で描画のみスキップされる）。

### 表示位置・描画様式
- `DrawRungComments`（`GuiEcad.Core/Rendering/DiagramRenderer.cs:940-949`）：右母線+2mm、
  `TextRole.DeviceName`スタイル流用（左揃え・FontSizeMm=3.0）。折返し・省略処理なし、単純
  `DrawText`1回のみ。**ecad2のDrawRungComments実装（DiagramRenderer.cs:972-980）と完全一致**
  （移植済み・対応不要）。
- ページ幅計算：`PageSize`メソッド（`DiagramRenderer.cs:120-134`）で、**`enableBorder=false`時のみ**
  最長行コメント文字数分だけページ幅を広げる（`rightExtra = 2.0 + maxRungLen*3.3 + MarginMm`）。
  `enableBorder=true`（枠あり・用紙固定サイズ）時はこの考慮が無い＝**長い行コメントは用紙から
  はみ出て切れうる**。ecad2はT-060裁定で「常に枠あり」に確定済みのため、この未対応領域が
  そのままecad2にも引き継がれる（DoD(4)論点3参照）。

### 既存コメントの編集・削除
- 新規作成と同じトリガー（再度ダブルクリック）で、既存テキストを読み込んでエディタを再オープン
  （`MainPage.Pointer.cs:951`）。「削除」は空文字列にするのみ（上記の通りモデルからは消えない）。
- 専用の右クリックメニュー項目は**見つからず**（T-069調査書の「コメント編集」項目はF2の要素
  コメントを指しており、行コメントとは別物と判明）。

### PDF出力への反映
- 画面・PDFとも同一`DiagramRenderer.Render`経由で統一済み（`MainPage.Drawing.cs:51`＝画面、
  `MainPage.Menu.cs:250-252`＝PDF出力、`PdfPreviewDialog.xaml.cs:43`＝プレビュー）。
  ecad2の既存設計（DiagramRenderer共通化）と同型、対応不要。

### Undo対応
- 完全対応。`AddRungCommentCommand`（追加、`Commands/ElementCommands.cs:379-387`）・
  `SetRungCommentCommand`（テキスト変更、`ElementCommands.cs:366-377`）。行削除コマンドも
  該当行のRungCommentをExecute/Undo双方で追随（`ElementCommands.cs:284,299-300,320,445,459-460,484`）。

## DoD(2): ecad2側の現状

- **モデル**：`RungComment{ int Row; string Text; }`（`src/Ecad2.Core/Model/Sheet.cs:51-55`）、
  `Sheet.RungComments`（同18行）。GuiEcadと完全同型。
- **RowOps**：行挿入・削除時のシフト/削除処理、既に完全対応済み
  （`src/Ecad2.Core/Model/RowOps.cs:26-27,60-61,78-79`）。
- **描画**：`DiagramRenderer.DrawRungComments`（`src/Ecad2.Core/Rendering/DiagramRenderer.cs:972-980`）
  で画面・PDF共通描画に**既に対応済み**（右母線+2mm、FontSizeMm=3.0、GuiEcadと同一実装）。
  ページ幅計算（`PageSize`、同132-137行）もGuiEcad同様、`enableBorder=false`時のみ考慮。
- **永続化**：`GcadSerializer`は`LadderDocument`全体をSystem.Text.Jsonで自動シリアライズ
  （`src/Ecad2.Core/Persistence/GcadSerializer.cs`）。`RungComments`も自動的に含まれ、明示対応
  不要・既に永続化可能。
- **App層の欠落範囲**：
  - ヒットテスト（右母線右側クリック判定）：存在せず。ecad2のマウス処理入口は
    `LadderCanvasHost_PreviewMouseLeftButtonDown`（`src/Ecad2.App/MainWindow.xaml.cs:393`）だが、
    右母線右側領域を判定する分岐は無い。`DiagramRenderer.RightBusX`は`private`
    （`DiagramRenderer.cs:105`）のため、App層から使うには`internal`公開または専用HitTestメソッド
    の新設が必要。
  - 入力UI：皆無。
  - 保存連動（MarkDirty）：皆無（入力UI自体が無いため）。
  - **唯一のApp層参照**：`MainWindowViewModel.cs:1354,1361`、行削除拒否判定
    （`sheet.RungComments.Any(rc => rc.Row == row)`）で「削除保護対象」として参照されるのみ
    （T-055增分1、殿裁定2026-07-10）。編集UIとは無関係。

## DoD(3): 実装方式の叩き台

### 新規実装が要る箇所
1. **ヒットテスト**：`LadderCanvasHost_PreviewMouseLeftButtonDown`に「右母線右側領域＋行範囲内」の
   分岐を追加。`DiagramRenderer`側に`RightBusX`相当を`internal`公開する、または専用の
   `HitTestRungCommentRow(Point, Sheet) -> int? row`メソッドを新設するのが既存のHitTest系
   （`HitTestConnector`等、同ファイル210行目以降）と一貫性が取れる。
2. **ダブルクリック検出**：WPFネイティブの`MouseButtonEventArgs.ClickCount==2`で足りる
   （GuiEcadの自前タイムスタンプ実装は不要、実装簡略化）。
3. **入力UI**：単一TextBoxのキャンバス上オーバーレイ。T-033の`ElementPlacementBar`
   （`MainWindow.xaml:584-594`、`MainWindow.xaml.cs:1541-1620`付近）と同じ「同一Window内
   オーバーレイ、Popup不採用」の設計方針が踏襲できる。ただし今回は種別選択等が不要な単純な
   1TextBoxで済むため、規模はElementPlacementBarよりかなり小さい。位置計算
   （`PositionPlacementBar`のTranslatePoint経由パターン、`MainWindow.xaml.cs:1604-`）も
   流用可能な参考実装。
4. **モデル操作**：`Sheet.RungComments`への追加/更新（既存の`RowOps`シフト対応は変更不要）。
5. **MarkDirty連動**：新規実装。T-065/T-066往復での教訓（値未変更時にMarkDirtyしない同値ガード
   規約）を踏まえ、最初から「テキストが実際に変化した場合のみMarkDirty」で設計するのが望ましい。
6. **Undo対応**：不要（下記参照）。

### 既存パターンの流用可否
- **T-033浮動インラインバー**：同一Window内オーバーレイという設計方針・位置計算ロジックは
  流用価値が高い。ただしUI規模（種別選択+デバイス名+アイコン群）は不要、単一TextBoxで足りる。
- **RecordSnapshot Undo**：**流用しない（対象外）**。ecad2のUndo基盤はMVPスコープ限定
  （`MainWindowViewModel.cs:1817-1818`のコメント「MVP対象範囲はSheetNavigationViewModelの
  シート追加/削除のみ」）であり、要素配置・機器名編集等も同様にUndo非対応が既存の一貫した設計
  判断。GuiEcadのコマンドベースUndo（`AddRungCommentCommand`/`SetRungCommentCommand`）は
  移植せず、ecad2の既存方針（Undo非対応）に合わせるのが自然（DoD(4)論点4参照、要殿確認）。

## DoD(4): UI/UX分岐（殿確認へ回す論点）

1. **トリガーがシングルクリックかダブルクリックか**：殿指示は「右母線の右側クリック」（原文まま）。
   GuiEcad原本は**ダブルクリック**。GuiEcad踏襲ならダブルクリックだが、殿の「クリック」という
   文言がシングルクリックを意図している可能性もあり、着手時に確認が必要。
2. **空文字列の扱い**：GuiEcadは「削除ではなく空文字列のまま保存」（モデルにはエントリが残る、
   表示のみスキップ）。ecad2でも同じ挙動にするか、空にしたら実際に`RungComments`リストから
   除去する（よりクリーンなモデル）にするかは設計判断。
3. **常に枠あり（`enableBorder=true`固定、T-060裁定済み）時の長い行コメントの扱い**：GuiEcad原本
   でも枠あり時はページ幅拡張ロジックが無く、長い行コメントは用紙からはみ出て切れる可能性がある
   （GuiEcadでも実質未対応の領域）。ecad2で「GuiEcad踏襲＝そのまま切れる仕様を受け入れる」か、
   何らかの対策（文字数の目安をヘルプ等で示す、自動縮小等）を新規に講じるかは要確認。
4. **Undo非対応の追認**：DoD(3)で述べた通り、行コメント編集もecad2の既存方針
   （Undo基盤はMVPスコープ外）に合わせて非対応とすることの明示的な追認が欲しい。
5. **キャンセル時の空エントリ残留挙動**：GuiEcadは新規作成トリガー時点で即座に空のRungCommentを
   モデルへ追加し、キャンセルしても残留する癖がある。ecad2でも同じ挙動にするか、「Enter確定まで
   モデルへ触れない」というよりクリーンな設計にするかは実装方針の分岐点（UXの見え方に差は
   ほぼ無いが、実装の単純さに影響する）。

## 出典一覧

- `src/Ecad2.Core/Model/Sheet.cs`（RungComment定義、Read）
- `src/Ecad2.Core/Model/RowOps.cs`（行シフト対応、Read）
- `src/Ecad2.Core/Rendering/DiagramRenderer.cs`（DrawRungComments・PageSize、Read）
- `src/Ecad2.Core/Persistence/GcadSerializer.cs`（永続化方式、Read全文）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（RungComments唯一の参照箇所、grep確認）
- `src/Ecad2.App/MainWindow.xaml.cs`（マウス処理入口・T-033配置バー実装、grep+Read）
- `src/Ecad2.App/MainWindow.xaml`（配置バーXAML、Read）
- `docs/ecad2-t069-context-menu-design-onmitsu2.md`（既存関連調査、Read）
- GuiEcad原本（サブエージェント経由で調査、査読済み）：
  `GuiEcad.App/MainPage.Pointer.cs`（トリガー・エディタ・確定/キャンセル・Undo呼び出し）、
  `GuiEcad.App/MainPage.xaml`（インラインTextBox宣言）、`GuiEcad.App/MainPage.Menu.cs`
  （ヘルプ文言・PDF出力配線）、`GuiEcad.App/Commands/ElementCommands.cs`（Undoコマンド）、
  `GuiEcad.Core/Rendering/DiagramRenderer.cs`（DrawRungComments・PageSize）、
  `GuiEcad.Core/Model/Sheet.cs`（RungCommentモデル定義）

## 不明点

- なし（全項目、原本コード・ecad2既存コードの直接確認で判明。DoD(4)は不明点ではなく殿判断事項）。

## 派生提案の有無

なし（本調査はT-080着手前調査のスコープ内で完結）。
