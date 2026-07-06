# T-035 .gcadpart読込時のID重複検出+再採番 レビュー（隠密・静的レビュー）

対象: コミット`fd032bb`（`feat(core): T-035 - .gcadpart読込時のID重複検出+再採番`）。
変更ファイル: `src/Ecad2.Core/Persistence/PartFolderStore.cs`・
`src/Ecad2.App/ViewModels/PartPaletteViewModel.cs`・`src/Ecad2.App/Diagnostics/TraceLog.cs`・
`tests/Ecad2.Core.Tests/PartFolderStoreTests.cs`（新規3件）。

家老委任の5観点＋`code-review`スキル（mediumレベル、8角度×並列finder→1-vote verify）を併用。
実機（.NET 8での文字列比較）でも検証した。

---

## 総評

**[要修正・重大] 「先勝ち」の判定基準（ファイルパスの辞書順）が、Windowsの標準的なファイル
コピー操作と衝突し、意図と逆の結果を生む。** P-009由来の問題（ファイルコピーでId重複が生じ、
意図しないパーツ定義が参照される）を修正するための実装が、**まさにその原因となるファイル
コピー操作によって、オリジナル側が誤って再採番されてしまう**という、皮肉な形で同種の問題を
再発させかねない。5系統の独立した調査（隠密本人・`code-review`のAltitude/AngleA/AngleC/AngleB
各角度）が同じ根本原因に到達しており、実機での文字列比較でも再現を確認した。

その他、再採番によるPartId参照切れが発生した場合に**警告なく別のパーツ（a接点扱い）として
静かに描画・シミュレートされる**という実害の大きい連鎖も確認した。

---

## 家老の5観点への回答

### 観点1: 書き戻し失敗時のメモリ内ID適用の整合性

**問題なし（事実）。** `def.Id = Guid.NewGuid().ToString("N");`（`PartFolderStore.cs`77行目）で
メモリ上の`PartDefinition`オブジェクト自体を直接変更してから`result.Add(...)`しているため、
`SaveOne`（ディスク書き戻し）が失敗しても、`Entries`に格納される値・`PartPaletteViewModel`が
構築する`Library.ById`のキーは常に新Idで一貫する。整合性の食い違いは無い。

### 観点2: 再採番が確実に「後発のみ」か

**ロジック自体は正しいが、「先/後」の判定基準に重大な欠陥がある（CONFIRMED）。**
`seenIds.Add(def.Id)`の成否判定自体（HashSetの標準動作）は正しく、ファイルパス順に処理して
2回目以降の同一Idのみ再採番するという意味では「後発のみ」は保証されている。

**しかし「ファイルパス順」という基準が、Windowsの標準的なファイルコピー命名規則
（「元の名前 - コピー.拡張子」）と衝突する。** 実機で`string.Compare("my-part - コピー.gcadpart",
"my-part.gcadpart", StringComparison.OrdinalIgnoreCase)`を実行した結果、**コピー側の方が
辞書順で先に来る**ことを確認した（半角スペースU+0020がピリオドU+002EよりコードポイントIntが
小さいため）。つまり、ユーザーがエクスプローラで自作パーツファイルをコピーした場合、
「先に見つかった方＝オリジナル」という設計意図に反し、**コピー側がオリジナルのIdを奪い、
オリジナル側が『重複の後発』と誤判定されて新Guidへ再採番・ファイルへ書き戻される**。
既存の.gcadドキュメントが参照しているのは元のId（オリジナルファイルが持っていたはずの値）
のため、この逆転が起きると参照切れが発生する（詳細は観点5）。

### 観点3: basic-*固定IDとの衝突考慮

**同じ問題が基本図形にも及ぶ（CONFIRMED）。** `BasicPartTemplates.cs`のコメント（「Idは固定値
とし、再シードや埋め込みで重複・齟齬が出ないようにする」）に反し、ユーザーが基本図形ファイル
（例:"a接点.gcadpart"、Id="basic-contact-no"）をコピーした場合（"a接点 - コピー.gcadpart"）、
上記と同じ理由でコピー側が先勝ちし、**オリジナルの基本図形ファイルの方がbasic-contact-noという
固定Idを失う**。実機で"a接点 - コピー.gcadpart" vs "a接点.gcadpart"でも同じ逆転を確認した。

なお、現状「ルート直下 vs 自作サブフォルダ」間の重複は、たまたま全ての基本図形名の先頭文字が
「自」（自作フォルダ名の先頭文字、U+81EA）よりコードポイントが小さいため正しく動作しているが、
これは明示的な設計ではなく偶然の一致であり、将来「自」以上のコードポイントを持つ名前の基本
図形が追加された場合に同様の逆転が起きる可能性がある（`code-review`Angle B指摘）。

### 観点4: Core→App依存方向の維持

**問題なし（事実）。** `PartFolderStore.cs`（Ecad2.Core）は`TraceLog`を一切参照していない。
`TraceLog.LogPartIdReassigned`の呼び出しは`PartPaletteViewModel.cs`（Ecad2.App）側で行われて
おり、依存方向はApp→Coreのみで正しく維持されている。

### 観点5: ElementInstance.PartId参照切れリスクへの手当て

**手当てが無い（CONFIRMED、実害の大きい連鎖）。** 観点2の逆転が起きてPartId参照が切れた場合、
`PartResolver`（`Ports`/`CreatesComponent`/`ComponentKind`いずれも）は`lib.Get(e.PartId)`が
nullを返すと**警告なく`e.Kind`（未設定なら既定値`ElementKind.ContactNO`）へフォールバックする**
ことを確認した。さらに`MainWindowViewModel.PlaceElementAtSelectedCell`（自作パーツ配置の主経路）
は`ElementInstance`生成時に`Kind`を明示設定しない（`PartId`のみ設定）ため、実際に保存される
`Kind`は常に既定値`ContactNO`。**つまり参照が切れた自作パーツ（ランプ・コイル・多端子等）は、
次回ドキュメントを開いた際、エラーも警告も出ないまま「a接点」として描画・シミュレートされる。**
ユーザーは回路図の意味が静かに書き換わっていることに気づけない。

---

## 追加発見事項

### [CONFIRMED] TraceLogの再採番記録が事後調査に使えない
`TraceLog.LogPartIdReassigned(int count)`は件数のみを記録し、対象ファイル・旧Id/新Idを一切
残さない。加えて`reassignedCount++`（`PartFolderStore.cs`79行目）は`SaveOne`書き戻しの成否に
関わらず無条件でカウントされるため、ログの数値だけでは「ディスクへ永続化できた」のか
「メモリ上のみで次回また同じ重複が検出される」のかも区別できない。観点2の逆転が実際に発生
した場合、忍者・隠密が事後調査で原因を特定する手掛かりが極めて乏しい。

### [CONFIRMED] PartDefinition.Idがnull/空文字列の場合の処理漏れ
壊れた/旧形式の`.gcadpart`ファイルで`Id`がnullまたは空文字列になっている場合、`HashSet.Add`は
最初の1件を「非重複」として通す（C#の`HashSet<string>`はnullを1要素として問題なく扱う）ため、
最初の1件は無効なIdのまま再採番されず永久に放置される。`PartLibrary.ById[null] = ...`は
`ArgumentNullException`を投げる可能性がある（Dictionaryのnullキー代入は例外）。発生頻度は
低いが、条件が揃えば起動時クラッシュにつながりうる。

### [REFUTED] Enumerate()の責務肥大化（副作用混入）
`Enumerate()`のXMLコメント自体が書き戻しの副作用を明記しており、現状は1回しか呼ばれず
（`PartPaletteViewModel`コンストラクタのみ）、複数回呼び出しの計画も`docs/todo.md`に見当たら
ない。将来のリスクとしても前提が存在せず、的外れと判断した。

---

## 結論・提案

観点2・3・5（Windowsコピー命名規則での逆転とその実害連鎖）は、P-009由来の問題を修正する
はずの実装が、同じトリガー（ファイルコピー）で別の形の同種被害（今度はオリジナル側の参照が
壊れる）を生みうるという点で、**要修正級の重大な指摘**と考える。「先勝ち」の判定基準を
ファイルパスの辞書順から、より頑健な基準（例: 拡張子直前のサフィックス除去後の比較、
ファイル作成/更新日時、または実行時にElementInstance.PartIdから実際に参照されているIdを
優先する等）へ見直す必要がある。

TraceLogの詳細情報欠如・null/空Id処理漏れは、上記ほど緊急ではないが合わせて手当てする価値が
ある。

---

## 出典
- コミット`fd032bb`（`git show`・`git diff fd032bb~1..fd032bb`で全文確認）
- `code-review`スキル（mediumレベル、8角度×並列finder→1-vote verify）
- 実機検証: .NET 8での`string.Compare`/`StringComparer.OrdinalIgnoreCase`による文字列比較
  （"my-part - コピー.gcadpart" vs "my-part.gcadpart"、"a接点 - コピー.gcadpart" vs
  "a接点.gcadpart"、いずれもコピー側が先着することを確認、検証後ファイル削除済み）
- `src/Ecad2.Core/Model/PartResolver.cs`・`Element.cs`・`MainWindowViewModel.cs`（PartId解決・
  Kind既定値の追跡）
- `src/Ecad2.Core/Persistence/BasicPartTemplates.cs`（固定Id設計意図のコメント）
