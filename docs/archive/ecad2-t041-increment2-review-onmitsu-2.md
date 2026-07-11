# T-041増分2 修正往復1周目レビュー（隠密、正式再レビュー）

> 2026-07-07 隠密レビュー。対象コミット`f5cbde8`（`fix(app): T-041増分2修正往復1周目 - 記入中
> 状態(draft)をSelectedCellのsetterへ集約`）。隠密指摘（`docs/ecad2-t041-increment2-review-onmitsu.md`
> 観点(3)CONFIRMED・観点(4)・所見D）への対応。家老指定観点＋実測検証を行った。

---

## 結論：**クリーン。増分1/2/3まとめて忍者実機確認へ回してよい**

指摘した観点(3)（シート切替時の制御回路限定・グリッド範囲の迂回）と観点(4)（記入中マウス
クリックの取りこぼし）は、いずれも増分1で確立した「`SelectedCell`のsetterを選択排他クリアの
唯一の真実源にする」パターンを`_connectorDraft`（記入中状態）へも一般化することで、**1箇所の
修正で両方を同時に解消**している。加えてConfirmConnectorDraft自体への防御的二重チェックも追加
されており、将来別経路が生まれても二重に守られる構成になった。所見D（描画ロジック重複）も
共通ヘルパーへ集約され解消。regression proofも実施済みで、侍の主張通り実際に効果を確認した。
所見C（矢印キー連打時のフルネットリスト再構築）は今回対応不要と判断する（末尾参照）。

---

## 対象差分

`git show f5cbde8`で確認。`MainWindowViewModel.cs`（`ClearConnectorDraftIfAny`新設・
`SelectedCell`setter/`ConfirmConnectorDraft`/`ReplaceDocument`への組み込み）、`LadderCanvas.cs`
（`ConnectorEndpointsDip`共通ヘルパー抽出）、`ConnectorDraftTests.cs`（新規3件）。

---

## 観点別の検証

### CurrentSheetIndex setterへのTool/_connectorDraftリセット追加 —— **正しく機能**

新設された`ClearConnectorDraftIfAny()`（`MainWindowViewModel.cs`）：

```csharp
private void ClearConnectorDraftIfAny()
{
    if (_connectorDraft is null) return;
    _connectorDraft = null;
    Tool = ToolState.SelectDefault;
    OnPropertyChanged(nameof(ConnectorDraftPreview));
}
```

これが`SelectedCell`のsetter（増分1で確立済みの「値変化の有無に関わらず常時実行」位置、
`SetProperty`の早期return判定より前）から呼ばれる。これにより：

- **矢印キー移動・選択解除ボタン**：既に増分1の仕組みで`SelectedCell`への代入が発生する経路
  であり、`_connectorDraft`も自動的にクリアされる。
- **シート切替（`CurrentSheetIndex`）**：値が実際に変化する通常のシート切替は、その本体で
  `SelectedCell = null;`を呼ぶため自動的にクリアされる。**DRC同一シートジャンプ（`SetProperty`
  早期return）の場合も**、`OutputPanelViewModel.JumpTo`が必ず`SelectedCell = ...`を実行する
  （増分1の再レビューで確認済みの一般原則）ため、この経路も正しくカバーされる。
- **新規作成/開く（`ReplaceDocument`）**：`_selectedCell = null`が唯一setterをバイパスする直接
  代入のため、`ClearConnectorDraftIfAny()`をこの箇所にも明示的に追加している。
- **マウスクリック（観点4）**：`LadderCanvasHost_PreviewMouseLeftButtonUp`の素通し分岐
  （`Tool.Mode!=Select`時、または`Select`だがヒットテスト不一致時）が`SelectedCell = ToGridPos
  (position);`を呼ぶため、記入中に任意の位置をクリックすると自動的に取消される。

`_connectorDraft is null`の早期returnにより、**`Tool.Mode==PlaceElement`（T-021分岐Aの継続配置
フロー）には一切影響しない**ことをコードで確認した（`Tool`への代入・`ConnectorDraftPreview`の
再通知とも、記入中でなければ発生しない）。これは重要な確認点で、既存の別モードへの副作用が
無いことを裏付ける。

### ConfirmConnectorDraftの防御的再チェック —— **妥当。境界値も正しく処理**

```csharp
if (_connectorDraft is not { } draft || CurrentSheet is not Sheet sheet || sheet.MainCircuit) return false;
int topRow = Math.Clamp(Math.Min(draft.AnchorRow, draft.CurrentRow), 0, sheet.Grid.Rows - 1);
int bottomRow = Math.Clamp(Math.Max(draft.AnchorRow, draft.CurrentRow), 0, sheet.Grid.Rows - 1);
double column = Math.Clamp(draft.Column, 0, sheet.Grid.Columns);
if (topRow == bottomRow) return false;
```

`sheet.MainCircuit`チェックが最初に入ったことで、上位の`SelectedCell`setter経由のクリアが万一
効かない別経路が将来生まれても、主回路シートへの`VerticalConnector`混入は二重に防がれる。
クランプ→ゼロ長判定という順序も適切だと判断した：クランプ後に範囲が潰れた場合（例:
旧シートで`AnchorRow=3, CurrentRow=4`だった記入が、新シートの`Rows=4`に対してクランプされ両方
`3`に収束するケース）は`topRow==bottomRow`で正しく拒否される。負の行（P-022/P-024で経過観察中の
境界値クイック）を起点にした場合も、クランプにより`[0, Rows-1]`内へ収まるか、あるいはゼロ長化して
拒否されるかのいずれかになり、範囲外の`VerticalConnector`が生成される余地が無いことを確認した。

### 所見D（描画ロジック重複）の解消確認 —— **正しく解消**

`ConnectorEndpointsDip(column, topRow, bottomRow, extendBottomMm)`ヘルパーへ集約された。ゼロ長時
の特例（`extendBottomMm: geo.CellMm * 0.3`）を数式で追跡し、集約前の
`yTop + CellMm*0.3*MmToDip`と完全に同じ計算結果になることを確認した（`geo.YRow(bottomRow)`に
`topRow==bottomRow`を渡すため`yTop`と同値、そこへ`extendBottomMm*MmToDip`を加算する式が元の
特例と数学的に同一）。選択ハイライト側（`extendBottomMm`省略時=0）も既存の描画結果と変わらない。

### 実測検証

`dotnet test --filter FullyQualifiedName~ConnectorDraftTests`で**12件全て合格**（新規3件を含む、
実行時間70ms）、`dotnet test src/Ecad2.sln`で**Core14件・App46件、全60件合格**を確認した
（侍のregression proof報告「46件」と一致）。新規3件の内容も確認した：

1. `SwitchingCurrentSheetIndex_WhileDrafting_CancelsDraftAndPreventsCrossSheetLeak`：主回路シートを
   追加し、制御回路シートで記入中に`CurrentSheetIndex`を切替え、`Tool.Mode`が`Select`へ戻り
   `ConnectorDraftPreview`がnullになること、その後`ConfirmConnectorDraft()`を呼んでも`false`で
   新シート（主回路）の`Connectors`に何も追加されないことを検証——観点(3)の再現手順を正確に
   なぞっている。
2. `SettingSelectedCell_WhileDrafting_CancelsDraft`：記入中に`SelectedCell`への再代入（クリック
   相当）で記入状態が取消されることを検証——観点(4)を正確になぞっている。
3. `ConfirmConnectorDraft_ReturnsFalse_WhenCurrentSheetIsMainCircuit`：`ConfirmConnectorDraft`単体
   の防御チェックを直接検証。

いずれも指摘した再現手順と1対1で対応しており、テストの実効性を確認した。

---

## 所見C（矢印キー連打時のフルネットリスト再構築）について —— **今回対応不要、妥当と判断**

家老照会の通り、本コミットでは対応されていない。severity判断（前回レビュー時点）は「中確度、
実測なしに遅延の有無は断定できない」であり、今回の修正が対処したのは**正しさ**（データ整合性・
不変条件維持）の問題で、所見Cは**性能**の懸念に過ぎない。優先度としては正しさの問題を先に
解消するのが妥当であり、今回のスコープに含めないのは合理的と判断する。忍者実機確認で「矢印キー
連打時に体感できる遅延があるか」を一項目として確認してもらい、実害が無ければ経過観察
（`docs/observations.md`）として記録するに留めるのが良いと考える。もし忍者確認で実際に遅延が
確認された場合は、別途改修（`RedrawCanvas`の呼び出し頻度抑制、あるいは`ConnectorDraftPreview`の
変更検知の粒度見直し等）を検討する必要がある。

---

## 忍者への申し送り

- 増分1/2/3まとめて実機確認をお願いする。増分2固有の確認観点として、上記regression testの
  再現手順（記入中のシート切替・記入中のマウスクリック）を実機で試し、見た目上も破綻しない
  ことを確認いただきたい。
- 所見C（矢印キー連打時の体感遅延の有無）も余裕があれば一項目として確認願う。

---

## 出典・参照

- 対象コミット`f5cbde8`（`git show`で全差分確認）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`ClearConnectorDraftIfAny`・
  `ConfirmConnectorDraft`・`SelectedCell`setter・`ReplaceDocument`）
- `src/Ecad2.App/Views/LadderCanvas.cs`（`ConnectorEndpointsDip`）
- `tests/Ecad2.App.Tests/ConnectorDraftTests.cs`（新規3件、実行して合格を実測）
- `dotnet test`実行結果（フィルタ実行12/12合格、`src/Ecad2.sln`全体でCore14件・App46件合格）
- `docs/ecad2-t041-increment2-review-onmitsu.md`（初回レビュー、観点(3)CONFIRMED・観点(4)・所見C/D）
- `docs/ecad2-t041-increment1-review-onmitsu-2.md`（`SelectedCell`setter集約パターンの確立経緯）
