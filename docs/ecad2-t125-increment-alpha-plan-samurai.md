# T-125 増分α 実装計画（侍・起草）

> 2026-07-27 侍起草。家老の下知（DoD 3件）に応じ、増分α（境界ガード横展開＋部品種別弁別ロジック
> 共通ヘルパー化）の計画を起草する。**起草のみ。実装には一切入っていない**（殿裁可を待つ）。
> ブランチ運用＝**main直**（殿裁定2026-07-27）を前提とする。

---

## 総括——起草の結果、増分αの前提に3点の訂正が要る

先に結論を述べる。**増分αは「既存パターンの機械的横展開」ではない。** 一次ソースを当たった結果、
着手前に立てられていた前提のうち3点が現況と合わぬことが判明した。いずれも計画の中身を左右する。

| # | 従来の前提 | 実態 | 影響 |
|---|---|---|---|
| 1 | `ValidatePlacement`を他6経路へ横展開する | **座標系が異なり、そのままでは1つも渡せない** | αの作業内容が変わる |
| 2 | 境界ガードは7経路中1経路のみ | **3経路は別手段で担保済み。欠けているのは3経路** | 着手前指標の定義が変わる |
| 3 | 部品種別弁別ロジックが4箇所重複 | **現況2箇所＋近縁1箇所。うち1件は削除済み・リスト自体に誤りあり** | α-3の要否が変わる |

---

## 訂正1：`ValidatePlacement`は横展開できない（座標系が異なる）

`ValidatePlacement`の実体は`MainWindowViewModel.cs:2794-2798`——

```csharp
private bool ValidatePlacement(GridPos pos, int cellWidth, Sheet sheet, ElementInstance? exclude = null)
    => IsWithinGridBounds(pos, cellWidth, sheet) && !IsOccupied(pos, cellWidth, sheet, exclude);
```

- 引数は`GridPos`（int の行・列）＋`cellWidth`
- `IsOccupied`（:2705）は`sheet.Elements`を走査する**`ElementInstance`専用**の区間交差判定

対して各コレクションの座標表現は次のとおり（型定義＝`src/Ecad2.Core/Model/Element.cs`）。

| コレクション | 要素型 | 座標表現 | `GridPos`を渡せるか |
|---|---|---|---|
| `Elements` | `ElementInstance` | `GridPos Pos` ＋ `int CellWidth` | **渡せる（適用済み）** |
| `WireBreaks` | `WireBreak` | `int Row` ＋ `double Boundary`（列境界・0.5刻み） | 不可（列が境界値） |
| `ConnectionDots` | `ConnectionDot` | `double XMm, YMm` | 不可（mm実座標） |
| `Images` | `ImageInsert` | `double XMm, YMm, WidthMm, HeightMm` | 不可（mm実座標） |
| `Frames` | `GroupFrame` | `GridPos TopLeft` ＋ `int Width, Height` | 不可（矩形。**専用の`IsFrameWithinGridBounds`が既にある**） |
| `Connectors` | `VerticalConnector` | `double Column`（列境界）＋ `int TopRow, BottomRow` | 不可（線分） |
| `FreeLines` | `FreeLine` | `double X1Mm, Y1Mm, X2Mm, Y2Mm` | 不可（mm実座標の線分） |

**`GridPos`＋`cellWidth`という形のまま渡せるものは一つも無い。** `GroupFrame`は既に矩形版
`IsFrameWithinGridBounds`（:2698）を持っており、これが「横展開の結果あるべき姿」の実例に当たる——
すなわち**同じ関数を呼ぶのではなく、座標系ごとに等価なガードを置く**のが本来の横展開の姿である。

ゆえに増分αの作業は「`ValidatePlacement`の呼び出しを増やす」ではなく、
**「各座標系に応じた境界ガードを、欠けている経路にだけ入れる」**となる。

---

## 訂正2：境界担保の実態——欠けているのは3経路（DoD(2)への回答）

### 6経路それぞれの見立て

| # | 経路（メソッド） | 現状の境界担保 | ガード要否 | 挙動が変わるか |
|---|---|---|---|---|
| 1 | **`PlaceWireBreakAtSelectedCell`**<br>（:860、F10即時記入） | **無し**。重複チェックのみ（`Row`と`Boundary`の一致） | **要**（原本にも同型ガードあり） | **変わる**（下記A） |
| 2 | **`PlaceConnectionDot`**<br>（:1221、F10即時記入） | **無し**。`XMm`/`YMm`の完全一致重複のみ | **要（下限のみ）** | **変わる**（下記B） |
| 3 | **`ConfirmFreeLineDraft`**<br>（:2029、Enter確定） | 相対ステップの`Math.Clamp`（:2019）のみ。**絶対mm境界の検査は無い** | **要（下限のみ）** | **変わる**（下記B） |
| 4 | `ConfirmImageInsertDraft`（:1331） | **担保済み**。上流`UpdateImageInsertDraftPosition`（:1314-1321）が`Math.Clamp(x, 0, Max(0, maxXMm - WidthMm))` | 不要 | — |
| 5 | `ConfirmFrameDraft`（:1598） | **担保済み**。`AdjustFrameDraft`（:1583）が`IsFrameWithinGridBounds`を満たす時だけ更新、開始アンカーも呼び出し元（`MainWindow.xaml.cs:1543-1552`）が範囲確認 | 不要 | — |
| 6 | `ConfirmConnectorDraft`（:1916） | **担保済み**。:1919-1921で`topRow`/`bottomRow`を`[0, Rows-1]`、`column`を`[0, Columns]`へ`Math.Clamp`（防御的二重検証と明記） | 不要 | — |

**（参考）7種9箇所の「9箇所目」＝`ConfirmOrJoinTarget`（:2997）**——`Connectors`への追加が2箇所目。
クランプは無いが、値は`BuildOrJoinCandidates`が**既存要素の行・列から算出**する。既存要素は
`ValidatePlacement`（`Column + CellWidth - 1 < Columns`）を通っているため`Column + CellWidth <= Columns`
が成り立ち、`RightColumn`は列境界の上限`Columns`に収まる。**原理的に範囲内**と見るが、
これは推論であって実測ではない（α実施時にテストで裏づける対象に含めたい）。

### 【最重要】原本（GuiEcad）の検分結果——「漏れ」と「意図的な不在」が混在する

`samurai.md`「**漏れと断ずる前に、それが意図的な不在かを確かめる**」（T-068増分3-cの教訓）に従い、
踏襲元`C:\Users\kojif\Desktop\生産物\gui_ecad`（HEAD=`333ce51`）を当たった。**結果は割れた。**

**(A) 配線分断＝原本にガードがある。ecad2で落ちている＝実装漏れ**

`MainPage.Pointer.cs:261`
```csharp
if (row >= 0 && col >= 0 && col < _sheet.Grid.Columns)
```
要素配置（同`:305`）と**完全に同一の条件式**。すなわち原本では要素と配線分断は非対称ではない。
ecad2側でこれが落ちているのは、原本にあるものを移植し損ねた形。

**(B) 接続点・自由直線＝原本にも上限ガードは無い。ただしそれは意図的**

`MainPage.Pointer.cs:248`（接続点）・`:234`（自由直線）はいずれも
```csharp
if (xMm >= 0 && yMm >= 0)
```
——**下限のみ**。上限が無いのは設計思想による。レンダラ側にその前提が明示されている：
- `DiagramRenderer.cs:73-75`「自由直線・接続点・枠はmm実座標でグリッド行範囲を超えて広がりうる」
- `DiagramRenderer.cs:437`「横はみ出しは元々問題にならない」
- `Model/Element.cs:73-75`（FreeLine＝「グリッドに依存しない自由直線」）

`git blame`でも導入コミット以来一度も触られておらず、「足し忘れた」痕跡は無い。
**ゆえにこの2種にグリッド上限を課すのは、原本の設計思想からの逸脱**にあたる。
原本に倣うなら**下限（`xMm >= 0 && yMm >= 0`相当）だけを入れる**のが筋と考える。

**(C) 行（Row）の上限は、原本には要素配置にすら無い**

`DiagramRenderer.cs:63-67`が実効行数を`Math.Max(Grid.Rows, maxElementRow + 1)`と定め、
`MainPage.Pointer.cs:182-183`に「要素配置は行数無制限で図面が下に伸びるため」と明記されている。
**原本では`row >= Rows`はエラー状態ではない。**

一方**ecad2の`IsWithinGridBounds`は`pos.Row < sheet.Grid.Rows`を課しており、既に原本と異なる**
（T-045増分Cで確立、殿裁定2026-07-09＝下限0）。これは ecad2 独自の設計判断として既に定着している。
**配線分断に同じ行上限を課すか否かは、ecad2内の一貫性を採るか原本準拠を採るかの分岐**であり、
殿裁可を要すると考える（後述「使用感が変わる箇所」C項）。

**(D) 操作方式が原本とecad2で根本的に異なる**

原本の3種は**縦パレットでツールを選びマウスでクリック／ドラッグ**する方式。`F10`という
キー定義自体が原本に存在しない（`MainPage.KeyBindings.cs:48-113`で確認）。
ecad2の「選択セル起点＋F10即時記入」は**ecad2独自の新設計**である。

原本のガードは「マウスがキャンバス外を指した場合の無視」を目的とするもので、
選択セル方式に対する妥当性検証ではない。**ゆえに「原本に無いからecad2にも不要」という論法は使えない。**

### 根っこ——`SelectedCell`はグリッド範囲外に出る（そしてそれは仕様である）

上記1・2・3が実際に不正値を受け取りうるのは、`SelectedCell`が範囲外を取りうるためである。
これは**バグではなく仕様**（`MainWindowViewModel.cs:2682-2687`のdocコメントに明記）：

> 選択(SelectedCell)自体の仕様範囲(行-1・列-2まで選択可、殿教示2026-07-07・P-022/P-024)には
> 触れず、配置前のフィードバック用の判定に留める(殿裁定2026-07-09=下限0、選択の仕様は不変)

`memory: ecad2_grid_negative_range_spec`とも一致する。
**ゆえに「`SelectedCell`をクランプして根治する」案は仕様違反であり、採ってはならぬ。**
ガードは記入側（`PlaceWireBreakAtSelectedCell`等）に置く一手のみとなる。

なお原本は`MoveFocusCell`（`MainPage.KeyboardMode.cs:51-58`）で列を`[0, Columns-1]`にクランプ
しており、ここでも ecad2 は原本と異なる方針を採っている（ecad2 は選択の自由度を優先した形）。

---

## 訂正3：部品種別弁別ロジックは「4箇所」ではない

### リスト自体に誤りがある

T-125調査書（`docs/ecad2-t125-app-layer-structure-survey-onmitsu.md:98-101`）が挙げる4ファイル
`PartEditorDialog.xaml.cs` / `MainWindow.xaml.cs` / `PartPaletteViewModel.cs` / `MainWindowViewModel.cs`
のうち、**前2者に当該パターンは存在しない**（確認済み）：

- `PartEditorDialog.xaml.cs`：`Category`の出現ゼロ。`IsOrEligible`は`:322`の引き継ぎ代入のみ（判定ではない）
- `MainWindow.xaml.cs`：`IsOrEligible`の出現は`:3923`の**コメント内のみ**（実コードはT-033増分5で削除済み）。`PartRole`の出現ゼロ

元となったT-045所見I（`docs/archive/ecad2-t045-increment-b-fix-review-onmitsu.md:107-113`）が
挙げていたのは`PartEntryToGlyphGeometryConverter.cs` / `PartThumbnailRenderer.cs` /
`PartPaletteViewModel.cs` / `ResolveDeviceClass`の4箇所であり、**T-125調査書はこれを取り違えて転記
している**と見る。加えて**1件（`PartEntryToGlyphGeometryConverter.cs`）はコミット`c779210`
（T-071、2026-07-11）で削除済み**。

### 現況＝3箇所。だが「同型の重複」ではない

| # | 場所 | 条件式 | 目的 | Categoryゲート |
|---|---|---|---|---|
| 1 | `MainWindowViewModel.cs:2838` | `{ Category: "", Role: SelectSwitch, IsOrEligible: false }` | 機器表のDeviceClass分類＋テストモード操作対象判定 | **有** |
| 2 | `PartPaletteViewModel.cs:75` | `e.Category == "" && e.Definition.IsOrEligible` | 部品選択リストへORa/ORb論理エントリを生成 | **有** |
| 3 | `PartThumbnailRenderer.cs:46-49` | `isOr && IsOrEligible` ＋ `Role == ContactNO/ContactNC` | サムネイル描画形状の決定 | **無**（下記） |

**3箇所は条件の組が互いに異なり、判定の目的も異なる。** さらに#3は`Ecad2.Rendering.Wpf`層にあり、
`Category`の保持者`PartFolderEntry`は`Ecad2.Core.Persistence`＋App層の概念のため、
**参照方向の制約で`Category`を見ることが原理的にできない**（`PartThumbnailRenderer.cs:17-20`の
コメントがグリフPath Dataの複製理由として同じ制約を述べている）。

隠密がT-045当時に述べた見立て——「**対象4ファイルの目的が異なるため単純統合は不可、共通述語の
抽出に留める設計判断が要る**」（T-125調査書:142）——は正しい。だが現況では**共通述語を括り出しても
使い手は2箇所**であり、rule of three（3箇所目で共通化）の閾に届かぬ。

### 侍の提案：α-3は「共通化」ではなく「陳腐化コメントの訂正」に縮める

**共通ヘルパー化は見送りを提案する。** 理由は3点：

1. 現況2箇所（＋層の制約で参加できぬ1箇所）で、rule of threeに未達
2. 3箇所は条件の組も目的も異なり、括ると「`Category`ゲートの有無」という**意味のある差**が
   ヘルパーの引数として表に出るだけで、可読性が向上しない
3. `samurai.md`「**漏れと断ずる前に意図的な不在かを確かめる**」——#3にCategoryゲートが無いのは
   層の制約による**意図的な不在**であり、揃えるべき「漏れ」ではない

代わりに**実害のある陳腐化を2箇所直す**ことを提案する（いずれも削除済みファイルを参照している）：
- `MainWindowViewModel.cs:2825`——「`PartEntryToGlyphGeometryConverter.cs:53-63`と同型」（参照先は存在しない）
- `PartThumbnailRenderer.cs:18`——「`Ecad2.App.Converters.PartEntryToGlyphGeometryConverter`のORa/ORbグリフと同一……両方を合わせて直すこと」（**存在しないファイルへの改修指示が残っている**）

**この2件は次に触る者を確実に迷わせる。** 共通化より優先度が高いと考える。

---

## DoD(1)：増分αの分割案

上記を踏まえ、**3段**に分ける。段ごとに検証パイプライン（隠密の静的レビュー→忍者の実機確認）を回す。

### α-1：配線分断の記入に境界ガードを入れる【原本準拠・実装漏れの補填】

- **対象**：`MainWindowViewModel.cs:860 PlaceWireBreakAtSelectedCell`
- **入れるガード**：原本`MainPage.Pointer.cs:261`に倣い`row >= 0 && column >= 0 && column < Columns`
  相当。**行上限を課すか否かは殿裁可待ち**（後述C項）
- **`ValidatePlacement`は使わない**（`Boundary`が列境界のdouble、占有概念も無いため）。
  座標系に合った専用の述語を`IsWireBreakWithinGridBounds`等として`IsFrameWithinGridBounds`の隣に置く
- **検証観点**：
  - RED先行証明——ガードを外すと範囲外記入が通ることを実測（`PlaceWireBreakAtSelectedCell`は
    ViewModelの公開メソッドゆえ**単体テスト可能**。View層依存なし）
  - 境界値：列`-2 / -1 / 0 / Columns-1 / Columns / Columns+1`、行`-1 / 0 / Rows-1 / Rows`
  - **`[Theory]`+`[InlineData]`で書く**（`samurai.md`の境界値網羅ルール）
  - **入力の対称性・退化性チェック**——`Boundary = Column + 0.5`という変換を挟むため、
    列と境界値の対応がずれる罠がある。`Rows == Columns`のような対称なグリッドを使わぬこと
  - 回帰：既存の正常系（範囲内への記入・重複拒否）が壊れていないこと
- **規模見込み**：実装10行程度、テスト15件程度

### α-2：接続点・自由線の記入に下限ガードを入れる【原本準拠・上限は課さない】

- **対象**：`MainWindowViewModel.cs:1221 PlaceConnectionDot` / `:2029 ConfirmFreeLineDraft`
- **入れるガード**：原本に倣い`xMm >= 0 && yMm >= 0`相当の**下限のみ**。
  **グリッド上限は課さない**（原本の設計思想＝mm実座標プリミティブはグリッド外へ広がりうる）
- **自由線は始点・終点の両方を見る**——原本は始点のみ見て終点を見ていないが、ecad2の確定経路
  （`ConfirmFreeLineDraft`）は始点・終点が揃った状態で走るため、両方を検査できる。
  **`memory: feedback_geometric_transform_endpoint_oversight`（始点・終点の見落とし、T-119実例）**
  を踏まえ、始点だけ見て終点を忘れる形にせぬこと
- **検証観点**：
  - RED先行証明——ガードを外すと負のmm座標が通ることを実測（両メソッドとも単体テスト可能）
  - 境界値：`-0.1 / 0 / 0.1`、および**始点だけ負・終点だけ負・両方負**の3通り（対称性を崩す）
  - `ConfirmFreeLineDraft`は`StepCount`から終点を算出するため、**負方向へ伸ばした場合**を必ず含める
  - 回帰：主回路シート限定・ゼロ長拒否の既存判定が壊れていないこと
- **規模見込み**：実装10行程度、テスト12件程度

### α-3：陳腐化コメントの訂正【共通化は見送りを提案】

- **対象**：`MainWindowViewModel.cs:2825` / `PartThumbnailRenderer.cs:18`
- **内容**：削除済み`PartEntryToGlyphGeometryConverter`への参照を、現存する実体
  （`PartThumbnailRenderer`のORa/ORbグリフ）を指すよう書き改める
- **検証観点**：コメントのみゆえテスト不要。ビルドとテスト全件GREENの確認に留める
- **規模見込み**：2箇所・数行

### 段の順序と理由

**α-1 → α-2 → α-3**。α-1とα-2は座標系が異なり互いに独立ゆえ順不同だが、
**α-1は「原本にあるものの補填」で判断が要らず、α-2は「原本に無いものを足す」ゆえ設計判断を含む**。
判断の軽い順に倒し、先行する段で作法（述語の置き場所・テストの書き方）を固めてから次へ進む。
α-3はコード挙動に触れぬため最後。

---

## DoD(3)：βの数え方の定義確定

### 三者の食い違いの正体

| 出所 | 主張 | 実際に数えていたもの |
|---|---|---|
| `docs/todo.md` T-125節 | 4系統 | **根拠を特定できず**（下記） |
| 隠密調査書 | 左右2メソッド | 左クリック**選択**と右クリックの2つ（＝掴む経路を数えていない） |
| 侍の前回実測 | 3系統 | 左Down・左Up・右Downの3つ |

### 定義案（侍）

> **1系統＝「マウス座標を受け取り、要素種別を優先順位付きで順に判定していくチェーン」を
> 独立に手書きしている箇所。ラダー編集画面（`MainWindow.xaml.cs`）に限る。**

この定義で数えると**3系統**。内訳（行番号はHEAD=`b567b4c`時点）：

| 系統 | 場所 | 判定順序 |
|---|---|---|
| 左Down（掴む） | `MainWindow.xaml.cs:1470 LadderCanvasHost_PreviewMouseLeftButtonDown` | Connector → WireBreak → FreeLine → ConnectionDot → ImageHandle → Image → Element → Frame |
| 左Up（選ぶ） | `MainWindow.xaml.cs:1819 LadderCanvasHost_PreviewMouseLeftButtonUp` | Connector → Frame → WireBreak → ConnectionDot → FreeLine → Image → [セル] |
| 右Down（メニュー） | `MainWindow.xaml.cs:2041 LadderCanvasHost_PreviewMouseRightButtonDown` | Element → Connector → Frame → Image → [行操作] |

**3系統は末端の`HitTestXxx`プリミティブのみ共有し、チェーン自体は3回手書きされている**
（共有メソッドは無い）。**判定順序も互いに一致していない**（上表）。

### 定義から外すもの（数え方を揃えるための線引き）

1. **`PartEditorCanvas.cs:418 BeginSelectOrMove`**——部品エディタ内の別画面・2種のみ
   （接続点→図形）。βの対象外とする
2. **状態ディスパッチ連鎖6系統**——`MouseMove`（:1701）・`MouseUp`確定（:1859）・
   `LostMouseCapture`（:2289）・Esc（:2465）・矢印キー（:2747）・Delete OR連鎖（:2833と:3008の2箇所）。
   これらは`IsDragging*`／`Selected*`という**状態**を順に見るもので、**座標判定ではない**。
   ただし**同じ種別順序を持つ兄弟であり、βの共通化が及ぶ余地はある**——本定義では数えぬが、
   **βの射程を検討する際の関連箇所として台帳に残すことを勧める**
3. **単一種別のみのヒットテスト**（テストモードの押下・右クリック）——連鎖ではない

**「4系統」の根拠は特定できなかった。** 台帳の記載時点で状態ディスパッチ連鎖を1つ混ぜたか、
`PartEditorCanvas`を足したかと**推測**するが、**推測ゆえ断じない**。
定義を上記で確定するなら、**着手前の値は3系統**とするのが筋と考える。

### 家老の申し送り（機能的非対称）についての裏取り結果——**隠密の所見は正しい**

自らコードを当たり、確認した。

- `SelectedElement`（`MainWindowViewModel.cs:2058`）は **`el.Pos == pos`の完全一致**
- `HitTestElement`（`:2715`）は **区間交差**（`el.Pos.Column <= pos.Column <= el.Pos.Column + CellWidth - 1`）
- 左Upの連鎖に**Elementは存在しない**——要素選択はセル選択の副次的結果（`SelectedCell`→`SelectedElement`）
- 右Downは`HitTestElement`（区間交差）を使う（`MainWindow.xaml.cs:2079`）

**ゆえに`CellWidth > 1`の要素（Motor等）の非アンカーセルでは、左クリックでは選択できず、
右クリックメニューは開く**——コード上、非対称は確かに成立する（実機再現は未確認）。

**βの射程についての侍の見立て**：この非対称の根は`SelectedElement`の定義（γ側）にあり、
**βを「3系統→1系統の機械的共通化」とだけ捉えると温存される**。共通化の際は
「Elementの判定に何を使うか」を一つに決める必要があり、それは必然的に`SelectedElement`の
定義に触れる。**家老の見立てのとおり、βはγ側へ及ぶ**と考える。

なお左Downにも同型が1つある——`MainWindow.xaml.cs:1656-1658`が`HitTestElement`を呼ばず
**同等ロジックを手書き**しており、こちらは区間交差。すなわち**要素の当たり判定が
「完全一致」1つと「区間交差」2つの計3実装に分かれている**。βの共通化はここも束ねる対象となる。

---

## 使用感が変わる箇所（殿裁可の材料）

家老の留意「適用により**今まで置けた場所に置けなくなる**等、使用感が変わる箇所は必ず挙げよ」への回答。

### A. 【α-1】配線分断が範囲外に置けなくなる

- **今**：`SelectedCell`は仕様として行`-1`・列`-2`まで選択できる。その位置でF10を押せば
  **配線分断は記入できてしまう**（境界チェックが無いため）
- **後**：範囲外では記入されなくなる。**ステータスバーに理由を出すか否かも要判断**
  （現状の重複拒否時は「この位置には既に接続点があります」等の文言を出している）
- **原本との関係**：原本にも同じガードがある。**原本準拠の方向**

### B. 【α-2】接続点・自由線が用紙原点より外に置けなくなる

- **今**：負のmm座標へ記入できる（`SelectedCell`が行`-1`・列`-2`のとき`CellToMm`が負値を返す経路）
- **後**：下限を割る位置では記入されなくなる
- **原本との関係**：原本も下限のみガードしている。**原本準拠の方向**
- **上限は課さない**——課すと原本の設計思想（mm実座標はグリッド外へ広がりうる）と衝突する

### C. 【要殿裁可】配線分断に「行の上限」を課すか

**ここが本増分で唯一、原本と ecad2 で方針が割れる論点にござる。**

- **原本**：行に上限は無い（要素配置にも無い。図面が下に伸びる設計）
- **ecad2**：`IsWithinGridBounds`が`Row < Rows`を課している（T-045で確立、既に定着）

| 案 | 内容 | 利 | 害 |
|---|---|---|---|
| **C-1** | ecad2内の一貫性を採り、**行上限も課す** | 要素と配線分断が同じ規則になる。説明しやすい | 原本と異なる。行を増やす前提の作図が塞がる |
| **C-2** | 原本準拠で**行上限は課さず、列のみ**（原本`col < Columns`と同一） | 原本と完全に一致 | ecad2内で要素と配線分断の規則が食い違う |

**侍の推奨＝C-1。** 理由は、ecad2 は既に要素配置で行上限を課しており（殿裁定2026-07-09）、
**同じ画面の中で「要素は行22に置けぬが配線分断は置ける」という状態のほうが使い手に説明しづらい**
と考えるゆえ。ただし**これは使用感の判断であり、殿の裁可を仰ぐべき事柄**と心得る。

### D. 【β予告・本増分の範囲外】非アンカーセルでの要素選択

βで`SelectedElement`を区間交差へ揃えると、**Motor等の複数セル幅要素を左端以外でクリックしても
選択できるようになる**（現状は選択できず、プロパティパネルが開かぬ）。改善ではあるが挙動変化ゆえ、
**βの計画起草時に改めて殿へ諮る**べき事項として、ここに予告として記す。

---

## 効果測定指標の改訂案（DoDに含める【MUST】への対応）

台帳の指標1「**α＝境界ガード（`ValidatePlacement`）適用箇所の網羅率**」は、
訂正1・2により**そのままでは測れない**（横展開できぬものの網羅率は意味を成さぬ）。改訂を提案する。

| | 現行 | 改訂案 | 着手前の値 |
|---|---|---|---|
| α | `ValidatePlacement`適用箇所の網羅率（7経路中1） | **「シートへの実体追加経路のうち、座標系に応じた境界ガードを持つものの数」** | **9箇所中6箇所**（下記内訳） |
| β | ヒットテスト優先順位ロジックの実装箇所数（現状4系統→1系統） | **定義を上記に確定したうえで3系統→1系統** ＋ **要素当たり判定の実装数3→1** | **3系統／要素判定3実装** |

**α改訂案の着手前内訳（9箇所）**：

| 担保あり（6） | 担保なし（3） |
|---|---|
| `PlaceElementAtSelectedCell`（`ValidatePlacement`） | `PlaceWireBreakAtSelectedCell` |
| `ConfirmImageInsertDraft`（上流クランプ） | `PlaceConnectionDot` |
| `ConfirmFrameDraft`（`IsFrameWithinGridBounds`） | `ConfirmFreeLineDraft` |
| `ConfirmConnectorDraft`（`Math.Clamp`） | |
| `ConfirmOrJoinTarget`（**入力が範囲内である推論による**、要テスト裏づけ） | |
| （`ConfirmOrJoinTarget`の左分岐・右分岐で2箇所と数える） | |

**α完了時の目標＝9箇所中9箇所**。ただし接続点・自由線は「下限のみ」ゆえ、
**「グリッド上限まで担保している」とは数えぬこと**を明記しておく（後年の追跡で誤読されぬため）。

---

## 未確認・留保（断じておらぬ事柄）

1. **実機での再現は一切未確認**——本起草は静的読解のみ。範囲外へ配線分断・接続点を記入した際に
   実際に何が見えるか（描画されるか、PDF出力でどうなるか、保存・再読込で残るか）は未確認。
   **α着手前に忍者へ現況の再現を頼むのが筋**と考える（「今どう見えているか」を押さえてから直す）
2. **`ConfirmOrJoinTarget`が原理的に範囲内**という判断は推論であって実測ではない
3. **「4系統」の根拠**は特定できなかった（上述）
4. **原本の`docs-notes`等の設計議論**は未調査——接続点・自由直線の上限を意図的に見送った旨が
   文書に残っているかまでは確かめていない（コードとレンダラの前提から「意図的」と判じた）
5. **左Down系統でElement分岐に`return`が無い件**（`MainWindow.xaml.cs:1653-1665`）——
   他11分岐は`return`で打ち切るのに、この分岐だけ落ちてFrame判定へ進む。ただし
   `SelectedElement`は`SelectedCell`由来、`SelectedFrame`選択時は`SelectedCell`がnullになるため
   **両立せず実害は無い見込み**。**推測ゆえ断じない**——βの共通化時に改めて見る対象として記す

---

## 出典

- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`ValidatePlacement`:2794、各記入メソッド）
- `src/Ecad2.App/MainWindow.xaml.cs`（ヒットテスト3系統、F10経路:3428-3516）
- `src/Ecad2.Core/Model/Element.cs`（各要素型の座標表現）
- `C:\Users\kojif\Desktop\生産物\gui_ecad`（HEAD=`333ce51`、`MainPage.Pointer.cs`・`DiagramRenderer.cs`・
  `MainPage.KeyboardMode.cs`・`MainPage.KeyBindings.cs`）
- `docs/ecad2-t125-app-layer-structure-survey-onmitsu.md`（隠密第1報）
- `docs/archive/ecad2-t045-increment-b-fix-review-onmitsu.md:107-113`（所見I＝弁別ロジック4箇所の原典）
- `docs/todo.md` T-125節
