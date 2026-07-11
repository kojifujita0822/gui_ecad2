# T-015実装(f881005〜6963a62)静的レビュー（隠密）

対象: 右パネル「部品選択」リストへのサムネイル追加。段階1(DrawPreview拡張)→段階2(サムネイル
生成ラッパー)→段階3(SelectionEntries新設+ItemTemplate)。`code-review`スキル(medium、8観点
finder→1-vote verify)を併用。

## 家老指定5観点への回答

| # | 観点 | 判定 |
|---|------|------|
| 1 | DrawPreview引数追加の共有影響 | 良好。既存呼び出し元は皆無(Grepで確認済み)、library=null時は`_lib`が変更されず従来挙動維持、try/finallyで正しく復元される。 |
| 2 | RenderTargetBitmapまわり | 良好。`bitmap.Freeze()`済み、DPI96/96で標準的、UIスレッド生成も保証される(MainWindowコンストラクタ内、STA)。 |
| 3 | Entries/SelectionEntriesの並行管理 | 現状は問題なし(コンストラクタで一度きり生成、再読込機能自体が未実装)だが、二重管理自体は将来の同期漏れリスクを残す設計(下記所見4)。 |
| 4 | サムネイルnull/生成失敗時のフォールバック | 通常経路は`library`に全パーツが事前登録済みのため到達しないが、**重複ID時に誤ったサムネイルが表示される具体的な欠陥を発見**(下記所見1)。 |
| 5 | 起動時一括生成のタイミング | 現状パーツ数(数十件未満)では軽微。ただしT-002 PoC(ベクター描画5万個113ms)との比較は負荷特性が異なり見積り根拠がやや弱い(下記所見3)。 |

## code-reviewスキル所見（verify後）

| # | 判定 | 内容 |
|---|------|------|
| 1 | **CONFIRMED** | `.gcadpart`ファイルをエクスプローラでコピーすると`PartDefinition.Id`(生成時に一度だけGuid.NewGuidで採番されJSONに保存)はコピー後も同一のまま残る(`PartLibrarySerializer.LoadOne`はId再採番をしない)。`PartPaletteViewModel`コンストラクタの一時`library`構築ループは後勝ちで上書きするため、重複ID状態(コピー後に片方だけ内容編集、ID重複のまま手編集等)になると、複数行が同じ(最後に登録された)図形のサムネイルを誤表示する。コピー直後は内容も同一なため通常は気づかれないが、その後の編集で顕在化しうる。 |
| 2 | **CONFIRMED（重複コード）** | `PartPaletteViewModel`コンストラクタ内の一時`PartLibrary`構築ループが、既存の`MainWindowViewModel.BuildPartLibrary`と完全に同一のロジック(`foreach entry: library.ById[entry.Definition.Id] = entry.Definition;`)を再実装している。構築順序を確認したところタイミング制約はなく、`PartPaletteViewModel`が`library`をpublicプロパティとして公開すれば`MainWindowViewModel`側の`BuildPartLibrary`呼び出し自体が不要になり、単純なリファクタで一本化できる。 |
| 3 | 経過観察(将来リスク) | 起動時一括生成のコスト見積り(T-002 PoC「5万個113ms」との比較)は、ベクター描画とパーツ毎のRenderTargetBitmap個別生成では負荷特性が異なり根拠がやや弱い。現状規模(数十件未満)では実害ないが、パーツ数が数百件規模に増えた場合は線形以上に効いてくる可能性があり、閾値を`docs/todo.md`等に明記しておくことが望ましい。 |
| 4 | 経過観察(設計メモ) | `PartSelectionEntryViewModel`は`INotifyPropertyChanged`非対応・`Thumbnail`はコンストラクタ確定の不変プロパティのため、「増えたら遅延生成へ切替」というコメントの想定に反し、実際に切替える際はViewModel契約自体の作り直しに近い規模の変更になる。現時点でこの抽象化を先取りするのはYAGNIだが、コメントの楽観度は実態と乖離している。 |
| 5 | 経過観察(設計メモ) | `Entries`/`SelectionEntries`の二重管理(型変更を避けるための並行リスト)。二重管理自体は「他利用箇所への影響回避」という理由が実際の消費コード(`FirstOrDefault(e => e.Definition.Id==...)`等)を見る限りやや過大評価。XAMLコメント(「自作パーツ含む全Entries」)が実際のバインド先(`SelectionEntries`)と不一致になっている軽微な追随漏れもあり。 |
| 6 | 経過観察(低優先度) | `DrawPreview`の`_lib`一時差し替えはスレッドセーフでない旨コメント済みだが、「使い捨てインスタンス限定」という暗黙の前提への言及がない。現状の唯一の呼び出し元(`PartThumbnailRenderer`)は毎回`new DiagramRenderer`のため実害なし。将来共有インスタンスに対して呼ばれるようになった場合のみ顕在化。 |
| 7 | 経過観察(低優先度・理論上) | サムネイルは1セル分の正方形固定でクリップ処理がないため、将来`WidthCells>1`や負のBoundaryOffsetを持つ自作パーツが登録されると図形の一部がビットマップ枠外に描かれる可能性。現状のパーツエディタでこの状態を作るUI導線は無く到達性は低い。 |

## 隠密所見

要修正候補として家老の判断を仰ぐべきは**#1・#2**。#1は実際に誤情報を表示しうる具体的な欠陥、
#2は既存コードとの重複でありリファクタで解消可能。両方とも今すぐの対応は必須ではない
(#1は編集後の稀なケース、#2は保守性の話)が、対応するなら侍への差し戻しが妥当。
#3〜#7はいずれも経過観察〜設計メモ相当、直ちの対応は不要と判断する。
