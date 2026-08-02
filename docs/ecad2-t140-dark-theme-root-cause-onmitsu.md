# T-140 ダークテーマに追随せぬ三箇所の原因調査（隠密）

調査日: 2026-08-02　調査者: 隠密（key=1785632944373）　依頼元: 家老（T-140、殿ご指摘2026-08-01による起票）

**本書は差分で書く。** WPF既定テーマの一般論・`SubMenuBorder`のStaticResource制約は
`docs/ecad2-t083-zoubun7-menu-dark-redesign-survey-onmitsu.md`に既にある。本書はその**続き**にござる。

---

## 結論（先出し）

### DoD1: 三箇所は同根か → **否。二系統に分かれる**

| | 箇所 | 原因の層 | 系統 |
|---|---|---|---|
| **(a)** | パーツエディタのツールバー文字色 | **WPF既定テーマ`ToolBar.*StyleKey`のStyle本体Setter** | **系統1** |
| **(b)** | ポップアップ第2・第3階層 | **WPF既定テーマ`SubmenuHeaderTemplateKey`内のStaticResource** | **系統1** |
| **(c)** | P-150（基準枠の灰180） | **ecad2自身のC#コードの色直書き** | **系統2** |

- **系統1＝「WPF既定(Aero2)テーマが持つ固定色が、ecad2の手当ての及ばぬ隙間に残っていた」**。
  (a)(b)は**別々のパターン再発**（下記）だが、**同じ増分でまとめて直せる**
- **系統2＝ecad2が自ら書いた固定値**。外部テーマとは無関係にて、**(a)(b)を直しても一切変わらぬ**

**家老の見立て「同根であれば一度に直せる」への答え＝一度には直せぬ。**
**ただし(a)(b)は一度に直せ、(c)は別立てを要する。**

### DoD3: 無効状態の低コントラストは仕様か → **箇所により分かれる。一律には判ぜられぬ**

- **パーツエディタの無効ボタン`#6D6D6D`＝仕様ではない**（(a)と同根。`SystemColors.GrayTextBrushKey`＝OS固定色）
- **メインウィンドウの無効ボタン＝殿裁定済みの意匠**（T-047「無効時に半透明化」殿選定、`MainWindow.xaml:50-51`）
- **プレースホルダ「要素を選択してください」`#808080`＝無効状態ではない。`Foreground="Gray"`の直書き**
  （`MainWindow.xaml:1515`）——**忍者の「いずれも無効状態ゆえ仕様の内やもしれぬ」は、この一件については前提が違う**
- **WCAG上は、無効UIコンポーネントのテキストはSC 1.4.3の対象外**。ゆえに**規格違反ではない**

### DoD2: P-150 → **記載の行番号に誤りあり。灰180は`668`行でなく`674`行**

### DoD4: 修正方針 → **§5。(a)(b)は案を推奨まで絞れた。(c)は色の選定がUI/UX分岐ゆえ殿へ諮る要あり**

---

## 1. (a) パーツエディタのツールバー文字色が黒い

### 1.1 機序（一次ソースで確定）

`ToolBar`は、**子要素の`Style`プロパティ自体を`ToolBar.*StyleKey`へ差し替える**。
一次ソース＝`ToolBar.cs:451-489`（`PrepareContainerForItemOverride`）:

```csharp
if (feType == typeof(Button))        resourceKey = ButtonStyleKey;
else if (feType == typeof(RadioButton)) resourceKey = RadioButtonStyleKey;
...
if (resourceKey != null) {
    BaseValueSourceInternal vs = fe.GetValueSource(StyleProperty, null, out hasModifiers);
    if (vs <= BaseValueSourceInternal.ImplicitReference)   // ←ここが分かれ目
        fe.SetResourceReference(StyleProperty, resourceKey);
    fe.DefaultStyleKey = resourceKey;
}
```

**`vs <= ImplicitReference`——すなわち「`Style`が未指定」または「暗黙的スタイル由来」の場合に差し替わる。**
**`App.xaml`の暗黙的Buttonスタイルが在っても、ToolBar内では握り潰される**（`ImplicitReference`ゆえ）。

差し替わった先のStyle本体（`ToolBar.xaml:786-787`・`857-858`、全テーマ共通セクション）:

```xml
<Style x:Key="{x:Static ToolBar.ButtonStyleKey}" TargetType="{x:Type Button}">
    <Setter Property="Foreground" Value="{DynamicResource {x:Static SystemColors.ControlTextBrushKey}}"/>
    <Setter Property="Background" Value="Transparent"/>
    ...
    <Trigger Property="IsEnabled" Value="false">
        <Setter Property="Foreground" Value="{DynamicResource {x:Static SystemColors.GrayTextBrushKey}}"/>
```

`ToolBar.RadioButtonStyleKey`は`ToolBar.ToggleButtonStyleKey`を`BasedOn`（`ToolBar.xaml:953-954`）ゆえ
**同じ`Foreground` Setterを継承する。**

**Style本体のSetterは、親（`ToolBar`要素）からのプロパティ値継承に、依存関係プロパティ優先順位で勝つ。**
ゆえに`PartEditorDialog.xaml:68`の`Foreground="{DynamicResource ToolBarForegroundBrush}"`は**子ボタンへ届かぬ。**

### 1.2 忍者の実測4値が、すべて一次ソースから再現できた

| 忍者の実測 | 一次ソースからの導出 | 一致 |
|---|---|---|
| 通常文字`#000000` | `SystemColors.ControlTextBrushKey`（OSライト固定） | 合 |
| 無効文字`#6D6D6D` | `SystemColors.GrayTextBrushKey` | 合 |
| 背景`#2D2D30`（ボタン地が透ける） | Style本体`Background="Transparent"` | 合 |
| 「選択」の背景`#224264` | `ToolBarButtonChecked`=`#400080FF`を`#2D2D30`へ合成 | **画素まで合致** |

**「選択」の合成計算**（α=0x40=64/255=0.251）:
R=0.251×0+0.749×45=33.7→`0x22` ／ G=0.251×128+0.749×45=65.8→`0x42` ／ B=0.251×255+0.749×48=100→`0x64`
→ **`#224264`。忍者の実測と完全一致。**

**再現手段**＝`ToolBar.xaml:275`（`<SolidColorBrush x:Key="ToolBarButtonChecked" Color="#400080FF"/>`、
Aero2セクション）と`Theme.Dark.xaml:25`（`ToolBarBackgroundBrush`=`#FF2D2D30`）を上式へ入れる。

### 1.3 なぜメインウィンドウのツールバーは無事なのか（対照）

**メインウィンドウの全ツールバーボタンは`Style`を明示指定しておる**——
`Style="{StaticResource ToolBarButtonStyle}"`（`MainWindow.xaml:1067`他）／
`PlacementToolBarButtonStyle`（同`1176`他）／`TestModeToolBarButtonStyle`（同`1135`・`1334`）。

**明示指定＝`vs`が`Local`となり、`vs <= ImplicitReference`が偽ゆえ差し替えが起きぬ。**
一方**`PartEditorDialog.xaml:69-88`の13要素は`Style`を一切指定しておらぬ**（RadioButton 9・Button 4）。

**これが分かれ目にござる。** 忍者が対照として挙げた「同じウィンドウ内のラベル類は`#F0F0F0`で正常」も
同根で説明がつく——`TextBlock`は`ToolBar`の子ではなくWindowの`Foreground`を継承しておるゆえ。

### 1.4 パターン再発判定【MUST】

**PR-24の3例目（変種）。**
- **PR-24の核**＝「Aero2テーマスタイル**本体のSetter**が`Foreground`を固定色で持ち、親からの継承に勝つ」
- **1例目**＝`AddSheetDialog`の`RadioButton`（2026-07-21、殿指摘）／**2例目**＝`StatusBar`（T-108）
- **本件との違い**＝1・2例目は「ecad2側に暗黙的スタイルが**不在**」ゆえテーマスタイルへフォールバックした形。
  **本件は暗黙的スタイルが在っても`ToolBar`が能動的に差し替える**——**成立経路は違うが、
  「Style本体Setterが継承を阻害する」という結果は同一。**
- **ゆえに「変種」として記帳を提案する**（台帳への追記は家老の采配を仰ぐ）

---

## 2. (b) ポップアップ第2・第3階層

### 2.1 機序（一次ソースで確定）

WPFの`MenuItem`は`Role`に応じて4種のテンプレートを使い分ける（`MenuItem.xaml:2490-2512`、Aero2）:

| Role | 適用条件 | ecad2の上書き |
|---|---|---|
| `TopLevelHeader` | `Menu`直下で子を持つ | **有**（`App.xaml:718`） |
| `TopLevelItem` | `Menu`直下で子を持たぬ | **無** |
| `SubmenuHeader` | サブメニュー内で**子を持つ** | **無** ← **本件の原因** |
| `SubmenuItem` | サブメニュー内で子を持たぬ（既定） | **有**（`App.xaml:812`） |

**未上書きの`SubmenuHeaderTemplateKey`**（`MenuItem.xaml:2433-2435`）:

```xml
<Border x:Name="SubMenuBorder"
    Background="{StaticResource Menu.Static.Background}"   <!-- #FFF0F0F0 -->
    BorderBrush="{StaticResource Menu.Static.Border}"
```

一次ソースの実値（`Menu.xaml:181-189`、Aero2）＝
`Menu.Static.Background`=**`#FFF0F0F0`** ／ `Menu.Static.Foreground`=**`#FF212121`** ／
`Menu.Disabled.Foreground`=`#FF707070`。
区切り線は`SystemColors.ControlDarkBrushKey`（=`#FFA0A0A0`、`MenuItem.xaml:2451`）。

**忍者の実測——第2・第3階層の背景`#F0F0F0`、区切り線`#A0A0A0`——と完全に一致する。**

### 2.2 経路の対応（殿の御指摘そのまま）

| 殿が辿られた道 | Role | テンプレート | 結果 |
|---|---|---|---|
| `パーツ(P)` | TopLevelHeader | **ecad2の派生** | 第1階層＝ダーク **正常** |
| └ `自作パーツ(C)` | **SubmenuHeader** | **既定（未上書き）** | 第2階層＝**ライト固定** |
| 　└ 部品名 | **SubmenuHeader** | **既定（未上書き）** | 第3階層＝**ライト固定** |
| 　　└ `編集(E)`/`削除(D)` | SubmenuItem | ecad2の派生 | 文字は継承（明色）→ **ライト地に明文字＝見えぬ** |

**殿の御言葉「文字色白・背景色白」が、この表の最下段にそのまま当たる。**

### 2.3 これは「時系列の産物」型の不在である

`App.xaml:694-695`のコメントが、当時の判断をそのまま残しておる:

> 対象2種のみ(家老裁量2026-07-17): 現行メニュー構造(MainWindow.xaml)は2階層のみで
> サブサブメニューを持たないため、TopLevelItemTemplateKey・SubmenuHeaderTemplateKeyは**現状未使用につき省略**。

**そして既存調査書§5-4は、この省略に条件を付けておった**（`ecad2-t083-zoubun7-...-onmitsu.md:132`）:

> 将来的にサブサブメニュー（例：「最近使ったファイル」等）を追加する計画がある場合はこの限りでない。

**その「将来」がT-068増分1（2026-07-24、自作パーツ管理メニュー新設）で到来し、**
**3階層のメニューが生まれた——だが省略の見直しは行われなんだ。**

**すなわち`onmitsu.md`「『無い』の理由を証跡で選り分ける」の(b)＝時系列の産物**にござる。
**(a)裁定によるものではない。** 当時の判断は当時の構造に対して正しく、**構造が変わったのに追随しなかった**のが穴。

### 2.4 パターン再発判定【MUST】

**PR-20パターン1（StaticResourceの固定解決）の再発。しかも同一箇所である。**
- **PR-20の1例目がまさにT-083増分7の`SubMenuBorder`**——`TopLevelHeaderTemplateKey`の方は直された
- **本件は同じ`SubMenuBorder`が、Role違いのテンプレートに取り残されていたもの＝横展開漏れ**
- **`onmitsu.md`「修正の横展開確認」の観点が、Role軸では働いておらなんだ**

### 2.5 【突合が取れておらぬ点・断じておらぬ】第2階層の文字色`#212121`

**忍者の実測は第2階層の文字を`#212121`（14.13:1、読める）としておるが、一次ソースからは説明が付かぬ。**

- 既定`SubmenuHeaderTemplateKey`のHeader用`ContentPresenter`（`MenuItem.xaml:2404-2411`）には**色指定が無い**
- 既定`MenuItem`のStyle本体（`MenuItem.xaml:2490-2498`）にも**`Foreground` Setterが無い**
- ゆえに**継承で明色（`#F0F0F0`）が降りるはず**にて、第3階層が「見えぬ」＝明色である実測とは整合する

**`#212121`と一致するものが一次ソースに一つある**——**`RightArrow`の`Fill="{StaticResource Menu.Static.Foreground}"`**
（`MenuItem.xaml:2424`）。**第2階層の項目は子を持つゆえ右向き矢印を持ち、第3階層の末端項目は持たぬ。**

**ゆえに (i)忍者が矢印の画素を拾った (ii)第2階層で継承が切れて別の既定色になっておる、の二説が立つ。**
**某は実機を測れぬ立場ゆえ、いずれとも断じ申さぬ。**

**ただし修正方針への影響は無い**——`SubmenuHeaderTemplateKey`を定義すれば、いずれの説でも第2・第3階層とも解決する。
**実装後の忍者の実測で決着するのが筋にござる。**

---

## 3. (c) P-150 — 基準枠の灰180

### 3.1 起票内容の行番号に誤りがある

**P-150・台帳とも`PartEditorCanvas.cs:668`と記すが、`668`行は`_theme.Background`にて、
これは`DrawingTheme`から引いておりテーマに正しく追随しておる。**

**灰180の直書きは`674`行**:

```csharp
var frameStroke = new StrokeStyle(new Ecad2.Rendering.Color(255, 180, 180, 180), 0.1, LineStyle.Dashed);
```

**再現手段**＝`Select-String -Path 'src\Ecad2.App\Views\PartEditorCanvas.cs' -Pattern '180, 180, 180'`

### 3.2 層が違う

`PartEditorDialog`は`new Views.PartEditorDialog(entry.Definition, _viewModel.IsDarkMode)`
（`MainWindow.xaml.cs:474`・`521`）と**ダークフラグを受け取っており、作図描画のテーマ追従機構は既に在る。**
**`frameStroke`だけが機構を通らず直書きで取り残されておる。**

**同じ行の近傍に他の直書きが2つある**（`676`行`selectedStroke`=OrangeRed／`677`行`draftStroke`=DodgerBlue）が、
**これらは「意味色」（選択中・記入中）であり、テーマ非依存が正当**と見る
（前例＝`Theme.Dark.xaml:19-20`の`TestModeActiveBrush`は「テーマ非依存の意味色ゆえLightと同値」と明記）。
**基準枠は意味色ではなく「目安の補助線」ゆえ、追従すべき側にあると判ずる。**

---

## 4. 横展開で見つけた、まだ報じられておらぬ穴

**依頼範囲外の気づきにござる。着手はせぬ。落とし先を添える。**

### 4.1 右クリックメニュー（`ContextMenu`）の背景がライト固定

**`ContextMenu`の既定Style本体**（`ContextMenu.xaml:15-16`、Aero/Aero2共通セクション）:

```xml
<Setter Property="Background" Value="#F5F5F5"/>   <!-- 直値。DynamicResourceですらない -->
```

**ecad2側に`ContextMenu`用のスタイルは一切存在せぬ**
（再現手段＝`src`配下から`obj/``bin/`を除いて`TargetType="{x:Type ContextMenu}"`を探すと**0件**。
`new ContextMenu()`は`MainWindow.xaml.cs:2091`・`2199`の**2件**）。

**すなわちダークモードでも右クリックメニューは白背景にござる。**
文字は`SystemColors.MenuTextBrushKey`（黒）ゆえ**読めるが、配色が不統一**——第2階層と同じ型。
**忍者は右クリックメニューを測っておらぬ。**

**落とし先＝T-140の対象へ含めるか否かの判断（家老）。** 機序は(b)と同系統ゆえ、同じ増分で直せる。

### 4.2 `SubmenuHeader`になる箇所は、自作パーツだけではない

**`SubmenuHeader`ロールを取る箇所の列挙**（目視で確定。自動列挙は複数行タグで誤検出したため手で数え直した）:

| # | 箇所 | 子 | 状態 |
|---|---|---|---|
| 1 | `MainWindow.xaml:936`「パネルを自動的に隠す(_A)」 | 静的4項目 | **同じ穴。未報告** |
| 2 | `MainWindow.xaml:989`「自作パーツ(_C)」 | 動的 | 殿ご指摘の箇所 |
| 3 | `MainWindow.xaml.cs:507`（動的生成の部品名） | 編集/削除 | 殿ご指摘の箇所 |
| 4 | `MainWindow.xaml.cs:2266`「線種」（右クリック） | 実線/破線/点線 | **同じ穴。未報告** |

**トップレベル7項目**（ファイル/編集/図面/表示/ツール/パーツ/ヘルプ、`MainWindow.xaml`の`899`/`909`/`920`/`925`/`963`/`972`/`994`）
は`TopLevelHeader`にて**対処済み。**

**「表示→パネルを自動的に隠す」は、T-140起票以前から同じ穴に落ちておった**——
**殿が辿られなんだだけにござる。** **落とし先＝T-140の対象範囲（家老の判断）。**

### 4.3 プレースホルダの`Foreground="Gray"`直書き

`MainWindow.xaml:1515`。**無効状態ではなく固定色の直書き**ゆえ、(c)と同系統。
**落とし先＝`proposed.md`（P-150と束ねるのが筋と見る）。**

---

## 5. DoD4: 修正方針の案

### 5.1 (a)への対処

| 案 | 中身 | 評 |
|---|---|---|
| **案A（推奨）** | **`App.xaml`へ`{x:Static ToolBar.ButtonStyleKey}`・`ToolBar.RadioButtonStyleKey`・`ToolBar.ToggleButtonStyleKey`・`ToolBar.SeparatorStyleKey`をキーとするStyleを定義**し、色のみDynamicResource化 | **全ToolBarへ一括で効く。前例あり**（`App.xaml:701`が`MenuItem.SeparatorStyleKey`を同じ手法で上書き済み）。**メインウィンドウの2ツールバーは`Style`明示ゆえ影響を受けぬ＝回帰リスクが小さい** |
| 案B | `PartEditorDialog`の13要素へ個別に`Style`を指定 | 対象が明快だが、**次にToolBarを足した時に同じ穴が空く** |

**案Aを推す。** ただし**PR-21の戒めが直に効く**——既定Styleを置き換えるゆえ、
**`ToolBar.xaml:784-854`のStyle本体Setter一式（`Padding`/`BorderThickness`/各`Alignment`）と
`ControlTemplate.Triggers`（MouseOver/KeyboardFocused/Pressed/Disabled）を漏れなく移植する要がある。**
**`Separator`も忘れず**（`PartEditorDialog.xaml:83`・`87`の2つ、`ToolBarSeparatorFill`=`#FFB6BDC5`のライト固定）。

### 5.2 (b)への対処

**`App.xaml`へ`SubmenuHeaderTemplateKey`の派生ControlTemplateを追加する**——
既存2種（`TopLevelHeaderTemplateKey`・`SubmenuItemTemplateKey`）と**同じ手法にて、新規性は無い。**

**`TopLevelItemTemplateKey`も併せて定義することを推す。**
**理由＝前回「現状未使用ゆえ省略」と判じたことが、まさに本件の穴を生んだゆえ**（§2.3）。
現状`Menu`直下に子なし項目は無いが、**残る1種を空けておけば同じ経緯を繰り返す。**

**§4.1の`ContextMenu`も同じ増分で直せる**（`ContextMenu`の暗黙的スタイルを1つ足し`Background`/`Foreground`をDynamicResource化するのみ。
テンプレートは`{TemplateBinding Background}`ゆえ**差し替え不要**＝PR-21のリスクが無い）。

### 5.3 (c)への対処 — **UI/UX分岐ゆえ殿へ諮る要あり**

`frameStroke`をテーマ追従へ移すのは技術的には易い。**されど「何色にするか」は意匠の判断にござる**——
基準枠は「目安」ゆえ前景色そのままでは主張が強すぎ、**減光の度合いを決めねばならぬ。**

**論点＝(i)`DrawingTheme`に補助線色を新設するか、既存の前景色を減光して用いるか
(ii)ライト時の見え方を変えてよいか**（現状の灰180はライトでは自然に見えておる。
**追従させれば、ライト側も現状と変わる**）。

**某は案の提示までにて、選定は殿の御裁可を仰ぐのが筋と存ずる。**

### 5.4 無効状態について（DoD3の帰結）

- **(a)の`#6D6D6D`は案Aで自ずと直る**（`IsEnabled`トリガーの色をDynamicResource化する）
- **メインウィンドウ側は殿裁定済みの意匠ゆえ手を触れぬ**（T-047）
- **プレースホルダは§4.3のとおり別件**

---

## 6. 【家老の追記への回答】メニューバー黒文字（殿の御画像2）

**「(a)(b)を直せば併せて解けるか」への答え＝解ける見込みが高い。ただし断じ申さぬ。**

**根拠**＝ダークモードで「明るい背景」が現れうる箇所は、本調査で洗った限り
**第2・第3階層のポップアップ（`#F0F0F0`）と右クリックメニュー（`#F5F5F5`）の2系統のみ**にて、
**いずれも(b)の対処で解ける。**
**`Menu`本体（メニューバー）はローカル値で`MenuBarBackgroundBrush`を指定済み**（`MainWindow.xaml:897`）ゆえ
**構造上ライトにはならぬ**——忍者の実測（`#2D2D30`/`#F0F0F0`/12.05:1）とも合う。

**断じられぬ理由**＝**某は御画像を見ておらぬ。** 御画像の対象が上記2系統のいずれかであれば解けるが、
**第三の箇所であれば解けぬ。** **忍者も「似ておることと同じであることを分けて報じ、断じておらぬ」と区切っており、
某もそこは越え申さぬ。**

---

## 7. 事実と推測の峻別

**事実（一次ソースで確認）**
- `ToolBar.cs:451-489`の差し替え機構と条件式 ／ `ToolBar.xaml:786-787`・`857-858`・`953-954`のForeground Setter
- `MenuItem.xaml:2365-2488`（`SubmenuHeaderTemplateKey`）・`2490-2512`（Style本体とRoleトリガー）
- `Menu.xaml:181-189`のブラシ実値 ／ `ContextMenu.xaml:15-16`の`#F5F5F5`
- ecad2側の全該当箇所（`App.xaml`・`MainWindow.xaml`・`PartEditorDialog.xaml`・`PartEditorCanvas.cs:674`）
- **忍者の実測4値が一次ソースから再現できたこと**（§1.2）

**推測・未確認**
- **第2階層の文字色`#212121`の出所**（§2.5）——**二説あり、断じておらぬ**
- **殿の御画像2の対象**（§6）——**画像を見ておらぬ**
- **`.NET 10`同梱テーマDLLのBAML逆コンパイルによる裏取りは行っておらぬ**（dotnet/wpf `main`のソースで代替。
  既存調査書と同じ限界にござる）

---

## 出典

- [dotnet/wpf `main`](https://github.com/dotnet/wpf)（2026-08-02取得、`curl`でscratchpadへ全文保存のうえ`Read`／`Select-String`）
  - `src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/ToolBar.cs`
  - `src/Microsoft.DotNet.Wpf/src/Themes/XAML/ToolBar.xaml`・`MenuItem.xaml`・`Menu.xaml`・`ContextMenu.xaml`
- ecad2実ソース（2026-08-02時点）＝`src/Ecad2.App/App.xaml`・`MainWindow.xaml`・`MainWindow.xaml.cs`・
  `Views/PartEditorDialog.xaml`・`Views/PartEditorCanvas.cs`・`Themes/Theme.Dark.xaml`
- GuiEcad原本＝`C:\Users\kojif\Desktop\生産物\gui_ecad\src`（`MainPage.xaml:157-158`・`MainPage.xaml.cs:274-289`。
  **原本は`ElementTheme.Dark`をRootGridへ与えるWinUI3の機構にて、全コントロールが自動追従する**——
  **ecad2の「明示的に手当てした箇所だけ追従する」方式とは前提が異なる。ゆえに(a)(b)は原本では起こらぬ型の欠陥**）
- `docs/ecad2-t083-zoubun7-menu-dark-redesign-survey-onmitsu.md`（既存調査書。§5-4の留保が本件を予言しておった）
- `docs-notes/pattern-recurrence-log.md` PR-20（56行）・PR-21（51行）・PR-24（60行）
- `docs/todo.md` T-140節（忍者の実測値）

## 派生提案

**§4の3件**（右クリックメニュー／「パネルを自動的に隠す」／プレースホルダ直書き）。
**落とし先は各項に明記した。着手はせぬ。**
