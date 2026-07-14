# T-061 A-1(268f6cc) 静的レビュー(隠密、2026-07-14)

対象: コミット268f6cc(セレクトSWの電気的導通判定、構造対処)。設計書
`docs/ecad2-t061-a1-select-switch-design-onmitsu.md`に基づく実装。手動観点確認+`code-review`スキル
(--effort high、3エージェント+sweep+追加verify1件)。

## 結論

**設計書どおり正しく実装されている。correctness bugは0件**。新規に中程度の指摘1件(G-1、CONFIRMED)を
発見——今回の対処で初めて到達可能になったコードパスが持つ既存の設計ギャップ(表示上の一時的不整合、
データ破損等ではない)。実機確認は殿指示により後日持ち越し。

## DoD確認結果

### DoD1: 設計書4節列挙のfile:lineどおりの実装

全4点、設計書と完全一致を確認:
- `PartDefinition.cs:8`付近: `PartRole`に`SelectSwitch`追加 — 確認
- `PartResolver.cs:47-64`: `ComponentKind`switchへ`PartRole.SelectSwitch => ElementKind.SelectSwitch`
  追加 — 確認
- `BasicPartTemplates.cs:157`: `SelectSwitch()`の`Role`を`PartRole.SelectSwitch`へ変更 — 確認
- `MainWindowViewModel.cs:2130`(横展開必須): `ResolveDeviceClass`の条件値を`PartRole.ContactNO`→
  `PartRole.SelectSwitch`へ変更 — 確認
- マイグレーション(`PartFolderStore.cs`): 既存IsOrEligible補正ブロック直後に同型のRole補正ブロック
  追加、固定Id限定・ContactNOの場合のみ対象 — 確認

### DoD2: 既存テスト崩れ対応(ケースB)の妥当性

`MainWindowViewModelTests.cs`のケースB(再採番Id相当のセレクトSW判定テスト)は、Role値を
`ContactNO`→`SelectSwitch`へ更新、Idは意図的に再採番値のまま据え置き。検証意図(Id固定でなく
データフィールドで分類できること)は保たれたまま新仕様に追従できている。**妥当**。

### DoD3: RED先行証明の実測結果の妥当性

7件FAIL実測+5件RED対象外という侍の説明を検証。特に`Enumerate_SelectSwitchAlreadyMigrated_
NoChangeIdempotent`(JSON内`"role":"selectSwitch"`を含む)について、**この値が旧enumに存在せず
JsonStringEnumConverterがJsonExceptionを投げるのではないか**という懸念を持ち重点検証した。

検証の結果: JsonStringEnumConverterが未知のenum文字列でJsonExceptionを投げること自体は実証確認した
一方、**侍のRED実測手順(git stash)はPartDefinition.csのenum追加を意図的に据え置いていた**(コミット
メッセージに明記、コンパイル前提のため4ファイルのみ差し戻し)。つまりRED実測時点でも`PartRole.
SelectSwitch`という値自体はenumに存在しており、デシリアライズは問題なく成功する。よって
「当然合格」という侍の説明は**正確**。懸念は杞憂だったが、確認する価値はあった。

その他の「変化しないこと」検証3件・Theory境界値2件(非導通同士の偶然一致)についても、侍の自己申告
どおり検出力なしと判断でき、正直な報告と確認。

### DoD4: code-reviewスキル併用

実施済み(--effort high、3エージェント+sweep+追加verify1件)。correctness bug 0件。cleanup2件
(参考、優先度低):
- `PartFolderStore.cs`のマイグレーションブロックが既存IsOrEligible補正ブロックと同型構造を複製
  (意図的パターン踏襲、T-037踏襲と明記済みのため許容範囲)
- `ResolveDeviceClass`の特殊フォールバック分岐が、今回の変更後は正規経路(ComponentKind経由)と
  完全に同一結果を返すため実質不要になっている(削除は家老裁定で見送り済み、追跡用の参照コメントは
  無し)

## G群: 新規発見(sweep、CONFIRMED)

### G-1(中程度): テストモード突入直後、セレクトSWの全ノッチが非導通表示になる

- **file**: `src/Ecad2.Core/Simulation/Evaluator.cs:148-150`(`IsConducting`)+
  `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:2146`(`CycleSelectSwitch`)
- `Evaluator.IsConducting`は`SimState.Positions`にデバイス名のキーが無い場合`TryGetValue`がfalseで
  即非導通に短絡する。一方`CycleSelectSwitch`は同じ未設定キーを「current=0(ノッチ0にいる)」とみなす
  実装。両者の「未設定時の既定解釈」が食い違っている。
- テストモード突入時にPositionsを既定ノッチへ初期化する経路が存在しない(`SimState`/`TestSession`/
  `GetOrCreateTestSession`いずれも確認、初期化なし)。
- **failure_scenario**: セレクトSWを配置しテストモードに入った直後、一度もクリックせずに評価すると、
  実物なら常にどこかのノッチに倒れているはずなのに、全ノッチが非導通(NonEnergizedGray)で描画される。
  ユーザーがノッチをクリックした瞬間から正しい表示に復帰する(一時的な表示不整合)。
- 今回のA-1対処で`Evaluator`のノッチ判定分岐が初めて到達可能になったことで顕在化した、既存の設計
  ギャップ(A-1が新たに持ち込んだバグではない)。データ破損等ではなく表示上の一時的な不整合。
- 既存テスト(`EvaluatorSelectSwitchTests.cs`は全ケースでPositions明示設定、`T061ModeFixTests.cs`の
  `TestModePress_SelectSwitch_CyclesNotchPosition`はクリック後の状態のみ検証)はこのギャップを未検証。
- **対処要否は家老・殿判断**。選択肢としては(a)テストモード突入時に全セレクトSWのPositionsを0で
  初期化する処理を追加(Evaluatorの「未設定=非導通」という一般則を崩さずに解決できる)、
  (b)Evaluator側の「未設定時はSwitchPosition==0を導通とみなす」という特別扱いに変更する、
  (c)表示上軽微なため見送り、のいずれか。忍者の実機確認(後日)で実際の見た目を確認してから
  判断してもよい。

## 不明点

なし。

## 派生提案

なし(G-1は本文に含めた、断定は避け家老・殿判断を仰ぐ形とした)。
