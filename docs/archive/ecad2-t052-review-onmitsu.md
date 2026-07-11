# T-052 コードレビュー記録（隠密）

対象: コミット `9393b8f`（DRC-PART-001 未解決PartId警告の追加、P-017案A採用・殿裁定2026-07-10）
変更ファイル: `src/Ecad2.Core/Simulation/DesignRuleCheck.cs`・`src/Ecad2.App/ViewModels/OutputPanelViewModel.cs`・`tests/Ecad2.Core.Tests/DesignRuleCheckPartIdTests.cs`（新規テスト3件）

手法: `code-review`スキル（medium）を8方向finder（並列agent）→ dedup → 1票verify（並列agent）で実施。手動観点3点は家老指示分。

---

## 1. 手動観点（家老指定）

1. **DRC文言・Severity=Warningが殿裁定文言通りか** — 一致確認。
   `docs/archive/ecad2-uiux-proposals-p017-p020-p023-onmitsu.md` P-017案Aの文言例
   「機器 {name}: 部品参照が見つからず、a接点として扱われています。部品の再選択をご確認ください。」と
   `DesignRuleCheck.cs:281`の実装が完全一致。Severity=Warningも一致（`DesignRuleCheck.cs:280`）。
2. **既存ジャンプ機構の流用のみで新規UI無し（変更3ファイルの妥当性）** — 妥当。
   新規診断コード`DRC-PART-001`を既存`Diagnostic`型・`OutputPanelViewModel.RunDrc()`・`JumpTo()`にそのまま
   乗せているのみで、XAML等の新規UIファイル追加は無し。変更3ファイルは実装1＋呼び出し1＋テスト1で妥当。
3. **正常ContactNOでの誤検知が起きない設計か** — 起きない設計と確認。
   `PartId`が`null`/空（組込み種別を直接指定した要素）は`string.IsNullOrEmpty`ガードで対象外
   （`DesignRuleCheck.cs:275`）。正常な自作パーツ（`PartLibrary`で解決可能）も`lib?.Get(...) is not null`で
   対象外（同276行）。基本図形（`BasicPartTemplates`）も`PartFolderStore.SeedBasics()`経由で通常の
   `PartLibrary`に登録されるため、正常配置時に誤検知する経路は無い。

## 2. code-reviewスキルの指摘（8 finder → verify後）

### CONFIRMED（要修正候補、3件）

| # | file:line | 指摘 | 失敗シナリオ |
|---|---|---|---|
| 1 | `OutputPanelViewModel.cs:96-98` | `JumpTo`は`DeviceName`空時に列を見ず「行内先頭要素」へフォールバックする。`CheckUnresolvedPartId`は既存チェック（`CheckCrossReference`/`CheckDeviceTypeConsistency`）と異なり`DeviceName`空要素も除外せず警告を出すため、誤ジャンプが起きる。 | 同一行に名前付き要素(col=1)と、DeviceName未入力のPartId未解決要素(col=5)が並ぶ図面で、DRC-PART-001診断をクリックするとcol=1へ選択が移り、実際に問題のある要素には辿り着かない。DeviceName未入力のまま自作パーツを配置することはUI操作上通常に起こりうる（`MainWindowViewModel.cs:1456`で空欄→null許容）。 |
| 2 | `DesignRuleCheck.cs:278-282` | `DeviceName`空要素の警告文言が `"機器 : 部品参照が見つからず..."` という不自然な表示になる。 | DeviceName未入力の自作パーツ配置直後にDRC実行すると、出力パネルに機器名が特定できない文言が表示される。同ファイル内`CheckSeriesCoils`は`"(無名)"`フォールバックを既に持っており、揃える選択肢があった。 |
| 3 | `DesignRuleCheck.cs:276` | 「PartId解決可否」判定 `lib?.Get(elem.PartId) is not null` が、`PartResolver.cs`のPorts/CreatesComponent/ComponentKind内に既にある同一判定を経由せず独自に複製している。 | 共通ヘルパー(`PartResolver.IsResolved`相当)が無いため、将来「解決」の定義が変わった際に4箇所同時修正が必要になり、修正漏れで警告の出/不出が実挙動とズレるリスクがある。 |

### PLAUSIBLE（経過観察・気づきとして記録、4件）

| # | file:line | 指摘 | 備考 |
|---|---|---|---|
| 4 | `DesignRuleCheck.cs:276` | `PartLibrary.Get`は大文字小文字を区別する既定Dictionary比較。 | 現状PartIdは固定小文字定数かGuid小文字16進のみで生成され、揺れの発生経路は無し（verify確認済み）。手動JSON改ざん等の外部要因を想定した場合のみの理論上リスク。 |
| 5 | `DesignRuleCheck.cs:271` | `CheckUnresolvedPartId`が3つ目の独立した全文書走査を追加。 | 既存2チェックも同型の重複関係にあり新規逸脱ではない。ユーザー起点のオンデマンド実行・要素数規模から実害は無視できる水準。 |
| 6 | `DesignRuleCheck.cs:271` | `doc.Sheets.OrderBy(...)`が同一`RunDrc()`内で3回実行される。 | 同上、シート数規模から実行時間への影響は理論上の指摘に留まる。 |
| 7 | `DesignRuleCheck.cs:281` | 文言「a接点として扱われています」は`PartResolver.ComponentKind`の「未解決時は`e.Kind`をそのまま返す」実装と「`e.Kind`の既定値は常にContactNO」という現状の呼び出し規約の両方への暗黙依存。 | 現行コードベースでは`Kind`を明示設定する経路が無く（生成2箇所とも未設定）、今すぐ矛盾は起きない。ただし将来`Kind`明示設定経路が追加されると文言と実態が乖離しうる設計上の脆さとして記録。 |

## 3. 結論

殿裁定文言・Severity・実装範囲（3ファイル・新規UI無し）は指示通り。**#1（JumpTo誤ジャンプ）・#2（文言不自然）は
DeviceName未入力状態という通常のUI操作経路から到達するため、要修正が妥当**（DRC-PART-001固有の新規劣化では
なく既存パターンの踏襲だが、実際にユーザーが引く不具合として顕在化する）。#3（判定ロジック重複）は保守性の
軽微な改善提案。#4〜7は経過観察のみで着手不要。

---

## 4. 往復1周目修正の再レビュー（コミット `d4bad3d`）

家老采配（再レビュー、2026-07-11）の3観点を確認。手動照合＋`dotnet build --no-incremental`→
`dotnet test`実測。T-055増分3往復レビューの前例に倣い、往復修正の再レビューはfull code-reviewスキル
再実行ではなく手動照合＋ビルド/テスト実測とした。

### 4-1. 指摘#1の修正（JumpTo誤ジャンプ）

`OutputPanelViewModel.JumpTo`のシグネチャを`(CircuitRef, string)`から`(Diagnostic)`へ変更し、
DeviceName不一致時のフォールバック順序を「①DeviceName一致要素 → ②（`diagnostic.Code ==
DesignRuleCheck.UnresolvedPartId`の場合のみ）`PartResolver.IsUnresolvedPartId`で実際に未解決の要素
→ ③行内先頭要素」の3段構成に変更（`OutputPanelViewModel.cs:96-101`）。他のDRCコードでは②が`null`に
畳み込まれ、③まで素通りするため既存挙動を保つ設計。新規テスト2件
（`OutputPanelJumpToTests.cs`）でこの2系統（DRC-PART-001での優先ジャンプ／他コードでの従来フォールバック
維持）を確認しており、往復1周目レビュー指摘の再現シナリオを正しく突いている。**適切。**

### 4-2. 指摘#2の修正（"(無名)"表記統一）

`DesignRuleCheck.CheckUnresolvedPartId`で`displayName`（メッセージ表示用、空なら"(無名)"）と
`name`（`Diagnostic.DeviceName`保持用、空文字のまま）を分離（`DesignRuleCheck.cs:278-281`）。
`Diagnostic.DeviceName`を書き換えなかったのは正しい判断——書き換えるとJumpToのDeviceName一致判定
（`string.Equals(e.DeviceName, deviceName, ...)`）が`"(無名)"`という文字列と実要素のDeviceNameを
比較することになり、一致し得ない別の不具合を生んだはず。新規テストで両者が分離されていることを
明示的に確認済み（`DesignRuleCheckPartIdTests.cs`新規1件）。**適切。**

### 4-3. 指摘#3の修正（判定ロジック一本化）

`PartResolver.IsUnresolvedPartId(ElementInstance, PartLibrary?)`を新設し、`DesignRuleCheck.
CheckUnresolvedPartId`と`OutputPanelViewModel.JumpTo`の両方から呼ぶ形に統一（`PartResolver.cs:36-39`）。
論理式`!string.IsNullOrEmpty(e.PartId) && lib?.Get(e.PartId) is null`は旧`CheckUnresolvedPartId`の
2行continue判定と同値（ド・モルガンの法則で確認）。往復1周目レビュー時点の指摘は「`PartResolver.cs`
内のPorts/CreatesComponent/ComponentKindとの重複」だったが、今回統合されたのは「DesignRuleCheckと
JumpTo間の重複」（2箇所→1箇所）であり、Ports等3メソッドとの統合はスコープ外のまま。ただしPorts等は
「解決結果を使って何かする」処理で「解決可否のみを問う」IsUnresolvedPartIdとは役割が異なるため、
今回の統合範囲は妥当と判断する。新規テスト4件（`PartResolverTests.cs`、PartId未解決/解決/null/lib null
の同値分割）で境界も確認済み。**適切。**

### 4-4. ビルド・テスト実測

`dotnet build src/Ecad2.sln --no-incremental` → `dotnet test src/Ecad2.sln --no-restore`で実測：
**Core 53件・App 346件、全合格**（T-052分の新規テストはCore+8件・App+2件、内訳：
DesignRuleCheckPartIdTests初回3件+往復1件、PartResolverTests新規4件、OutputPanelJumpToTests新規2件）。

### 4-5結論

**3件ともクリーン。往復2周目の指摘なし。** 忍者実機確認へ回して差し支えない。
