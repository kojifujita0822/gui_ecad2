# T-130 AutoHideフライアウトの軸解決の機序（隠密）

調査日: 2026-08-02　調査者: 隠密（key=1785632944373）　依頼元: 家老
発端＝忍者の実機確認（`docs/ecad2-t130-otherpanels-verification-ninja.md`）＝
**機器表・プロパティは`AutoHideWidth=280`を入れても効かず、高さ`106`（≒`AutoHideMinHeight`100＋クローム）で開く**

---

## 0. 結論

**家老の推量どおりにござった。`MainWindow.xaml:1492`の`LayoutPanel Orientation="Vertical"`が原因にござる。**

**AvalonDockは「そのパネルが画面のどちら側に在るか」を見ておらぬ——見ておるのは
「親`LayoutPanel`の`Orientation`」ただ一つにござる。**

**ゆえに機器表・プロパティは、画面上は右側に在りながら、AvalonDockの内部では
`AnchorSide.Top`／`AnchorSide.Bottom`と解決され、`AutoHideHeight`を見て描かれる。**
**`AutoHideWidth=280`は正しく永続化されるが、描画には一切関与せぬ。**

---

## 1. 機序（一次ソースで確定）

### 1.1 サイドを決めておるのは`GetSide()`ただ一つ

`LayoutAnchorable.cs:597`（AutoHide化の処理）:

```csharp
var anchorSide = parentPane.GetSide();
switch (anchorSide)
{
    case AnchorSide.Right:  root.RightSide?.Children.Add(newAnchorGroup); break;
    case AnchorSide.Left:   root.LeftSide?.Children.Add(newAnchorGroup); break;
    case AnchorSide.Top:    root.TopSide?.Children.Add(newAnchorGroup); break;
    case AnchorSide.Bottom: root.BottomSide?.Children.Add(newAnchorGroup); break;
}
```

その`GetSide()`（`Extensions.cs:63-78`）:

```csharp
public static AnchorSide GetSide(this ILayoutElement element)
{
    if (element.Parent is ILayoutOrientableGroup parentContainer)
    {
        var layoutPanel = parentContainer as LayoutPanel ?? parentContainer.FindParent<LayoutPanel>();
        if (layoutPanel != null && layoutPanel.Children.Count > 0)
        {
            if (layoutPanel.Orientation == Orientation.Horizontal)
                return element.IsInAnchorablePaneAtStartOfPanel(layoutPanel) ? AnchorSide.Left : AnchorSide.Right;
            return element.IsInAnchorablePaneAtStartOfPanel(layoutPanel) ? AnchorSide.Top : AnchorSide.Bottom;
        }
    }
    Debug.Fail("Unable to find the side for an element, possible layout problem!");
    return AnchorSide.Right;
}
```

**判定材料は2つだけにござる——(1)親`LayoutPanel`の`Orientation` (2)その中で先頭か否か。**
**「その`LayoutPanel`自身が、外側のどこに在るか」は一切見ておらぬ。**

### 1.2 ecad2のレイアウト構造に当てはめる

`MainWindow.xaml`の入れ子（一次ソースで確認）:

```
1353  LayoutPanel Orientation="Horizontal"          ← 大枠
1362    LayoutAnchorablePane DockWidth="190"        → LeftPalette
1405    LayoutDocumentPane                          → Canvas
1492    LayoutPanel Orientation="Vertical" DockWidth="280"   ← 右列
1497      LayoutAnchorablePane DockMinHeight="80"   → DeviceTable
1511      LayoutAnchorablePane DockMinHeight="80"   → RightPanelBottom
1645  （右列・大枠を閉じる）
1654  LayoutAnchorablePane DockHeight="160"         → OutputPanel
```

| パネル | 親`LayoutPanel`の`Orientation` | 先頭か | `GetSide()`の戻り | 見る値 | 実測 |
|---|---|---|---|---|---|
| **LeftPalette** | **Horizontal**(1353) | 先頭 | `Left` | `AutoHideWidth` | **190→196 ✓効く** |
| **DeviceTable** | **Vertical**(1492) | 先頭 | **`Top`** | **`AutoHideHeight`** | **高さ106 ✗** |
| **RightPanelBottom** | **Vertical**(1492) | 先頭でない | **`Bottom`** | **`AutoHideHeight`** | **高さ106 ✗** |
| **OutputPanel** | Vertical（外側） | 先頭でない | `Bottom` | `AutoHideHeight` | **160→166 ✓効く** |

**忍者の実測とすべて符合いたし申す。**
**とりわけ「全幅・上辺から下へ短く開く」は`AnchorSide.Top`の挙動そのもの**（機器表）。
**リサイズハンドルが水平バーで上下にのみ動くという観測も、高さ軸であることの直接の証左にござる。**

### 1.2.1 【2026-08-02訂正】上表の`RightPanelBottom`＝`Bottom`は誤り。実測は`Top`であった

**忍者の追加実測（`docs/ecad2-t130-rightpanelbottom-and-t140-system2-verification-ninja.md`）＝
`RightPanelBottom`も`Top`解決。機器表とほぼ同一のPane座標にござった。**

**原因＝隠密が`IsInAnchorablePaneAtStartOfPanel`という関数名を「先頭の1枚か」と読み、
実装を読まずに意味を推し量ったこと**にござる。**実装（`Extensions.cs:114-130`）はこうであった**——

```csharp
foreach (var child in layoutPanel.Children)
{
    if (!(child is LayoutAnchorablePane || child is LayoutAnchorablePaneGroup))
        return false;                                    // Pane以外に出くわしたら即false
    if (child.Equals(element) || child.Descendents().Contains(element))
        return true;                                     // elementに行き当たったらtrue
}
return false;
```

**真の意味＝「先頭から順に見て、`AnchorablePane`以外に出くわす前に`element`へ行き当たるか」。**
**「先頭の1枚か」ではない——`AnchorablePane`が連続する限り、何枚目でも`true`になる。**

**訂正後の対応表（4件すべてが忍者の実測と符合する）**

| パネル | 親`LayoutPanel`の子の並び | 判定 | 結果 | 実測 |
|---|---|---|---|---|
| **LeftPalette** | [0]自身(Pane) | 即`true` | `Left` | 幅190→196 ✓ |
| **DeviceTable** | [0]自身(Pane) | 即`true` | `Top` | 高さ106 ✓ |
| **RightPanelBottom** | [0]機器表(Pane)→**Paneゆえ`return false`せず** [1]自身 | **`true`** | **`Top`** | **高さ106・機器表とほぼ同座標 ✓** |
| **OutputPanel** | [0]`LayoutPanel`(**Paneでない**) | 即`false` | `Bottom` | 高さ160→166 ✓ |

**忍者の見立て「`Vertical`内の2枚を分ける追加ロジックが一次ソースにあるはず」への答え＝在らぬ。**
**分ける仕組みそのものが無く、条件が揃えば両方とも`Top`になる。**

**【直し方への影響】案4（幅・高さ両方に値を入れる）は変わらぬ。**
**むしろ簡明になった——機器表・プロパティとも`Top`ゆえ、高さの既定値は両方同じでよい。**

### 1.3 なぜ`AutoHideWidth=280`は「効いておるように見えた」か

**永続化はされ申す**——`AutoHideWidth`は`LayoutAnchorable`の公開プロパティにて、
値は保存XMLへ書き出され、読み戻され申す。
**されど描画側（`LayoutAutoHideWindowControl.cs:316,327`）は`AutoHideHeight`しか参照せぬ。**
**「値は正しいが描画に反映されぬ」——PR-20そのものの型にござる。**

---

## 2. 直し方の案

**【前提の確認】殿の当初の御指図は「シートパネルと同様に直す」**（2026-07-27、台帳T-130節）にて、
**症状は「細く開く」ことにござった。「右から横に開くべし」とまでは仰せでない。**
**ゆえに下記いずれも御指図は満たし申す——分かれ目は「上下に開く姿を容れるか」にござる。**

| 案 | 中身 | 長 | 短 |
|---|---|---|---|
| **案1** | **表の軸を`Height`へ改め、適切な高さを入れる** | 実装は表の書き換えのみ・最小 | **「上下に開く」が確定する**。**かつ「通常ドック時の寸法に揃える」という不変条件が崩れる**——機器表の通常時の高さは分割で決まる動的値ゆえ、揃える相手がござらぬ |
| **案2** | **`LayoutPanel Orientation="Vertical"`(1492)を外し、Horizontalな親の直下へ置く** | 軸がWidthに解決され「右から横に開く」が実現 | **右列に縦2段で並ぶ見た目そのものが壊れ申す。不可** |
| **案3** | AutoHide化の後、`LayoutRoot.RightSide`へ`LayoutAnchorGroup`を移す | 見た目も本来の姿になる | **モデル手術にあたる**——`memory: avalondock_hidden_invariants_survey_entry_points`「モデル手術でなくLayout差し替えに任せよ」に真っ向から抵触。**T-099(c)で3周を要した領域にござる。推さぬ** |
| **案4（推奨）** | **幅・高さの両方に値を入れる**（表を「軸1つ」から「幅と高さの両方」を持つ形へ） | **軸がどちらに解決されても既定100にはならぬ**。**構造を触らぬ**。**将来レイアウトを変えても破綻せぬ** | 「上下に開く」姿は変わらぬ。**高さの既定値を選ぶ根拠が要る** |

### 2.1 **某の推奨＝案4。理由は「軸の解決を当てにせぬ」ことにござる**

**案1は「軸はHeightである」を前提に固定いたし申すが、その前提は`GetSide()`の挙動
＝レイアウト構造に依存しており申す。** **将来`LayoutPanel`の`Orientation`や入れ子を変えれば、
再び食い違い申す——そして今回と同じく、静的には気づけ申さぬ。**

**案4なら、どちらに解決されても既定100にはなり申さぬ。**

### 2.2 **殿へ諮るべき点＝高さの既定値をいくつにするか**

**機器表・プロパティの通常ドック時の高さは固定値でなく、分割で決まり申す**
（`DockMinHeight="80"`のみ指定）。**ゆえに「通常時に揃える」という従来の根拠が使え申さぬ。**
**目安の案を2つ挙げ申す**——
- **(あ) 出力パネルと同じ`160`**（下から開く点で性質が近い）
- **(い) より大きく`240`程度**（機器表は行数が多く、100や160では数行しか見え申さぬ）

**某は(い)寄りと見申すが、これは見た目の判断ゆえ殿の御裁可を仰ぐ筋にござる。**

---

## 3. 【家老のお問い】静的に検出する術は在ったか——**在り申した。某が辿り切らなんだ**

**在り申した。** `GetSide()`（`Extensions.cs:63`）を読めば、
**軸が「親`LayoutPanel`の`Orientation`」で決まることは机上で分かり申す。**

**某は静的レビューで`LayoutAutoHideWindowControl.cs:297/306/316/327`まで読み、
「右左は`AutoHideWidth`、上下は`AutoHideHeight`」までは確かめ申した。**
**されど「では`_side`は何で決まるのか」を辿り申さなんだ。**
**軸が決まった後の一次ソースは読み、軸が決まる前を読んでおらぬ。**

**型としては、某が本日`onmitsu.md`へ自著したばかりの戒めと同じにござる**——
**「`BasedOn`・継承で組まれた定義から値を引くときは、派生側が上書きしておらぬかを必ず見る」。**
**あちらは定義の層を辿る話、こちらは呼び出しの上流を辿る話にて、
いずれも「その値を最後に決めておるのはどこか」を辿り切れという同じ戒めにござる。**

### 3.1 **さらに悪いのは、某が正しい場所を指しながら時制を誤ったことにござる**

**某は静的レビューの申し送りにこう書き申した**——
> **軸を静的な表で固定しており申す。パネルが別サイドへ移されれば軸が食い違い申すが、
> 対象4件とも`CanFloat="False"`ゆえ移動の余地は小さく、実害の見込みは低うござる。**

**懸念の在り処は正しゅうござった。されど「移されれば」という将来形で述べ申した。**
**実際には最初から食い違うており、しかも`CanFloat="False"`は何の関係もござらぬ**
——**軸を決めるのは移動の可否ではなく、最初から親`LayoutPanel`の`Orientation`ゆえ。**

**「実害の見込みは低い」と自ら評価を下げたことで、家老がこの一行を追う理由も消し申した。**

**How to apply（落とし先の提案）**：
**「懸念を書くときは、それが『既に起きておること』か『将来起きうること』かを分けて書く。
分けられぬなら、分けられぬと書く。」**
**某は分けられると思い込み、確かめずに将来形を選び申した。**
**落とし先＝`onmitsu.md`の報告フォーマット節（「数を書いたら再現手段を添えよ」の系列）と見申すが、
家老の裁可を仰ぐ。**

---

## 4. 事実と推測の峻別

**事実（一次ソースで確認）**
- `Extensions.cs:63-78`の`GetSide()`実装 ／ `LayoutAnchorable.cs:597-605`の分岐
- `LayoutAutoHideWindowControl.cs:297/306/316/327`の軸別参照
- `LayoutAnchorable.cs:34,36`の既定値100.0
- `MainWindow.xaml`の入れ子構造（1353/1362/1405/1492/1497/1511/1645/1654）
- **上記から導いた4パネルの`GetSide()`戻り値が、忍者の実測とすべて符合すること**

**推測・未確認**
- ~~**`RightPanelBottom`が`Bottom`と解決されること自体は実測しておらぬ**~~
  **【2026-08-02解決＝予測は外れた】忍者が実測し`Top`と判明。§1.2.1に訂正を記した。**
  **未確認と書き残したゆえ忍者が測り、食い違いが露見した**——**未確認の申告がそのまま働いた形にござる**
- **高さの既定値の目安（160／240）は、機器表の行数からの見当にて実測ではござらぬ**

## 5. 【この調査自身の教訓】名を信じて外れた三度目にござる

**隠密が本日、同型の誤りを三度犯した。**

| # | 案件 | 信じた名 | 実態 |
|---|---|---|---|
| 1 | T-141 | `ToolBarButtonStyle`の`Width=46` | **派生`PlacementToolBarButtonStyle`が`36`へ上書き** |
| 2 | T-130 | `AutoHideWidth` | **右列では参照されず、`AutoHideHeight`が使われる** |
| 3 | **T-130（本節）** | **`IsInAnchorablePaneAtStartOfPanel`** | **「先頭の1枚か」ではなく「Pane連続の中にあるか」** |

**三件とも「その値を最後に決めておるのはどこかを辿り切らなかった」型**にて、
**本書の既存の戒め（`onmitsu.md`「`BasedOn`・継承で組まれた定義から値を引くときは派生側を必ず見る」）で括れる。**

**新しいのは一点のみ**——**辿ろうとすら思わなかったのは、名が十分に説明的に見えたゆえ。**
**`IsInAnchorablePaneAtStartOfPanel`は31文字あり、意味が書いてあるように見える。**
**されど書いてあったのは「意図」であって「実装」ではなかった。**

**短い名は疑うが、長く具体的な名ほど信じてしまう**——**制度化の要否は家老の判断を仰ぐ。**
