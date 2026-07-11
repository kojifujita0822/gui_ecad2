# ecad2 仕様書：部品選択・自作パーツ管理

T-075（殿裁定、2026-07-11起票）体系の第9号、第4弾4件目。実装コード・殿裁定記録
（`docs/todo.md`/`docs/todo-archive.md`/`docs/observations.md`）・忍者実機検証記録
（`docs-notes/`配下）を突き合わせ、「仕様として確定している挙動」を出典付きで明文化する。
配置フロー自体（F5〜F8、「自作パーツ」ボタンからの配置）は`docs/spec/ecad2-spec-placement.md`
5節を参照。

---

## 0. 要点：自作パーツの「作成・編集」機能は現状存在しない

**自作パーツを追加する唯一の実運用経路は、ユーザーがExplorer等で`図形\自作\`フォルダへ`.gcadpart`
ファイルを直接配置すること**——`PartFolderStore.SaveCustom`（自作パーツ書き出しAPI）の呼び出し元は
テストのみで、App層の本番コードからは一切呼ばれていない。新規作成・編集用のダイアログやエディタ
画面はリポジトリ内に存在しない（T-068、未着手、規模大でロードマップ最後尾）。

**ピン留め機能も同様に未接続**——`PinnedPartStore.cs`（JSON永続化）はCore層に実装済みだが、
App層のどこからも参照されていない「孤立したクラス」。

---

## 1. `.gcadpart`ファイルフォーマット

`PartLibrarySerializer.SerializeOne(PartDefinition part)`は`PartDefinition`を**素のままJSON化**
（`SchemaVersion`フィールドを持たない）。ライブラリ全体用（`.gcadparts`、末尾に`s`が付く別形式）とは
非対称な設計。

`PartDefinition`本体：`Id`(GUID文字列)、`Name`、`WidthCells`/`HeightCells`(セル単位外形)、
`Role`(電気的役割enum)、`IsOrEligible`(bool、OR配置対象フラグ、T-037で追加)、`Ports`、`Primitives`
（`JsonPolymorphic`でline/circle/arc/rect/polyline/textを`"type"`判別子付きで多態シリアライズ）。

読込時は断片化した直線を`PartOptimizer.MergeCollinearLines`で自動マージ（エディタ・組込みパーツ
共通処理）。

### 保存先フォルダ構成

既定ルートは`マイドキュメント\Ecad2\図形`（T-011殿裁定でGuiEcad→Ecad2へ変更）。

| 場所 | Category |
|---|---|
| ルート直下 | ""（基本図形） |
| `図形\自作\` | "自作" |

`SaveCustom`は常に`CustomDir`（`図形\自作\`）固定で`<パーツ名(サニタイズ済み)>.gcadpart`として
書き出す。

---

## 2. `.gcadpart`読込時のID重複検出・再採番（T-035）

`PartFolderStore.Enumerate()`の走査順：**`CreationTimeUtc`昇順→同時刻タイはパス辞書順
（`OrdinalIgnoreCase`）**。

### 走査順の裁定経緯（隠密レビューで発見した実装バグ）

初期実装（`fd032bb`）は「パス辞書順のみでの先勝ち再採番」だったが、**隠密静的レビューでCONFIRMED
の実装バグが発見された**：Windowsコピー命名「元 - コピー.gcadpart」の半角スペース（U+0020）が
ピリオド（U+002E）より小さいため、コピー側が誤って先着扱いされ、**オリジナル側が誤って再採番される**
致命的逆転が起きる。実機確認でも再現。修正裁定＝先勝ち基準を`CreationTime`最古優先へ差し替え
（`893a7f9`、2026-07-06）、隠密再レビューでクリーン確定。

### 重複判定のロジック

1. `IsOrEligible`後方互換補正（Id重複チェックより前に実施）：固定Id（a接点/b接点）のみ
   `IsOrEligible=false`→`true`へ補正し書き戻し（ベストエフォート、例外は握りつぶし）。
2. `seenIds`（`HashSet<string>`）に対し、Id欠落（壊れた/旧形式ファイル）も含めて`!seenIds.Add(def.Id)`
   なら再採番対象。再採番は`Guid.NewGuid().ToString("N")`。
3. 書き戻し失敗はファイル単位で例外隔離（起動時列挙全体を壊さない設計、T-039の教訓）。

結果は`PartEnumerationResult(Entries, Reassignments)`。`Reassignments`は`TraceLog.LogPartIdReassigned`
（`event=PartIdReassigned file=... oldId=... newId=... saved=...`）へ記録される。

### 残存リスク（経過観察、未対処）

`docs/observations.md`に2件記載：
- #10：robocopy等タイムスタンプ保持コピーで`CreationTime`が一致した場合、辞書順の逆転が再発しうる。
- #11：OneDriveリダイレクト環境での再同期時に`CreationTime`が書き換わる可能性。

### 実機検証

観点1（Id維持・再採番・書き戻し）・観点2（TraceLog詳細記録）・観点4（非重複時の無変更）・観点5
（既存フロー回帰）いずれもOK。一時的に配置フロー異常（P-018）が疑われたが、忍者の再検証で
「操作手順の逆順による誤観測」と確定しwithdrawn（実装バグなし）。

---

## 3. サムネイル生成

`PartThumbnailRenderer.Render(PartDefinition, PartLibrary, isOr, cellMm)`：`DiagramRenderer.DrawPreview`
を`DrawingVisual`→`RenderTargetBitmap`化する薄いラッパー。1セル分の正方形として、`MarginMm=0`・
`Pos=(0,0)`の専用`DiagramRenderer`で原点合わせして描画。

- OR論理エントリ（`isOr=true`かつ`definition.IsOrEligible`）の場合、ツールバーF5/F6と同一のGX様式
  グリフ（Path Geometry）で描画。**判定はId非依存、`IsOrEligible`/`Role`ベース**——Explorer複製由来の
  再採番パーツでもOR表現が欠落しないための設計。
- 呼び出しは`PartPaletteViewModel`のコンストラクタ内で`SelectionEntries`全件分を**起動時一括生成**
  （パーツ数が少ないためKISS、増えたら遅延生成へ切替検討というコメントあり）。
- 表示は`PartSelectionList`（`ListBox`）の各行に`Image`(24x24)＋`Category`（灰色小文字）＋
  `DisplayName`。

### 関連タスク

T-043（ORa/ORbサムネイルのシンボル統一、完全Done 2026-07-07）：往復2周
（`IsOrEligible`/`Role`ベース化＋Categoryゲート）で確定。T-052/T-055系（`ElementPlacementBar`への
サムネイル+名前表示エリア追加、配置バー側の別経路のサムネイル拡張）。

---

## 4. ピン留め機能（未接続）

`src/Ecad2.Core/Persistence/PinnedPartStore.cs`：`Load()`が`マイドキュメント\Ecad2\pinned-parts.json`
から`List<string>`をJSON読込→`HashSet<string>`化（Id集合と推測）、`Save(IEnumerable<string> ids)`が
書き込み。両方とも例外を握りつぶすベストエフォート実装。

**UI結線・テストとも存在せず、機能としては未接続**（`PartPaletteViewModel`/`MainWindowViewModel`/
XAML全てにピン留め関連のコードなし）。T-068起票の中に「作成/編集/削除/ピン留め/インポート・
エクスポート」として一括で内包されており、ピン留め機能単独の殿裁定は見当たらない。

---

## 5. `PartFolderEntry`の構造

```
public sealed record PartFolderEntry(string Category, string FilePath, PartDefinition Definition);
```

- `Category`：「図形/」からの相対カテゴリ文字列（ルート直下=""、自作="自作"）。
- `FilePath`：実ファイルの絶対パス（`.gcadpart`）。
- `Definition`：パース済み`PartDefinition`本体。

併せて`PartIdReassignment(FilePath, OldId, NewId, Saved)`・`PartEnumerationResult(Entries,
Reassignments)`が同ファイルに定義され、`Enumerate()`の戻り値型として使われる。
`PartSelectionEntryViewModel`は`Entry`(元の`PartFolderEntry`)を保持し、`Category`/`Definition`を
転送プロパティとして公開する。

---

## 6. T-068「自作パーツ管理・編集UI」（未着手、規模大）

GuiEcadは独立ウィンドウ`PartEditorWindow`（描画ツール・Undo/Redo込みで約950行超）の大規模GUI
エディタで実現していたが、ecad2側はCore層（`PartDefinition`/`PartFolderStore`/
`PartLibrarySerializer`/`PinnedPartStore`/`PartResolver`/`PartOptimizer`）は完備なものの、App層は
`PartPaletteViewModel`（配置用選択のみ）に留まり「作成・編集・削除・ピン留めのUIが皆無」（区分＝
Core完備・App結線欠如）。ロードマップ優先順位8（最後尾、規模大ゆえ）。将来の増分計画の具体的な
記述はまだ無い（着手時に起草予定）。

---

## 7. 実機確認記録

- 自作パーツ（.gcadpart）の読込・配置：テスト自作パーツ（菱形6線定義、role=coil）を自作フォルダへ
  配置、サムネイル・キャンバス描画とも定義どおりの菱形で一致確認。
- サムネイル表示：固定5種+自作パーツ全件に24px角のサムネイル表示を確認（ただし部品選択リストの
  固定種がORa/ORbでなく「セレクトSW」という前提食い違いを検出した経緯もある、P-010起票→T-037で
  解消済み）。
- ID重複検出・再採番：忍者実機確認（2026-07-06）で全観点OK。

## 不明点

- 該当なし（調査範囲内で全項目が事実確認できた）。
