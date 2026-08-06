# T-133 原本GuiEcad突合調査（モータ意匠・組み込みパーツ・基準枠基準点）（隠密、2026-07-28）

家老采配6。**原本ソース実在（`C:\Users\kojif\Desktop\生産物\gui_ecad\`）を受け、T-133計画書§4-2・§5-4が
「原本不在」を理由に留保していた箇所を突合した。調査のみ、意匠の採否は殿の裁定事項。**

**すべて一次ソース直読**（推測を要した箇所はその旨明記）。

---

## 0. 総括

| 留保事項 | 結論 |
|---|---|
| (1) 縦向きモータの意匠 | **原本にも存在せず**。GuiEcadのモータは横向き1種のみ、V/H選択の器自体が無い |
| (2) 組み込みパーツ2件の中身 | **完全な定義を確認**（`.gcadpart`実体、ports・primitivesとも） |
| (3)【副次】基準枠の基準点 | **中心基準と確定**（`port-integer-and-template-audit-onmitsu.md`の「不明」を解消） |

---

## 1.【DoD1】縦向きモータの意匠——原本にも無い

### 1-1. GuiEcadのモータは向き選択の器そのものを持たない

`MainPage.Tools.cs`の`OtherBuiltins`配列（全9件、全文直読済み）——

```
("セレクトSW", "SelectSwitch"),
("サーマル(OL)", "ThermalOverload"),
("非常停止", "EmergencyStop"),
("三相モータ", "Motor"),                                    ← V/H無し、単一エントリ
("ブレーカ(NFB/MCCB/ELB) 縦", "Breaker3P#V"),
("ブレーカ(NFB/MCCB/ELB) 横", "Breaker3P#H"),
("電磁接触器 主接点 縦", "ContactorMain3P#V"),
("電磁接触器 主接点 横", "ContactorMain3P#H"),
("サーマル(OL) 2極 縦", "ThermalOverload3P#V"),
("サーマル(OL) 2極 横", "ThermalOverload3P#H"),
```

**Breaker3P・ContactorMain3P・ThermalOverload3Pの3種は、いずれも「縦」「横」の2エントリを持つ
（タグに`#V`/`#H`付き）。モータだけが単一エントリで、向き選択の余地が無い。**

`SymbolGlyphs.cs:43`（switch式）でも`Motor(r, s, width, cell)`と**orient引数を渡していない**
（ecad2の現状と完全に同一の呼び出し形）。`Motor()`本体（`:219-238`、全文直読）も向き分岐を持たぬ
単一の描画ロジックのみ。

**帰結＝殿裁定5「縦向きモータ図形を新規追加」は、GuiEcadにも踏襲元が存在しない、ecad2独自の
新規意匠**。侍の計画書§4-2(a)が「原本の意匠を踏襲すべきだが」と書いた前提自体が成り立たず、
**「踏襲すべき原本の絵が無い」という結論**になる。第一案（既存横向きモータを90度回した形）を
たたき台に**殿へ意匠そのものをご覧いただいて確定する**という侍の申し送りは、そのまま生きる
（ただし「原本と付き合わせて確認」ではなく「ecad2独自の意匠として殿に決めていただく」という
位置づけになる）。

### 1-2. 【副次】モータのポート/描画直交はGuiEcad原本由来——ecad2の移植ミスではない

`ElementCatalog.cs:24-29`（GuiEcad）——

```csharp
ElementKind.Motor => new[]
{
    new PortDef("U", 0, 0),
    new PortDef("V", 0, 1),
    new PortDef("W", 0, 2),
},
```

**ports は全て`RowOffset=0`（同一行・横並び）**——**ecad2の定義（`ElementCatalog.cs:24-29`）と
1文字違わず一致**（コメント「三相 U/V/W の3端子を横並びに確保（暫定レイアウト）」も同文）。

一方`Motor()`本体（`:219-238`）の描画は`foreach (var y in new[] { -1.0, 0.0, 1.0 })`で
**端子3個を縦（y=-1/0/1）に描く**——**ポート（横並び）と描画（縦並び）が、GuiEcad原本の時点で
既に直交しておる**。

**これは既存の隠密調査（`docs/ecad2-t133-height-and-port-pitch-survey-onmitsu.md`§5-6）が
「バグか意図かは不明」と留保した論点に対する新情報**——**ecad2の移植時に生じた食い違いではなく、
GuiEcad原本がそもそも抱えていた性質**と確定した。`DefaultCellWidth`のコメント「暫定レイアウト」
という語自体が、GuiEcad原本の作者も仮置きと認識していたことを示唆する（**推測**）。

**含意**＝この直交を「直す」対処を採る場合、**「ecad2独自のバグ修正」ではなく「原本から引き継いだ
既知の粗を、移植の機会に正すか」という性質の判断**になる。対処の要否・範囲は引き続き殿・家老の
判断事項。

---

## 2.【DoD2】組み込みパーツ2件（thermal-relay a/b）の中身——完全な定義を確認

**実体ファイル**＝`GuiEcad.App/Assets/Parts/thermal-relay-{a,b}.gcadpart`
（`EmbeddedResource`として`GuiEcad.App.csproj:36-40`で埋め込み、`MainPage.xaml.cs:56-57`で
`GuiEcad.App.thermal-relay-{a,b}.gcadpart`のロジカル名として読込）。

### thermal-relay-a（サーマルリレ-a、role=`contactNO`）

| 項目 | 値 |
|---|---|
| サイズ | 幅1・高さ1セル |
| ポート | `L`(row0,boundary0)・`R`(row0,boundary1)——通常のa/b接点と同型の2端子水平 |
| 図形 | 円2個（端子、cx=0.125/0.875・r=0.125）＋横線1本（y=-0.1875、端子間）＋×印1組
  （y=-0.3125〜-0.0625の交差2本、**中心は横線と同じy=-0.1875——横線をまたぐ形で配置**） |

### thermal-relay-b（サーマルリレーｂ、role=`contactNC`）

| 項目 | 値 |
|---|---|
| サイズ | 幅1・高さ1セル（aと同一） |
| ポート | `L`(row0,boundary0)・`R`(row0,boundary1)（aと同一） |
| 図形 | 円2個（同位置・同半径）＋横線1本（y=+0.1875）＋×印1組
  （y=+0.0625〜+0.3125、**中心は横線と同じy=+0.1875——横線をまたぐ形で配置（aと同型）**） |

**a/bの違いは横線自体のy位置のみ**（aはy=-0.1875、bはy=+0.1875）——**×印はどちらも横線を
またぐ形で横線と中心を揃えて配置され、×印自体の上下配置がaとbで異なるわけではない**
（【2026-08-06訂正】初版は「×印が横線の上/下に配置」と書いたが誤り、侍の実装時実測で判明。
数値自体は正しく採っていたが、×印の中心座標(-0.1875/+0.1875)が横線のy座標と一致することを
見落とし「上/下」と誤読した）。通常の接点記号に、この横線位置違いの×印を足したものが
サーマルリレー記号という構成にござる。

**表記ゆれに気づいた（副次所見）**＝`name`フィールドが**「サーマルリレ-a」（長音「ー」欠落）／
「サーマルリレーｂ」（全角小文字ｂ）**——原本自体に軽微な表記ゆれがある。ecad2側で移植する際は
機械的にコピーせず、正しい表記（「サーマルリレー」・半角a/b）に整えるのが妥当と考える
（**判断は侍・家老に委ねる**）。

**ポート・サイズとも既存のecad2 a接点/b接点（幅1・高さ1・L/R2端子水平）と完全に同型**——
**移植は既存のContactNO/ContactNC相当の器へ、図形定義（primitives）を差し替えるだけで済む見込み**
（**推測、実装規模の見積りは侍の領分**）。

---

## 3.【副次・DoD3】基準枠の基準点——「不明」を解消、中心基準と確定

`docs/ecad2-t133-port-integer-and-template-audit-onmitsu.md`§8「不明と明示する」節が
「**GuiEcad原本の基準枠描画が上端基準か中心基準か——原本ソースが当環境から参照できぬ**」と
留保していたが、**原本実在によりこれも解消できた**。

`PartEditorWindow.xaml.cs`（GuiEcad、全956行中の該当箇所を直読）——

```csharp
private double Sx(double cx) => _originX + cx * _cellPx;
private double Sy(double cy) => _originY + (cy + _h / 2.0) * _cellPx;
```

**`Sy`の変換式に`+ _h / 2.0`が入っている**——モデル座標`cy=0`（ポート・図形定義の原点）が、
スクリーン座標では`_originY + (_h/2)*_cellPx`、すなわち**基準枠の上端(`_originY`)と下端
(`_originY + _h*_cellPx`)のちょうど中間**に写像される。

基準枠自体の描画（`:265`）も`ds.DrawRectangle(Sx(0), Sy(-_h/2.0), _w*_cellPx, _h*_cellPx, ...)`で、
**上端を`Sy(-_h/2.0) = _originY`（オフセット0）に置く**——`cy=0`が中心となるよう`Sy`が
設計されておることと整合する。

**帰結＝GuiEcad原本の基準枠は中心基準（縦方向）**。**Sxには`_w/2`のオフセットが無く、横方向は
左境界基準**——これは`docs/ecad2-t133-height-and-port-pitch-survey-onmitsu.md`が既に確立した
「横は境界基準・縦は中心基準」という非対称と完全に符合する。

**含意**＝T-133§4-1で殿裁定済みの「基準枠の基準点＝中央」（`PartEditorCanvas.cs:674`の改修対象）は、
**GuiEcad原本の設計と一致する方向**。原本追認の材料として申し送るに値する
（**すでに裁定済みの事項の後付け確認であり、裁定を動かすものではない**）。

---

## 4.【DoD3】T-133の他の「原本不明」留保の見直し——上記3件で尽きる

`docs/ecad2-t133-implementation-plan-samurai.md`・`docs/ecad2-t133-port-integer-and-template-audit-onmitsu.md`
の2文書を「原本.{0,15}(不在|不明|未確認)」で機械的に検索し、ヒットした4箇所を直読した
（再現手段＝`grep -rn "原本.*不明\|原本.*不在\|原本.*未確認" docs/ecad2-t133-*.md`）。

- 実装計画書§4-2(a)・§5-4の2箇所＝**本調査の§1・§2で解消**
- 監査書§8の1箇所＝**本調査の§3で解消**

**他の「不明」（モータ直交がバグか意図か、殿が実際に枠のずれに気づかれたか等）は原本ソースの
有無とは無関係の論点であり、本調査の対象外**（原本を読んでも答えが出ない性質の問い）。

---

## 出典

- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Tools.cs:238-262`（`OtherBuiltins`全文）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.Core\Rendering\SymbolGlyphs.cs:43, 219-238`
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.Core\Model\ElementCatalog.cs:1-38`（全文）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\Assets\Parts\thermal-relay-a.gcadpart`（全文）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\Assets\Parts\thermal-relay-b.gcadpart`（全文）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\GuiEcad.App.csproj:36-40`
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.xaml.cs:56-57`
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\PartEditorWindow.xaml.cs:31, 98-99, 195-196, 265`
- `docs/ecad2-t133-implementation-plan-samurai.md:426, 458-459`
- `docs/ecad2-t133-port-integer-and-template-audit-onmitsu.md:322-329`
- `docs/ecad2-t133-height-and-port-pitch-survey-onmitsu.md`（既存調査、横境界基準・縦中心基準の
  非対称の初出。本調査§3の傍証として参照）

## 不明点

- サーマルリレー記号の×印（primitives内の交差2本）が業界標準の記号としてどこまで正確かは
  電気図面の専門知識を要し本調査の範囲外
- ポート・図形とも直読で確認したが、**GuiEcad側でこの2部品が実機上どう見えるか（実際の描画結果）は
  未検証**（本調査はソースコード直読のみ、GuiEcad.App.exeの起動確認はしておらぬ）
