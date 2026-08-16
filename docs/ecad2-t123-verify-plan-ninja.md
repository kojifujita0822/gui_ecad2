# T-123 残件（`.gcad` 関連付け）実機検証の段取り——忍者

作成: 2026-08-16、家老の下命（段取りを先に組んでおけ）による。
本書は着手前の計画にて、実測結果は含み申さぬ。ただし「インストール前の対照」だけは
本日のうちに採ってある（下記1節）——インストール後に採っても比べる相手が無いゆえ。

## 0. 段取りの位置

1. 侍が `.iss` を `HKA`（常にHKLM着地）へ改め、検証用インストーラーを生成
2. 隠密が静的レビュー
3. 殿がインストール（昇格を要するゆえ殿のお手）
4. **忍者が実測** ← 本書の範囲
5. 検証後、`.gcad` を GuiEcad へ戻す（下記5節。要裁可）

## 1. 【採取済み】インストール前の対照（2026-08-16、読み取りのみ）

インストール後の値と突き合わせるための素の状態にござる。

| 測ったもの | 値 |
|---|---|
| `C:\Program Files\Ecad2\` 等 | 不在（(x86)・LOCALAPPDATA\Programs も同じ） |
| `FindExecutable('.gcad検体')` | 戻り値 **31**（`SE_ERR_NOASSOC`）、結果は空文字列 |
| `AssocQueryString` id=1 COMMAND | `0x80070483`（ERROR_NO_ASSOCIATION） |
| 同 id=2 EXECUTABLE | `0x80070483` |
| 同 id=3 FRIENDLYDOCNAME | `GuiEcad 回路図ファイル` |
| 同 id=4 FRIENDLYAPPNAME | `0x80070483` |
| `HKLM\SOFTWARE\Classes\.gcad` | **キー自体が不在** |
| `HKCU\Software\Classes\.gcad` | キーは在るが既定値が未設定 |
| `HKCU\...\FileExts\.gcad\UserChoice` | キー不在 |
| 同 `UserChoiceLatest` | キーは在るが `Hash` のみ。**`ProgId` 値は無い** |
| 同 `OpenWithProgids` | `gcad_auto_file` / `GuiEcad.Document` / `Ecad2.Document` の三件が名だけ残存 |
| 同 `OpenWithList` | a=`PickerHost.exe` / b=`GuiEcad.App.exe` / c=`Ecad2.App.exe` |
| `HKLM\...\Classes\GuiEcad.Document` | 実在。cmd=`"C:\Program Files\GuiEcad\GuiEcad.App.exe" "%1"`（exe実在） |
| `HKCU\...\Classes\gcad_auto_file` | 実在。cmd=`"C:\Program Files\Gui_cad\GuiEcad.App.exe" "%1"`（**このパスは実在せぬ**） |
| `HKCR\Applications\GuiEcad.App.exe` | 実在。cmd=**`Gui_cad` の死んだパス**（5節で効いてくる） |
| `Ecad2.Document` | HKLM・HKCU とも不在（アンインストールで消えた） |

要は、`.gcad` は現在どのアプリにも解決され申さぬ。文書の呼び名だけが `GuiEcad.Document` の
残骸から引かれておる状態にござる。

### 測り方の落とし穴（本日踏んだ）

レジストリの既定値を `Get-ItemProperty -Name '(default)'` で問うと、キーが在っても
「不在」と返り申す。`(Get-Item $path).GetValue('')` で問い直すこと。
初回の測定はこれを誤り、HKCU 側のキーの実在を見落としかけ申した。

## 2. 観点別の測り方

### 観点A: 着地先が HKLM になったか（`HKA` 改修の直接の証）

- `HKLM\SOFTWARE\Classes\.gcad` の既定値（`GetValue('')` で）
- `HKLM\SOFTWARE\Classes\Ecad2.Document\shell\open\command`
- HKCU 側の残骸（既定値の空いた `.gcad` キー）が残るか消えるか
- `HKCR\.gcad` の統合ビューが HKLM・HKCU いずれを写しておるか

侍が `.iss` から割った機序（HKCRへ書くと既にHKCUに在るキーへ吸い寄せられる）が正しければ、
改修後は HKLM に着地し、HKCU の空キーは残ったまま「負けておる」状態になる筈。
**残骸が残ること自体は異常ではない**——どちらが勝つかが要点にござる。

### 観点B: `.gcad` の解決（非侵襲 → 侵襲の順）

**非侵襲（起動もダイアログも伴わぬ。二軸で採る）**

1. `FindExecutable`（shell32）——1節で対照済み。改修後に `C:\Program Files\Ecad2\Ecad2.App.exe`
   が返れば解決は通った証
2. `AssocQueryString` id 1〜8 の全掃き——同じく1節で対照済み

**侵襲（実際に起動する）**

3. `Start-Process '<検体>.gcad'`——ShellExecute 経由ゆえダブルクリックと同じ解決を通る

【限界・先に断る】`Start-Process` はエクスプローラーのダブルクリックと**完全に同一ではない**。
エクスプローラーは `UserChoice` を独自に検証する経路を持つ。8/14に決め手となったのも
殿の実ダブルクリックにござった。ゆえに**最終確認は殿のお手を要する**と見ており申す——
上記3までで通れば「殿のお手は一度きりで済む」形にはできる。

【関連付けが効いておらぬ場合】「アプリを選択してください」の画面が殿の画面に出申す。
出た場合は `Esc` で閉じる。**画面を占有する操作ゆえ、殿の在席を確かめてから3へ進むこと。**

### 観点C: 起動したのが Ecad2 か GuiEcad か

`ninja.md` および `ecad2-ui-automation` 6.8節の条（名前で絞らず全プロセスの増減で測れ）が
そのまま効く場面にござる。

- 操作**前**に全プロセス一覧を採る（`Get-Process` を名前で絞らず全件）
- 操作**後**に同じく採り、**差分で増えたものを見る**
- 増えたプロセスの `Path` と `StartTime` も採る（どの exe が起きたかを名でなく実体で同定）

`grep -i ecad2` の類は用いぬ——`GuiEcad.App.exe` に「ecad2」の並びは含まれ申さぬ。

### 観点D: 引数のファイルが実際に読み込まれたか

「入口が開いたことは通れたことではござらぬ」（同6.8節）——起動しただけでは通ったことにならぬ。
**結果側の指標**で測る。

検体＝`sample/normal-test-t123.gcad`（1373バイト）。中身は着手前に読んである：

| 期待値 | 中身 |
|---|---|
| 機器表（`DeviceTableGrid`） | **1行**（`Y8`、class=relay、quantity=1） |
| キャンバス | a接点（`contactNO`）が**1つ**、row=0・column=0 |
| シート | **1枚**、名は `シート1`、10行×20列 |
| 母線名 | 左 `N24` / 右 `P24`（既定の `L`/`N` 等と異なるゆえ弁別に使える） |

母線名が既定と違うのが好都合にござる——**空で起動した場合と、この検体を読んだ場合が
一目で分かれる。** 機器表1行と併せて二軸で判ずる。

## 3. 「測れる形か」の判定（家老の下問への答え）

**測れ申す。**ただし完全に殿のお手を要さぬわけではない。

- 解決そのもの（どのアプリに向いておるか）＝**忍者だけで測れる**。`FindExecutable` と
  `AssocQueryString` は起動もダイアログも伴わぬ
- 実起動と読み込み＝**`Start-Process` で測れる見込み**。ただし画面を占有しうるゆえ在席確認を要す
- エクスプローラーのダブルクリックと同一経路であることの最終確認＝**殿のお手が要る**

すなわち殿のお手は「インストール」と「最後のダブルクリック一度」の二度で足りる見込みにござる。

## 4. 実測の順序（画面占有を最後に寄せる）

1. レジストリ実測（観点A）——非侵襲
2. `FindExecutable` ＋ `AssocQueryString`（観点B前半）——非侵襲
3. ここまでで解決先が Ecad2 でなければ、**この時点で報告**（起動を試す前に）
4. 全プロセス一覧を採る（観点Cの前半）
5. 殿の在席を確かめ、`Start-Process` で起動（観点B後半）
6. 全プロセス差分・`Path`・`StartTime`（観点C後半）
7. UIA で機器表・母線名・シート名（観点D）
8. 殿へダブルクリックの最終確認をお願いする

## 5. 【要裁可】検証後、GuiEcad へ戻す手順

現在 `.gcad` がどのアプリにも関連付いておらぬのは、我らの検証が残した傷にござる。
戻すにあたり、下調べで**厄介な事実**が一つ出申した。

### 厄介な事実——「プログラムから開く」の GuiEcad は死んだパスを指しうる

`HKCR\Applications\GuiEcad.App.exe` の command は `C:\Program Files\Gui_cad\GuiEcad.App.exe`
にて、**このディレクトリは実在せぬ**（実在するのは `C:\Program Files\GuiEcad\`）。
`HKCU\...\Classes\gcad_auto_file` も同じ死んだパスを指しており申す。

一方 `HKLM\...\Classes\GuiEcad.Document` は生きたパスを持つ。

すなわち殿が「右クリック → プログラムから開く → 常にこのアプリを使う → GuiEcad」を選ばれた際、
**どちらの登録を掴むかで結果が分かれ申す。**死んだ側を掴めば、関連付けは付いても起動し申さぬ。

**これは実測せねば分からぬ**——推測にござる。

### 【実測で埋めた】生きた登録はどれか——殿へ名指しできる形

2026-08-16、家老の下命により追って調べた分にござる。読み取りのみ。

| 候補 | 在り処 | 指すパス | 生死 |
|---|---|---|---|
| `GuiEcad.Document` | **HKLM**\SOFTWARE\Classes | `C:\Program Files\GuiEcad\GuiEcad.App.exe` | **生きておる** |
| `gcad_auto_file` | **HKCU**\Software\Classes | `C:\Program Files\Gui_cad\…` | 死んでおる |
| `Applications\GuiEcad.App.exe` | **HKCU**\Software\Classes | 同上 `Gui_cad\…` | 死んでおる |
| `Ecad2.Document` | — | — | 不在 |

**死んだ登録は二つとも HKCU 側、生きた登録は HKLM 側**にきれいに分かれ申した。
`Applications\GuiEcad.App.exe` の `FriendlyAppName` は空にて、
「プログラムから開く」に出る表示名は exe のメタから引かれる形。
**同じ「GuiEcad」の名が複数並ぶ恐れがあるが、これは実測しておらぬ——推測にござる。**

### 【実測で埋めた】インストーラーの現物は在る。ただし版数に注意

`C:\Users\kojif\Desktop\生産物\gui_ecad\installer\` に五版が実在
（`1.0.4` / `1.0.5` / `1.0.6` / `1.0.7` / `1.0.61`。いずれも約71.8MB）。

**現在インストールされておるのは `1.0.6`**（`C:\Program Files\GuiEcad\GuiEcad.App.exe` の
`ProductVersion` = `1.0.6+83582ef…`、更新 2026-07-01 00:05:28）。
すなわち**版を動かさずに戻すなら `GuiEcad_Setup_1.0.6.exe`** にござる。
`1.0.61` を入れれば殿の GuiEcad の版が上がる——検証のための復旧が、別の変更を持ち込む形になり申す。

### 【最重要】GuiEcad の `.iss` も `Root: HKCR` を使うており申す

`installer\GuiEcad_Setup.iss:61`——

```
Root: HKCR; Subkey: ".gcad"; ValueType: string; ValueName: ""; ValueData: "GuiEcad.Document"; Flags: uninsdeletevalue
```

**侍が Ecad2 の `.iss` から割った機序（HKCRへ書くと、既にHKCUに在るキーへ吸い寄せられる）が、
GuiEcad の再インストールにもそのまま当てはまり申す。**
現況で `HKCU\Software\Classes\.gcad` のキーは実在するゆえ、
**再インストールしても HKCU 側へ着地する公算が高い。**

ただし**HKCU へ着地しても関連付けとしては働く**（HKCU が優先されるゆえ）。
戻し手として成立せぬわけではござらぬ——**着地先が HKLM にならぬ、というだけ**にござる。
`ChangesAssociations=yes` ゆえエクスプローラーへの通知も行われる。

### 再インストールに伴う副作用（殿へ先にお伝えすべきもの）

同 `.iss` より：

- `[InstallDelete] Type: filesandordirs; Name: "{app}"` ——**`C:\Program Files\GuiEcad\` を
  丸ごと削除してから入れ直す。** 殿の GuiEcad が一時的に消え申す
- `PrivilegesRequired=admin` ——殿のお手が要る
- `CloseApplications=yes` ——GuiEcad 起動中なら自動終了される
- `[Run]` ——完了後に GuiEcad が起動する（チェックを外せば起きぬ）

### 候補（いずれも家老・殿のご判断を仰ぐ）

| 案 | 中身 | 見立て |
|---|---|---|
| **(b) `GuiEcad_Setup_1.0.6.exe` を再実行** | 関連付けが書き直される | **最も確実。**現物あり・版も動かぬ。ただし `{app}` 丸ごと削除を伴い、殿のお手（昇格）を要す |
| (a) 殿が「プログラムから開く」で GuiEcad を選ぶ | 手は一度きり | 死んだ候補が二つ並んでおり、掴み誤る恐れ。選んだ後に `FindExecutable` で確かめれば判るが、誤れば選び直しになり申す |
| (c) レジストリを直接書く | 一発で戻る | **家老が禁じておられる。**候補として挙げるのみ |

**忍者の見立て＝(b) を推す。** (a) は手が軽い代わりに、死んだ登録を掴めば
「関連付けは付いたのに起動せぬ」という、最も判りにくい壊れ方をし申す。

**順序の注意**——Ecad2 の検証が済んだ後に Ecad2 をアンインストールすると、`.gcad` は
再び「どこにも向かぬ」状態へ戻る公算が高い（8/14がまさにそれにござった）。
ゆえに**戻す作業はアンインストールの後**に置かねば、二度手間になり申す。

## 6. 未確定・断っておくこと

- `FindExecutable` が `UserChoice`（HKCU FileExts）を尊重するかは**確かめておらぬ**。
  ゆえに `AssocQueryString` と二軸で採り、食い違えばその事実ごと報ずる
- `Start-Process` とエクスプローラーのダブルクリックの経路差も**実測しておらぬ**。
  上記3の断りはその前提に立つ
- 戻し手順(a)の見立ては推測にござる。実測で覆りうる
