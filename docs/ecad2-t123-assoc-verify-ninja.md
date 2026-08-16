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

## 3. 測定結果（13:21）——(C) と (D) が的中

| 項 | 実測 | 予測との対応 |
|---|---|---|
| `UserChoice` | **不在** | **(C)** |
| `UserChoiceLatest` | `Hash = /UYkoly0L7A=` のみ。**`ProgId` は再生成されておらぬ** | **(C)** |
| `OpenWithProgids` | `gcad_auto_file` / `GuiEcad.Document` / `Ecad2.Document`（前と同一） | — |
| `OpenWithList` | a=`PickerHost.exe` b=`GuiEcad.App.exe` c=`Ecad2.App.exe`、**`MRUList` が `cba` → `bca`** | — |
| GuiEcad の `Path` | **`C:\Program Files\GuiEcad\GuiEcad.App.exe`**（生きた方）、PID=19708、13:17:52 | **(D)** |

**`MRUList` の変化が、殿のダブルクリックが `b`（GuiEcad）を選んだ記録**にござる。
ただし**開いた後の記録ゆえ、原因ではなく結果**。

## 4. 【最も重い一事】忍者の仮説は誤りであった

分岐(C) に書いたとおり——**`FindExecutable` の `err=31` は実態を正しく写しておった。**

忍者は「測定手段の側の問題」と見て、`.gcad` の HKCU 空キーが `FindExecutable` を
**誤らせておる**という仮説を立てた。**実際は誤らせておったのではなく、正しく
「関連付けなし」を報じておった。**

**疑うべきはレジストリ二軸の方であった**——`assoc`／`ftype`／統合ビューは
**「書かれておる」ことを示せても「シェルがそれを使う」ことまでは示せておらぬ。**

**朝に「読み取り時の解決を測ったにすぎぬ」と断りを置いておきながら、
その断りの外側を測らぬまま「関連付けは完全に成立しており申す」と報じた**——
**断りを置くことと、断りの外側へ踏み込まぬことは別**にござった。
`ninja.md`「射程を断っても、断った先の記述精度が伴わねば意味を成さぬ」の、
射程の外側版と言える。

## 5. 新たな仮説（未検証と明記する）

**シェルの ProgId 解決は、HKCU の `.gcad` 空キーで打ち切られておるのではないか。**

1. HKCU に `.gcad` キーが在り、既定値が無い
2. シェルは HKCU を先に見る → 既定値が無い → **そこで打ち切り、HKLM へ落ちぬ**
3. ゆえに `OpenWithProgids`／`OpenWithList` の側で解決し、GuiEcad へ至る

**`.eml` とも整合する**——`.eml` も HKCU に空既定値のキーを持ち、
`FindExecutable` は ProgId（`Outlook.File.eml.15`）ではなく **thunderbird** を返しておった。
**すなわち `.eml` も ProgId 経由では解決しておらぬ。**
本日「`.ext` の既定値さえ在れば解決は通る」と結論したのは、
**レジストリの読み取りについては正しく、シェルの解決については誤り**であった。

**侍の懸念は正しかった**——ただし「キー単位で覆い隠す」という機序ではなく、
**「シェルの解決が HKCU の空キーで打ち切られる」**という形で。
**レジストリの読み取りは値単位でマージされるが、シェルの解決は別の機構にござる。**

## 6. 手当ての候補（いずれも裁可を要す）

| 案 | 中身 | 見立て |
|---|---|---|
| (i) | `HKCU\Software\Classes\.gcad` の空キーを取り除く | **仮説を検めるにも最も安い。**一手で決する |
| (ii) | `.iss` で HKCU 側にも書く | `HKA` の意義が薄れる |
| (iii) | `.iss` で当該 HKCU キーを消す指定を加える | 他環境にも効くが、他アプリの登録を消す恐れを検める要あり |

**(i) を試せば仮説は一手で決し申す**——空キーを除いて `FindExecutable` を測り直すのみ。
**されど書き換えゆえ、殿の裁可を待つ。**

## 6-2. 【測る前に書き置く・三度目】空キーを除いた後、何が見えれば何が言えるか

殿ご裁可により `HKCU\Software\Classes\.gcad` の空キーのみ除いて測り直す。
**本節は削除の前に書いたものにござる。**

### 予測1（忍者の本命）＝`FindExecutable` が `C:\Program Files\ecad2\Ecad2.App.exe` を返す

→ **新仮説が確定する。** 「シェルの ProgId 解決は HKCU の空キーで打ち切られる」。

**帰属**＝`.iss` の `HKA` 改修は正しく、瑕疵は無い。
**遮っておったのは既存環境に残った空の殻**にござる。
手当ては環境側の掃除、あるいは `.iss` で当該キーを除く指定を加える筋へ移る。

**併せて言えること**＝新規環境（空キーが無い）では**元より通っておった**ことになり申す。
すなわち T-123 の残件「新規環境でダブルクリックしてEcad2が開くか」は、
**殿の環境が新規環境でなかったがゆえに再現できなんだ**という形。

### 予測2＝なお `err=31` を返す

→ **新仮説も外れ。** 原因は別の層に在る。候補は三つ。

- **(a) シェルのキャッシュ**——`ChangesAssociations=yes` の通知が届いておらぬ、
  あるいはプロセスを跨いで古い解決が保たれておる
- **(b) `FindExecutable` は元より ProgId 経由を見ぬ API である**——
  本日測った `.rar`／`.eml`／`.cbz` は**いずれも ProgId の既定値とは違う答え**を返しており、
  三つとも `OpenWith` 系で解決しておった疑いがござる。
  **もしそうなら `FindExecutable` は本件の判定に元より使えぬ**
- **(c) `.gcad` 固有の何か**——`NoOpen` 値等

**とりわけ (b) が重い**——**その場合、忍者が主軸に据えた三軸のうち二軸目までが落ちる**
（朝に `AssocQueryString` が落ち、次に `FindExecutable` が落ちる形）。
**残るのはレジストリ直読のみとなるが、それは「書かれておる」しか示せぬ。**
**すなわち実態を決するのは殿のダブルクリックただ一つ**という所へ戻り申す。

### 予測3＝`HKCR\.gcad` の統合ビューは変わらず `Ecad2.Document`

HKCU にキーが無くなれば HKLM がそのまま見える。自明に近いが、
**変わらぬことを確かめておかねば「除いたせいで別の何かが壊れた」を排除できぬ。**

### 対照を併せて採る

`.txt`／`.cbz` も同時に測る。**手段自体が働いておることを毎回確かめる**
——本日、測定手段が二度まで違う答えを返しておるゆえ。

## 7. 操作手法・現況

- 測定はすべて読み取りのみ。書き換えは一切行うており申さぬ
- `FindExecutable`／`cmd` の `assoc`・`ftype`／レジストリ直読の三軸。
  `AssocQueryString` は本日の実測で使えぬと判じ、用いており申さぬ（段取り書1節）
- Ecad2 のプロセスは見当たらず（家老が起こされた PID=16624 は既に不在）。
  GuiEcad が PID=19708 で走っておる。いずれも忍者は手を触れておらぬ
