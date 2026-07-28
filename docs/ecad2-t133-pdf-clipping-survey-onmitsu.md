# 殿ご下問：描画が公称の矩形をはみ出した場合、PDF出力で切られるのか

隠密、2026-07-28。**調査のみ。`src/`への書き込みは行っておらぬ。**

**背景**＝侍がT-133の計画起草で「描画は公称2×2をはみ出す」と発見（ELBのテストボタンが0.55セル外＝
`SymbolGlyphs.cs:309`、Breaker3Pの弧が0.42セル外＝`:302`）。
**殿のご意向**＝**「0.5セルほどのはみ出しはユーザーが設置時に気づくので問題視しない。
描画部が印刷されるなら不問」**——**ゆえに論点は「紙に出るか否か」の一点。**

---

## 0. 答え——**切られませぬ**

| 問い | 答え | 確度 |
|---|---|---|
| **クリッピングで切られるか** | **切られぬ**。**要素（記号）の描画にクリップは一切かからぬ** | **確定**（コード読解、下記1節） |
| **画面とPDFで扱いが違うか** | **違わぬ**。**同一の`DiagramRenderer`が同一の順序・同一の条件で描く** | **確定**（下記2節） |
| **用紙の物理的な端を越えた場合** | **紙には出ぬ**（PDFの性質上）。**ただし拙者は実測しておらぬ** | **不明と区切る**（下記3節） |

**殿の裁定の観点でいえば「不問」で差し支えなしと存ずるが、3節の但し書きのみ申し添える。**

---

## 1.【DoD 1】クリッピングは在るが、要素には掛からぬ

### 1-1. `PushClip` の呼び出しは全体で2箇所のみ

**`src/`全体で`PushClip`を呼ぶのは`DiagramRenderer.cs:590`と`:645`の2箇所**（再現手段は4節）。
**いずれも`PopClip`と対で閉じており**、範囲は各メソッド内で完結する——

| 箇所 | 何を囲むか | 条件 | 閉じ |
|---|---|---|---|
| `:590` | **自由直線**（`DrawFreeLines`内） | `partialPage`＝複数ページ分割時**のみ** | `:599` |
| `:645` | **枠**（`DrawFrames`内） | 同上 | `:662` |

**単一ページならクリップは一切掛からぬ**（`partialPage = rowStart > 0 || rowEnd < totalRows`）。

### 1-2.【肝】要素の描画は、クリップの外側で行われる

`DiagramRenderer.Render`の描画順（`:314-334`）——

```
L314  DrawImages          L326  DrawConnectors
L315  DrawGrid            L327  DrawFreeLines   ← 内部で PushClip(590)〜PopClip(599)
L320  DrawRails           L328  DrawDots
L321  DrawBusLabels       L329  DrawFrames      ← 内部で PushClip(645)〜PopClip(662)
L323  DrawRowNumbers      L330  foreach (var e in sheet.Elements)
L325  DrawRungWires       L331      if (InWindow(e.Pos.Row)) DrawElement(...)   ← ここ
```

**要素の描画（`:330-331`）は`DrawFrames`から戻った後**にござる。
**`DrawFrames`は`:662`で`PopClip`しておるゆえ、`:331`の時点でクリップは解除されておる。**

**ゆえにELBのテストボタンもBreaker3Pの弧も、クリップで切られることはない。**

**なお`InWindow(e.Pos.Row)`（`:302`）はアンカー行での二値判定**——**ページ窓内なら丸ごと描き、
外なら丸ごと描かぬ**。**「一部だけ切る」という挙動は持たぬ。**

---

## 2.【DoD 4】画面とPDFで扱いは違わぬ

**両者は同一の`DiagramRenderer`を通る。** `PdfExporter.Export`（`PdfExporter.cs:41-45`）は
`dr.Render(renderer, ...)`を呼ぶだけで、**PDF固有の描画分岐もクリップも持たぬ。**

差は`IRenderer`の実装だけにござるが、**`PushClip`の意味論も揃っておる**——

| | 実装 | 出典 |
|---|---|---|
| 画面 | `_dc.PushClip(new RectangleGeometry(...))` | `WpfRenderer.cs:38-39` |
| PDF | `_g.IntersectClip(new XRect(...))` | `PdfRenderSurface.cs:174-178` |

**加えて、画面側にコードでのクリップ追加は無い**——`.cs`全体で`ClipToBounds`・`.Clip =`の設定は
**0件**（再現手段は4節）。XAMLの`ClipToBounds`3箇所（`MainWindow.xaml:285`ほか）は
**AvalonDockのパネル外枠のControlTemplate内**であり、**ラダー描画の内容ではなくパネル領域を切るもの**
にござる。

**すなわち「画面で見えておるものが紙で消える」という事態は、クリップに関する限り起こらぬ。**

---

## 3.【区切り】断じられぬこと

**用紙の物理的な端を越えた描画**は、**クリップとは別の話**にござる。
**PDFの性質上、ページ矩形の外は出力されぬ**と理解しておるが、**拙者はPDFを生成して実測しておらぬ**
——**ゆえに「不明」と区切る。**

**ただし実害の見込みは薄いと申し添える**（**これは推測と明示する**）——
0.55セル＝**約5mm**（既定Cell=9.0mm）にすぎず、**用紙端に達するには要素を図面の最外周へ置く要がある。**
外周には余白（`MarginMm`）と図面枠がござる。

**確かめるなら**＝**要素を用紙の端へ寄せて置き、PDFを出して目視する**のが早い（忍者の領分）。

### 3-1. 副次所見——「切る」のではなく「縮める」機構が別に在る

`Render`の`:310-312`に**縮小フィット**がござる（T-080、殿裁定）。
内容が図面枠の幅を超える場合、**内容全体を一様スケールして枠内へ収める**。
**これは切り落としではなく縮小**ゆえ、**はみ出しは消えず、小さくなって紙に出る。**

**ただし縮小の判定基準（`CalcPageScale`）が記号のはみ出し分を勘定に入れておるかは未確認**にござる。
**入っておらずとも「切られる」わけではない**ゆえ、本ご下問の答えは変わらぬ。

---

## 4.【DoD 5】再現手段

```powershell
# (1) PushClip の呼び出し箇所（結果=DiagramRenderer.cs:590,645 の2件。他は各Rendererの実装）
Get-ChildItem -Recurse -Path C:\ECAD2\src -Filter *.cs |
  Select-String -Pattern '\.PushClip\(' | Select-Object @{n='F';e={$_.Filename}}, LineNumber

# (2) PushClip/PopClip の対応（結果=590→599、645→662）
$d = Get-Content C:\ECAD2\src\Ecad2.Core\Rendering\DiagramRenderer.cs
for ($i=575; $i -lt 700; $i++) { if (($d[$i] -replace '//.*$','') -match 'PushClip|PopClip') { "L$($i+1)" } }

# (3) 要素描画の位置がクリップの外か（結果=L330-331、DrawFrames(L329)の後）
for ($i=310; $i -lt 340; $i++) { "L{0}  {1}" -f ($i+1), $d[$i] }

# (4) コード側のクリップ追加（結果=0件）
Get-ChildItem -Recurse -Path C:\ECAD2\src -Filter *.cs | Select-String -Pattern 'ClipToBounds|\.Clip\s*='
```

---

## 5. 出典

- `src/Ecad2.Core/Rendering/DiagramRenderer.cs:300-356`（`Render`本体の描画順）／`:578-599`（自由直線のクリップ）／`:640-662`（枠のクリップ）／`:982`（`DrawElement`定義）
- `src/Ecad2.Core/Rendering/IRenderer.cs:32-33`（`PushClip`/`PopClip`の宣言）
- `src/Ecad2.Rendering.Wpf/WpfRenderer.cs:38-41`／`src/Ecad2.Pdf/PdfRenderSurface.cs:174-180`
- `src/Ecad2.Pdf/PdfExporter.cs:35-60`（PDF固有の描画分岐が無いこと）
- `src/Ecad2.Core/Rendering/SymbolGlyphs.cs:302, 309`（侍が発見したはみ出しの実数）
- `src/Ecad2.App/MainWindow.xaml:285`（AvalonDockのControlTemplate内`ClipToBounds`）
