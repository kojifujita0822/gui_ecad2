# T-140 テスト設計（隠密起草）— (a)ツールバー文字色 ／ (b)ポップアップ階層

起草日: 2026-08-02　起草者: 隠密（key=1785632944373）　依頼元: 家老（`karo.md`「テスト設計と実装の分離【MUST】」）
原因調査＝`docs/ecad2-t140-dark-theme-root-cause-onmitsu.md`（本書は同書を前提とする）

**対象＝系統1の4件**（**殿ご裁定2026-08-02により(a)(b)から広がった**）。
(c)P-150・P-158は色の選定が要るゆえ範囲外。

| # | 対象 | 対処の関係 |
|---|---|---|
| **(a)** | パーツエディタのツールバー文字色 | 独立（`ToolBar.*StyleKey`） |
| **(b)** | ポップアップ第2・第3階層 | `SubmenuHeaderTemplateKey`新設 |
| **(i)** | 右クリックメニュー全体がライト固定 | **別のリソース**（`ContextMenu`の暗黙的スタイル新設）。同じ`App.xaml`のメニュー系 |
| **(ii)** | 「表示→パネルを自動的に隠す」 | **(b)と同一の対処で直る**——**独立した作業ではない** |

**【(i)と(ii)は粒度が違う】** **(ii)は`SubmenuHeader`ロールゆえ(b)を直せば自ずと直り、
別途の実装を要さぬ**（**要るのは検証の観点のみ**）。**(i)は別のリソースを足す実作業が要る。**
**「同系統ゆえまとめる」と括る際、この違いを潰さぬこと。**

---

## 0. 【最初に読まれたし】このテストが守るもの・守らぬもの

**家老のご下問「XAMLリソース解決の検証がテストで成り立つか」への答え＝
**半分は成り立つ。されど肝心の半分は成り立たぬ。**

| 層 | 何を測るか | 成否 | 担い手 |
|---|---|---|---|
| **層1** | **リソース定義が在り、色参照が目的キーを指すこと** | **成る**（前例あり・§1） | 侍（自動テスト） |
| **層2** | 実際に構築したVisualの解決後Brush | **成らぬ**（§0.2） | ——— |
| **層3** | **実機の画素** | 成る | **忍者** |

### 0.1 **層1は「消失・改悪」しか検出できぬ**

**本件の(b)はPR-20型——「コードは正しいのに描画へ届かぬ」型にござる。**
**層1のテストは「定義が在ること」しか測れぬゆえ、まさにPR-20型の再発を検出できぬ。**

- **検出できる**＝将来の改修で該当キーの定義が消える／`DynamicResource`が固定色へ戻る／
  新たなRoleが未定義のまま残る
- **検出できぬ**＝定義は正しいのに描画へ届かぬ（優先順位に負ける・`StaticResource`で遅延解決されぬ等）

**すなわち今回のバグそのものは、層1では二度と捕らえられぬ。**
**捕らえられるのは「今回直した箇所が、また元へ戻ること」だけにござる。**
**この区別を曖昧にしたまま「テストで守られた」と言うてはならぬ。**

### 0.2 **層2（STAでVisualを構築する）は推さぬ**

- `tests/Ecad2.App.Tests/Ecad2.App.Tests.csproj:5`に`<UseWPF>true</UseWPF>`は在るが、
  **STAの設定は無く、既存テストはVisualを一つも構築しておらぬ**（`UsageWindowTests`等はいずれも静的データ・変換ロジックのみ）
- STA化には外部依存（`Xunit.StaFact`等）か自前のSTAスレッド包み込みが要る。
  **前者はCLAUDE.md「不要な外部依存を追加しない」に抵触し、後者は`memory:
  feedback_hard_to_test_is_design_smell`が「特殊な仕掛けは最後の手段」と戒める**
- **かつ、層2を通しても層3（実機）は省けぬ**——`ToolBar`の差し替えは
  `PrepareContainerForItemOverride`＝レイアウトパスで走るゆえ、Measure/Arrangeまで模さねば
  実機と同じにならず、**模したところで「実機で見えるか」は別問題**

**ゆえに層1＋層3の二段構えを推す。** **層2を欠く分は、忍者の観点を厚くして補う（§3）。**

---

## 1. 層1: アーキテクチャテスト（侍が書く）

### 1.1 前例と手法

**`tests/Ecad2.App.Tests/DispatcherDependencyArchitectureTests.cs`と同型**にござる——
`[CallerFilePath]`からリポジトリルートを辿り、ソースをテキストとして検査する。
**外部依存なし・STA不要・既存の流儀に沿う。**

```csharp
private static string GetAppDirectory([CallerFilePath] string thisFilePath = "")
{
    var testProjectDir = Path.GetDirectoryName(thisFilePath)!;      // tests/Ecad2.App.Tests
    var repoRoot = Directory.GetParent(Directory.GetParent(testProjectDir)!.FullName)!.FullName;
    return Path.Combine(repoRoot, "src", "Ecad2.App");
}
```

### 1.2 同値分割 — **`Role`の4分割が本件の分割そのもの**

**入力の分割軸＝`MenuItem.Role`**（一次ソース`MenuItem.xaml:2490-2512`）。
**T-140(b)は「有効域を2つしか実装せず、残り2つが無効域として取り残されていた」不具合**にござる。

| # | Role | 適用条件 | 修正前 | 修正後の期待 |
|---|---|---|---|---|
| 1 | `TopLevelHeader` | `Menu`直下で子あり | 定義済 | 定義済 |
| 2 | `TopLevelItem` | `Menu`直下で子なし | **未定義** | **定義（§5.2の推奨に従うなら）** |
| 3 | `SubmenuHeader` | サブメニュー内で子あり | **未定義** | **定義（必須）** |
| 4 | `SubmenuItem` | サブメニュー内で子なし | 定義済 | 定義済 |

**`[Theory]`+`[InlineData]`で4件を回す**——`[Fact]`の複製にせぬこと
（`onmitsu.md`「ケース追加の心理的コストを上げ、境界値の間引きを誘発する」）。

```
[Theory]
[InlineData("TopLevelHeaderTemplateKey")]
[InlineData("TopLevelItemTemplateKey")]
[InlineData("SubmenuHeaderTemplateKey")]
[InlineData("SubmenuItemTemplateKey")]
public void App_xaml_MenuItemの全Roleに派生テンプレートが定義されている(string resourceId)
```

**このテストの値打ち＝「4つ揃っておらねば落ちる」という形にすることで、
`TopLevelItemTemplateKey`を省く判断を将来また採れば、その場で落ちる。**
**T-083当時の「現状未使用ゆえ省略」が本件を生んだゆえ、そこへ網を張る。**

### 1.3 ToolBar側（(a)）

**`ToolBar`が差し替える`*StyleKey`のうち、ecad2で使われうる型を分割する**
（一次ソース`ToolBar.cs:462-477`の分岐が、そのまま同値クラスの列挙にござる）。

| # | StyleKey | `PartEditorDialog`での使用 | 要否 |
|---|---|---|---|
| 1 | `ButtonStyleKey` | **4件**（元に戻す／やり直し／選択を削除／ズームを戻す） | **必須** |
| 2 | `RadioButtonStyleKey` | **9件**（選択〜接続点） | **必須** |
| 3 | `SeparatorStyleKey` | **2件**（`PartEditorDialog.xaml:83`・`87`） | **必須**（`ToolBarSeparatorFill`=`#FFB6BDC5`のライト固定） |
| 4 | `ToggleButtonStyleKey` | 0件 | **推奨**（`RadioButtonStyleKey`が`BasedOn`で参照する土台ゆえ） |
| 5 | `CheckBoxStyleKey`/`ComboBoxStyleKey`/`TextBoxStyleKey`/`MenuStyleKey` | 0件 | 対象外（**「無い」＝現時点の裁定。将来ToolBarへ足せば同じ穴**） |

**再現手段**＝`PartEditorDialog.xaml:69-88`を数える。**RadioButton 9・Button 4・Separator 2。**

### 1.4 「固定色が残っておらぬこと」の検査 — **禁止パターン方式**

`DispatcherDependencyArchitectureTests`の`ForbiddenPatterns`と同型にて、
**ecad2が定義した`ToolBar.*StyleKey`／Role別テンプレートの範囲内に、
テーマ非追従の参照が残っておらぬか**を検査する。

**禁止パターンの候補**
- `SystemColors.ControlTextBrushKey` ／ `SystemColors.GrayTextBrushKey`（**(a)の原因そのもの**）
- `{StaticResource Menu.Static.` ／ `{StaticResource Menu.Disabled.`（**(b)の原因そのもの**）

**【重要な限界・自ら区切る】この検査は`App.xaml`全体に掛けてはならぬ。**
**`App.xaml:274`・`309`・`340`・`374`・`495`等、既存のButton/ToggleButton/ComboBoxスタイルは
現に`ControlTextBrushKey`・`GrayTextBrushKey`を用いており、これらはT-140の対象外にござる。**
**範囲を「T-140で新設・改修したStyle/Templateの内側」に限らねば、既存箇所を巻き込んで即座に落ちる。**
**範囲を機械的に切るのが難しければ、本項は見送り§1.2・§1.3のみとしてよい**——
**無理に書けば、次の改修で邪魔になって消される網になる。**

### 1.5 RED証明

**層1のテストは修正前に必ずREDになる**（`SubmenuHeaderTemplateKey`等が存在せぬゆえ）。
**ゆえにRED証明は容易に立つ。侍は修正前の状態で一度回し、落ちることを確かめられたい。**

**【併せて問うこと・`samurai.md`「感度を失わせる編集は他に無いか」】**
**テキスト検査ゆえ、`App.xaml`のコメント中に`SubmenuHeaderTemplateKey`の語が在るだけでも通ってしまう。**
**実際、現在の`App.xaml:694-695`のコメントには当の語が書かれておる**（「…`SubmenuHeaderTemplateKey`は
現状未使用につき省略」）——**すなわち素朴な`Contains`では修正前でもGREENになりかねぬ。**
**`ComponentResourceKey`を含む定義行の形で照合するか、コメント行を除いて数えること**
（`onmitsu.md`「`grep`の生の一致数を、そのまま実呼び出し数として報じない」と同根の穴にござる）。

---

## 2. 状態遷移 — **テーマ切替の往復**

**本件は「一度きりの表示」ではなく「Light↔Darkの動的差し替え」にござる**
（`MainWindow.xaml.cs`の`ApplyUiChromeTheme`が`MergedDictionaries`を差し替える方式）。

| 遷移 | 期待 | なぜ要るか |
|---|---|---|
| 起動（Light）→ Dark | 追従して暗色化 | 主症状 |
| Dark → Light | **元へ戻る** | **`DynamicResource`なら戻る。`StaticResource`のままなら「Darkで一度暗くなったが戻らぬ」形で残りうる** |
| Light → Dark → Light → Dark | **1周目と2周目が同値** | 差し替えの取りこぼし検出 |
| **パーツエディタを開いたままテーマ切替** | 追従する | **ダイアログは別`Window`にて、`Application.Resources`の差し替えが届くかは別問題** |

**最後の一行は忍者へ強く申し送りたい**——`PartEditorDialog`は
`new Views.PartEditorDialog(entry.Definition, _viewModel.IsDarkMode)`と
**構築時にダークフラグを受け取っておる**（`MainWindow.xaml.cs:474`・`521`）。
**すなわち「開いた後の切替」は想定されておらぬ作りやもしれぬ。**
**某は確かめておらぬゆえ、これは仮説にござる。**

---

## 3. 層3: 忍者の実機検証観点（**層2を欠く分、ここを厚くする**）

### 3.1 **【最重要】測る前に「今ダークである」ことを独立に確かめる**

**本件には測定の罠がござる**——
**ライトモードの正しい文字色は`#000000`。ダークモードの不具合時の文字色も`#000000`。**
**両者は画素として区別がつき申さぬ。**

**ゆえに「ダークで測ったら`#000000`だった」だけでは、
「不具合が残っておる」のか「そもそもライトのまま測っておる」のか判ぜられぬ**
（`memory: ecad2_comparison_target_identity_pitfall`＝比較測定は対象の同定を先に固定）。

**対照の置き方＝同じウィンドウ内の正常箇所を同時に測る。**
**忍者は前回まさにこれを実践しておられた**——「名前:」「幅(セル…)」等のラベルが`#F0F0F0`／12.05:1。
**この対照を必ず同じ撮影の中に含めること。**

### 3.2 観点表

| # | 対象 | 測る値 | 期待 |
|---|---|---|---|
| 1 | ツールバーのRadioButton 9件 | 文字色 | ライト地色でない明色（`ToolBarForegroundBrush`相当） |
| 2 | ツールバーのButton 4件（有効時） | 文字色 | 同上 |
| 3 | **同（無効時＝元に戻す／やり直し／選択を削除）** | 文字色 | **`#6D6D6D`から改まっておること** |
| 4 | Separator 2件 | 線の色 | ライト固定`#B6BDC5`でないこと |
| 5 | 「選択」（チェック中） | 背景 | 従前の`#224264`は**意匠として妥当**——**変わらぬのが正**やもしれぬ。**殿の裁可を要す** |
| 6 | ポップアップ第1階層 | 背景・文字 | **従前どおり**（回帰していないこと） |
| 7 | **ポップアップ第2階層** | 背景・文字 | 暗色地・明色文字 |
| 8 | **ポップアップ第3階層** | **色数** | **背景と文字で2色以上あること** |
| 9 | 第2階層の文字色 | 実測値 | **§4の未決着に決着をつける** |
| 10 | **右クリックメニュー(i)** | 背景 | `#F5F5F5`でないこと。**空セル上・要素上・枠上の3経路とも見る**（`MainWindow.xaml.cs:2091`・`2199`の2つの`ContextMenu`が対象） |
| 11 | **表示→パネルを自動的に隠す(ii)** | 背景 | 暗色。**(b)の対処で自ずと直るはず——直っておらねば(b)の対処が`SubmenuHeader`へ届いておらぬ証** |
| 12 | **右クリック「線種」サブメニュー** | 背景 | **(ii)と同型**（`MainWindow.xaml.cs:2266`）。**(i)と(b)の両方が効いて初めて正しくなる箇所ゆえ、両対処の合流点として値打ちが高い** |

**【観点11の使い道】** **(ii)は「直すべき箇所」であると同時に「(b)の対処が効いた範囲を測る物差し」にござる**——
**殿がご指摘なされた自作パーツ経路とは別のメニューにて、同じRoleを通る。**
**ここが直っておれば、対処がRole単位で効いておる（＝経路単位の対症療法でない）ことの裏づけになる。**

### 3.3 **第3階層は「比」でなく「色数」で測られたし**

**忍者が前回自ら掴まれた手法にござる**——
**「`popup-0`を`Read`で開くと真っ白なのに、比は2.29:1と出た」という食い違いが色数を数えさせた。**
**比だけでは「薄い」と「消えておる」が分かれ申さぬ。**
**修正後も同じ手法で測れば、「見えるようになった」を数で言える。**

### 3.4 **対照実験に素朴なベースラインを混ぜる**

**修正後に「第3階層が見えるようになった」と報ずる前に、
「自作パーツが1件も無い状態」（第2階層が`(なし)`のみ＝第3階層が生じぬ）でも一度測られたい**
（`memory: feedback_control_experiment_needs_naive_baseline`）。
**「見えた」が本当に修正の効果か、たまたま別の要因かを切り分ける。**

---

## 4. **決着をつけるべき未決着事項**

**原因調査§2.5で断じなんだ一件——第2階層の文字色`#212121`の出所。**

- **一次ソースからは説明が付かず、二説が立つ**＝(i)忍者が右矢印（`Fill="{StaticResource
  Menu.Static.Foreground}"`=`#FF212121`、`MenuItem.xaml:2424`）の画素を拾った (ii)第2階層で継承が切れておる
- **修正後の実機検証が、そのまま決着の場になる**——
  **修正後も第2階層に`#212121`が残るなら(i)の線が濃い**（矢印色は別途DynamicResource化を要する）／
  **消えるなら(ii)であった**
- **忍者へのお願い＝第2階層を測る際、「文字の画素」と「右向き矢印の画素」を分けて採られたい**

---

## 5. 設計の要点まとめ（侍への申し送り）

1. **層1は`[Theory]`で4Role・4StyleKeyを回す。`[Fact]`の複製にせぬこと**
2. **修正前に一度回してREDを確かめる。ただし§1.5の「コメント中の語で誤GREEN」に注意**
3. **§1.4の禁止パターン検査は、範囲を機械的に切れぬなら見送ってよい**——無理に書かぬ
4. **本設計に無いテストを足すのは自由。在るものを省くのは不可**（`onmitsu.md`）
5. **PR-21の戒めが(a)に直に効く**——`ToolBar.*StyleKey`を上書きするなら、
   **Style本体のSetter一式とControlTemplate.Triggers 4種を漏れなく移植する**

## 6. **PR-27（テスト入力の対称性・退化性）の自己点検**

**本設計の検証値に、対称・退化の罠が無いかを己で検めた。**

- **`ToolBarForegroundBrush`はLight=`#000000`／Dark=`#FFF0F0F0`で異なる**ゆえ、
  **Light/Darkの弁別に使える**（`Theme.Dark.xaml:26`・`Theme.Light.xaml`同キー）
- **【罠】`TestModeActiveBrush`のような「テーマ非依存の意味色」を検証値に選ぶと、
  Light/Darkどちらでも同値ゆえ、追従しておらずとも通ってしまう**（`Theme.Dark.xaml:19-20`に
  「テーマ非依存のためLightと同値」と明記）。**§3の観点表でこの種のキーは一つも用いておらぬ**
- **層1のテキスト検査は入力が「ファイルの中身」ただ一つゆえ、対称性の罠は生じ得ぬ**
