# T-136(A)増分2静的レビュー（1周目軽量＋最重点＝穴埋めテストの妥当性）

隠密（key=1785485896132）記す。2026-07-31。対象＝`7ce7015`（push済み、通常形ビルド0エラー・1410件全合格を家老報告済み）。

## 結論：指摘なし。ただし改変Aの件数に1件の差異あり、侍への確認を具申する

## 実装確認

`PartEditorDialog`のROW0へ`AffinityCombo`（`Role`と同型のenum→日本語ラベル）、`PartEditorExternalState`の第4項目`SheetAffinity`（既定値なし、`PartDefinition.cs`同様の作法）、`PartPaletteViewModel.ApplyPlaceability`/`RefreshPlaceability`と`PartSelectionEntryViewModel.IsPlaceable`、`MainWindow.xaml`の`ListBoxItem.IsEnabled`バインディングを一次ソースで確認した。いずれも設計どおり。

## 最重点：改変C（穴埋めテスト）が実際に当該経路を突いているか——**確認できた**

`PartPaletteViewModel.Load()`（`:53-82`）は末尾で`ApplyPlaceability()`を呼ぶ（`:81`）。この1行が無ければ、`Load()`が生成する新しい`PartSelectionEntryViewModel`インスタンスは既定値`IsPlaceable=true`のまま残る（`PartSelectionEntryViewModel.cs`の`_isPlaceable=true`初期化子）。

新設テスト`保存で一覧を作り直した後も配置可否が当たる`を追跡した——`RefreshPlaceability(false)`で`_lastSheetIsMainCircuit=false`を設定した後、`SaveNewPart(mainOnly)`→`Load()`が新規エントリを生成、末尾の`ApplyPlaceability()`が`_lastSheetIsMainCircuit`(false)と`mainOnly.SheetAffinity`(`MainCircuitOnly`)から`IsAllowedOnSheet(MainCircuitOnly,false)=false`を計算し新規エントリへ反映する。**もし`Load()`末尾の呼び出しを取り除けば、新規エントリは既定の`true`のまま残り、テストの`Assert.False(entry.IsPlaceable)`が確実に落ちる**——手計算でも侍の「1件RED」の実測と符合し、**この網は本当に当該経路（`Load()`末尾の当て直し忘れ）を突いていると確認した**。

## RED証明の検算

| 改変 | 侍の実測 | 隠密の机上検算 |
|---|---|---|
| B（シート切替への繋ぎ込みを外す） | 1件RED | `シート切替でパレットの配置可否が追随する`のみが`NotifyCurrentSheetDependentPropertiesChanged`経由の配線を実際に通る。他8件は`RefreshPlaceability`を直接呼ぶため無関係。**1件で一致** |
| C（Load後の当て直しを外す） | 当初0件→網追加後1件RED | 上記のとおり確認済み。**1件で一致** |
| **A（判定を素通し）** | **4件RED** | **手計算では5件相当**（`RefreshPlaceability_枷に合うシートでのみ置ける`のうち期待値がfalseの2ケース＋`枷を持たぬ部品は巻き込まれぬ`＋`保存で一覧を作り直した後も`＋`シート切替で追随する`の後半アサーション）。**1件の差異があり、侍が具体的にどの改変を当てたか（`IsAllowedOnSheet`自体を`true`固定にしたか、`ApplyPlaceability`のループ自体を無効化したか等）によって内訳が変わりうるため、断定はせず確認を要する点として記す** |

## 案2（配置バーのコンボ経路）の実装整合——**確認できた**

`PlacementPartComboBox.ItemsSource = _viewModel.PartPalette.SelectionEntries`（`MainWindow.xaml.cs:3687`）——**フィルタなしの全件**であり、配置バーのコンボで`IsPlaceable=false`の部品へも切り替えられる。実際の拒否は`PlaceElementAtSelectedCell`→`ValidatePlacement`（増分1）が担う。**この構図は既存のT-071コメント（`MainWindow.xaml.cs:3664-3666`）が「配置バー表示後にコンボボックスで別部品へ切り替えられた場合はValidatePlacement...が最終防御になる」と、幅（`CellWidth`）の事前チェックについて既に同型の設計を明記しており、本増分もその前例に倣った形と確認できた**。「案2の形として実装が整合しておるか」という問いには、整合していると判ずる（案の是非は殿の領分ゆえ判じない）。

## 気づき（軽微）

- **改変Aの件数差異（4件 対 手計算5件）**——上記のとおり。実害というより確認漏れの可能性を残すため、侍に「どの箇所へどう改変したか」を一言確認いただくのが安全と見る。
- `dotnet test`（`PartPaletteViewModelTests`フィルタ）で13件全合格（既存4件＋新規9件）を確認、報告と一致。

## 出典

`7ce7015`の全diff直読、`PartPaletteViewModel.cs:53-100`・`PartSelectionEntryViewModel.cs`・`PartEditorDialog.xaml.cs`・`MainWindow.xaml:1613-1621`・`MainWindow.xaml.cs:3664-3667,3687`。`dotnet test`実測。

## 派生提案

なし。改変Aの件数確認は本レビューの範囲内の疑問点として上記に記載済み。
