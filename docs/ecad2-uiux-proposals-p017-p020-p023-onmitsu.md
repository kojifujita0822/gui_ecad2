# UI/UX見せ方案起草（P-017・P-020・P-023）

殿裁定2026-07-10により着手対象に選定、隠密が見せ方案の起草を委譲された3件。いずれもコード変更を伴わない案起草のみ。着手順はP-017→P-020→P-023（家老指定）。

---

## P-017: 未解決PartId時のa接点フォールバック、警告の見せ方

### 現状（調査結果）

未解決PartIdは配置時（`ElementInstance.Kind`未設定＝enum既定値0＝`ElementKind.ContactNO`）と読込時（`PartFolderStore.Enumerate()`のID重複再採番で既存図面のPartId参照が孤立）の両方の経路でContactNOへ静かにフォールバックする（`PartResolver.cs:37-53`）。現状、描画（`DiagramRenderer.DrawElement`）は通常のContactNOと完全に同じ経路を通り、未解決を示す特殊表示は皆無。

既存の警告UI資産：
- **DRC出力パネル**（`Diagnostic`レコード：Severity/Code/DeviceName/Message/Locations）。`SelectedDiagnostic`→`JumpTo()`で該当セルへ移動する連携が既にある。既存チェック種別はDRC-XREF/TYPE/CONN/LOAD等。文言トーンは「機器 {name}: 〜です/ありません。〜してください。」の断定＋対処提示型。重大度色：Error=Firebrick、Warning=DarkOrange、Info=Gray。
- **破線プリミティブ**（`LineStyle.Dashed`、`DrawingTheme.cs`で既にGroupFrame用に使用中）。
- カスタムアイコン資産（Warningグリフ等）は不在。色分けテキスト＋システムダイアログアイコンのみで表現する既存文化。

### 案

**案A: DRC新規チェック項目として追加（実装コスト: 小）**

`DesignRuleCheck`に新規診断コード（例: `DRC-PART-001`「部品参照未解決」）を追加し、既存のDRC出力パネル・ジャンプ機構をそのまま活用する。Severity=Warning。文言例：「機器 {name}: 部品参照が見つからず、a接点として扱われています。部品の再選択をご確認ください。」

- 長所: 既存の警告UIパターンを完全流用でき実装コスト最小。ジャンプ機能も自動的に付いてくる。
- 短所: DRCは実行契機（明示的チェック実行、または常時バックグラウンド実行かは要仕様確認＝不明）に依存するため、「図面を眺めているだけでは気づかない」タイムラグが生じうる。

**案B: 配置時の視覚的プレースホルダ表示（実装コスト: 中）**

`DiagramRenderer.DrawElement`で`_lib?.Get(e.PartId)`がnullの場合、通常のContactNO描画ではなく、既存の破線プリミティブ＋警告色（DRC Warning色=DarkOrangeと統一）で枠を強調する視覚的プレースホルダを描く。

- 長所: 図面を見た瞬間に異常箇所がわかる。DRC実行を待たない。
- 短所: レンダラへの新規分岐追加が必要（既存の破線・警告色資産は流用可能なため実装コストは中程度に留まる）。「a接点として動作する」という機能自体は変わらないため、シミュレーション結果への注意喚起にはならない。

**案C: 案A＋案Bの併用（実装コスト: 大）**

常時の視覚的違和感（案B）とDRC実行時の明示的メッセージ（案A）を二段構えにする。

- 長所: 最も見落としにくい。
- 短所: 実装コストが最大。

### 隠密所感

見せ方の性質が異なる（案A=能動的チェック時、案B=受動的常時表示）ため、殿の意向次第で選択が分かれる。両立させるなら案C。**着手順1件目としてはまず案A（DRC活用）が最小コストで既存パターンとの整合性も高く、有力**と考えるが、決定は殿に委ねる。

---

## P-020: 機器表「種別」列の一律Other表示

### 重要な前提修正

`docs/proposed.md`のP-020記述時点（T-037差分確認時）は`Device.Class`が無条件`DeviceClass.Other`固定だったが、**現在のコードベースでは既にT-045増分Bで`MapToDeviceClass`/`ResolveDeviceClass`（`MainWindowViewModel.cs:1326-1361`）が実装済み**であり、ElementKind→DeviceClassのマッピング自体は解消されている（殿裁可済みの案A対応表、忍者実機検証済み）。

**残る論点は「種別列の日本語表示化」のみ**（`MainWindow.xaml:428`の`<DataGridTextColumn Header="種別" Binding="{Binding Class}"/>`がConverter未指定で、`DeviceClass`のenum名がそのまま英語表示される点）。

### 既存の先例（構造的に完全一致）

`MainWindow.xaml:497`の「重大度」列が`DiagnosticSeverityToTextConverter`（`Converters/DiagnosticSeverityToTextConverter.cs`、enum switch→日本語）を使用しており、今回の「種別」列と**寸分違わぬ構造**。Window.Resourcesへの登録パターンも既存。

またCore層`DiagramRenderer.cs:817-828`の`DeviceClassLabel`（private、PDF出力のBOM表で使用中）に、DeviceClass→日本語ラベルの対応が既に存在：Relay=リレー、PushButton=押しボタン、SelectSwitch=切替SW、Lamp=表示灯、Timer=タイマ、Counter=カウンタ、Terminal=端子台、Other=その他。

### 案

**案A: IValueConverter方式（`DiagnosticSeverityToTextConverter`に倣う、推奨）**

新規`DeviceClassToTextConverter`を作成し、`MainWindow.xaml:428`の種別列バインディングへ適用。

- 長所: 既存の構造的先例と完全一致し実装が最も素直。Window.Resources登録パターンも流用可能。
- 論点: PDF出力の`DeviceClassLabel`（Core層private）と表示文言を一致させるか。一致させる場合、Core層のラベル文言をApp層Converterへコピーするか、`DeviceClassLabel`をinternal/public化してApp層から参照するかの設計判断を伴う（画面とPDFの表記統一 vs 層分離の維持）。

**案B: ViewModel計算プロパティ方式**

`DeviceTableViewModel`側に`DeviceClassDisplay`のような計算プロパティを設け、DataGrid列のバインド先をそちらへ切り替える。既存の`SelectedElementKindDisplay`/`KindDisplayName`（`MainWindowViewModel.cs:1153-1181`）が同型の先例。

- 長所: ViewModel層に既存の先例がある。
- 短所: Converter方式より一手間多い（DeviceTableViewModel側でDeviceのラップ表示用プロパティを新設する必要がある）。

### 隠密所感

構造的に完全一致する先例（案A）が既にあるため、**案Aが有力**。文言統一の要否のみ殿判断が必要。

---

## P-023: 部品選択リストの選択ハイライトが残らない

### 現状（調査結果）

`PartSelectionList`（`MainWindow.xaml:455`、ListBox）は`SelectedItem`バインドが一切なく、**選択状態を保持する場所がそもそも存在しない**設計（`PartPaletteViewModel`自体も選択状態プロパティを持たない）。クリックは`PreviewMouseLeftButtonDown`のコードビハインドイベントで処理され（`ListBoxItem.Selected`が同一アイテム再選択時に発火しないWPF仕様への対処、T-016確認済み既存知見）、`TryPlaceElement`実行後ただちに`IsPlacementBarVisible=true`が立つ。

`MainContentArea`（`PartSelectionList`を含む右パネル全体）は`IsEnabled="{Binding IsPlacementBarVisible, Converter=InverseBool}"`で配置バー表示中は丸ごとdisabledになる（現行モーダル同等の使用感、既存コメントに明記）。

既存デザイン言語：キャンバス上の選択ハイライトは**OrangeRed系**（`SelectedCellPen`矩形枠線・`SelectedConnectorPen`太線）がアプリ内で唯一の意図的な選択表現。DataGrid/ListBoxの独自選択スタイルは他に存在せず（既定テーマ依存）。対照的に出力パネル`OutputGrid`は`SelectedItem="{Binding OutputPanel.SelectedDiagnostic}"`で選択状態をVM側に保持する設計（非対称）。

部品配置のキーボードショートカット（F5等）は`PartSelectionList`を経由しない別系統（`SelectedCell`前提の直接呼び出し）。リストは事実上マウス専用の操作面。

### 案

**案A: 配置バー内に選択中部品の情報を表示（実装コスト: 小、推奨）**

`ElementPlacementBar`（配置バー）自体に、選んだ部品のサムネイル・名前を表示するエリアを追加する。リスト側の選択状態保持は不要——配置バー表示中はリストがdisabledになる現行設計と競合せず、「今何を配置しようとしているか」を配置バー側で完結して示せる。

- 長所: 既存の「クリック即配置バー表示」設計を変更せず、disabled化とも競合しない。実装範囲が配置バーのみで完結。
- 短所: リスト自体のハイライトという原初の要望（「どの部品をクリックしたか」の視覚的な残存感）には直接応えない、あくまで配置バー側での代替表示。

**案B: PartPaletteViewModelへSelectedEntry等を追加しSelectedItemバインド（実装コスト: 中）**

出力パネル`OutputGrid`の既存パターン（`SelectedItem`をVM側に保持）に倣い、`PartPaletteViewModel`に選択状態プロパティを新設、`PartSelectionList`と双方向バインドする。配置バー表示中も選択状態を保持し続け、配置バー非表示に戻った際にハイライトが復元される。

- 長所: アプリ内の設計一貫性（出力パネルと同型）が得られる。ハイライト方式自体は既定のDataGrid/ListBox選択色、またはOrangeRed系で明示的にスタイル定義するかは別途選択可能。
- 短所: 「同一アイテム再選択時に`ListBoxItem.Selected`が発火しない」というWPF仕様上の制約（既存コメントに明記）があるため、単純なSelectedItemバインドだけでは不十分——既存の`PreviewMouseLeftButtonDown`ハンドラと併用し、そちらから明示的に`SelectedEntry`を代入する形の実装が必要になる。

**案C: ItemContainerStyleでOrangeRed系の一時ハイライトを追加（実装コスト: 小〜中）**

案Bの状態保持を伴わず、クリックされた項目に対しコードビハインドから一時的なスタイル（背景色等）を付与する簡易版。

- 長所: 状態管理の新設が不要で実装が最小。
- 短所: 配置バー表示中は`MainContentArea`ごとdisabled＝グレーアウトされるため、ハイライトが視認できるかは実機確認が必要（disabled状態でのVisualStateがどう見えるか、既存デザインでは未検証）。状態保持がないため配置バーを閉じた後にハイライトが残るかも設計次第。

### 隠密所感

原要望（「クリックした部品がわかる視覚フィードバック」）に最も直接的に応えるのは案B（出力パネルとの設計一貫性も得られる）だが、実装コストは中程度。最小コストで体感改善を狙うなら案A（配置バー側での代替表示）が有力。両立も可能。決定は殿に委ねる。

---

## 派生提案の有無

なし（3件とも家老采配の範囲内）。

## 不明点

- P-017: DRC実行契機（明示的チェック実行か、常時バックグラウンド実行か）は本調査では未確認。案Aの実効性（気づきやすさ）に関わるため、必要なら追加調査可能。
