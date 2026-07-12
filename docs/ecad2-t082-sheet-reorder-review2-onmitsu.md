# T-082 シート並び替え機能 静的コードレビュー(隠密・往復2周目後の再レビュー)

- 対象コミット: `1b42fd3`(修正1・3+テスト補強、テスト10件追加)、`2ad2825`(修正2=殿裁定案A、テスト2件追加)
- 実施日: 2026-07-12
- 実施者: 隠密
- 方式: DoD突合(a)(b)+RED証明代替方式の妥当性検証(c)+`code-review`スキル併用(d、effort high、10角度並列finder→集約)、フル観点
- スコープ境界: レビューのみ、書き込みなし。経過観察7件(前回報告済み)は再指摘せず悪化検分のみ

## 結論サマリ

**修正1(所見L型再発)は本質的に未解決のまま残っている。往復3周目の修正が必要。** 修正3・修正2は正しく機能している。

## 要再修正(CONFIRMED、最重要)

### 修正1が「移動対象自体が選択中シート」という最も基本的な使い方で未解決

**該当**: `SheetNavigationViewModel.cs:245-247`

```csharp
int newSelectedIndex = selectedSheetBeforeMove is null ? -1 : Sheets.IndexOf(selectedSheetBeforeMove);
if (newSelectedIndex >= 0 && newSelectedIndex != oldSelectedIndex)
    _owner.SetCurrentSheetIndexCore(newSelectedIndex);
```

このガードは「選択中シートの**添字**が変化したかどうか」を判定基準にしている。しかし、**MoveSheetCommandの実行によって選択中シートの実体(オブジェクト参照)が変わることは原理的に一度も無い**(移動操作はシートを削除・追加するのではなく位置を入れ替えるだけ)。にもかかわらず、以下2つの具体シナリオで添字は必ず/しばしば変化するため、`SetCurrentSheetIndexCore`(値変化の有無に関わらず常時`SelectedCell=null`を実行、T-041由来の既存仕様)が呼ばれ、**表示中シートの中身は一切変わっていないのにSelectedCell・記入中ドラフトが消える**——これは前回指摘した「所見L」型問題そのものであり、まだ解消されていない。

**シナリオ1(最頻出・最重要、Angle A発見)**: 移動対象自体が選択中シートの場合。`fromIndex == oldSelectedIndex`かつCanMoveSheetのガードで`fromIndex != toIndex`が保証されているため、`newSelectedIndex(=toIndex) != oldSelectedIndex(=fromIndex)`は**必ず**true。つまり「自分が今開いているシートをAlt+上下やドラッグで動かす」という並び替え機能の最も直接的な使い方をするたび、必ずSelectedCell・記入中ドラフトが消える。

**シナリオ2(Angle B発見、前回report済みの4枚以上シナリオの再確認)**: 無関係シート移動で選択中シートの添字が間接的にシフトするケース(4枚シート[A,B,C,D]でC選択中、AをD以降へ移動→Cのindexが2→1にシフト)でも同様に発生。

**根本原因**: 判定基準を「添字の変化」ではなく「**CurrentSheetの実体が変わるかどうか**」にすべきだった。MoveSheetCommand内ではこの実体は常に不変なので、本来`SetCurrentSheetIndexCore`(クロスカット処理込み)を呼ぶ必要は一度も無いはずである。CurrentSheetIndexの値自体(表示上の添字)は同期させる必要があるため、**「クロスカット処理(SelectedCellクリア等)を伴わずにCurrentSheetIndexの値だけを更新する」新しい経路**が必要(RenameCommandが「同一シートに留まる操作ゆえCurrentSheetIndexへ触れない」とした設計判断の精神を、「indexは変わるが実体は変わらない」ケースにも拡張する形)。

**テストの穴**: 既存の回帰テスト(`MoveSheetCommand_WhenMovingUnrelatedSheets_DoesNotClearSelectedCell`)は「移動対象が選択中シートでなく、かつ添字も不変」という1パターンのみを検証しており、**移動対象=選択中シート自身のケースでSelectedCellの保持を検証するテストが無い**。他の全既存テスト((0,1)や(1,0)を使うもの)はいずれもindex0=既定の選択中シートが移動対象または間接shift対象になっているにもかかわらず、SelectedCellを一切アサートしていないため、569件全合格の報告のままこの再発が見逃されている。

**検出経路**: code-reviewスキルAngle A・Bの2系統独立検出+隠密によるコード読解裏取り(シナリオ1・2とも実際にトレースし確認済み)。

## 修正内容の突合(DoD (a)(b))

### 修正1(SetCurrentSheetIndexCore条件付き化): 対症療法、上記の通り不十分

意図(「無関係シート入替で選択中シートの添字が不変な場合にクリアを防ぐ」)は前回指摘の一部を正しく解消しているが、判定基準の選び方(添字変化 vs 実体変化)が誤っており、要修正1の主要シナリオ(自分のシートを動かす)を解消できていない。

### 修正3(SelectedSheet通知の遅延発火): 正しく機能

`_dispatcher.BeginInvoke(ContextIdle, () => RefreshSelectedSheet(selectedSheetBeforeMove))`はAdd/Renameと同型のパターンで、`selectedSheetBeforeMove`(移動前後で実体不変)を旧値として渡す設計は意味論上正しい(RenameCommandの「旧値=新値」パターンと同型)。回帰テスト`MoveSheetCommand_RaisesSelectedSheetPropertyChanged`で通知発火自体も直接検証されている。新規バグの作り込みなし。

### 修正2(DRC結果破棄+ステータスバー案内): 正しく機能、不変条件も充足

`CanMoveSheetのガードを通過すれば必ずモデル順序が変わる(fromIndex!=toIndex確定)ため追加条件は不要`という侍の判断は正当(CanMoveSheetの定義どおり)。「診断が存在する場合のみ破棄+案内」という不変条件もテスト2件(`WhenDiagnosticsExist_ClearsResultsAndShowsStatusMessage`/`WhenNoDiagnostics_DoesNotOverwriteStatusMessage`)で正しく検証されている。新規バグの作り込みなし(ただし下記の経過観察も参照)。

## RED証明代替方式の妥当性(DoD (c))

- 修正1・3: git stash方式(既存コードの修正のため、修正前の状態を機械的に再現可能)
- 修正2: 修正ブロック一時コメントアウト方式(新規追加コードのため、同一コミット内のテストコードごとstashされる問題を回避する代替)

いずれも「実装が無い状態でテストがFAILすることを示し、実装を戻すとPASSすることを示す」というRED証明の目的を達成する代替手段として妥当。現在のコミット済みコードにコメントアウトの痕跡は残っていない(両diffで確認済み)。**方式自体に問題なし**。

## 経過観察(新規、今回の修正由来)

- **Alt+上下キーのキーリピートでBeginInvokeが無条件に積み上がる**(`MainWindow.xaml.cs:942-951`、Angle H): `e.IsRepeat`を見ておらず、キー押しっぱなし中はOSのキーリピート(20-30Hz)のたびBeginInvoke(ContextIdle)が積まれる。ContextIdleはInputより低優先度のため、押している間ずっとキューが消化されずに蓄積しうる。実機確認要。
- **StatusMessageの無条件上書き**(`SheetNavigationViewModel.cs:236`、Angle B): 直前に別操作(行削除の警告等)でStatusMessageに重要な文言が出ていても、DRC結果が残っていれば即座に「DRC結果が削除されました」で上書きされる。退避・復元やタイミング考慮なし。
- **DeleteCommandにDRC結果破棄のフックが無い一貫性の欠如**(Angle I、E、範囲外の気づき): 今回MoveSheetCommandにだけ「モデル順序変化→DRC結果破棄」が実装されたが、DeleteCommand(シート削除、PageNumber再採番はしない既存仕様)は対象外のまま。将来DeleteCommandが再採番するよう変更されると、要修正2と同型の問題が再発しうる。ReplaceDocument/Undo-Redo/MoveSheetCommandの3箇所でDRC結果破棄のルール(通知の有無等)がそれぞれ異なる実装になっている点も含め、一般化されたフック(Document構造変更を一箇所で捕捉する仕組み)が無いことが根本。
- **責務混在**(Angle E、I): `SheetNavigationViewModel`(シートナビゲーション専任)が`OutputPanelViewModel.Diagnostics`/`ClearResults()`と`MainWindowViewModel.StatusMessage`へ直接介入している。
- **コード複雑化**(Angle G): `MoveSheetCommand`のExecuteラムダが往復1〜2周目の3修正の積み重ねで責務過多(約50行に7つの関心事が直列)。`oldSelectedIndex`/`selectedSheetBeforeMove`という2つの「移動前状態」変数の並存。`BeginInvoke(ContextIdle,...)`パターンがAdd/Rename/Moveの3箇所目のコピペ。テスト`MovesFirstSheetDownByOne`/`MovesLastSheetUpByOne`がほぼ同一コピペ(Theory化の余地)。

既存の経過観察7件(前回報告分)は、今回の差分では触れられておらず悪化なし。

## 派生提案の有無

範囲外の新規気づき: DeleteCommandのDRC結果破棄フック欠如(上記)は、T-082の実装範囲を超えるため、家老経由でP-XXX起票を検討されたい。
