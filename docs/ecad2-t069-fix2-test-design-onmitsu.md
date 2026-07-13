# T-069 往復2周目指摘 修正テスト設計書(隠密起草・往復3周目)

- 題目: 往復2周目レビュー(`docs/ecad2-t069-context-menu-review2-onmitsu.md`)で発見した要修正3件への修正テスト設計
- 起草者: 隠密
- スコープ境界: 設計書のみ。実装はしない。侍が本設計に基づきテストコードへ落とす(設計にないテスト追加は自由、設計にあるものを勝手に省くのは不可)

## 1. 要修正1(最重要): SelectedElement系操作のEnd-to-End検証

### 1.1 仕様(あるべき振る舞い)

**表示と実行の整合原則**: CellWidth>1要素(Motor=3、Breaker3P/ContactorMain3P/ThermalOverload3P=2)の**占有範囲内のどのセルで右クリックしても**(アンカーセルであれ非アンカーセルであれ)、そこで開いたメニューの「削除」「機器名変更」は**同一の要素**に対して正しく作用する。「メニューは出るが実行が伴わない」という不整合を許さない。

この仕様は実装アプローチに依存しない(`SelectedElement`ゲッターをHitTestElementベースへ統一する案/右クリック側で局所対応する案、いずれでも成立すべき契約)。

### 1.2 同値分割

軸1: セル位置の分類
- (A) アンカーセル(要素の左上、`el.Pos`そのもの)
- (B) 非アンカーセル(占有範囲内、アンカー以外)

軸2: 対象要素のCellWidth
- CellWidth=2(Breaker3P/ContactorMain3P/ThermalOverload3P、代表としてBreaker3Pを使用)
- CellWidth=3(Motor)

軸3: 実行する操作
- 削除(`DeleteSelectedElement()`)
- 機器名の取得(`SelectedElementDeviceName`のgetter)
- 機器名の設定(`SelectedElementDeviceName`のsetter)

### 1.3 境界値分析

CellWidth=Nの要素は列`[anchor, anchor+N-1]`を占有する。境界は「アンカー(下限)」「アンカー+N-1(上限)」。CellWidth=3の場合のみ中間セル(アンカー+1)という第3の代表点が生じる。

| CellWidth | 検証すべきセル位置 |
|---|---|
| 2 | アンカー(列0)、非アンカー(列1=上限) |
| 3 | アンカー(列0)、中間(列1)、非アンカー右端(列2=上限) |

### 1.4 対称性点検表(このテストスイートが最終的に埋めるべきセル)

| セル位置\操作 | 削除 | 機器名取得 | 機器名設定 |
|---|---|---|---|
| CellWidth=2・アンカー | 済(既存) | 済(既存) | 済(既存) |
| CellWidth=2・非アンカー | **新規・必須** | **新規・必須** | **新規・必須** |
| CellWidth=3・アンカー | 済(既存) | 済(既存) | 済(既存) |
| CellWidth=3・中間 | **新規** | **新規** | **新規** |
| CellWidth=3・非アンカー右端 | **新規・必須** | **新規・必須** | **新規・必須** |

「既存」としたセルも、実際には往復2周目時点で`HitTestElement`単体の回帰テスト(アンカー一致)はあるが、`SelectedElement`経由の削除・機器名変更をEnd-to-Endで検証したテストは無かった(1周目の穴、隠密指摘)。**このEnd-to-Endテスト自体は今回全パターンが新規**と扱ってよい。CellWidth=3・中間は必須ケースの隙間を埋める任意扱いでも可(境界値分析上の主眼は上限・下限であり、中間は代表点確認の位置づけ)。

### 1.5 具体的テストケース(Theory推奨)

`[Theory]`+`[InlineData(cellWidth, targetColumnOffset)]`で、`anchor + targetColumnOffset`のセルを`SelectedCell`相当として扱い、以下を1テスト内で検証:

```
[InlineData(2, 0)] // CellWidth=2, アンカー
[InlineData(2, 1)] // CellWidth=2, 非アンカー(上限)
[InlineData(3, 0)] // CellWidth=3, アンカー
[InlineData(3, 1)] // CellWidth=3, 中間
[InlineData(3, 2)] // CellWidth=3, 非アンカー(上限)
```

各ケースで:
1. 対象CellWidthの要素(DeviceName="M1"等、既知の値)をアンカー位置に配置
2. 「アンカー+targetColumnOffset」のセルを対象として選択解決を行う(実装アプローチに応じ、`SelectedCell`直接代入、または`HitTestElement`の結果を経由する等、侍の実装に合わせた入口を使う——ここは実装依存になるため、侍が選んだ入口に対して同じアサーションを課す)
3. アサーション(3点、同一テスト内で全て検証):
   - `SelectedElementDeviceName`(getter)が配置した要素の`DeviceName`と一致する
   - `SelectedElementDeviceName`(setter)で新しい名前を設定すると、対象要素の`DeviceName`が更新される
   - (削除は状態を変更するため別Factに分離してもよい)`DeleteSelectedElement()`を呼ぶと`true`を返し、`sheet.Elements`から対象要素が消える

### 1.6 実装アプローチ非依存の検証ヘルパー

修正が(A)`SelectedElement`ゲッター統一、(B)右クリック側の局所正規化、いずれで実装されても、テストは「非アンカーセルを指定した後、削除・機器名変更が正しい要素に作用する」という結果だけを見る。**入力の与え方(SelectedCellへ何を代入するか)は侍の実装に応じて設計書ではなくテストコード側で決めてよい**——ただし、(A)の場合は`SelectedCell = 非アンカー位置`を直接テストで代入すればよく、(B)の場合は右クリックハンドラ相当のView層ロジックを経由しないとテストできない可能性がある。後者の場合、正規化ロジック(非アンカー→アンカーへの変換)をstaticメソッドとして抽出し、`CalculateSheetDropIndex`(T-082の先例)と同型のパターンで直接テストする設計を推奨する。

## 2. 要修正3: 行操作コマンド実行時のCommitDeviceNameEdit漏れ

### 2.1 仕様

機器名編集中(未確定)に、その要素より上の行に対して行挿入/削除コマンドを実行しても、編集中の内容は失われない(実行前に確定されるか、少なくとも警告なく消失しない)。

### 2.2 テスト化の限界と方針

`CommitDeviceNameEdit()`はView層(`MainWindow.xaml.cs`)のメソッドで、`DeviceNameBox`というWPF UI要素の未確定テキスト(`UpdateSourceTrigger=Explicit`)に依存する。この「未確定入力が実際に消えるかどうか」自体はView層のバインディング挙動であり、ViewModelの単体テストでは再現できない(往復1周目の`CommitDeviceNameEdit`修正時と同じ制約)。

**テスト化可能な代替観点**: `InsertRowBeforeCommand`/`DeleteRowAtCommand`の実行が、`SelectedCell`(延いては`SelectedElementDeviceName`)のPropertyChangedをどのタイミングで発火させるかは、ViewModelレベルで検証可能。以下を推奨:

- **Fact**: `InsertRowBeforeCommand`/`DeleteRowAtCommand`を実行すると、対象行より下の選択中要素の`SelectedCell`が正しくシフトすること自体は既存機能として検証済みのはず(既存テストの有無を侍が確認、無ければ穴として追加)。
- 修正内容(行操作分岐でも`CommitDeviceNameEdit()`を呼ぶ横展開)自体はView層のイベントハンドラ内ロジックのため、**単体テスト原理的に不可**——ここは家老指示どおり実機確認(忍者)へ委ねる。

### 2.3 実機確認への申し送り事項(設計書に明記し侍・忍者へ伝達)

「機器名編集中(未確定)に、行操作メニューから行を挿入/削除しても入力内容が消えないこと」を実機確認の観点として明記する。

## 3. 要修正2: Tool.Modeガード粒度

### 3.1 仕様

右クリック処理は、**実際に記入中ドラフトを保持しているモード**(PlaceConnector/PlaceLine/PlaceImageのうち、対応する`_connectorDraft`/`_freeLineDraft`/`_imageInsertDraft`等が非null)の間のみ制限されるべきで、記入中ドラフトを持たないモード(PlaceElement等の静的な状態、あるいはドラフトを持つモードでもドラフト未開始の瞬間)では、既存の右クリックメニュー機能(削除・行操作・縦コネクタ削除)が引き続き使えるべきである。

### 3.2 テスト化可能な境界

修正方針(Tool.Modeガードを「実際にドラフトを持つ状態のみ」へ絞る)を実装する際、**「現在ドラフトを保持しているか」を判定する専用プロパティ/メソッド**(例: `HasAnyDraft`のような、既存の`_connectorDraft is not null || _freeLineDraft is not null || _imageInsertDraft is not null`等を集約したもの)を`MainWindowViewModel`に新設するなら、これは純粋なプロパティとして単体テスト可能になる。

**推奨テストケース(Theory)**:
- ドラフトなし(Tool.Mode==Select) → `false`
- `BeginConnectorDraft()`実行後 → `true`
- `BeginFreeLineDraft()`実行後 → `true`
- `BeginImageInsertDraft(...)`実行後 → `true`
- Tool.Mode==PlaceElement(ドラフト無し、部品配置準備中のみ) → `false`(ここが往復2周目で誤ってブロックされていた核心のケース)
- 各ドラフト確定/取消後 → `false`に戻る

### 3.3 テスト化不可能な部分

右クリックハンドラ自体(`LadderCanvasHost_PreviewMouseRightButtonDown`)がこの判定プロパティを正しく参照しているか、行操作メニュー分岐が実際に開けるようになったか、は View層のイベントハンドラ+WPF ContextMenu依存のため単体テスト不可。実機確認(忍者)の観点として「PlaceElementモード中(連続配置中)に既存要素の削除・行操作・縦コネクタ削除の右クリックメニューが機能すること」「PlaceConnector等の記入中は右クリックしてもドラフトが保持されたままメニューが出ないこと」の両方を明記する。

## 4. 派生提案の有無

なし(全てT-069往復3周目の範囲内)。
