# T-107増分2 静的レビュー（隠密）

日付: 2026-07-21
対象コミット: `6553fab`（機器コメントをデバイス単位で共有、Device.Comment新設・Element.Comment廃止）
手法: `git show 6553fab -- <path>` で範囲を明示した手動レビュー（code-reviewスキルはSkillツール経由起動不可のため代替、`docs-notes/roles/onmitsu.md`既定）。effort=low（1周目既定）。

## 結論

**指摘なし。DoD全項目を満たすと判断する。** 忍者実機確認へ進めてよい。

## 確認観点と根拠

### (1) 呼び出し元3系統へのdevices引数配線漏れ（家老指定の重点確認事項）

- 画面: `MainWindow.xaml.cs` RedrawCanvas → `LadderCanvas.Draw(..., devices: _viewModel.Document.Devices)` → `DiagramRenderer.Render(..., devices: devices)`。配線確認。
- PDF出力: `PdfExporter.cs` の `Render(..., devices: devices)`。`devices`変数は33行目`var devices = document.Devices;`で既に定義済み（既存コード、BOMページ描画で使用中）だったものを新たにRender呼び出しにも渡すよう追加。配線確認。
- PDFプレビュー: `PdfPreviewDialog.xaml.cs` の `Render(..., devices: _document.Devices)`。配線確認。

3系統とも漏れなし。

### (2) null安全性（`ElementInstance.DeviceName`は`string?`）

- `CrossReferenceBuilder.Build`: 66行目で`if (string.IsNullOrEmpty(elem.DeviceName)) continue;`によりガード済み。87行目の`doc.Devices.ByName.TryGetValue(elem.DeviceName, ...)`到達時点でDeviceNameは非null非空と保証される。
- `DiagramRenderer.DrawElementLabel`: `e.DeviceName is string dn && dn.Length > 0 && _devices?.ByName.TryGetValue(dn, ...) == true` のパターンマッチガードで安全。
- `MainWindowViewModel.SelectedElementComment` の getter/setter: 同型のガードパターンで安全。

いずれも問題なし。

### (3) 永続化（保存・読込）

`GcadSerializer`はDTOマッピング方式ではなく`LadderDocument`全体を`System.Text.Json`で直接シリアライズする方式（`GcadSerializer.cs`にDevice/Devicesという語自体が出現しない）。よって`Device.Comment`新設は自動的にシリアライズ対象へ含まれる。新規テスト`GcadSerializer_Device_Commentが往復一致する`で実際に往復一致を確認済み。

### (4) CrossReferenceBuilderのComment集約単純化

`CrossRefEntry.Comments`(`List<string>`)→`Comment`(`string?`)への変更、集約ロジックも「同一デバイス名なら値は1つに定まる」前提で単純化されており設計と整合。新規テスト3件（Device存在時/未登録時/同一デバイス名複数要素）でカバー。

### (5) 選択切替時の派生表示プロパティ通知4箇所（samurai.md項目9、T-107本実装で制度化）

`MainWindowViewModel.cs`の4箇所（457-469行/2354-2366行/2488-2500行/3008-3020行）全てに`OnPropertyChanged(nameof(SelectedElementComment))`が既存のまま含まれることを確認。侍の所見「参照先がDevice経由に変わってもSelectedElementの変化自体は同じ経路のため、既存の対応をそのまま維持」で妥当（getter/setterの参照先変更のみで、通知トリガー自体はSelectedElement/SelectedCellの変化に紐づくため変更不要）。

### (6) Element.Comment廃止の完全性

`grep -rn "\.Comment\b" src/`で全参照を洗い出し、残存する`e.Comment`（`DiagramRenderer.cs:816`）は`CrossRefEntry.Comment`（新設のstring?型）であり旧`ElementInstance.Comment`の残骸でないことを確認。旧プロパティへの参照は残っていない。

### (7) テストカバレッジ

Core層3ファイル（新規`CrossReferenceCommentTests.cs`3件、`DiagramRendererLabelTests.cs`書き換え+2件追加、`GcadCompatibilityTests.cs`+1件）、App層`T107CommentTests.cs`+2件（共有ケース・DeviceName未設定編集不可ケース）とも、DoDの主要シナリオ（共有・非共有・後方互換・永続化）を網羅していることを確認。

## 不明点

なし。

## 派生提案（範囲外の気づき）

特になし。
