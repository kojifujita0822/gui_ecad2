# T-064 画像挿入機能のUI結線 静的コードレビュー(隠密)

- 対象コミット: `9bacb85`(侍実装、新規実装)
- 実施日: 2026-07-13
- 実施者: 隠密
- 方式: 台帳DoD整合確認+`code-review`スキル併用(軽量既定、新規実装1周目ゆえ4角度に絞って実施)+既知トラップ狙い撃ち(家老指定: T-069で見た「表示側/実行側不一致」パターン、SetProperty早期return)
- スコープ境界: レビューのみ、書き込みなし

## 結論サマリ

殿裁定DoD(1)〜(6)は実装され、選択排他制御・記入中ドラフトのクロスカットクリア等、**T-082/T-069の教訓(所見L型再発防止・唯一の入口設計)を強く意識した丁寧な実装**にはなっている。家老指定の既知トラップ「表示側/実行側の不一致」パターンそのものは、画像がオブジェクト参照を直接保持する設計のため回避できている。一方、**新規に5件の要修正**を発見した——うち1件(リサイズ境界外はみ出し)は複数角度で独立検出、1件(削除の取り違え)は実害が大きい。

## 家老指定の既知トラップ2点の検証結果

- **「表示側/実行側の不一致」(T-069型)**: 画像選択・削除・移動・リサイズは全て`ImageInsert`オブジェクト参照を直接受け渡す設計(グリッド非依存の自由配置要素のため、セル位置を介した間接参照を経由しない)。`BeginDragImage`/`BeginResizeImage`の開始条件も`HitTestImage(position, sheet) == dragImg`という一致確認をしており、T-069で見た「HitTestElement(区間交差)とSelectedElement(単純一致)の不整合」に相当する連鎖バグは**発生しない設計**。削除経路(Key.Delete・DeleteMenuItem_Click)への`DeleteSelectedImage()`統合も両方に正しく反映されている。
- **SetProperty早期returnの既知トラップ**: `SelectedImage`セッターは`SetProperty`の戻り値をガードに使わず、`ForceCancelDragImageIfAny()`等の前処理は常時実行される設計になっており、この既知パターンには該当しない。ただし別の通知漏れを発見(下記経過観察参照)。

## 要修正(CONFIRMED)

### 1. リサイズの最小サイズ制約と境界クランプの相互作用で画像がページ境界外へはみ出す(最重要、4系統独立検出)

**該当**: `MainWindowViewModel.cs` `UpdateResizeImage`

```csharp
double clampedCurrentX = Math.Clamp(currentXMm, 0, _resizeImageMaxXMm);
...
double width = Math.Max(ImageMinSizeMm, Math.Abs(rawWidth));
...
image.XMm = Math.Min(_resizeImageAnchorXMm, _resizeImageAnchorXMm + signedWidth);
```

最小サイズ制約(`ImageMinSizeMm=5.0`)が**ページ境界クランプの後**に適用されるため、以下のいずれかのケースで画像がページ境界外へはみ出す:
- アンカーが境界近くにある状態で対角ハンドルを大きく縮める(例: X=2mm位置の画像をBottomRightハンドルで縮めると`XMm=-3`になる)
- 挿入直後の画像が5mm未満の小さいサイズ(`CalculateInitialImageSizeMm`は上限120mmのみクランプし下限が無いため、極小画像は5mm未満のまま追加されうる)をページ端付近でリサイズする

XMLドキュメントコメント自体が「ページ境界もはみ出さないようクランプする」と明記しているが、実際には満たされていない。

**テストの穴**: `ImageInsertTests.cs`のリサイズ系テストは全て`maxXMm=maxYMm=1000`(実質無制限、境界から遠い)でしか検証されておらず、この回帰を一切検出できない。

**検出経路**: 隠密の手動確認+code-reviewスキルAngle A・B・Cの3系統独立検出(計4系統)。

### 2. 矢印キー処理にPlaceImageモードの分岐が無く、画像挿入ドラフトが無警告で破棄される

**該当**: `MainWindow.xaml.cs:1227-1250`付近(矢印キーのswitch case)

```csharp
if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceConnector)
    AdjustConnectorDraft(e.Key, cellCenterStep: false);
else if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceLine)
    AdjustFreeLineDraft(e.Key);
...
else
    MoveSelectedCell(e.Key);
```

`PlaceConnector`/`PlaceLine`には記入中ドラフト調整の専用分岐があるが、**`PlaceImage`の分岐が無い**。殿裁定「案A」の2段階配置フロー(メニュー→ファイル選択→キャンバス上でホバー追従→クリック確定)の途中、キャンバスにフォーカスがある状態(キーボードファースト運用では起こりやすい)で矢印キーを押すと、いずれの分岐にも一致せずelse節の`MoveSelectedCell(e.Key)`へ落ちる。これが`SelectedCell`のsetterを経由し、無条件の`CancelImageInsertDraft()`を発火させるため、**配置待機中の画像ドラフトが警告なく消え、Tool.ModeもSelectへ戻る**。

**根拠**: 同じswitch文内のEscape処理(層2'''、`else if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceImage) _viewModel.CancelImageInsertDraft();`)ではPlaceImageに対する分岐が正しく用意されており、**矢印キー側だけ対称性が崩れている**。

**検出経路**: code-reviewスキルAngle A発見、隠密がコードを読解し裏取り済み。

### 3. Escキーで画像単独選択が解除できない

**該当**: `MainWindow.xaml.cs`のEsc「層3: 選択解除」条件

```csharp
else if (_viewModel.SelectedCell is not null || _viewModel.SelectedConnector is not null
    || _viewModel.SelectedWireBreak is not null || _viewModel.SelectedFreeLine is not null
    || _viewModel.SelectedConnectionDot is not null)
```

この条件リストに`SelectedImage`が含まれていない。画像単独選択時(他の選択が全てnull)にEscを押すと、層2(PlaceImage中でない)にも層3(この条件)にも該当せず無視され、選択枠・リサイズハンドル・プロパティパネルが表示されたまま残る。他の全選択種別はEscで解除できるのに画像だけ非対称。

**検出経路**: code-reviewスキルAngle B発見。

### 4. 要素選択と画像選択が同時にnon-nullになり、Deleteキーで意図しないものが削除される

**該当**: `MainWindowViewModel.cs` `ConfirmImageInsertDraft()`

```csharp
public bool ConfirmImageInsertDraft()
{
    ...
    CancelImageInsertDraft();
    SelectedImage = image;
    return true;
}
```

`SelectedCell`を`null`にせず`SelectedImage`だけを設定している。既存要素(セル)を選択したまま図面メニューから画像を挿入・確定すると、旧選択要素(`SelectedCell`)を残したまま新規画像(`SelectedImage`)が設定され、`HasSelectedElement`/`HasSelectedImage`が両方`true`になる。コミットメッセージが謳う「プロパティパネルの3状態切替」の排他性が崩れ、プロパティパネルの要素用・画像用が同時表示されうる。

**実害**: この状態でDeleteキーを押すと、`DeleteSelectedElement() || DeleteSelectedConnector() || ... || DeleteSelectedImage()`というOR連鎖の**先頭**が先にヒットするため、**ユーザーが今挿入したばかりの画像ではなく、旧選択要素が削除される**。

**検出経路**: code-reviewスキルAngle B発見。

### 5. 右クリックメニューが画像をヒットテストせず、行操作メニューにフォールバックする

**該当**: `MainWindow.xaml.cs` `LadderCanvasHost_PreviewMouseRightButtonDown`

右クリックのヒットテストチェーンは`HitTestElement`→`HitTestConnector`のみで、`HitTestImage`を呼んでいない。挿入済み画像の上で右クリックすると、両方nullのためelse分岐(行の挿入/追加/削除)が開かれ、画像の選択・削除メニューが一切提示されない。左クリック(`LadderCanvasHost_PreviewMouseLeftButtonUp`)は`HitTestImage`で画像を正しく拾うため、**左右で対応が非対称**(削除経路の統合漏れの一種)。

**検出経路**: code-reviewスキルAngle B発見。

## 経過観察

- **`SelectedImageIsTracingOnly`の通知漏れ**(Angle C): `SelectedImage`のsetterが`OnPropertyChanged(nameof(SelectedImageIsTracingOnly))`を発火しないため、選択画像を切り替えるとプロパティパネルのトレース用下絵チェックボックスが前の画像の値のまま残留する(実データは正しいが表示が古いまま)。`SelectedCell`のsetterが`SelectedElementDeviceName`等を選択変更時に明示通知する既存パターンとの不整合。
- **Undo実行後の実際の復元検証テストが無い**(Angle C): `ConfirmDragImage`/`ConfirmResizeImage`後に`UndoCommand.Execute(null)`を実際に呼び、画像が元の位置・サイズへ復元されることを検証するテストが無く、`UndoCommand.CanExecute`の真偽しか確認していない。「開始時位置へ一旦戻す→RecordSnapshot→確定値へ戻す」という特殊なタイミング実装の手順ミス(例: 順序の入れ替わり)があってもテストで検出できない。
- **リサイズ境界値5.0ちょうどの明示テストが無い**(Angle C)。

## 派生提案の有無

なし(全指摘T-064の実装範囲内)。
