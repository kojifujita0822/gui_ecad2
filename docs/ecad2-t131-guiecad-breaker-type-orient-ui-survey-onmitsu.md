# T-131 原本GuiEcadにおけるUI形式調査（隠密）

> 2026-07-27 隠密調査。家老采配「T-131 原本GuiEcadにおけるUI形式の調査」（P-100・P-101起票、
> 殿裁可2026-07-27）を受けての静的調査。原本＝`C:\Users\kojif\Desktop\生産物\gui_ecad`。
> 前段＝`docs/ecad2-paramkeys-unwired-survey-onmitsu.md`（隠密、2026-07-15）。
> スコープ境界：**調査のみ、UI案の決定はしない、ecad2側の実装方針にも踏み込まない**。

---

## 総括

両方とも原本に対応する仕組みが**在った**。ただし性質が異なる：

- **P-100（Breaker3P種別）**：**事後編集可能なUI**が在る（プロパティパネル内ComboBox）。
- **P-101（主回路記号の向き）**：**事後編集UIは意図的に存在しない**（原本コメントに「切替不可」と
  明記）。代わりに**配置前選択UI**（メニュー項目が縦版・横版に分かれる）で向きを確定する設計。
  「意図的な不在」であり、実装漏れではないと判断する。

---

## P-100：Breaker3P種別（NFB/MCCB/ELB）

### (1) 原本に該当UIが在るか

**在る。**

### (2) 形式と操作手順

**形式＝プロパティパネル内、種別限定で出現するComboBox**（`MainPage.Properties.cs:261-290`）。

- 対象要素が`ElementKind.Breaker3P`のときのみ、プロパティパネルに「ブレーカ種別」ラベル＋
  `ComboBox`（項目＝NFB/MCCB/ELBの3値、`BreakerTypes`配列`MainPage.Properties.cs:400`）が
  追加される。
- 操作手順：(a) 図面上でブレーカ要素を選択 (b) 右パネルのプロパティタブに自動遷移
  （`ShowPropertiesPanel()`） (c) 「ブレーカ種別」ComboBoxで選択 (d) `SelectionChanged`
  イベントで即座に`CommitBreakerType`が呼ばれ、`Params[ParamKeys.Type]`へ`Undo`対応
  （`SetParamCommand`経由）で書き込まれる。
- 補足説明文言も併記される：「NFB/MCCB は同形・ELB は漏電テストボタン印付き。記号脇に
  ラベル表示。」（`MainPage.Properties.cs:284`）
- 未設定時（`Params`にキーが無い場合）は`"NFB"`を既定値としてComboBoxの初期選択に使う
  （`MainPage.Properties.cs:264-265`、ecad2の`DiagramRenderer`側フォールバックと同じ既定値）。

**ecad2との対比（事実のみ、実装提案ではない）**：この形式は、ecad2の既存プロパティパネル
実装（`SelectedElementKindDisplay`等、要素の種別に応じて表示内容を切り替える構造、
`MainWindowViewModel.cs`）と構造的に同型——「選択中要素の種別で条件分岐し、専用の入力欄を
プロパティパネルへ追加する」という設計パターンが原本・ecad2の両方に共通する。

---

## P-101：主回路記号の向き（V/H）

### (1) 原本に該当UIが在るか

**「配置後に変更する」UIは存在しない（意図的）。ただし「配置時に選択する」UIは存在する。**

### (2) 形式と操作手順

**形式＝配置ツールのメニュー項目が、記号種別＋向きの組み合わせごとに個別に用意されている**
（`MainPage.Tools.cs:238-252`、`OtherBuiltins`配列）：

```
("ブレーカ(NFB/MCCB/ELB) 縦", "Breaker3P#V"),
("ブレーカ(NFB/MCCB/ELB) 横", "Breaker3P#H"),
("電磁接触器 主接点 縦", "ContactorMain3P#V"),
("電磁接触器 主接点 横", "ContactorMain3P#H"),
("サーマル(OL) 2極 縦", "ThermalOverload3P#V"),
("サーマル(OL) 2極 横", "ThermalOverload3P#H"),
```

対象は主回路（三相動力）用の3極記号3種（Breaker3P・ContactorMain3P・ThermalOverload3P）
いずれも同型。

- 操作手順：(a) 「その他部品」メニュー（または左パレット）を開く (b) 「◯◯ 縦」または
  「◯◯ 横」のいずれかを選ぶ（`OnOtherPartSelected`→`ToolFromTag`→`ParseSymbolTag`が
  タグの`#`以降を`Orient`として解釈、`MainPage.Tools.cs:63-72`） (c) 図面上をクリックして
  配置——このとき`PlaceElementAt`内で選択済みの向きが`Params[ParamKeys.Orient]`へ書き込まれる
  （`MainPage.Pointer.cs:117`）。
- **配置後の変更手段は無い**。原本のコメントが明確に意図を述べている
  （`MainPage.Tools.cs:244-245`）：
  > 主回路（三相動力）用の3極記号。タグ "Kind#V/#H" で配置時に向きを確定（**切替不可**）。
  > 型(NFB/MCCB/ELB)はブレーカ配置後にプロパティパネルで切替。

**この一文が「型は事後編集可・向きは配置時固定」という非対称な設計を、原本の作者自身が
明示的に意図して書いたことを裏付けている。** P-101調査時点（前段調査、2026-07-15）で
隠密が「未設定時はnull扱いで実質常に既定向きのまま描画されると推測（実機未確認）」と
留保していた点は、**原本を見る限り「未実装」ではなく「配置前に確定させ、以後は変更させない」
という設計判断の産物**と判明した。**「意図的な不在」の可能性を潰す**という本タスクのDoD(3)
に照らし、これは実装漏れではないと判断する。

---

## 3. T-068増分3-c教訓との整合確認

家老采配文が触れた「T-068増分3-cの教訓＝原本を見ずに『欠陥』と判ずるな」との関係：本調査は
まさにその教訓の実践に当たる。P-101について、原本を見ずにecad2側だけで判断していれば
「事後編集UIが無いのは実装漏れ」と早合点しかねなかったが、原本の一次ソース（コメント含む）
を確認したことで「意図的な設計」と確定できた。

---

## 不明点

- 「配置後に向きを変えたい場合はどうするか」（原本のユーザー体験上の代替手段、例えば
  削除して再配置する運用を前提にしているか等）は、コード上の意図表明（コメント）のみが
  根拠であり、GuiEcad側の`docs/`にUI設計意図を記した文書があるかまでは確認していない
  （本調査の範囲外、必要なら追加調査可）。
- Breaker3P以外の2種（ContactorMain3P・ThermalOverload3P）のプロパティパネル実装は
  本調査では確認していない（P-100・P-101の対象範囲はBreaker3P/Orient全般のため、型の
  事後編集がBreaker3P限定か他の3極記号にも及ぶかは、`MainPage.Properties.cs`の全文
  （既に確認済み・528行）を見る限り**Breaker3P以外に型のComboBoxは存在しない**——
  ContactorMain3P・ThermalOverload3Pには型の区分自体が無いためと推測される、未確認）。

---

## 出典・参照

- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Properties.cs`
  （528行、全文。261-290行＝Breaker3Pプロパティパネル、399-409行＝`BreakerTypes`/
  `CommitBreakerType`）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Tools.cs`
  （266行、全文。36-48行＝`ToolState`/`PlaceOrient`、63-72行＝`ParseSymbolTag`、
  238-252行＝`OtherBuiltins`配列とその意図コメント）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Pointer.cs`
  （992行中前半500行確認。117行＝`PlaceElementAt`内の`Params[ParamKeys.Orient]`書込）
- `docs/ecad2-paramkeys-unwired-survey-onmitsu.md`（隠密前段調査、2026-07-15、P-100/P-101の
  ecad2側未結線状況）
- `docs/todo.md`（T-131節、73-96行）・`docs/proposed.md`（P-100/P-101記載、109-110行）
