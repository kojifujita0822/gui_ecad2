# T-071バグ修正 テスト設計（隠密）

対象: task_id=往復1周目（家老采配）。要修正1（Motor重複/境界検出漏れ）・要修正2（タイマラベル未描画）の
テスト設計を、仕様側（あるべき振る舞い）から起草する。**本書は設計のみ、実装は侍へ委譲する。**

現状コードの再確認では、Agentの一次報告に「SelectSwitchもWidthCells=3」との記載があったが、
`BasicPartTemplates.cs`を直接grepし**SelectSwitchは`WidthCells=1`（誤り）**と訂正済み。**全15種
（既存5種+T-071新規10種）のうちMotorのみが真にWidthCells=3の唯一の複数セル幅パーツ**であることを
確認した上で本設計を起草する。

---

## 前提（既存コードの確認事項）

- `IsWithinGridBounds(GridPos pos, Sheet sheet)`：`pos`（アンカーセル1点）のみ判定、`CellWidth`は
  考慮しない（`MainWindowViewModel.cs:1435-1437`）。
- `ValidatePlacement(GridPos pos, Sheet sheet)`：`IsWithinGridBounds && !sheet.Elements.Any(el =>
  el.Pos == pos)`——占有チェックもアンカーセルの完全一致のみ（同1439-1443）。
- `PlaceElementAtSelectedCell`：`const int cellWidth = 1`（コメント「基本図形は全て1セル幅」）が
  OR接続の縦コネクタ列位置計算にのみ使われる。`ElementInstance.CellWidth`（既定値1）は配置時に
  一度も`part.WidthCells`から設定されない。
- `PartResolver.BoundarySpan`は多セル要素の実占有範囲を計算できる実装を持つが、**src全体で呼び出し
  箇所がゼロ**（未使用）。
- 既定グリッドは`NewDocument()`で`Rows=10, Columns=20`に上書きされる（既存テストの境界値パターンも
  この前提）。
- 既存テスト`MainWindowViewModelTests.cs`の`PlaceElementAtSelectedCell_BoundaryRowAndColumn_
  PlacesOnlyWithinGridRange`が`[Theory]`+`[InlineData]`でRow/Column境界値を検証する先例パターン
  （本設計もこれに倣う）。

---

## 修正方針の技術的検討（提案、実装判断は侍に委ねる）

1. 配置するパーツの`WidthCells`を`PlaceElementAtSelectedCell`内で取得し、`ElementInstance.CellWidth`
   に反映する。
2. `ValidatePlacement`/`IsWithinGridBounds`を、アンカー列から`WidthCells`分の連続列
   `[pos.Column, pos.Column + cellWidth - 1]`全体について「境界内」「未占有」を判定するよう拡張する。
3. 重複チェックは、既存要素の占有列範囲`[el.Pos.Column, el.Pos.Column + el.CellWidth - 1]`と新規
   配置の占有列範囲が交差するかどうかで判定する（単純な区間交差判定）。
4. View層`IsSelectedCellWithinGrid`/`IsSelectedCellOccupied`（配置バー表示前のプレチェック）も
   同様に拡張が必要——ここが漏れると配置バー自体が誤って開いてしまう（UX上の不整合）。

---

## テスト設計1：Motor(WidthCells=3)の境界外・重複配置検出

### 境界値分析（Columns=20前提、既存BoundaryRowAndColumnテストと同一グリッド）

Motor(WidthCells=3)を列cに配置する場合、実占有列範囲は`[c, c+1, c+2]`。

### 重複の同値分割

- ケースA：アンカーセル自体・`+1`列目・`+2`列目すべて空き → 正常配置
- ケースB：アンカーセル自体に既存要素（従来ケース、対称性確認のため含める）
- ケースC：`+1`列目のみに既存要素（新規、現状バグでは検出漏れ）
- ケースD：`+2`列目のみに既存要素（新規、現状バグでは検出漏れ）

### パラメタライズド設計（`[Theory]`+`[InlineData]`、`Columns=20`固定）

| # | PartId | 配置列c | 周辺占有状態 | 期待結果 | 現状(修正前)の挙動 |
|---|---|---|---|---|---|
| 1 | `ContactNOId`(1セル) | 19 | なし | 配置成功（既存回帰、上限） | 成功（変化なし） |
| 2 | `ContactNOId`(1セル) | 20 | なし | 配置失敗（既存回帰、上限+1） | 失敗（変化なし） |
| 3 | `MotorId`(3セル) | 0 | なし | 配置成功（下限） | 成功 |
| 4 | `MotorId`(3セル) | 17 | なし | 配置成功（上限、`[17,18,19]`ちょうど収まる） | 成功 |
| 5 | `MotorId`(3セル) | 18 | なし | **配置失敗**（`[18,19,20]`、20が範囲外） | ★現状バグ：誤って成功してしまう |
| 6 | `MotorId`(3セル) | 19 | なし | **配置失敗**（`[19,20,21]`、2列はみ出す） | ★現状バグ：誤って成功してしまう |
| 7 | `MotorId`(3セル) | 5 | 列5(アンカー自体)に既存要素 | 配置失敗（従来の重複、対称性確認） | 失敗（変化なし） |
| 8 | `MotorId`(3セル) | 5 | 列6(`+1`列目)に既存要素 | **配置失敗**（新規重複ケース） | ★現状バグ：誤って成功してしまう |
| 9 | `MotorId`(3セル) | 5 | 列7(`+2`列目)に既存要素 | **配置失敗**（新規重複ケース） | ★現状バグ：誤って成功してしまう |
| 10 | `MotorId`(3セル) | 5 | 周辺すべて空き | 配置成功（正常系対称ケース） | 成功（変化なし） |

各ケースのアサーションは、既存パターンに倣い配置後の`sheet.Elements`に該当要素が存在するか
（またはしないか）を検証する。ケース5・6・8・9は「RED先行証明」の核——現状の実装ではこれらが
（誤って）成功扱いになることを確認してから修正を当て、修正後にGREENへ転じることを確認する。

### 対称性確認（1セル幅パーツの回帰、ケース1・2で兼ねる）

既存の1セル幅パーツは修正の前後で挙動が変わらないこと（`WidthCells=1`のケースでは
`[pos.Column, pos.Column]`という単一要素範囲になるため、既存ロジックと数学的に等価）。

---

## テスト設計2：タイマ接点の限時/瞬時ラベル描画

### 修正方針（提案）

`DiagramRenderer.cs`951行目の`TimerContactMark(e.Kind)`を、852-855行の`isLoad`判定と同型の解決
パターン（`part is not null ? PartResolver.ComponentKind(e, _lib) : e.Kind`）へ置き換える。

### 同値分割・パラメタライズド設計

| # | PartId | Role | 期待マーク |
|---|---|---|---|
| 1 | `TimerContactNOId` | `TimerContactNO` | "限" |
| 2 | `TimerContactNCId` | `TimerContactNC` | "限" |
| 3 | `TimerInstantContactNOId` | `TimerInstantContactNO` | "瞬" |
| 4 | `TimerInstantContactNCId` | `TimerInstantContactNC` | "瞬" |
| 5 | `ContactNOId`(通常接点、対照) | `ContactNO` | マークなし(null) |
| 6 | `CoilId`(コイル、対照) | `Coil` | マークなし(null) |

### 検証方法（侍への技術的提案、断定ではない）

`TimerContactMark`は`DiagramRenderer`内のprivateメソッドで、既存に`IRenderer`経由の描画命令
（`DrawText`呼び出し内容）を検証するテスト基盤が存在しない（隠密調査で確認済み、
`DiagramRendererTests.cs`相当のファイルはリポジトリに無い）。以下いずれかの方式を侍の判断で選択：

- (a) `IRenderer`のテストダブル（Fake、`DrawText`呼び出しの引数を記録）を新規作成し、
  `Render`実行後に該当要素の描画呼び出しに"限"/"瞬"の文字列が含まれるか検証する。
- (b) `TimerContactMark`のロジック自体を`internal`化し`InternalsVisibleTo`経由で
  `ElementKind`引数のみの単体テストにする（ただし、これでは「951行の呼び出し元が正しい`Kind`を
  渡しているか」までは検証できない点に注意——953行の解決パターン差し替え自体の回帰防止には(a)の
  方が確実）。

いずれの方式でも、**「配置された要素（PartId経由）から、正しいマークが最終的に描画されること」を
end-to-endで検証する**のが本テストの主眼——`e.Kind`が常に既定値のままという構造上の罠を再発させ
ないための回帰テストとして機能させる。

### ランプ色ラベル（890行）の同型疑いへの対応（検証対象に含める）

家老より「検証対象に含めるか判断されたし」との指示を受け、**含める**と判断した。同一パターン
（`e.Kind`直接比較、`PartResolver.ComponentKind`未経由）のため、951行と同一コミットで修正するのが
合理的。

**追加調査で判明した重要事実**：組込み`Lamp`パーツ（T-071で新設）自体も`PartId`経由でのみ識別され
`Kind`は常に既定値`ContactNO`のまま——**890行目の`e.Kind == ElementKind.Lamp`は、新設された組込み
表示灯パーツに対しても常にfalseとなり、ランプ色ラベルは一切描画されない可能性が高い**（TimerContactMark
と全く同一構造の罠）。

| # | 配置パーツ | Roleパラメータ | LampColorパラメータ | 期待動作 | 現状(修正前)の挙動 |
|---|---|---|---|---|---|
| 7 | `LampId`(組込み表示灯) | `Lamp` | "赤"等セット | ラベル描画される | ★現状バグ：`e.Kind`が既定値のため描画されない |

修正方針は951行と同型（`part is not null ? ... : e.Kind`パターンへの置き換え）。

---

## 侍への申し送り事項

- 上記テストケース表（表1: #1-10、表2: #1-7）を、既存の`MainWindowViewModelTests.cs`
  （表1）・新設または既存拡張の描画系テスト（表2）へ実装する。
- 表1の★印ケース（5, 6, 8, 9）と表2の★印ケース（7）は、**修正前にREDになることを確認してから
  実装に着手する**（RED先行証明、`ecad2_red_proof_build_reflection_pitfalls.md`記載の罠——
  `dotnet test`が古いDLLで走る問題に注意し`--no-incremental --no-build`等で確実にビルド後の
  結果を見ること）。
- 設計にないテストの追加は自由。設計にあるものを勝手に省くのは不可。
- 修正方針（`ValidatePlacement`拡張・`TimerContactMark`呼び出し元の解決パターン差し替え）は提案
  であり、侍の技術的判断でより良い実装があればそちらを優先してよい。ただし、本設計のテストケースが
  すべてGREENになることを最終確認基準とする。
