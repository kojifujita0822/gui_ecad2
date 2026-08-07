# T-145（モータのデバイス名を大円中央へ寄せる） 静的レビュー（隠密）

対象コミット: `96544e4`。深度＝軽量既定。観点＝(1)範囲判断 (2)改変Hの訂正 (3)期待値の取り方に絞る（家老采配のとおり）。

## 結論

**要修正なし。** 三点いずれも実相に即しており、範囲判断も家老の裁定どおり妥当と判じ申す。

## 検分の詳細

### (1) `ResolveLabelKind`をVMから`PartResolver.LabelKind`へ寄せた範囲判断

**範囲内と判じ申す。** `DiagramRenderer.cs`の差分（`PartResolver.LabelKind(e, _lib)`・`PartResolver.LabelOrient(e, _lib)`を直接呼ぶ形へ変更）を確かめたところ、**描画側とプロパティパネル側が同一の静的メソッドを呼ぶ一本道**になっている。

もし一元化せず、VM側の`ResolveLabelKind`を旧のまま（`DiagramRenderer`の`resolvedKind`と同じ二分岐を書き写す形）にしておけば、**T-145で新設したモータ迂回（`LabelKind`の第二分岐）をVM側にも別途書く必要が生じ、二つの複製が生まれる**。これはecad2で過去に繰り返し起きた「並行実装の片方だけ直して食い違う」型（`docs-notes/pattern-recurrence-log.md`該当型、隠密が静的レビューで常に確認する観点）そのものであり、**一元化は今回の修正が要求する最小限の対応**——リファクタリングのための拡大ではなく、バグの再発を防ぐために必須の変更と判ずる。家老の采配は正当。

### (2) 改変Hの訂正（迂回ガードの検出力）

`ElementCatalog.CreatesComponent(ElementKind.Breaker3P)`を一次ソースで確認（`ElementCatalog.cs:168,191-201`）——`IsContact`/`IsLoad`/`IsPassthrough`のいずれにも`Breaker3P`は含まれず、**`CreatesComponent(Breaker3P)==false`を確認**。

これにより`LabelKind`の実装（`PartResolver.cs:148-153`）で、`Kind=Breaker3P・PartId=MotorId・libは空`の入力は：
- 第1分岐（`CreatesComponent`）→ 偽（Breaker3Pゆえ手前で弾かれず通過）
- 第2分岐（`lib?.Get(e.PartId) is not null && ...`）→ 偽（libが空で未解決）
- `return e.Kind`＝`Breaker3P`

という経路を辿り、**ガード（`lib?.Get(e.PartId) is not null`）を外した改変（H）を当てれば`e.PartId == MotorPartId`が真になり誤って`Motor`を返す**——テスト`非シミュレート種別でも解決できぬPartIdは迂回せぬ`（`T145MotorLabelOffsetTests.cs:168`）はこの分岐差を正しく突いている。

**訂正前のテスト**（`解決できぬPartIdは迂回せぬ`、`Kind`は既定値`ContactNO`のまま）は`CreatesComponent(ContactNO)==true`ゆえ第1分岐で必ず捕まり、ガードの有無に関わらず`ComponentKind`→`e.Kind`＝`ContactNO`に落ちる——**「手前の枝に守られていただけ」という侍の訂正は一次ソースと完全に整合**する。

### (3) 期待値の取り方（実際に描かれた大円の中心と突き合わせる形）

`SymbolGlyphs.cs`の`Motor`（235-257行）・`MotorV`（276-297行）を一次ソースで確認：
- 本体大円の半径は横向き・縦向きとも`0.75 * k`（`k=CellWidth*Cell/3`）で共通。端子の円は`0.125 * k`と大きく異なるため、テストの`recorder.Circles.Single(c => Math.Abs(c.Radius - BodyR) < 1e-9)`は**曖昧さなく本体大円のみを一意に拾える**
- 大円中心座標は横向き`(2.0, 0)`・縦向き`(1.0, 1.75)`セルで、`ElementCatalog.cs`のコメントが挙げる値と一致

`AssertLabelSitsOnBodyCircle`（`T145MotorLabelOffsetTests.cs:313-329`）は、`LabelDx=0`/`LabelDy=0`を与えた対照要素のラベル位置（定義上「幅の中央・行中心」）から要素左境界・行中心の絶対座標を逆算し、実際に描かれた大円の中心（ローカル座標）を足して期待値を作る——**座標定数を一切書き写さず、意匠（`SymbolGlyphs`）が変わればテストの期待値も自動で追随する形**になっている。狙いどおり効いていると判じる。

## 「測っておらぬ」箇所（実機の見え方）

侍が区切ったとおり、理論値のみが入っている。`LabelDx`はApp層のソースを検索した限り**UI（プロパティパネルの入力欄）への結線は0件**（`grep -rn "LabelDx" src/Ecad2.App/`はコンパイル済みDLLのみに一致、ソースには無し）——`LabelDy`との非対称は家老承認・殿報告済みとのことで、追加の指摘は無い。忍者の実機画素採取（Coilの前例と同型のずれが出るか）を待つのが筋と判じる。

## 追加検証（再現手段）

- **ビルド**：`dotnet build src/Ecad2.sln --no-incremental` → 0エラー（既存警告4件のみ、対象外）
- **テスト**：`dotnet test src/Ecad2.sln --no-build` → **Core 528／App 1351／合計1879件合格**。家老の申告と一致
- **テスト件数の数え直し**：`T145MotorLabelOffsetTests.cs`の`[Fact]`14件＋`[Theory]`のInlineDataケース（5+7=12）＝**26件**。コミットメッセージの「テスト26件」と一致
- **簡体字混入チェック**：`git show 96544e4`を`增|实|检|殷|侪`で照合 → 0件
