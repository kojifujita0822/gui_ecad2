# T-037再レビュー（往復2周目=上限）：ORセレクトSW除外・IsOrEligibleフラグ（隠密）

> 2026-07-06 隠密レビュー。対象コミット `8341062`（`fix(app): T-037 - ORセレクトSW除外・IsOrEligible専用フラグへ置換(往復2周目)`）。
> 家老指定の4観点＋`code-review`スキル（medium相当・軽量版）併用。

---

## 結論（最重要・往復上限に抵触）

**コード差分自体（4ファイル）はロジックとして正しく一貫している。しかし、この開発機の実ファイルで
直接確認した結果、本修正は「既に一度でもアプリを起動しドキュメントフォルダへ図形が展開済みの環境」では
実機で機能しない（CONFIRMED、推測ではなく実ファイル確認済み）。往復2周目=上限の「一発で仕留める」を
満たせていない。**

### 致命的懸念（観点3で発見・code-review併用でも独立に再検出）

- `PartFolderStore.SeedBasics()`（`src/Ecad2.Core/Persistence/PartFolderStore.cs:126-139`）は
  「冪等：既存ファイルは上書きしない」設計（`if (File.Exists(path)) continue;` 134行目）。
- 単体パーツファイル(`.gcadpart`)は`PartLibrarySerializer.DeserializeOne`（`PartLibrarySerializer.cs:61-73`）
  で読み込まれるが、**SchemaVersion検査を意図的に持たない**（同ファイル64-65行目のコメントで明記）。
- この殿PCの実フォルダ（`Environment.SpecialFolder.MyDocuments`がOneDriveへリダイレクトされた実体
  `C:\Users\kojif\OneDrive\ドキュメント\Ecad2\図形\`）に既存の`a接点.gcadpart`（作成日時2026-07-03、
  本コミットより前）を直接読んだところ、`isOrEligible`キーが存在しない旧版JSONだった。
- `IsOrEligible`（`PartDefinition.cs:47`）はbool既定値`false`。System.Text.Jsonの標準挙動により、
  JSONにキーが無ければデフォルト値`false`で復元される。
- **結果**: 本コミット適用後にこの開発機でアプリを起動しても、`SeedBasics()`が既存ファイルを
  上書きしないため`a接点.gcadpart`/`b接点.gcadpart`は旧版のまま→`IsOrEligible=false`として
  読み込まれる→`PartPaletteViewModel`のOR一覧フィルタ（`e.Definition.IsOrEligible`）が両方とも
  falseと判定→**部品選択リストのORa/ORb項目が0件になる**。T-037がそもそも目的としていた機能
  （OR項目の追加）自体が、この環境では動作しない。

### なぜテスト20件で検出されなかったか

コミットメッセージの「test 20件合格（回帰なし）」は、`BasicPartTemplates.All()`経由でメモリ上に
新規生成される`PartDefinition`インスタンス（`IsOrEligible=true`が最初から埋め込まれている）を使う
テストであり、実際の「図形/」フォルダの既存ファイルを経由するシナリオを検証していない
（`code-review`併用でも同様の指摘：既存の`GcadCompatibilityTests.cs`は`Role`のみをAssertし
`IsOrEligible`値は検証していない）。

---

## 観点別結果

| 観点 | 判定 | 補足 |
|---|---|---|
| (1) 除外方式がコピー耐性を損なわぬか | **技術的には維持** | `IsOrEligible`はId/Nameに非依存。ただし上記の理由で既存ファイルの値自体が更新されないため、コピー耐性以前に機能が無効化されるケースがある |
| (2) OR項目がORa/ORbの2件のみになるか | **コード上は○（新規環境限定）** | `BasicPartTemplates.All()`の5パーツ中`IsOrEligible=true`はContactNO/NCの2つのみ（コイル/端子台/セレクトSWは全てfalse、見落としなし）。ただし既存環境では0件になりうる（上記致命的懸念） |
| (3) Persistence/データ形式/既存保存ファイル互換への影響 | **重大な懸念あり（CONFIRMED）** | 上記致命的懸念を参照 |
| (4) MainWindow側修正が同一根本原因の一貫修正として妥当か | **妥当** | `MainWindow.xaml.cs`のnullチェック構造・置換パターンは`PartPaletteViewModel`と同型で一貫 |

---

## `code-review`スキル併用の追加指摘（medium相当・軽量版）

1. **[上記致命的懸念と同一事象、独立に再確認]** `PartFolderStore.cs:134`のSeedBasics冪等仕様と
   `IsOrEligible`既定値falseの組み合わせによる既存データ不整合。
2. **[軽微・推測]** `IsOrEligible`の値自体や後方互換シナリオを検証する自動テストが無い。
   既存`GcadCompatibilityTests.cs`は`Role`のみAssertし`IsOrEligible`は未検証。
3. **[軽微・推測]** `Role`と`IsOrEligible`が意味的に独立したフラグになったため、将来の基本図形追加時
   （例: c接点等）に`IsOrEligible=true`の付与漏れが再発しうる。既定値がfalse（安全側）のため実害は
   「表示されない」方向に倒れ、誤混入事故は起きにくい。

差分自体のline-by-line検査・再利用性検査では、条件反転・null参照・冗長コード等の明確な問題は
見つからなかった（`MainWindow.xaml.cs`のnullチェック`selectedEntry is not null && ...`は変更前後で
同型を維持）。

---

## 隠密からの技術的所見（決定は家老・殿の裁定事項）

- **応急対応**（この開発機のみ）: `図形/`フォルダ内の`a接点.gcadpart`・`b接点.gcadpart`を手動削除し
  再起動すれば`SeedBasics()`が最新定義を再展開し、当面の実機確認は通る。ただし本番導入後の
  エンドユーザー環境で同じ問題が再発するため、恒久対応が必要。
- **恒久対応の方向性**（技術的選択肢の提示のみ、実装は侍・裁定は家老/殿）:
  - 基本図形（Id が `basic-*`）に限り、起動時に最新の`BasicPartTemplates`定義との差分を検出し
    補完・上書きするマイグレーション処理を`SeedBasics()`または起動シーケンスに追加する。
  - あるいは`.gcadpart`にもSchemaVersion相当の仕組みを導入する（現状は意図的に非対称）。
- いずれもCore層の設計変更を伴い、往復2周目=上限の枠内での「一発で仕留める」修正としては
  収まらない可能性が高い。家老の采配（3周目の許可／応急対応で凌ぎ恒久対応を別タスク化／殿への
  上申）を仰ぎたい。
