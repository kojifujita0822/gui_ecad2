# T-033 残り2コミット静的レビュー（隠密、T-033完了判定）

> 2026-07-07 隠密レビュー。対象コミット `77c2f81`（拡張表示ボタンTabオーダー除外、殿裁定）・
> `025e8d6`（増分4、配置バー種別選択のシンボル化）。家老指定観点(a)〜(f)＋`code-review`スキル
> （medium、統合4角度→1-vote verify）併用。

---

## 結論：**クリーン。忍者の最終確認へ回してよい（これが通ればT-033完全Done）**

---

## 対象差分

- `77c2f81`：`src/Ecad2.App/MainWindow.xaml`のみ、+7/-5。拡張表示ボタンへ
  `KeyboardNavigation.IsTabStop="False"`を追加。
- `025e8d6`：新規`src/Ecad2.App/Converters/PartEntryToGlyphGeometryConverter.cs`、
  `src/Ecad2.App/MainWindow.xaml`、`src/Ecad2.Core/Persistence/BasicPartTemplates.cs`の3ファイル、
  計+84/-5。配置バーの種別選択ComboBoxをテキスト表示からGX様式シンボル表示へ変更。

---

## 家老指定観点の検証

### (a) コンバータのグリフ判別ロジックの正しさ —— **問題なし**

`(value as PartFolderEntry)?.Definition.Id`によるnull安全な取得＋switch式（`BasicPartTemplates`の
5定数＋フォールバック`Custom`）を確認した。`value`がnullまたは型不一致の場合もid=nullとなり
`Custom`へ正しくフォールバックする。ComboBoxのItemsSourceは`PartPaletteViewModel.Entries`
（`IReadOnlyList<PartFolderEntry>`）そのままのため、実運用でnull/型不一致に到達するケースは想定されない。

### (b) ツールバーグリフ流用の整合 —— **一致確認（コミットメッセージより広範囲に一致）**

コンバータ内の`ContactNo`/`ContactNc`/`Coil`/`Terminal`のPath Dataを、`MainWindow.xaml`内の
F5/F6/F7/F8ボタンのPath Dataと逐一比較し、**4種すべて文字列として完全一致**することを確認した。
さらにcode-reviewのReuse角度finderが、`Custom`（フォールバック用アイコン）も「自作パーツ」ツールバー
ボタンのPath Dataと完全一致することを発見した（コミットメッセージは「4種の流用」と述べているが、実際は
5種が一致）。境界ボックスの統一（コメント記載のx2-16,y4-14等）も確認し、セレクトSW新規グリフもこの
規約に沿っていることを確認した（構図自体の最終確定は殿目視の想定通り、様式統一のみ検分）。

### (c) セレクトSW新規グリフの様式統一 —— **確認済み**

X範囲2〜16・Y範囲4〜14で、F5/sF5と同じ境界ボックス規約に沿っていることを目視確認した。構図自体の
最終確定は殿目視を待つ設計のため、様式の統一性のみを検分対象とした。

### (d) ToolTip/UIA名維持とItemContainerStyleの実装整合 —— **問題なし**

`AutomationProperties.Name="{Binding SelectedItem.Definition.Name, RelativeSource={RelativeSource Self}, FallbackValue=種別選択}"`
は、`SelectedItem`がnullの場合パス評価不能でFallbackValueが正しく適用される（WPF標準仕様）。
`ItemContainerStyle`側の`ToolTip`/`AutomationProperties.Name`（`Binding Definition.Name`）も、
ドロップダウン各項目のDataContext（PartFolderEntry）から正しく解決される。

**追加検証（code-reviewのAngle A finderが提起、`ConvertBack`の例外リスク）**：`PartEntryToGlyphGeometryConverter.ConvertBack`は常に`NotSupportedException`を投げる実装だが、これが実際にWPFバインディングエンジンから
呼び出されクラッシュを引き起こす可能性がないか、verifyエージェントが最小WPFプログラム（scratchpad内、
src/への書き込みなし）で実測検証した。結果：**REFUTED**。`Path.Data`（`Shape.Data`）依存関係プロパティは
`BindsTwoWayByDefault=false`（リフレクションで実測確認）であり、Mode省略時はOneWayが既定となる。
ComboBoxの開閉・選択変更を伴う実際の使用パターンを再現しても`ConvertBack`は一度も呼ばれず（`ConvertCalls=4,
ConvertBackCalls=0`）、クラッシュしないことを確認した。

### (e) Tab除外がプレースホルダ既存パターン踏襲か・T-028復帰コメント —— **確認済み**

`KeyboardNavigation.IsTabStop="False"`は既存の小アイコン類・GridSplitterと同一書式で踏襲されている。
コメント（77c2f81差分）に「T-028で詳細画面機能を実装する際にTabオーダーへ復帰させること」と明記済み。
前回（onmitsu-6）指摘した実害（Enter確定阻害）は本コミットで解消されたことを確認した。

### (f) 便乗変更なし・増分1設計堅持 —— **問題なし**

- `BasicPartTemplates.cs`の変更：`CoilId`/`TerminalId`/`SelectSwitchId`公開定数化は、置換前のリテラル値
  （"basic-coil"等）と1文字違わず完全一致することを確認した（家老の一次確認と一致）。値の変更なし。
- Grepで`"basic-select-switch"`等の残存リテラルを検索した結果、`tests/Ecad2.Core.Tests/PartFolderStoreTests.cs:188`
  にJSON後方互換フィクスチャとして1件残るが、値は新定数と一致しており実害なし（定数参照への置き換え漏れ
  ではなく、独立したテストフィクスチャ）。
- 両コミットとも意図した範囲外の変更（便乗）はなし。`IsPlacementBarVisible`単一情報源等の増分1設計への
  介入もなし。

---

## `code-review`スキル併用の追加所見

### 所見1（PLAUSIBLE・修正不要・記録のみ）: グリフPath Dataの二重ハードコード

`PartEntryToGlyphGeometryConverter`（C#コード）と`MainWindow.xaml`のツールバーボタン（XAML）に、同一図形の
Path Data文字列が独立してハードコードされている。verifyエージェントが`docs/todo.md`を調査した結果、
対象グリフ（F5〜F8、T-040）は2026-07-06中に**3回作り直された経緯**があり、かつ隣接するT-043
（OR部品サムネイルとツールバーグリフの見た目不一致、専用タスク化済み）という類似の視覚不整合の前例が
このプロジェクトに実在することを確認した。現時点でこの2箇所間の食い違いは無い（T-040確定後にコピーして
作られたため）が、将来どちらか一方だけ意匠修正されると同一部品のシンボルが食い違うリスクは、理論上の
懸念ではなく実績に基づくものと言える。**T-033最終増分の場での修正は不要**（レビュー往復コストに見合わ
ない）。次にF5〜F8いずれかのグリフに手を入れる機会（またはT-043着手時）に、共通Geometryリソース化
（`x:Static`等で1箇所に集約）を検討候補として記録する。

### 検討したが不採用の候補

- `tests/Ecad2.Core.Tests/PartFolderStoreTests.cs:188`のリテラル残存：実害なし（値一致）、修正不要。
- Simplification/Efficiency/Conventions角度：該当なし。

---

## 忍者への申し送り

- 025e8d6の見た目確認：種別選択ComboBoxの閉状態・ドロップダウン展開の両方でシンボルが正しく表示される
  こと、ToolTip/UIA名（正式名称）がホバー・UIA検証で正しく取得できること。セレクトSW新規グリフは殿目視
  確定用にスクリーンショットを取得されたい。
- 77c2f81の確認：拡張表示ボタンがTabオーダーから正しく除外され、機器名入力→Tab連打でOK/キャンセルの
  循環に戻ることを確認（前回onmitsu-6で確認した実害の解消確認）。

---

## 出典・参照

- 対象コミット `77c2f81`・`025e8d6`（`git show`で全差分確認）
- `src/Ecad2.App/Converters/PartEntryToGlyphGeometryConverter.cs`（新規、全51行）
- `src/Ecad2.App/MainWindow.xaml`（200-270/480-521行目、ツールバー・配置バー種別選択）
- `src/Ecad2.Core/Persistence/BasicPartTemplates.cs`（Id定数化差分）
- `docs/archive/ecad2-t033-review-onmitsu-6.md`（前回レビュー、拡張表示ボタンEnter確定阻害の指摘）
- `docs/todo.md`（T-040の3回作り直し経緯、T-043の前例）
- `code-review`スキル（medium、統合4角度→1-vote verify、REFUTED1・PLAUSIBLE1・該当なし多数）
- verifyエージェントによる実測検証（`Path.DataProperty`のBindsTwoWayByDefaultリフレクション確認＋
  ComboBox実使用パターン再現、scratchpad内・src/への書き込みなし）
