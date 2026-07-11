# T-033増分1 静的レビュー：非モーダル配置バー化（隠密）

> 2026-07-07 隠密レビュー。対象コミット `de9543c`（`feat(app): T-033増分1 - 配置後入力の非モーダル浮動インライン化(骨格)`）。
> 家老指定の観点(1)〜(5)＋`code-review`スキル（high、8角度finder→10件verify、全件CONFIRMED）併用。

---

## 結論：要修正（重大）。観点(1)に致命的な欠陥あり

**「バー表示中はモーダル同等の使用感にする」（殿裁定）という要件が、`Window_PreviewKeyDown`冒頭の
早期リターン1箇所（キーボード経路限定）でしか実現されておらず、マウス経由の6系統の操作
（キャンバスクリック・部品選択リストクリック・DRC出力パネル行クリック・シートナビゲーション・
選択ツールボタン・新規作成/開くボタン）がバー表示中も無防備なまま素通しになっている。**
うち1件（DRC出力パネル経由）は**同一セルへの要素二重生成という実データ不整合**まで起こり得る
最重要の欠陥であり、他は「入力内容の無警告消失」「意図しない配置」「サイレントno-op」という
UX上・機能上の欠陥。いずれもコード読解のみで到達可能性を確認済み（実機検証は不要なほど明確）。

---

## 観点別結果

### (1) 無効化の網羅性——マウス経路の整合 — **不備・重大（CONFIRMED多数）**

`Window_PreviewKeyDown`冒頭の`if (ElementPlacementBar.Visibility == Visibility.Visible) return;`
（243行目）は**この1箇所のみ**で、`MainWindow.xaml.cs`内で`ElementPlacementBar.Visibility`を
参照する箇所は定義・表示・非表示の3箇所を除き他に無い（grep確認済み）。以下6系統のマウス経路が
このガードの対象外:

| # | 経路 | 症状 | 深刻度 |
|---|---|---|---|
| 1 | キャンバスクリック(`LadderCanvasHost_PreviewMouseLeftButtonUp`→`TryPlaceActiveTool`) | `TryPlaceElement`再入、バー内容(デバイス名入力・種別選択)が無警告で初期化・対象セルすり替え | 高 |
| 2 | 部品選択リストクリック(`PartSelectionItem_Clicked`) | 同上（`IsPartSelectionVisible`はTool.Mode==PlaceElement連動でバー表示中も表示され続けるため操作可能） | 高 |
| 3 | **DRC出力パネル行クリック**(`OutputGridRow_Clicked`→`JumpToDiagnostic`) | `SelectedCell`が占有済みセルへジャンプ。`PlaceElementAtSelectedCell`は占有再チェックをせず`Sheet.Elements`(`List<ElementInstance>`)も重複Posを防がないため、**OKで同一Posに要素が二重生成される不正状態**が生じ得る | **最重要** |
| 4 | シートナビゲーション(`SheetNavList`) | `CurrentSheetIndex`セッターの副作用で`SelectedCell=null`→OKが戻り値チェックなしに無言no-op、バーは正常終了したかのように閉じる | 中 |
| 5 | 選択ツールボタン（Esc相当） | `SelectedCell=null`になるがバーは開いたまま。続けて別セルをクリックすると`Tool.Mode`は不変のため、**意図せずキャンセルしたはずの旧パーツ種別・デバイス名で新セルへ実配置**されうる | 高 |
| 6 | 新規作成/開くボタン | ドキュメント自体が差し替わるが、バーは開いたまま。旧文書由来のパーツ種別・デバイス名が新文書のセルへ実配置されうる（キーボードのCtrl+N/Oは243行目でブロックされるのに対し、マウス経路のみ非対称に素通し） | 高 |

**根本原因（Altitude、CONFIRMED）**: 個別ハンドラへの後追いガード追加という対症療法パターンで、
コミット自身がT-021のモグラ叩き教訓に言及しているにもかかわらず同じ轍を踏んでいる。恒久対応には
「バー表示中はメインコンテンツ全体を無効化する」一般化した仕組み（例: メイン作業域を包む
`Grid`/`ContentControl`に`IsEnabled`をバー表示状態にバインドする等）が必要と考えられる。

### (2) 分岐B＝キャンセル時の原子的取消の維持 — **単体では維持、複合操作で新規リスク**

`PlacementCancelButton_Click`→`ClosePlacementBar`のみで要素は作られず、素朴な「キャンセル」単独の
挙動としては原子的取消が保たれている。ただし上記(1)の#5（選択ツールボタン）のように、**キャンセル
相当の操作をした後に別操作(キャンバスクリック等)を挟むと、SelectedCellが更新されTool.Modeは
不変のまま残るため、結果的に意図しない実配置が起きる**という複合的な抜け道がある。単体の分岐B
自体は無傷だが、(1)の穴と組み合わさることで実質的に骨抜きになるケースがある。

### (3) フォーカス復帰の一箇所集約 — **集約自体は良好、初期表示側に別の懸念**

`ClosePlacementBar`にOK/キャンセル両経路の`FocusCanvas()`が一箇所集約されている点はT-021の教訓
（遷移点を複数箇所に分散させない）に忠実で妥当。後追いFocusCanvas散在は見当たらない。

ただし対称的なもう一方（**バーを開く際**の初期フォーカス）には別の懸念がある（CONFIRMED）:
`ElementPlacementBar.Visibility = Visibility.Visible;`の直後、同一コールスタックで
`PlacementDeviceNameBox.Focus()`を同期呼び出ししている（616-617行目）。旧`ElementPlacementDialog`は
`Loaded`イベント（レイアウト完了後）で`Focus()`していたのに対し、新実装は`Collapsed→Visible`直後の
Measure/Arrange未完了のタイミングで呼んでおり、Microsoft Learn公式ドキュメントも「初期フォーカスは
`Loaded`または`Dispatcher.BeginInvoke`で設定すべき」と明記している。`Focus()`失敗時は例外もフィード
バックも無いため、気づかれにくい潜在バグになりうる。

### (4) Esc4層・T-021既存規約との整合 — **単体では整合、(1)の穴が波及**

バー表示中のEsc処理がバー自身の`IsCancel`ボタン一本に委譲され、`Window_PreviewKeyDown`側の層1コメント
更新（「層1はバー表示中の早期リターンにより対象外」）も整合的に更新されている。これ自体は妥当。
ただし(1)の各経路でSelectedCell/CurrentSheetが書き換わった状態でEsc/OKを押した場合の挙動は、
Esc/4層設計そのものの問題ではなく(1)由来の副作用として現れる。

### (5) オーバーレイ要素のUIA到達性の静的確認 — **問題なし（推奨通りの構成）**

`ElementPlacementBar`は同一Window内の通常の`Border`+`StackPanel`（Popup不使用、隠密の事前調査
`docs/ecad2-t033-ui-automation-impact-survey-onmitsu.md`の推奨通り）であり、既存の
`ecad2-ui-automation`スキルの`Get-Ecad2Root`（`AutomationElement.FromHandle`+`Descendants`探索）が
そのまま到達できる構成になっている。Popup特有の別HWND化・フォーカス委譲の懸念は生じない。
`Visibility=Collapsed`時はWPF標準挙動としてレイアウト・AutomationPeer共に非対象になるため、
表示/非表示の切替もUIA探索と自然に整合する。静的確認の範囲で問題は見当たらない。

---

## `code-review`スキル併用の追加指摘（high、Reuse/Simplification、CONFIRMED）

- **Reuse**: `ElementPlacementBar.Visibility`をcode-behindで直接操作しており、`IsPartSelectionVisible`
  等のViewModelプロパティ+コンバータバインディングという既存の「表示状態はViewModel単一情報源」規約
  から外れている。これは(1)のガード漏れとも無関係ではなく、ViewModel側に`IsPlacementBarVisible`相当の
  プロパティがあれば単体テストでガード漏れの一部を検出できた可能性がある。
- **Simplification**: `_placementIsOr`という一時状態がクラスフィールドとして永続化されており、
  バー非表示中は無意味な値を持ち続ける。増分2以降で同種のフィールドが増殖するリスクがある。

---

## 総評・推奨

往復1周目としては**(1)の穴を塞ぐ修正が必須**と考える。対症療法（各マウスハンドラへ個別ガード追加）
ではなく、Altitudeの指摘通り「バー表示中はメイン作業域全体を無効化する」一般的な仕組みへの集約を
推奨する。特にDRC出力パネル経由の占有チェックなし二重配置（#3）はデータ整合性に関わる最重要の
欠陥であり、優先的な対処が必要。フォーカスタイミング（観点3の初期表示側）は軽微だが合わせて手当て
可能なら望ましい。

修正後は忍者による実機確認（Enter/Esc/マウスクリックの三経路、キャンセル時の原子的取消、上記穴が
実機でも塞がっていること）を推奨する。

---

## 出典・参照

- 対象コミット `de9543c`（`git show`で全差分確認）
- `src/Ecad2.App/MainWindow.xaml`, `src/Ecad2.App/MainWindow.xaml.cs`
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`PlaceElementAtSelectedCell`, `IsSelectedCellOccupied`, `CurrentSheetIndex`）
- `src/Ecad2.App/ViewModels/OutputPanelViewModel.cs`（`JumpTo`）
- `src/Ecad2.Core/Model/Sheet.cs`（`Elements`の型、重複Pos制約の有無）
- `docs/ecad2-t033-implementation-plan-samurai.md`（3.3節・6.2節、家老依頼の背景）
- `docs/ecad2-t033-ui-automation-impact-survey-onmitsu.md`（隠密の事前調査、観点5の前提）
- `docs/ecad2-t021-focus-design-consolidation-plan-onmitsu.md`（フォーカス集約設計の先例）
- `code-review`スキル（high、finder 8角度→verify 10件、全件CONFIRMED、Agent実行ログ）
- Microsoft Learn「Focus Overview - WPF」（観点3・初期フォーカスタイミングの一次情報）
