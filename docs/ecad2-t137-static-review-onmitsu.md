# T-137静的レビュー（1周目・軽量既定）

隠密（key=1785485896132）記す。2026-07-31。対象＝`82f6df3`（push済み）。

## 結論：指摘なし。台帳DoD・テスト設計とも符合を確認

## (a) 台帳DoD整合

`docs/todo.md` T-137節の殿裁定4件を実装(`PartEditorCanvas.cs`)と1対1で突き合わせた。

| 殿裁定 | 実装での確認箇所 |
|---|---|
| 1. 案B（マス目・キャンバス全域） | `DrawCellGrid`が`DipToCell(0,0)`〜`DipToCell(w,h)`で可視範囲を逆算し全域へ描画（`:741-742`） |
| 2. 中心行(y=0)の強調＝せぬ | `GridLineYs`は全ての境目を一様に返し、`DrawCellGridLines`にも特別扱いなし |
| 3. 表示の入切＝設けぬ | `DrawCellGrid`は`Draw()`から無条件呼び出し、切替用フィールド・バインディング新設なし |
| 4. 枠内外の濃淡＝外を一段薄く | `outer = inner with { Color = ... A/2 }`で外側パス→内側(枠範囲)を`inner`で再描画（`:735-747`） |

「家老裁量で承認」2件（線の色・太さ＝`DrawingTheme.Get(StrokeRole.Grid)`流用、列刻み＝1セルのみ）も実装と一致。「範囲外の具申」（枠の色がテーマ非追従）は`P-150`として`proposed.md`に起票済みを確認（`docs/todo.md:733`、`proposed.md`内に存在確認）。

## (b) `code-review`スキル

GitHub PR前提（`gh pr`取得）の設計であり、本件はローカルコミットのみでPRが存在せぬため適用不可（`onmitsu.md`既知の恒久事象と同根）。手動レビューで代替。

## (c) 狙い撃ち観点

1. **純粋関数のテスト可能性**——`PartShapeGeometry.GridLineXs/GridLineYs`は`static`・`double`引数のみでUI依存なし。既存の`FrameRect`/`ClampPort`と同型（`samurai.md`「テストしにくいは設計の匂い」に沿う）。
2. **可視範囲の逆算で線の本数が有界か**——`Zoom`は`Math.Clamp(value, 0.2, 8.0)`（`PartEditorCanvas.cs:143`、一次ソースで確認）。下限0.2・`CellMm=9.0`より1セル最小約6.8DIP。`DrawCellGrid`は毎`Draw()`呼び出しで可視範囲を再計算するのみで状態を蓄積せぬため、線数は有限のDIP幅/高さに比例し発散しない。侍の「180本弱・上限ガード不要」の見立ては机上の見立てとして妥当（見やすさの実測は忍者の領分、侍も明示的に留保済み）。
3. **RED証明とテスト内容の整合**——`samurai.md`新設節「本数・件数を測るテストは位置の誤りを検出せぬ」の実例どおり、本数のみを測る`GridLineYs_枠の範囲では高さの2倍の本数になる`は改変A(半整数→整数)ではGREENのまま残る性質だが、**位置を直接比較する別テスト**（`GridLineYs_高さ2の枠では半整数の位置に入る`・`GridLineXsとGridLineYsは同じ範囲でも別の位置を返す`・`GridLines_端の線が基準枠の辺と一致する`等、配列を`Assert.Equal`で直接比較）が改変A/Bを検出する経路として別途存在することを静的に確認した。集約値テストの弱点はテストスイート全体としては既に別観点でカバーされている。

## 追加確認（依頼範囲外・念のため）

- `dotnet test`（`PartShapeGeometryTests`フィルタ）を実行、125件全合格を確認（再現手段：`dotnet test tests/Ecad2.Core.Tests/Ecad2.Core.Tests.csproj --filter "FullyQualifiedName~PartShapeGeometryTests"`）。
- `src/Ecad2.App/Views/PartEditorCanvas.cs`に一時計装・残置マーカーの類なし（`Grep "TEMP|DEBUG_|FIXME"`該当なし）。

## スコープ境界（依頼どおり触れず）

絵の見え方（枠が線の海に埋もれぬか・ダークモードの濃淡・最も引いた状態で潰れぬか）は忍者の領分ゆえ、本レビューでは判じない。

## 派生提案

なし。
