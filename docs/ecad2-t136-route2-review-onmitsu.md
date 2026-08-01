# T-136(A)経路2 静的レビュー（隠密、2026-08-01）

**対象**＝`cdde8a3`「配置バーのコンボでも置けぬ部品を無効化(T-136(A)経路2、殿裁可2026-08-01)」
**範囲**＝`git show cdde8a3`（2 files changed, **85 insertions(+)・削除0**）
**深度**＝1周目ゆえ軽量既定（`karo.md`「レビュー深度の既定」）。`code-review`スキルは併用せず手動のみ。

**総括**＝**所見7件（要修正1・経過観察1・確認済み4・範囲外1）。実装（XAML 1行）そのものに穴は見当たらぬ。**
**指摘はいずれもテスト側にござる。**

---

## 所見1【要修正・中】テストの偽陰性経路が**現に開いておる**

### 何が起きるか

新設テストは、切り出したブロックから**`IsPlaceable`を含む最初の行**を拾い、その行に`IsEnabled`が
在るかを見る（`T136PlacementEntryPointsTests.cs:31-37`）。

```csharp
var setterLine = block.Split('\n').FirstOrDefault(line => line.Contains("IsPlaceable"));
Assert.True(setterLine is not null, ...);
Assert.Contains("IsEnabled", setterLine!);
```

**行が`<Setter>`であるかを問うておらぬゆえ、コメント行でも一致する。**

### 現に開いておる、というのはこういうことにござる

**パレット側のコメント（`MainWindow.xaml:1616-1619`）は、すでに`IsEnabled`の語を含んでおる**——

```xml
<!-- T-136(A)増分2: 現在のシートへ置けぬ部品は選ばせぬ(予防)。
     IsEnabled=False は PreviewMouseLeftButtonDown ごと止めるゆえ、   ← :1617
     クリック経路の遮断もこれ1つで足りる。実際の拒否は
     ValidatePlacement が受け持つ(防御・サイレント)。 -->
<Setter Property="IsEnabled" Value="{Binding IsPlaceable}"/>          ← :1620
```

**今は`IsPlaceable`の語がコメントに無いゆえ、`FirstOrDefault`は`:1620`のSetterを拾う。**
**されどこのコメントに`IsPlaceable`の語が一つ加わった瞬間、**
**`:1617`が先に拾われ、その行には`IsEnabled`も在るゆえ、**
**`:1620`のSetterを丸ごと削除してもテストはGREENのまま通る。**

**危ういのは、このコメントが`IsPlaceable`と`IsEnabled`の関係を説明しておる箇所そのもの**である点にござる
——「`IsPlaceable`がfalseなら`IsEnabled=False`となり」と書き足すのは、**次に触る者のごく自然な筆**にて、
**その一筆でテストの検出力が黙って失われる。**

### RED証明が通っておることは、本件の反証にならぬ

侍の改変B（パレット側Setter除去）は**ListBoxケースのみREDと実測されておる**が、
**それは現時点でコメントに`IsPlaceable`が無いゆえ**にござる。
**改変Bが「Setterの実在を測っておる」ことを示すのではなく、「今はコメントが邪魔をしておらぬ」ことを
示したにすぎぬ**——**RED証明は、その時点の本文に対してしか効かぬ。**

### 対処案（いずれか1つで足りる）

- **(案a・小)** 抽出条件へ`<Setter`を足す
  ——`line.Contains("<Setter") && line.Contains("IsPlaceable")`
- **(案b・より確か)** 1行の中で`Property="IsEnabled"`と`{Binding IsPlaceable}`の**対応**まで測る
  （正規表現1本。現行は両語が同じ行に在れば通るゆえ、`Property="IsPlaceable" Value="{Binding IsEnabled}"`
  のような取り違えも通ってしまう）
- **(案c)** ブロックからコメント（`<!-- ... -->`）を除いてから測る

**某は(案b)を推す**——**所見1と、上記の取り違えの両方が同時に塞がるゆえ。**

---

## 所見2【経過観察】バインド名のタイポ・VM側リネームは原理的に検出できぬ

家老の観点2そのものにござる。**`{Binding IsPlaceableX}`と綴り誤っても`Contains("IsPlaceable")`は真**にて、
テストはGREEN、ビルドも通り、**実行時は`IsEnabled`が既定`true`のまま黙って穴が復活する**
（WPFのBindingは実行時解決ゆえ、失敗しても例外にならぬ）。

**ただし現時点で穴ではない**——**`IsPlaceable`の実在を一次ソースで確かめ申した**（→所見3）。

**対処案**＝テスト側で**VMのプロパティ実在を結ぶ**。1行で足りる。
```csharp
Assert.NotNull(typeof(PartSelectionEntryViewModel).GetProperty("IsPlaceable"));
```
**これで「XAMLに書かれた名」と「VMに在る名」が同じ文字列で結ばれ、片方のリネームで必ずREDになる。**

**経過観察に留める理由**＝**1周目軽量既定であり、現に壊れてはおらぬ**ゆえ。
**要否は家老のご判断を仰ぐ**（所見1を直す際に併せて入れるのが安い、というのが某の見立て）。

---

## 所見3【確認済み】観点3——`IsPlaceable`は実在し、コンボは同型を受け取る

| 確かめた事 | 一次ソース |
|---|---|
| `IsPlaceable`が`PartSelectionEntryViewModel`に実在する | `PartSelectionEntryViewModel.cs:36-40`（`bool`、`SetProperty`による通知つき） |
| `SelectionEntries`の型 | `PartPaletteViewModel.cs:36`＝`IReadOnlyList<PartSelectionEntryViewModel>` |
| パレットが受け取る先 | `MainWindow.xaml:1603`＝`ItemsSource="{Binding PartPalette.SelectionEntries}"` |
| コンボが受け取る先 | `MainWindow.xaml.cs:3687`＝`PlacementPartComboBox.ItemsSource = _viewModel.PartPalette.SelectionEntries` |
| 値の更新経路 | `PartPaletteViewModel.cs:98-99`＝`foreach`で全entryへ`PartResolver.IsAllowedOnSheet(...)`を当てる |

**すなわち両入口は同じリストの同じVMインスタンスを共有しており、シート切替時の一括更新が
両方へ同時に効く。** **別々の型・別々のインスタンスを渡しておらぬ**（`ecad2_comparison_target_identity_pitfall`
の轍＝別物同士を測る誤りは、本件では起きておらぬ）。

---

## 所見4【確認済み】切り出しの終端探索は正しい

`ExtractElementBlock`は`</{tagName}>`の**最初の出現**までを取る（`:60`）。
**途中に`</ListBox.ItemTemplate>`(`:1613`)・`</ListBox.ItemContainerStyle>`(`:1623`)が在るが、
文字列`</ListBox>`とは一致せぬ**（`.`と`>`が違う）ゆえ、**正しく`:1624`まで取れる。**
ComboBox側も同様（`</ComboBox.ItemTemplate>`は一致せず、`:1855`を取る）。
**侍のコメント「同種要素の入れ子を持たぬゆえ最初の終了タグまでで足りる」も現物と合うており申す。**

**軽微な脆さ**＝開始タグの探索が`<{tagName} x:Name="{controlName}"`という**属性順序への依存**を持つ
（`:57`）。`<ComboBox Width="40" x:Name="...">`と並べ替えられればテストは「宣言が見つからぬ」でREDになる。
**失敗側へ倒れるゆえ害は無く、直す要は無いと判ずる**（記録のみ）。

---

## 所見5【確認済み】網羅の判断——入口が2つであることは裏づいた

`SelectionEntries`の全出現を、`obj/`・`bin/`を除き**`.cs`と`.xaml`の両方**から洗った。
**総数17件**、その内訳は次のとおり。

| 種別 | 件数 | 箇所 |
|---|---:|---|
| **UIへ並べる入口** | **2** | `MainWindow.xaml:1603`（`ItemsSource`）／`MainWindow.xaml.cs:3687`（代入） |
| コメント中の言及 | 5 | `PartPaletteViewModel.cs:25 :51`／`MainWindow.xaml:1507 :1820`／`MainWindow.xaml.cs:3684` |
| 宣言 | 2 | `PartPaletteViewModel.cs:35 :36` |
| 構築 | 3 | `PartPaletteViewModel.cs:65 :76 :78` |
| 一括更新（`foreach`） | 2 | `PartPaletteViewModel.cs:98 :139` |
| 検索・既定選択の取得 | 3 | `PartPaletteViewModel.cs:131 :132`／`MainWindow.xaml.cs:3689` |

**侍の「入口は2つのみ」は正しい。**

### 【この所見は、危うく甘い根拠で「確認済み」と断ずるところであった】

**某の最初の探索は、XAMLについては`x:Name`と`IsPlaceable`しか見ておらず、
`SelectionEntries`でXAML全体を洗っておらなんだ。**
**§「数の再現手段」を書く段で数え直したところ、XAMLに3件が現れ**——
**一瞬「入口が2つ」の前提が崩れたかと見えた**（実際は`:1507`・`:1820`とも**コメント**であった）。

**二つ引き当てたものがござる。**
1. **`onmitsu.md`「`grep`の生の一致数を、そのまま実呼び出し数として報じない」がそのまま出た**
   ——**17件のうち5件（29%）がコメント**にて、**ecad2はコメントに設計意図を厚く書く流儀**という
   既知の傾向と合う
2. **`onmitsu.md`「結論を出したら、それを使う作業まで自分で一歩進めよ」が働いた**
   ——**再現手段を書こうとした行為そのものが検算になった**（`:423`「数を書いたら再現手段を添えよ」が
   説くとおり、**手段を書く行為が測り方の誤りを炙り出す**）

---

## 所見6【DoD整合】台帳との突き合わせ

`docs/todo.md:1298-1300`（殿裁可＝案1）と1件ずつ照合した。

| 台帳の求め | 実装 | 判定 |
|---|---|---|
| `PlacementPartComboBox`の`ItemContainerStyle`へ`IsEnabled`バインド1行 | `MainWindow.xaml:1850`（1行） | **合** |
| テスト1件 | **3件**（Theory 2ケース＋対照1件） | **合**（設計にあるものを省くのが不可であって、足すのは自由） |
| 既存の防御（サイレント拒否）は二重の網として残す | `ValidatePlacement`に差分なし（**削除0行**） | **合** |

**既存テストの改変＝0件**（`--stat`が`85 insertions(+)`のみ）。
家老の申し送りどおり、**検出力毀損の観点は本件に当たらぬ。**

---

## 所見7【範囲外の気づき・忍者への申し送り候補】

**配置バーを開いたままシートを切り替えると、`PartPaletteViewModel.cs:98-99`により
選択中の項目が`IsPlaceable=false`へ変わりうる。** このとき
**ComboBoxの`SelectedItem`は無効化された項目を指したまま残る**——
**「選べぬはずの部品が、選ばれた状態で表示されておる」**という見え方になる公算がある。

**実害は無い見込み**（`PlacementOkButton_Click`の`ValidatePlacement`が最終防御で拒む）。
**されどこれは静的読解からの推測にて、実際にその状態へ至れるか・どう見えるかは実機でしか分からぬ。**
**忍者の実機確認の観点に加えるかは家老のご判断を仰ぐ**（`onmitsu.md`「気づきの扱い」に従い、
某は台帳化・着手をせぬ）。

---

## 数の再現手段

- **差分の規模**＝`git show cdde8a3 --stat`
- **XAML内の`IsPlaceable`出現**＝`Select-String -Path src\Ecad2.App\MainWindow.xaml -Pattern 'IsPlaceable'`
  → **2件**（`:1620`パレット／`:1850`コンボ。いずれも`<Setter>`行）
- **入口の数**＝`Get-ChildItem C:\ECAD2\src -Recurse -Include *.cs,*.xaml |
  Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' } | Select-String -Pattern 'SelectionEntries'`
  → **17件**。内訳は所見5の表のとおり（**UI入口2・コメント5・宣言2・構築3・更新2・検索3**）。
  **`-Include`から`*.xaml`を落とすと14件になり、XAMLの`ItemsSource`（`:1603`）を取り逃す**
  ——**某が初手で踏んだ穴ゆえ、条件をそのまま書き残す**

**所見は全7件**（要修正1・経過観察1・確認済み4・範囲外1）。
**「確認済み」を1行に束ねておらぬ**のは、`onmitsu.md`「束ねた行は数を潰す」の戒めによる。
