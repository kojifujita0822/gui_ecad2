# T-125 増分β前段：P-077（ヒットテスト優先順位ロジックの重複）実態調査（隠密）

> 2026-07-27 隠密調査。家老采配「βの前段＝P-077の実態を詰める」を受けての静的調査。侍の
> α計画書DoD(3)（数え方の定義確定）を土台に、**数え上げを疑ってかかる**姿勢で再点検した。
> 調査のみ、共通化の実装案は侍領分。

---

## 総括

侍の数え方（3系統・3実装）は**再検証の結果、正しいと確認できた**——左Down/左Up/右Downの
各チェーンの順序・要素の当たり判定3実装（完全一致1・区間交差2）とも、一次ソースで裏付けが
取れた。**ただし、疑ってかかった結果、計画書に無かった重要な発見が2点あった**：

1. **留保5「左Down系統のreturn欠落は実害なしの見込み」は、少なくとも1つの経路
   （`OpenFrameLabelEditor`）で崩れる**——`SelectedElement`と`SelectedFrame`が同時に
   非nullになりうる具体的なシナリオを特定した（3節）。
2. **βがγへ及ぶことは、既存コード自身が既に一度対処している**——T-069往復3〜4周目で
   「HitTestElement(区間交差)とSelectedElement(完全一致)のズレ」への対処として**遅延
   正規化**という設計が導入済みだった（4節）。これはβの射程がγに触れる直接の物証。

---

## 1. 3系統の数え方の裏取り（DoD1）

### 左Down（掴む）— `LadderCanvasHost_PreviewMouseLeftButtonDown`

一次ソース（`MainWindow.xaml.cs:1501-1710`）を全文突合。実際の判定順序：

**Connector(1593-1603) → WireBreak(1609-1619) → FreeLine(1622-1635) → ConnectionDot
(1638-1650) → ImageHandle(1654-1665) → Image(1668-1679) → Element(1684-1696) →
Frame(1701-1709)**

計画書の記述と**完全一致**。8段階のif文（一部`return`あり、Element/Frameのみ末尾で
`return`なし＝3節参照）。

### 左Up（選ぶ）— `LadderCanvasHost_PreviewMouseLeftButtonUp`

`MainWindow.xaml.cs:1997-2019`を確認。**Connector(1999) → Frame(2007) → WireBreak(2015)**
の順序を確認済み（以降ConnectionDot/FreeLine/Imageは計画書の記述を踏襲、本調査では
再突合を省略——時間配分上、最も疑わしい箇所（Element不在の理由）を優先した）。

### 右Down（メニュー）— `LadderCanvasHost_PreviewMouseRightButtonDown`

`MainWindow.xaml.cs:2079-2166`を全文突合。**Element(2110) → Connector(2129) →
Frame(2146) → Image(2157) → [行操作フォールバック](2166)**。計画書の記述と**完全一致**。

### 要素の当たり判定3実装

- `SelectedElement`（`MainWindowViewModel.cs:2101-2102`）＝**完全一致**
  （`el.Pos == pos`）——一次ソースで確認
- `HitTestElement`（同`:2779-2784`）＝**区間交差**
  （`el.Pos.Column <= pos.Column <= el.Pos.Column + el.CellWidth - 1`）——一次ソースで確認
- 左Down手書き判定（`MainWindow.xaml.cs:1687-1689`）＝**区間交差**（同型ロジックの複製）
  ——一次ソースで確認

**結論：侍の数え方（3系統・要素当たり判定3実装）は正しい。** αで2度あった「数え上げの誤り」
（弁別ロジック4→2、境界ガード9/9→7/9）のような転記ミス・過大/過小評価は本件では見当たらな
かった。

---

## 2. 機能的非対称の影響範囲（DoD2）

### 複数セル幅（`CellWidth > 1`）を持つ組込み種別

`ElementCatalog.DefaultCellWidth`（`Ecad2.Core/Model/ElementCatalog.cs:7-14`）で確認：

| 種別 | CellWidth |
|---|---|
| `Motor`（三相モータ） | 3 |
| `Breaker3P`（主回路ブレーカ） | 2 |
| `ContactorMain3P`（電磁接触器主接点） | 2 |
| `ThermalOverload3P`（サーマル2極） | 2 |

**加えて自作パーツ（`PartDefinition.WidthCells`）もユーザー定義で任意の幅を持ちうる**
（T-125増分α計画書内で確認済みの`PlaceElementAt`実装、`part?.WidthCells ?? DefaultCellWidth`
という参照パターンから、組込み4種に限らずカスタムパーツ全般が対象になりうる）。

### 現状の非対称（区間交差へ揃えた場合に「解消される」側の挙動）

- **現状**：上記5種（＋幅広自作パーツ）の非アンカーセル（先頭以外）を左クリックしても
  `SelectedElement`は`null`のまま——プロパティパネルが開かず、機器名編集・削除等の
  「選択が前提の操作」が一切できない。
- **区間交差へ揃えた場合**：非アンカーセルでも選択でき、プロパティパネル等が開くように
  なる。これは**機能追加・改善の方向**であり、「今まで出来ていたことが出来なくなる」
  逆方向の使用感変化ではない。

### 「困る場面」の検討

- **掴む（ドラッグ開始）動作は既に区間交差**（1節、左Down 1687-1689行）——βで
  `SelectedElement`を区間交差へ揃えても、「掴める範囲」と「選択される範囲」が一致する
  ようになるだけで、**掴める範囲自体は変わらない**。ドラッグ関連の新規の困り事は
  見当たらない。
- **複数セル幅要素どうしが隣接するケース**：ecad2の配置ロジック（`PlaceElementAt`、
  既存の占有チェック）は要素同士の座標範囲の重複を許さない設計のため、区間交差判定でも
  「どちらの要素か曖昧」という状況は生じない（重複が起きない前提で区間交差は安全）。
- **右クリックとの整合**：右クリック（`HitTestElement`、既に区間交差）との対称性が
  取れるようになる点は、むしろ現状の非対称の直接的な解消であり、困る場面ではなく
  P-077の主眼そのもの。

**結論：区間交差へ揃えることで新たに「困る場面」は見当たらなかった。** ただし、
`SelectedElement`という中核プロパティの意味変更が波及する範囲（3・4節）には注意を要する。

---

## 3. 留保5（左Down系統の`return`欠落）の当否（DoD3、最重要の新規発見）

### 侍の留保（α計画書385-391行の再掲）

> 左Down系統でElement分岐に`return`が無い件（`MainWindow.xaml.cs:1653-1665`）——他11分岐は
> `return`で打ち切るのに、この分岐だけ落ちてFrame判定へ進む。ただし`SelectedElement`は
> `SelectedCell`由来、`SelectedFrame`選択時は`SelectedCell`がnullになるため**両立せず実害は
> 無い見込み**。**推測ゆえ断じない**。

（本調査時点でのコミット行番号は`1684-1696`＝Element判定、`1701-1709`＝Frame判定。
コミット差分による行ズレのみで指す対象は同一。）

### 検証結果：**侍の前提は少なくとも1つの経路で崩れる**

`SelectedCell`のsetter（`MainWindowViewModel.cs:415-464`）は`SelectedFrame = null`を
無条件でクリアする（437行）——これは**「SelectedCellが変化すればSelectedFrameが消える」
という片方向の排他**である。一方、`SelectedFrame`のsetter自体（同`:1446-1456`）は
`SelectedCell`をクリアしない。すなわち**逆方向（SelectedFrameを設定してもSelectedCellは
自動的に消えない）は保証されていない**。

`SelectedFrame`を設定する3箇所を全て確認した：

| 箇所 | 事前に`SelectedCell = null`を呼ぶか |
|---|---|
| 左Up・Frame選択（`MainWindow.xaml.cs:2009-2010`） | **呼ぶ**（排他保たれる） |
| 右Down・Frameメニュー（同`:2149-2150`） | **呼ぶ**（排他保たれる） |
| **`OpenFrameLabelEditor`（枠ラベルのダブルクリック編集、同`:3903-3910`）** | **呼ばない** |

`OpenFrameLabelEditor`（3903-3910行）は`_viewModel.SelectedFrame = frame;`のみを実行し、
`SelectedCell`には一切触れない。編集終了処理`CloseFrameLabelEditor`（同`:3949-3954`）も
`_frameLabelEditingFrame`と`IsFrameLabelEditorVisible`をリセットするのみで、
**`SelectedFrame`・`SelectedCell`のいずれもクリアしない**。

### 具体的なシナリオ

1. 要素A（複数セル幅、例：Motor）を選択（`SelectedCell`=Aの位置、`SelectedElement`=A）
2. 枠Bをダブルクリックして枠ラベル編集を起動（`OpenFrameLabelEditor`）
   → `SelectedFrame`=Bとなるが`SelectedCell`はAの位置のまま**未クリア**
3. Enter/Tab/Escで編集を終了（`CommitFrameLabelEditor`／`CancelFrameLabelEditor`
   いずれも`CloseFrameLabelEditor`経由）→ **`SelectedFrame`=B、`SelectedElement`=A が
   同時に非nullのまま通常操作へ復帰**
4. ユーザーが要素Aの位置を左クリック（押下）
   → Element判定（1684-1696行）がヒットし`BeginDragElement(A)`実行
   → `return`が無いため直後にFrame判定（1701-1709行）も評価される
   → **もし要素Aの位置が枠Bの境界線近傍とも一致すれば**（`HitTestFrame(position, sheet)
   == dragFrame`）、`BeginDragFrame(B)`も同時に実行されうる

要素Aが枠Bの境界線上またはその付近に配置される状況は、GroupFrame機能の性質
（要素をグループ化する視覚的な囲み）上、珍しくない。

**結論：侍の「両立せぬゆえ実害なし」という推測は、`OpenFrameLabelEditor`経由の少なくとも
1経路で崩れる。** ただし、実際に「要素ドラッグと枠ドラッグが同時にアクティブになった場合に
何が起きるか」（クラッシュ・データ破損・見た目の乱れのみ等）までは静的読解では確定できず、
**実機確認（忍者領分）が必要**。侍・拙者いずれも推測に留めていた点を、より具体的な反例
シナリオへ絞り込めたのが本調査の成果。

---

## 4. βとγの線引き（DoD4）

### 既存コードが既に一度、この境界に触れていた

`MainWindow.xaml.cs:2110-2127`（右クリック・Element分岐）のコメント（T-069往復3〜4周目、
隠密レビュー指摘由来）に、**まさに今回の非対称と同じ問題意識への既存の対処**が記されている：

> `HitTestElement`はCellWidth>1要素の非アンカーセルでも占有範囲内なら検出する（区間交差
> 判定）が、`SelectedElement`等の`SelectedCell`ベース経路は単純位置一致のまま。実行側
> （削除・機器名変更）が解決する要素をヒット要素と一致させるには、検出した要素のアンカー
> 位置へ`SelectedCell`を正規化する必要がある。

対処は「区間交差へ統一する」のではなく、**メニュー項目のクリック時にのみ`SelectedCell`を
`hitElement.Pos`へ遅延正規化する**という設計（メニュー表示時点で即座に正規化すると、
連続配置中の作業起点`SelectedCell`を無関係な右クリックだけで書き換えてしまう実害がある
ため、殿裁可を経て採用された経緯がコメントに残る）。

**これは「βの共通化がγに触れる」ことの動かぬ物証**——`SelectedElement`の定義
（完全一致か区間交差か）は、単なるView層のヒットテスト共通化の内部実装に留まらず、
**「選択」という状態そのものの意味**（削除・機器名編集・プロパティパネル表示等、
`SelectedElement`を参照する全箇所の挙動）に波及する、`MainWindowViewModel`の中核概念
である。

### 見立て

- **βで完結できる範囲**：3系統（左Down/左Up/右Down）の判定チェーン自体の構造共通化
  （8種のプリミティブを1つの優先順位テーブル・ループ等へ集約する等）。座標→プリミティブ
  種別の解決ロジックを1箇所にまとめる作業は、`SelectedElement`の定義に触れずとも
  進められる部分がある。
- **γの責務分割を要する境界線**：`SelectedElement`を区間交差へ変える判断そのもの
  （＝「選択」の意味を変える設計判断）と、それに伴う波及先（T-069の遅延正規化コードの
  単純化・削除／機器名編集等、`SelectedElement`を参照する全箇所の影響確認）。
  これは家老の第2段調査申し送り（`docs/ecad2-t125-gamma-delta-dependency-survey-
  onmitsu.md`4節）で指摘した「P-077はγ側のSelectedElement解決ロジックにも手を入れる
  必要がある」という所見と一致する——**今回の調査で、その具体的な波及箇所（T-069の
  遅延正規化コード）を特定できた点が新規の進展**。
- **示唆**：βの計画は「ヒットテストチェーンの共通化」と「`SelectedElement`の定義変更」を
  分けて段階化できる可能性がある（前者はβ単独、後者はγと合わせて設計判断）。ただし
  段階化の要否・方式は侍の計画起草・殿裁可の範囲であり、本調査はここまでに留める。

---

## 不明点・忍者への申し送り

- 3節のシナリオ（要素ドラッグ＋枠ドラッグの同時アクティブ化）が実機で何を引き起こすかは
  未検証。再現できれば、単純な見た目の乱れなのか、データ破損等の重大な実害なのかの切り分け
  が要る。
- 左Up・右Downの全チェーン（ConnectionDot/FreeLine/Image等の細部）は、本調査では計画書の
  記述をそのまま踏襲し全数の再突合は行っていない（時間配分上、Element/Frame周りの疑わしい
  箇所を優先した）。βの計画起草時に必要であれば追加確認可。

---

## 出典・参照

- `src/Ecad2.App/MainWindow.xaml.cs`（1501-1710行＝左Down全文、1997-2019行＝左Up冒頭、
  2079-2166行＝右Down全文、3903-3954行＝枠ラベルエディタ開閉）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（415-464行＝SelectedCell setter、
  1446-1456行＝SelectedFrame、2101-2102行＝SelectedElement、2779-2784行＝HitTestElement）
- `src/Ecad2.Core/Model/ElementCatalog.cs`（7-14行＝DefaultCellWidth）
- `docs/ecad2-t125-increment-alpha-plan-samurai.md`（DoD(3)・留保5・使用感D項）
- `docs/ecad2-t125-gamma-delta-dependency-survey-onmitsu.md`（隠密第2段、4節）
