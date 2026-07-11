# T-035 .gcadpart読込時のID重複検出+再採番 再レビュー（隠密・静的レビュー、往復1周目）

対象: コミット`893a7f9`（`fix(core): T-035 - 先勝ち基準をCreationTime最古優先へ是正(往復1周目)`）。
変更ファイル: `src/Ecad2.Core/Persistence/PartFolderStore.cs`・
`src/Ecad2.App/ViewModels/PartPaletteViewModel.cs`・`src/Ecad2.App/Diagnostics/TraceLog.cs`・
`tests/Ecad2.Core.Tests/PartFolderStoreTests.cs`（新規2件追加）。

前回レビュー原本（`docs/archive/ecad2-t035-review-onmitsu.md`）の指摘3点を追跡表として突合、＋
`code-review`スキル（mediumレベル、8角度×並列finder→1-vote verify）を併用。申し合わせ通り
「元 - コピー」実命名での実機確認を含めた。

---

## 総評

**前回指摘3点はいずれも正しく解消・退行なし。** 実機で「Windowsの標準ファイルコピー操作は
新しいCreationTimeを持ち、LastWriteTimeは元のまま維持する」ことを確認し（オリジナル
CreationTime 01:42:13、コピーCreationTime 01:42:14）、`CreationTimeUtc`最古優先という新基準が
この最も一般的な操作に対して正しく機能することを裏付けた。

**ただし、`code-review`の独立探索により、CreationTime基準にも限界があることが判明した。**
Windows標準コピー以外の複製手段（タイムスタンプ保持型のバックアップ・同期ツール）では、
同じクラスの逆転が別の経路で再発しうる。これは往復対応の要修正ではなく、設計の残存リスクと
して報告する（PLAUSIBLE、優先度は中〜低）。

---

## 追跡表: 前回指摘3点の解消確認

| # | 前回指摘 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 先勝ち基準（パス辞書順）がWindowsコピー命名と衝突し逆転する | **解消（実機確認済み）** | `Enumerate()`のソートキーを`CreationTimeUtc`最古優先（同時刻タイはパス辞書順）へ変更（`PartFolderStore.cs`65-67行目）。実機で「部品.gcadpart」と「部品 - コピー.gcadpart」相当のコピー操作を再現し、オリジナルのCreationTimeがコピーより古いことを確認した。新設テスト`Enumerate_WindowsCopyNamingPattern_KeepsOriginalIdByCreationTime`も実命名パターンでオリジナルId維持を実証している |
| 2 | Id null/空文字列の最初の1件が処理漏れで永久放置 | **解消** | `bool needsReassign = string.IsNullOrEmpty(def.Id) \|\| !seenIds.Add(def.Id);`で無条件再採番扱いに変更（69行目付近）。新設テスト`Enumerate_NullOrEmptyId_ReassignsBothWithoutThrowing`で両方とも再採番されユニークなIdを持つことを確認 |
| 3 | TraceLogが件数のみで事後調査不能 | **解消** | `TraceLog.LogPartIdReassigned`をファイルパス・旧Id・新Id・保存成否（`saved`）まで記録するよう拡張。`PartIdReassignment`レコードで詳細情報を保持し、`PartPaletteViewModel`側で1件ずつ記録するよう変更済み |

実機で`dotnet test tests/Ecad2.Core.Tests`を実行し8件合格（App.Testsと合わせて家老報告の20件と
一致）を確認した。

---

## 新規発見（往復対応ではなく残存リスクとして報告）

### [PLAUSIBLE] タイムスタンプ保持型の複製手段では逆転が再発する
`robocopy /COPY:DAT`（Timestampsオプション含む）でファイルをコピーする実機検証を行った結果、
コピー元・コピー先の`CreationTimeUtc`が完全に一致することを確認した。この場合
`OrderBy(CreationTimeUtc)`はタイになり、`ThenBy`のパス辞書順フォールバックが効くため、前回
指摘した「半角スペース(U+0020) < ピリオド(U+002E)」による逆転が、これらのツール経由の複製
（フォルダ丸ごとバックアップ・復元等）では再発しうる。ただし、これはExplorer標準コピー
（今回のコミットが確実に解消した最も一般的なケース）とは異なる限定的な運用シナリオ。

### [PLAUSIBLE] OneDriveリダイレクト環境でのCreationTime不安定性
`PartFolderStore.CreateDefault()`は「マイドキュメント」配下をデフォルト保存先とする。
Windows 11ではマイドキュメントがOneDriveへリダイレクトされる構成（企業管理PCで特に一般的）が
広く使われており、OneDriveの再同期・PC移行後の初回ダウンロード・Files On-Demandの再ハイド
レート時にファイルの作成日時が同期/ダウンロード時刻へ書き換わる事象がMicrosoft公式Q&Aでも
複数報告されている。このメカニズム自体は実在するが、ecad2の実運用環境（OneDrive KFM構成の
有無、図形フォルダの一括再ハイドレートが実際に起きるか）は未検証であり、現時点で被害報告も
無い。優先度は中〜低。

**所見**: 上記2点はいずれも「CreationTime基準」という選択自体が、Windows標準コピー以外の
経路では万能ではないことを示す。より根本的な解決（例：既存の.gcadドキュメントから実際に
参照されているIdを優先する）と比較したトレードオフだが、今回のコミットの対応範囲（Windows
標準コピーでの逆転解消）としては十分であり、往復対応を要求するほどの緊急性は無いと判断する。

### [REFUTED] File.GetCreationTimeUtcの例外未処理懸念
`code-review`のfinder角度が複数（3系統）独立に「`File.GetCreationTimeUtc`がtry/catch外で
呼ばれ、列挙中のファイル削除・ロックで例外が伝播しEnumerate全体がクラッシュするのでは」と
指摘したが、verifyで実機検証した結果、**`File.GetCreationTimeUtc`は存在しないファイル・
排他ロック中のファイルいずれに対しても例外を投げず、既定値（1601-01-01 UTC）を返す**ことを
確認した（.NET公式仕様通り）。finderの推測が実機で否定された例。

### [REFUTED] needsReassignの可読性懸念
「Id欠落」と「Id重複」を1つの`bool`変数に統合している点への懸念は、実際には
`PartIdReassignment.OldId`フィールドで両ケースが区別可能（空文字列 vs 実際の重複Id）に保持
されているため、実害なしと判断した。

---

## 結論・提案

**往復1周目の対象（前回指摘3点）はクリーン。往復2周目（追加修正）は不要と考える。**
新規発見の2件（PLAUSIBLE）は、今回のスコープの延長で対応するか、経過観察とするかは家老の
裁量に委ねる。忍者実機確認へ進めることを妨げる性質の欠陥ではない。

---

## 出典
- コミット`893a7f9`（`git show`・`git diff 893a7f9~1..893a7f9`で全文確認）
- 前回レビュー`docs/archive/ecad2-t035-review-onmitsu.md`
- `code-review`スキル（mediumレベル、8角度×並列finder→1-vote verify）
- 実機検証: PowerShellでのファイルコピー（CreationTime/LastWriteTime変化確認）、
  `robocopy /COPY:DAT`でのタイムスタンプ保持コピー検証、`File.GetCreationTimeUtc`の例外有無
  検証（いずれも検証後ファイル削除済み）
- `dotnet test tests/Ecad2.Core.Tests`実行結果（8件合格）
- WebSearch: OneDrive Files On-Demand再ハイドレート時のCreationTime変化に関するMicrosoft Q&A
