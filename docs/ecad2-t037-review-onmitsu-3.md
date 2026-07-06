# T-037最終レビュー（往復3周目）：Enumerate内後方互換補正（隠密）

> 2026-07-06 隠密レビュー。対象コミット `8a6077a`（`fix(app): T-037 - IsOrEligible後方互換補正(旧版JSON既存ファイル対応、往復3周目)`）。
> 家老指定の観点(1)〜(4)＋追加観点(5)（テストの環境非汚染）。

---

## 結論：クリーン。忍者の実機差分確認へ進めてよい

往復2周目で確認した致命的懸念（既存の旧版JSON環境でORa/ORbが0件になる問題）は、本コミットで解消
されたことをこの開発機の実ファイルで直接確認した（CONFIRMED）。テストの環境汚染疑いは調査の結果
REFUTEDと判定する。

---

## 観点別結果

### (1) 補正がId重複チェックより前に実行されるか — **CONFIRMED**

`PartFolderStore.cs:85-89`（補正処理）は`94行目`のId重複チェック(`needsReassign`判定)より前に
配置されている。コードの読み順どおり確認済み。これによりコピー後の再採番書き戻しにも補正済みの
`IsOrEligible=true`が引き継がれる（コピー耐性を保つ設計として妥当）。

### (2) 書き戻しの安全性（OneDrive実体への書込失敗時に起動を壊さぬか） — **妥当**

`PartFolderStore.cs:88`：`try { PartLibrarySerializer.SaveOne(def, file); } catch { /* ベストエフォート */ }`
で例外を握りつぶし、書き戻し失敗時もメモリ上の補正値で処理を継続する。既存の再採番処理
（107-108行目）と同じ例外隔離パターンを踏襲しており一貫性がある。テスト
`Enumerate_LegacyContactJsonReadOnly_BackfillsInMemoryWithoutThrowing`で読み取り専用ファイルに
対する非スロー動作を確認済み。

### (3) 旧版JSON環境でORa/ORbが2件復活しセレクトSW除外が維持されるか — **CONFIRMED（実ファイル直接確認）**

この開発機（殿PC）のOneDriveリダイレクト先実フォルダを直接読んだ結果：

- `a接点.gcadpart`：`"isOrEligible": true` に補正済み（更新日時2026-07-06 18:16、コミット時刻
  18:16:36と一致）
- `b接点.gcadpart`：同じく`"isOrEligible": true`に補正済み
- `セレクトSW.gcadpart`：`isOrEligible`キー自体が無し（＝false扱い）のまま、補正対象外として
  正しく除外されている（固定Id判定が`ContactNOId`/`ContactNCId`のみを対象にしているため再混入なし）

往復2周目で「実機ではORa/ORbが0件になる」と指摘した状態から、実際に2件へ復活したことを同一ファイルで
直接確認できた。

### (4) 後方互換テストが添えられているか — **妥当（4件）**

`tests/Ecad2.Core.Tests/PartFolderStoreTests.cs`に追加：
1. `Enumerate_LegacyContactJsonWithoutIsOrEligible_BackfillsTrueAndSaves` — 旧版JSON→true補正・書き戻し確認
2. `Enumerate_LegacySelectSwitchJsonWithoutIsOrEligible_StaysFalse` — セレクトSW非該当確認
3. `Enumerate_LegacyContactJsonReadOnly_BackfillsInMemoryWithoutThrowing` — 読み取り専用時の安全側継続確認
4. `Enumerate_LegacyContactCopy_ReassignsIdButKeepsIsOrEligibleTrueForBoth` — コピー耐性確認

いずれも観点(1)〜(3)の主張を裏付ける内容で、往復2周目の指摘に対応した回帰テストとして妥当。

### (5)【家老追加観点】テストが実環境フォルダを汚染していないか — **REFUTED（汚染の疑いは晴れた）**

4件の新規テストを含め、`PartFolderStoreTests.cs`内の全テストは共通ヘルパー`CreateTempDir()`
（`Path.Combine(Path.GetTempPath(), $"ecad2-test-{Guid.NewGuid():N}")`、22-27行目）で作成した
一意なテンポラリディレクトリのみを`PartFolderStore`のコンストラクタに渡している。
`PartFolderStore.CreateDefault()`（実際のMyDocumentsパスを使う静的メソッド）を呼ぶテストは無い。
**テストコードは実環境から完全に分離されている。**

侍報告にあった「実機のa接点.gcadpartが補正されたことを副次的に検知」は、テスト実行の副作用では
なく、**侍が実装確認のためにアプリを実機起動（`CreateDefault()`経由で実フォルダを読込）した際に、
`Enumerate()`の後方互換補正ロジックが意図通り作動した正常な結果**である（更新日時がコミット時刻と
一致することから確認）。汚染ではなく、修正が実環境で機能していることの生きた実地証拠と判定する。

---

## 残存所見（軽微・往復完了の妨げにはならない）

- 往復2周目レビューで挙げた「将来の基本図形追加時にIsOrEligible付与漏れが再発しうる」というリスクは
  未解消のままだが、これは本T-037の枠外（将来のパーツ追加時の一般的な留意点）であり、往復完了を
  妨げるものではない。
