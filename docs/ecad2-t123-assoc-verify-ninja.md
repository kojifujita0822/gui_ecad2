# T-123 `.gcad` 関連付けの実機確認——忍者

対象＝`b483ab5`（`.iss` の書き込み先を HKCR から HKA へ改める）。検証日 2026-08-16。
段取りは `docs/ecad2-t123-verify-plan-ninja.md`。

## 1. ここまでに判っておること（2026-08-16 13:10〜13:18）

| 軸 | 答え |
|---|---|
| `HKLM\SOFTWARE\Classes\.gcad` 既定値 | `Ecad2.Document` |
| 同 `Ecad2.Document\shell\open\command` | `"C:\Program Files\ecad2\Ecad2.App.exe" "%1"` |
| `HKCR\.gcad`（統合ビュー） | `Ecad2.Document` |
| `cmd` の `assoc .gcad` | `.gcad=Ecad2.Document` |
| `cmd` の `ftype Ecad2.Document` | `"C:\Program Files\ecad2\Ecad2.App.exe" "%1"` |
| `FindExecutable` | **`err=31`（NOASSOC）**。対照 `.txt`→Notepad／`.cbz`→NeeView は正常 |
| **殿のダブルクリック** | **GuiEcad が開いた** |

**着地そのものは成功しており申す。**HKCU 側は触られておらず、予測（`.eml` と同じ形ゆえ
統合ビューに HKLM が出る）も的中した。**にもかかわらず実操作では GuiEcad が開く。**

## 2. 【測る前に書き置く】何が見えれば何が言えるか

**家老の下命により、以下を測る前に書いたものにござる。**
本日二度、先に予測を置く作法が効いたゆえ、ここでも同じくいたす。

### 分岐(A) `UserChoice` キーが生まれ、`ProgId = GuiEcad.Document` を持つ

→ **Windows がユーザー選択を作り直した。** エクスプローラーは `UserChoice` を最優先ゆえ、
これが GuiEcad を開いた直接の原因と言える。HKLM の `.gcad` 既定値は**上位に負けておる**。

**帰属**＝`HKA` 改修は正しく効いており、`.iss` に瑕疵は無い。
**届かぬのは Windows の既定アプリ機構が上位に在るゆえ**にござる。
手当ての筋は `.iss` ではなく、既定アプリの設定側へ移る。

### 分岐(B) `UserChoice` は無いが `UserChoiceLatest\ProgId` が再生成されておる

→ Win11 の新しい機構が働いた形。上位で GuiEcad を指す点は (A) と同じ。**帰属も (A) と同じ。**

### 分岐(C) `UserChoice` も `UserChoiceLatest\ProgId` も無い（インストール前と同じ `Hash` のみ）

→ **ユーザー選択は関与しておらぬ。** ならば GuiEcad が開いた原因を別に求めねばならぬ。

**この場合、`FindExecutable` の `err=31` は実態を正しく写しておったことになる**
——すなわち**「測定手段の側の問題」と見た忍者の仮説（`.gcad` の HKCU 空キーが
`FindExecutable` を誤らせておる）が誤り**であったことになり申す。

**二つの独立した観測（`FindExecutable` と実操作）が同じ方向を指す**以上、
疑うべきは**レジストリ二軸の方**——`assoc`/`ftype`/統合ビューが「書かれておる」ことは示せても
「シェルがそれを使う」ことまでは示せておらぬ、という筋にござる。

**忍者は (C) を最も重く見る。** `FindExecutable` の err=31 と実操作の GuiEcad が符合するゆえ。

### 分岐(D) GuiEcad プロセスの `Path`

- `C:\Program Files\GuiEcad\`（生きた登録）＝`HKLM\...\GuiEcad.Document` が使われた筋
- `C:\Program Files\Gui_cad\`（死んだパス）＝**起動し得ぬ**ゆえ、起動しておるならこちらではない

**予測＝`GuiEcad\`（生きた方）。** これが出れば、
「`.gcad` → `GuiEcad.Document`（HKLM）」という経路が今も生きて使われておることになる。

**そしてそれは奇妙にござる**——`HKLM\...\.gcad` の既定値は今 `Ecad2.Document` に書き換わっており、
`GuiEcad.Document` を指す `.gcad` の登録は**どこにも残っておらぬ筈**。
ゆえに (D) が予測どおりなら、**シェルは `.gcad` の既定値以外の何かを見て GuiEcad へ至っておる**。
その何かの候補＝`OpenWithProgids`／`OpenWithList`／`Applications\GuiEcad.App.exe`。

### 併せて観る＝`OpenWithProgids`／`OpenWithList` の差分

インストール前＝`OpenWithProgids` に三件（`gcad_auto_file`／`GuiEcad.Document`／`Ecad2.Document`）、
`OpenWithList` に `PickerHost.exe`(a)／`GuiEcad.App.exe`(b)／`Ecad2.App.exe`(c)、`MRUList=cba`。

**`MRUList` の順が変われば、殿のダブルクリックが記録された証**になり申す。

---

（以下、測定結果を追記する）
