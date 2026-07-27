# T-125 増分β 実装計画（侍・起草）

> 2026-07-27 侍起草。家老の下知（DoD 5件）に応じ、増分β（ヒットテスト優先順位ロジックの共通化）の
> 計画を起草する。**起草のみ。実装には一切入っていない**（殿裁可を待つ）。
> ブランチ運用＝**main直**（殿裁定2026-07-27、αと同じ）。
> 一次ソースの行番号はすべて `ffbec7d` 時点。

---

## 総括——着手前指標に誤りがあった。ただし今度は着手前に見つけた

**「要素の当たり判定は3実装」という数え方は誤りである。実際は5箇所。** UI Automation 経路の2件が
どの調査にも含まれていなかった（家老の台帳・隠密のβ前段調査・侍自身のα計画書、いずれも3としていた）。

αでは同種の数え上げ誤りが**2度**起きた（弁別ロジック「4箇所」→実は2箇所、境界ガード「9/9」→実は7/9）。
いずれも**実装の後**に判明した。**今回は着手前に見つかった**——家老の「βの3系統も改めて裏を取れ」
という指示がそのまま効いた形にござる。

| | 従来の数え方 | 実際 |
|---|---|---|
| 座標→種別の優先順位チェーン | 3系統 | **3系統**（変動なし。裏取り済み） |
| 要素の当たり判定 | 3実装 | **5箇所**（完全一致3・区間交差2） |

---

## DoD(5)：着手前の指標を確定する

### 指標1＝座標→要素種別の優先順位チェーン：**3系統**

| 系統 | file:line | メソッド | 判定順序 |
|---|---|---|---|
| A（掴む） | `MainWindow.xaml.cs:1501` | `LadderCanvasHost_PreviewMouseLeftButtonDown` | Connector → WireBreak → FreeLine → ConnectionDot → ImageHandle → Image → **Element** → Frame |
| B（選ぶ） | `MainWindow.xaml.cs:1850` | `LadderCanvasHost_PreviewMouseLeftButtonUp` | Connector → Frame → WireBreak → ConnectionDot → FreeLine → Image → **セル（＝Element暗黙）** |
| C（メニュー） | `MainWindow.xaml.cs:2072` | `LadderCanvasHost_PreviewMouseRightButtonDown` | **Element** → Connector → Frame → Image → 行操作 |

**3系統は末端の `HitTestXxx` のみ共有し、チェーン自体は3回手書きされている。** 判定順序も互いに一致
しない。数え方の定義は**α計画書で定めたものを踏襲**する——「マウス座標を受け取り、要素種別を
優先順位付きで順に判定していくチェーンを、ラダー編集画面（`MainWindow.xaml.cs`）に限って数える」。
`PartEditorCanvas.cs:418`（部品エディタ、2種のみ）・状態ディスパッチ連鎖6系統・単一種別経路2件は
対象外とする（理由はα計画書に既述）。

### 指標2＝要素（`ElementInstance`）を解決する式：**5箇所**（完全一致3・区間交差2）

| # | file:line | 式 | 尺度 | 役割 |
|---|---|---|---|---|
| 1 | `MainWindowViewModel.cs:2101` | `el.Pos == pos` | **完全一致** | `SelectedElement`（プロパティパネル・削除・移動・ドラッグの起点） |
| 2 | `MainWindowViewModel.cs:2779` | `el.Pos.Row == pos.Row && el.Pos.Column <= pos.Column && pos.Column <= el.Pos.Column + el.CellWidth - 1` | **区間交差** | `HitTestElement`（右クリック・テストモード） |
| 3 | `MainWindow.xaml.cs:1684` | 同上を手書き | **区間交差** | 左Downのドラッグ開始（選択済み1件への内包判定） |
| 4 | `LadderCanvasAutomationPeer.cs:65` | `e.Pos == cell` | **完全一致** | UIA `ISelectionProvider.GetSelection()` |
| 5 | `SymbolAutomationPeer.cs:80` | `SelectedCellForAutomation == _element.Pos` | **完全一致** | UIA `ISelectionItemProvider.IsSelected` |

**#4・#5 が従来の数え方から漏れていた。** これらは「クリック位置から要素を決める」当たり判定ではなく
「選択セルから要素を決める」**選択解決**だが、**同じ完全一致式の複製であり、#1 の定義を変えれば
必ず影響を受ける**。ゆえに指標に含めるのが筋と考える。

**β完了時の目標＝チェーン 3系統→1系統／要素の解決式 5箇所→1箇所**（ただし後者は下記の案により変わる）。

---

## DoD(2)【最重要】：P-077の機能的非対称をどう扱うか

### 症状（隠密が確定、侍が裏取り済み）

`CellWidth > 1` の要素（`Motor`=3セル、`Breaker3P`/`ContactorMain3P`/`ThermalOverload3P`=各2セル、
および自作パーツ）について——

- **左クリック**：非アンカーセル（左端以外）では**選択できない**。プロパティパネルが開かない
- **右クリック**：非アンカーセルでも**メニューは開く**（`HitTestElement`＝区間交差を使うため）
- **ドラッグ**：既に区間交差（`MainWindow.xaml.cs:1684`）ゆえ、**選択さえできていれば**掴める

### 三案

#### 案A：`SelectedElement` を区間交差へ揃える

`MainWindowViewModel.cs:2101` の式を `HitTestElement` と同じ区間交差にする。

- **利**：一箇所の変更で非対称が解消する。最も直截
- **害**：**`SelectedCell` と `SelectedElement.Pos` がずれる状態が生まれる**。非アンカーセルを選択した
  まま `SelectedElementDeviceName` の setter（`:2147`、機器表 `Document.Devices` を書き換える）や
  `SelectedElementSetpoint`・`SelectedElementLabelDy` 等の setter が走る経路が生じ、Undo・機器表の
  整合を全て検証し直す必要がある（派生プロパティは**13個**）
- **さらに**：**UIA の #4・#5（完全一致）と食い違う**。画面では選択されているのに UIA は
  「選択されていない」と報告する状態になり、**忍者の実機確認スキルが誤った判定を下しうる**
  （`memory: ecad2_comparison_target_identity_pitfall` と同型の罠を我らが作ることになる）

#### 案B：現状維持（チェーンの共通化のみ行い、非対称は温存）

- **利**：挙動が一切変わらない。最も安全
- **害**：P-077 は未解決のまま。**家老が「βの共通化がこの非対称を温存したまま固めてしまう恐れ」と
  懸念したとおりの結末**になる

#### 案C【推奨】：`SelectedCell` を要素のアンカー位置へ正規化する

左Up チェーン（系統B）に Element の区間交差判定を追加し、**非アンカーセルがクリックされたら
`SelectedCell` へ要素のアンカー位置を代入する**。`SelectedElement` の定義（完全一致）は変えない。

- **利1**：**不変条件「`SelectedCell` が要素上を指すなら、それは必ずアンカー位置」が保たれる**。
  ゆえに派生プロパティ13個・削除2経路・移動2経路・ドラッグ1経路は**一切無影響**
- **利2**：**UIA の #4・#5 も無影響**——`SelectedCell` が常にアンカーを指すため、完全一致のままで
  正しく解決される。案Aで生じる「画面とUIAの食い違い」が起きない
- **利3**：**既存の対処と同型**。右クリック経路は既に「選択の正規化を各メニュー項目の Click 内へ
  遅延する」方式を採っており（`MainWindow.xaml.cs:2250/2258/2269`、T-069往復3〜4周目）、
  **「アンカーへ正規化する」という考え方はプロジェクトに既にある**
- **利4**：要素の解決式を**5箇所→1箇所**（区間交差の共通ヘルパー）へ寄せつつ、**完全一致側3箇所は
  そのまま残せる**——正規化により完全一致でも正しく解くため
- **害**：`SelectedCell` がユーザーのクリック位置と厳密には一致しなくなる（非アンカーをクリックすると
  左端へ寄る）。**セル選択の見た目（カーソル枠）が左端へ移動する**ため、使用感の変化は生じる

### 侍の推奨＝**案C**

理由は上記の利2が決定的にござる。**案Aは、我らが忍者の検証手段そのものを壊す**——画面とUIAが
食い違えば、実機確認の判定が信用できなくなる。案Cは影響範囲を左Upチェーンの1分岐に閉じ込めつつ、
症状を解消する。

**ただし使用感の変化（カーソル枠が左端へ寄る）は殿の裁可を要する**と心得る。α計画書で
「βの計画起草時に改めて殿へ諮るべき」と予告した事項がこれにござる。

**殿へ諮る形**：「Motor等の幅広部品を左端以外でクリックしたとき、(1)今のまま選択できないでよいか、
(2)選択できるようにしてカーソル枠は左端へ寄せるか、(3)選択できるようにしてカーソル枠はクリック位置に
留めるか（＝案A、ただし内部整合の検証が大きい）」。**侍推奨は(2)＝案C**。

---

## DoD(3)：留保5（左Down系統の `return` 欠落）の扱い

### 事実（侍が一次ソースで確認）

`MainWindow.xaml.cs:1684-1696` の Element 分岐だけが、**最後の分岐でないのに `return` を持たない**。
他6分岐（Connector/WireBreak/FreeLine/ConnectionDot/ImageHandle/Image）はすべて成功時に `return` する。

### α計画書での我が留保と、その後

α計画書に「`SelectedElement` は `SelectedCell` 由来、`SelectedFrame` 選択時は `SelectedCell` が
null になるため**両立せず実害は無い見込み**。**推測ゆえ断じない**」と記した。

**この留保が正しかった。** 隠密のβ前段調査が、両立する具体経路を特定した——
**`OpenFrameLabelEditor`（`MainWindow.xaml.cs:3903-3916`）のみ、`SelectedFrame` の設定前に
`SelectedCell = null` を呼んでいない**（侍が一次ソースで確認済み。他3箇所＝`MainWindow.xaml.cs:2010`・
`:2150`・`MainWindowViewModel.cs:1623` はいずれも直前に `SelectedCell = null` を伴う）。

同メソッドのコメントは「編集ボックス表示中は `IsMainContentEnabled` 経由でキャンバス操作が
ブロックされるため、編集中に `SelectedFrame` が他へ変化する心配はない」と述べるが、
**編集終了後については何も述べていない**。

**もし断じていれば、この経路を塞がぬままβを固めていた。**

### 扱い＝**β-1 に含めて塞ぐ**

`return` を補うだけでは片手落ちにござる。**`OpenFrameLabelEditor` 側の非対称も併せて正す**
（`SelectedFrame` 設定前に `SelectedCell = null` を置く、または編集終了処理でクリアする）。
どちらが筋かは着手時に一次ソース（`RenameSelectedFrame` 等の確定処理）を読んで決める。

**実機での実害の再現は忍者の領分**であり、実機は殿がT-130の観測に使われている。**再現を待たずに
構造として塞ぐ**——`return` の欠落はパターンからの明確な逸脱であり、実害の有無に関わらず正すべき。

---

## DoD(4)：γ側へ及ぶ範囲の線引き

### βに含める（構造が閉じており、γの責務分割を待たずに直せるもの）

1. 3系統のチェーンを共通化する（末端 `HitTestXxx` は既に共有。**チェーンの順序と分岐を1箇所へ**）
2. 左Down の `return` 欠落と `OpenFrameLabelEditor` の非対称（上記）
3. 要素の当たり判定を区間交差の共通ヘルパーへ寄せる（#2・#3 の重複解消）
4. 案C を採るなら、左Up チェーンでの `SelectedCell` 正規化

### γに残す（`MainWindowViewModel` の責務分割と不可分なもの）

1. **`SelectedElement` 派生プロパティ13個**（`SelectedElementDeviceName`/`Comment`/`NotchPosition`/
   `LampColor`/`Setpoint`/`LabelDy` 等）の整理——プロパティパネルの表示ロジックそのものであり、
   責務分割の対象。**βでは触れない**
2. **変更通知ブロック4箇所の重複**（`MainWindowViewModel.cs:467-480`・`2434-2446`・`2569-2581`・
   `3222-3234`、13〜14行の同一列挙が4回複製）——`memory: ecad2_setproperty_early_return_trap` の
   親戚筋。γの責務分割で自然に解消される見込み
3. **UIA の完全一致2箇所**（#4・#5）——案Cを採れば無影響ゆえ、βでは触れない。案Aを採るなら
   βに含めざるを得ない（これも案Cを推す理由の一つ）

### 線引きの原則

**「`SelectedCell` → `SelectedElement` の解決経路」まではβ、「`SelectedElement` から先の表示・
編集・通知」はγ**——この一線で切る。案Cはまさにこの線の内側で完結する。

---

## DoD(1)：増分βの分割案

**3段**に分ける。段ごとに検証パイプライン（隠密の静的レビュー→忍者の実機確認）を回す。

### β-1：`return` 欠落と `OpenFrameLabelEditor` の非対称を塞ぐ【バグ修正・挙動改善のみ】

- **対象**：`MainWindow.xaml.cs:1684-1696`（`return` 追加）、`:3903-3916`（`SelectedCell` の扱い）
- **検証観点**：
  - RED先行証明——`SelectedElement != null && SelectedFrame != null` を作り、`return` が無いと
    両方の `BeginDrag*` が走ることを実測する。**ViewModel 側で状態を組めるためテスト可能**
    （`_draggingElement`/`_draggingFrame` の同時非nullを検証）
  - 回帰：通常のドラッグ（要素のみ・枠のみ）が従来どおり働くこと
  - **`OpenFrameLabelEditor` の変更は枠ラベル編集の既存挙動を壊さないこと**（編集→確定→再編集）
- **規模見込み**：実装5行程度、テスト6件程度
- **この段だけは殿裁可を待たずに進められる**（挙動改善のみ、使用感は変わらない）と考えるが、
  判断は家老に委ねる

### β-2：3系統のチェーンを共通化する【挙動不変のリファクタリング】

- **対象**：系統A/B/Cの分岐チェーンを、種別と判定順を持つ単一の解決メソッドへ寄せる
- **設計の要**：**3系統は判定順序が異なる**（下表）。単純に1つへ揃えると挙動が変わるため、
  **「順序表を引数で渡す」か「用途ごとに順序を定数として持つ」形**にする必要がある

| 種別 | 系統A | 系統B | 系統C |
|---|---|---|---|
| Element | 7 | 暗黙 | **1** |
| Connector | 1 | 1 | 2 |
| Frame | 8 | **2** | 3 |
| WireBreak | 2 | 3 | **無** |
| ConnectionDot | 4 | 4 | **無** |
| FreeLine | 3 | **5** | **無** |
| Image | 5,6 | 6 | 4 |

  - **系統AとBでFreeLine/ConnectionDotの順序が逆**。系統Bの順序には
    `MainWindow.xaml.cs:2022-2024` に「T-116(P-107対処)、逆順だと交点上の接続点が選択不能」と
    理由が明記されている。**この順序は壊してはならぬ**
  - 系統Cに WireBreak/ConnectionDot/FreeLine が無いのは非対称だが、**βで揃えるかは別途判断**
    （右クリックメニューの項目を新設することになり、範囲が広がる）。**本増分では順序のみ共通化し、
    「どの種別を対象にするか」は各系統の現状を保つ**ことを提案する
- **検証観点**：
  - **挙動不変であることの証明が主眼**。共通化の前後で、同一座標に対する判定結果が3系統とも
    一致することをテストで固定する
  - 系統Bの FreeLine/ConnectionDot 順序（T-116の対処）が保たれること——**この1点はRED証明を要する**
    （順序を入れ替えるとREDになるテストを置く）
  - 決着ルールの混在（先頭一致／最短距離／最前面／面積最小の4種）は**共通化しない**——
    各 `HitTestXxx` の内部に閉じており、それぞれ理由がある（`LadderCanvas.cs:464-466`・`486-488`）
- **規模見込み**：中。実装60〜100行程度の再編、テスト20件程度

### β-3：要素の当たり判定を一本化する【案Cの場合＝挙動変化、殿裁可要】

- **対象**：`MainWindowViewModel.cs:2779`（`HitTestElement`）を正とし、`MainWindow.xaml.cs:1684` の
  手書き判定をこれへ寄せる。加えて左Upチェーンに Element 分岐を追加し、
  **ヒットしたら `SelectedCell` を要素のアンカー位置へ正規化**する
- **検証観点**：
  - RED先行証明——`CellWidth=3` の要素を置き、非アンカーセル（+1、+2）をクリックしたときに
    `SelectedElement` が非nullになること。**修正前はnullであることを実測**
  - **アンカー位置への正規化が効いていること**（`SelectedCell` が要素のアンカーと一致）
  - **UIA 経路（#4・#5）が従来どおり選択を報告すること**——正規化により完全一致でも解けることの確認
  - 回帰：`CellWidth=1` の要素、要素の無いセル、境界（要素の右端+1セル）
  - **入力の対称性・退化性**（`samurai.md`）——`CellWidth=1` は「アンカー＝全域」ゆえ正規化の
    有無が結果に現れない**退化ケース**。**必ず `CellWidth>=2` を主検体とする**
- **規模見込み**：小〜中。実装20行程度、テスト12件程度

### 段の順序と理由

**β-1 → β-2 → β-3**。β-1は挙動改善のみで独立、β-2は挙動不変、β-3のみ使用感が変わる。
**判断を要するものを最後に置く**ことで、殿の裁可を待つ間もβ-1・β-2を進められる。

---

## 副次所見（本増分の範囲外。家老の判断を仰ぐ）

1. **`HasNoPropertySelection` の変更通知が3箇所で欠落している疑い**——`SelectedElement` の変更通知
   ブロック4箇所のうち、`SelectedCell` setter（`:467-480`）だけが `HasNoPropertySelection` を
   発火し、`DeleteSelectedElement`（`:2434-2446`）・`NotifySelectedElementChanged`（`:2569-2581`）・
   `ReplaceDocument`（`:3222-3234`）の3箇所は発火しない。**要素削除後にプロパティパネルの
   「選択なし」表示が更新されない可能性**がある。
   **【未確認】侍は行番号と欠落の事実のみ確認しており、実機での表示崩れは未検証。**
   `memory: feedback_checklist_layer_mismatch_blindspot`（T-107で同型が起きた）と同じ匂いがする。
   **γで扱うか、別建てで起票するかを家老に諮りたい**
2. **右クリックチェーンに WireBreak/ConnectionDot/FreeLine が無い非対称**——過去に Image（T-064）・
   Frame（T-067(5)）が同じ形で後から追加された記録がある（`MainWindow.xaml.cs:2153-2156` のコメント）。
   **同型の横展開漏れが3種残っている**と読めるが、**メニュー項目の新設を伴うため使用感の設計判断**。
   βには含めず、**別途起票が筋**と考える

---

## 未確認・留保（断じておらぬ事柄）

1. **実機での再現は一切未確認**——本起草は静的読解のみ。留保5の実害（要素と枠の二重掴み）も、
   P-077の症状も、実機では確かめていない。**実機は殿がT-130の観測に使われている**ため後回しとした
2. **`OpenFrameLabelEditor` の編集終了処理**（`RenameSelectedFrame` 等）が `SelectedFrame` を
   クリアするか否かは未読。**β-1着手時に一次ソースを読んで対処を決める**
3. **`tests/` 配下に既存のヒットテスト優先順位テストがどこまであるか**は未調査
   （`RungCommentHitTestTests` の存在は確認済み）。**β-2着手前に確認する**——共通化で壊れる
   テストがあれば、それは「現仕様を固定していた」証拠として扱う
4. **副次所見1の実害**（プロパティパネルの表示崩れ）は未検証

---

## 出典

- `src/Ecad2.App/MainWindow.xaml.cs`（系統A `:1501` / 系統B `:1850` / 系統C `:2072` /
  `return`欠落 `:1684-1696` / `OpenFrameLabelEditor` `:3903-3916`）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`SelectedElement` `:2101` /
  `HitTestElement` `:2779` / `SelectedCell` setter `:415-483` / 変更通知4ブロック）
- `src/Ecad2.App/Views/LadderCanvas.cs`（`HitTestXxx` 10個の定義と決着ルール）
- `src/Ecad2.App/Views/LadderCanvasAutomationPeer.cs:65`・`Views/SymbolAutomationPeer.cs:80`
  （**従来の数え方から漏れていた完全一致2箇所**）
- `docs/ecad2-t125-beta-p077-investigation-onmitsu.md`（隠密のβ前段調査）
- `docs/ecad2-t125-increment-alpha-plan-samurai.md`（α計画書、数え方の定義と留保5の初出）
