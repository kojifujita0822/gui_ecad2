# T-133 GuiEcad「その他図形」原本調査（隠密、未了）

> 2026-07-27 隠密調査。**殿の打ち止めにより中断。ここまでの所見のみ。次セッションはDoD(1)後半・
> (3)・(4)から続けられたい。** 原本＝`C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App`。

---

## 進捗状況（次セッションへの申し送り）

- DoD(1)「原本の『その他図形』とは何か」＝**メニュー構造・構築ロジックは特定済み**。**個々の
  図形を列挙して数を出す作業が未了**（`OtherBuiltins`配列の全文未読）
- DoD(2)「原本のUI形式」＝**部分的に判明**（メニュー階層・配置操作の骨格は掴めたが、パラメータの
  持ち方は未確認）
- DoD(3)「ecad2側の現況」＝**未着手**
- DoD(4)「移植の規模見積」＝**未着手**

---

## 判明した事実（DoD1・2、途中まで）

### 「その他図形」の所在＝メニューバー「図形(G)」配下のサブメニュー

`MainPage.Parts.cs`の`RebuildShapeMenu()`（56-130行目）が構築する。

```
図形(G) メニュー
├─ その他図形（MenuFlyoutSubItem、63-64行目）
│    ├─ 組込みのその他記号（AddOtherBuiltins、MainPage.Tools.cs 237行目以降の
│    │    OtherBuiltins配列——T-131調査で一部確認済み。中身は未列挙、後述）
│    ├─ 組み込みパーツ（EmbeddedResource、_builtinParts。thermal-relay等）
│    ├─ （区切り）ピン留め済み自作図形（直接配置）
│    ├─ （区切り）
│    └─ 自作図形（サブメニュー、BuildCustomShapesSubItem）
├─ （区切り）
├─ 自作図形を作成...
├─ 自作図形を読み込んで編集...
├─ （区切り）
├─ 自作パーツをエクスポート (.gcadparts)...
└─ 自作パーツをインポート (.gcadparts / .gcadpart)...
```

**同じ内容が左パネルの「その他部品」ボタン（`OtherPartButton`、`MainPage.Tools.cs`側の
`RebuildOtherPartMenu()`）とも共有される**（`MainPage.Tools.cs`63-64行目コメント「左パネル
『その他▼』と同じ並び」）——上部メニューと左パレットの2箇所から同じ`OtherBuiltins`配列に
アクセスできる、という構造。

### 「組込みのその他記号」＝`OtherBuiltins`配列（`MainPage.Tools.cs`237行目以降）

T-131調査（`docs/ecad2-t131-guiecad-breaker-type-orient-ui-survey-onmitsu.md`）で確認済みの
一部：

```
("ブレーカ(NFB/MCCB/ELB) 縦", "Breaker3P#V"),
("ブレーカ(NFB/MCCB/ELB) 横", "Breaker3P#H"),
("電磁接触器 主接点 縦", "ContactorMain3P#V"),
("電磁接触器 主接点 横", "ContactorMain3P#H"),
("サーマル(OL) 2極 縦", "ThermalOverload3P#V"),
("サーマル(OL) 2極 横", "ThermalOverload3P#H"),
```

**注意：これはT-131調査時にBreaker3P/Orient関連の抜粋として引いた6件であり、`OtherBuiltins`
配列がこの6件で全部かは未確認。** コメント「『その他図形』の組込み記号（基本記号 a接点〜端子台
は含めない）」（237行目）から、基本図形（a接点・b接点・コイル・端子台等、ツールバー常設ボタン
分）を除いた、それ以外の全記号がここに列挙されている可能性が高い。**次セッションは
`MainPage.Tools.cs`の`OtherBuiltins`配列を238行目から終端まで全文読み、1件ずつ列挙してから
数を出すこと**（`onmitsu.md`調査ワークフロー「数を報告する前に個々の項目を列挙してから合計を
出す」【MUST】、**T-131調査書からの転記のみで済ませず、必ず一次ソースで再確認する**——
`onmitsu.md`同ワークフロー「転記と再現は別物」）。

### 未確認（次セッションの探索経路）

- `OtherBuiltins`配列の全文（`MainPage.Tools.cs`238行目〜、終端行未確認）
- 配置操作の詳細（クリック配置かドラッグか、パラメータ入力の有無）——T-131調査によれば
  「メニュー選択→キャンバスクリックで配置」という骨格は確認済みだが、「その他図形」全種に
  共通する形式かは未確認
- パラメータの持ち方——`Breaker3P#V`のような`Kind#Orient`タグ形式が全種共通か、種別によって
  異なる形式があるか未確認
- ecad2側の現況（Core層モデル・Rendering層に対応する型が存在するか）は`ElementKind` enum・
  `ElementCatalog`等をecad2側で確認する必要がある（本調査は未着手）

---

## 次セッションが最初にやること

1. `MainPage.Tools.cs`の`OtherBuiltins`配列を全文読み、図形を1件ずつ列挙して数を確定する（DoD1完了）
2. 各図形の配置操作・パラメータ形式を確認する（DoD2完了）
3. ecad2側（`src/Ecad2.Core/Model/ElementCatalog.cs`等）でDoD1で列挙した図形が存在するか照合する（DoD3）
4. Core/Rendering/Appそれぞれで何が要るか見積もる（DoD4）

---

## 出典（現時点）

- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Parts.cs`（全文441行、確認済み）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Tools.cs`（237-252行目のみ確認済み、
  全文266行のうち未読部分あり）
- `docs/ecad2-t131-guiecad-breaker-type-orient-ui-survey-onmitsu.md`（前段調査、OtherBuiltins
  6件の抜粋元）
