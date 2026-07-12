# T-082 シート並び替え機能 静的コードレビュー(隠密)

- 対象コミット: `9c147ce`（侍実装、src/Ecad2.App 3ファイル+新規Adorner1件、tests 1件）
- 実施日: 2026-07-12
- 実施者: 隠密
- 方式: 手動観点レビュー(家老指定の検証観点+SetProperty早期returnの罠)＋`code-review`スキル併用(effort high、10角度並列finder→集約)
- スコープ境界: レビューのみ、書き込みなし。実機領分(Alt+上下の実機反応、ドラッグ視覚)は対象外

## 結論サマリ

DoD(1)〜(6)は実装済みで単体テストも通っているが、**このコードベースが過去に繰り返し踏んだ「SetProperty早期return／CurrentSheetIndex数値不変時のクロスカット処理」トラップの再発**が1件、**DRC出力パネルとの整合崩れ**が1件、確度の高い要修正事項として見つかった。いずれも見た目に出にくいシナリオ(無関係シートの並び替え、DRC実行後の並び替え)で発現するため、単体テストの13(実11)件では検出できていない。

## 要修正（confirmed相当、複数観点で独立検出）

### 1. MoveSheetCommandが「所見L」パターンを再発させている

**該当**: `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs:226-229`

```csharp
int newIndex = selectedSheetBeforeMove is null ? -1 : Sheets.IndexOf(selectedSheetBeforeMove);
if (newIndex >= 0) _owner.SetCurrentSheetIndexCore(newIndex);
```

`selectedSheetBeforeMove`（移動前に選択中だったシート）が存在する限り、移動対象がそのシート自身であろうとなかろうと、**newIndexは常に`>=0`になるため、MoveSheetCommand実行のたびに毎回`SetCurrentSheetIndexCore`が無条件で呼ばれる**。`SetCurrentSheetIndexCore`（`MainWindowViewModel.cs:192-197`）は値変化の有無に関わらず常時`SelectedCell = null`を実行する既存仕様（T-041由来）。

**失敗シナリオ**: シート1(選択中、キャンバス上でセルを選択済み or 配線ドラフト編集中)を表示中に、無関係なシート2とシート3だけをドラッグ&ドロップで入れ替える。シート1のindexは0のまま変化しないが、`SetCurrentSheetIndexCore(0)`は無条件で呼ばれ、シート1の内容は何も変わっていないのに選択中セル・記入中ドラフトが警告なく消える。

これは`RenameCommand`が既に踏んで修正済みの問題と同型（`SheetNavigationViewModel.cs:189-197`のコメント参照、「改名は同一シートに留まる操作のため、CurrentSheetIndexへの再代入は不要」として対処済み）。MoveSheetCommandはこの教訓を踏まえていない。

**検出経路**: 自分の手動レビュー観点（SetProperty早期returnの罠、家老指定）+ code-reviewスキルAngle A/B/C の3系統独立検出。

### 2. DRC出力パネルのPageNumberキャッシュとの整合崩れ

**該当**: `SheetNavigationViewModel.cs:222`（全シートPageNumber再採番）と `OutputPanelViewModel.cs:93`（PageNumberでシート同定）

`Diagnostic`は`sealed record`（`DesignRuleCheck.cs:9`）で、`Locations`内の`PageNumber`はDRC実行時点の`sheet.PageNumber`を**値としてコピー**して埋め込む（例: `DesignRuleCheck.cs:71`）。参照ではないため、後からシートのPageNumberが変わっても追従しない。

一方`OutputPanelViewModel.Diagnostics`は、RunDrc再実行時と`ClearResults()`（Document差し替え=新規/開く時のみ）以外ではクリアされない。`MoveSheetCommand`はどちらも呼ばない。

**失敗シナリオ**: DRC実行→出力パネルに診断一覧表示→シート並び替え(全シートPageNumber再採番)→画面に残ったままの古い診断行をクリック→`JumpTo`(`OutputPanelViewModel.cs:93`)が古いPageNumber値で`Document.Sheets.FindIndex`を引き、**並び替え後にそのPageNumberを持つ別のシートへ誤ジャンプする**。

**検出経路**: code-reviewスキルAngle B/C/D/Iの4系統独立検出＋隠密が`Diagnostic`型定義・`ClearResults`呼び出し箇所を読解して裏取り済み（殿からの確認質問に対する回答でも実証済み）。

### 3. SelectedSheetのPropertyChanged通知が一度も発火されない

**該当**: `SheetNavigationViewModel.cs:206-229`(MoveSheetCommand全体)

`AddCommand`(141行目)・`DeleteCommand`(165行目)・`RenameCommand`(200行目)は全て、`SetCurrentSheetIndexCore`呼び出し後(または index不変時)に`OnPropertyChanged(nameof(SelectedSheet), oldValue)`または`RefreshSelectedSheet(oldValue)`を明示的に発火する規約を持つ。`MoveSheetCommand`だけこれを一切呼ばない。

XAML側は`SelectedItem="{Binding SheetNavigation.SelectedSheet}"`(TwoWay)。選択中シート自身をドラッグ移動した場合、`Sheets.RemoveAt`→`Insert`の2イベント(Move()でなくRemove+Add)の間にWPFのListBox.Selectorが選択状態を一時的に見失う可能性があり、通知が来ないため復旧されない懸念がある(理論的懸念、実機確認要)。

**検出経路**: code-reviewスキルAngle A/C/E の3系統独立検出。実害の有無(見た目に影響するか)は理論上の議論に留まり、忍者の実機確認で最終判定が必要。

## 経過観察（実害は軽微〜理論的、次増分の隙間で検討可）

- **ObservableCollection.Move()未使用**(`SheetNavigationViewModel.cs:218-219`): `RemoveAt`+`Insert`はRemove/Addの2イベントを発火させコンテナを破棄・再生成する。標準の`Sheets.Move(fromIndex, toIndex)`なら1イベントで済みコンテナが再利用される。Angle Aは「移動直後にキーボードフォーカスがSheetNavList外へ抜け、連続Alt+下操作ができなくなる」という具体的な実機影響シナリオを提示(実機確認要)。
- **DoDragDropペイロードのシリアライズ懸念**(`MainWindow.xaml.cs:1943`): `Ecad2.Model.Sheet`の生オブジェクトをそのまま`DoDragDrop`に渡している。同一プロセス内ドラッグでは通常シリアライズ不要だが、コードベース内唯一のDoDragDrop使用箇所で先例が無く実機未検証(侍申告どおり忍者領分)。
- **CanExecute/Executeの契約非対称**(`SheetNavigationViewModel.cs:231`): `param`が`ValueTuple<int,int>`でない場合、CanExecuteは`Sheets.Count > 1`を返すがExecuteは無条件no-op。現在の呼び出し元(2箇所)は常にタプルを渡すため実害なしだが、将来XAMLで直接バインドされると「ボタン有効なのに押しても無反応」を生む。else分岐の実質的な死コードでもある(code-reviewスキルAngle D/G/I 3系統検出)。
- **try/finally欠如**(`MainWindow.xaml.cs:1942-1945`): `DoDragDrop`呼び出し中に例外が発生すると、`Opacity`復元と`RemoveSheetReorderAdorner`が実行されずUI状態が壊れたまま残る。
- **軽微な重複**: `insertAfter`判定式(上半分/下半分の中央線判定)がDragOverとDropで一字一句重複(Angle F/G検出)。MoveSheetCommand呼び出しパターン(CanExecute確認→Execute)がMoveCurrentSheetとSheetNavList_Dropで重複。`FindAncestor<T>`は既存のWPF標準API`ItemsControl.ContainerFromElement`で代替可能(車輪の再発明、Angle F検出)。ドラッグしきい値判定が既存の円形(ユークリッド距離)判定と異なる矩形(チェビシェフ)判定を新規採用しており、既存4箇所(コネクタ/ワイヤーブレーク/フリーライン/接続点)のパターンから逸脱。
- **Alt+上下のF10類推コメントがやや不正確**(`MainWindow.xaml.cs:942-949`): F10は「単体キーでもシステムキー扱い」という個別仕様が根拠だが、Alt+上下は「Alt押下中は任意のキーがシステムキー扱い」という一般規則であり根拠が異なる。実装(条件分岐)自体は理論上正しく書き分けられているが、「F10と同型」というコメントの説明はやや不正確。実機検証は既に忍者確認予定として認識済み。
- **PageNumber全件再採番・Adorner毎回再生成の非効率**: シート数が多い場合の軽微な無駄(数十枚規模でなければ実害は無視できる)。

## テストコード網羅性点検

追加テスト、コミットメッセージは「13件」だが実カウントは**11件**(diffのFactメソッド数、軽微な数値誤り)。カバー観点と抜けは以下:

**カバー済み**: CanExecute境界(1枚のみ/先頭を超えて上/末尾を超えて下/from==to)、Execute実行(末尾→先頭の移動+ミラー整合)、PageNumber再採番、MarkDirty、選択追従(移動対象=選択中/非選択中)、Undo対象外、保存読込往復。

**抜け**:
1. 境界値分析でいう「下限-1／上限+1」(異常系)はCanExecuteでカバーされているが、「下限そのもの／上限そのもの」(先頭シートを隣接1つ下へ、末尾シートを隣接1つ上へ)の**実行結果検証**テストが無い。
2. **要修正1の実害を検出できたはずのテストケースが無い**: 「選択中でない移動によって、選択中シート自身のCurrentSheetIndexの数値が実際に変わる」ケース(4枚以上のシートが必要)が未カバー。既存テスト`MoveSheetCommand_WhenMovingOtherSheet_SelectedSheetStaysSame`はCurrentSheetIndexが0のまま変化しない特殊ケースのみ。
3. **要修正3に対応する検証テストが無い**: 既存の`DeleteCommand_WhenIndexNumberStaysSameAndSelectedCellAlreadyNull_RaisesCurrentSheetChanged`・`RenameCommand_MarksDirty`は`PropertyChanged`購読で通知発火自体を直接検証しているが、MoveSheetCommandには同型のテストが無い。
4. ドラッグ&ドロップの`toIndex`算出ロジック(`SheetNavList_Drop`内、上半分/下半分判定+除去後座標系補正)が private インスタンスメソッド内に直書きされ、既存の先例(`ShouldOpenRungCommentEditor`等のstaticメソッド抽出+直接テストするパターン)に反してテスト不能になっている。D&Dで最もバグを生みやすい部分が未テストのまま。

## 派生提案の有無

範囲外の新規気づきなし(全指摘はT-082の実装範囲内)。
