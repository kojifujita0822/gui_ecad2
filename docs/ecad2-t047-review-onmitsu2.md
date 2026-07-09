# T-047 静的レビュー（隠密2）

> 2026-07-09 隠密2（key=1783558661707）レビュー。家老采配により、隠密（先着、GXアイコン長期調査中）に代わり本件を受け持つ。
> 対象コミット: `4ecae77`（main上、`git log origin/main..HEAD`確認時点でorigin/mainと同一=既にpush済み）
> 「feat(app): T-047 - 手動配線系(F9/Shift+F9/F10)のツールバーボタン新設」
> 変更ファイル: `src/Ecad2.App/MainWindow.xaml` / `src/Ecad2.App/MainWindow.xaml.cs` / `src/Ecad2.App/ViewModels/MainWindowViewModel.cs` / `tests/Ecad2.App.Tests/ManualWiringSheetTypeActivationTests.cs`（新規）
> 手法: 静的読解（一次情報）＋`code-review`スキル（Skill tool、effort=high、8アングル並列＋検証エージェント）併用。共有main上への一時注入検証は行っていない（読み取り専用の`dotnet test`実行のみ）。

---

## 結論（先出し）

**判定: 要修正（軽微〜中程度のUX不備1件）。機能ロジック自体（対応表・通知一元化・XAML配下）に誤りはない。**

家老指定の観点(1)〜(4)はいずれもクリーン。`code-review`スキル（8アングル）で新たに検出した1件（新設3ボタンのキーボード起点操作で記入モードが継続不能に見える）が唯一の要修正相当の所見。他は将来リスク・簡潔化提案レベルで経過観察が妥当と考える。

---

## 1. 家老指定観点の検証結果

### 観点(1) 5ボタンClick→既存Try系4メソッドの対応関係

**クリーン。** 全5ボタンの対応関係を`Window_PreviewKeyDown`の既存キー分岐（F9/Shift+F9/F10のシート種別切替ロジック、`MainWindow.xaml.cs:728-758`）と突き合わせ、取り違えなしを確認した。

| ボタン | Clickハンドラ | 呼び出し先 | 内部ガード方向 | IsEnabled | 判定 |
|---|---|---|---|---|---|
| 自由線(横線)記入 F9 | `FreeLineHorizontalButton_Click` | `TryBeginFreeLineDraft(true)` | 主回路限定 | `IsMainCircuitSheet` | 一致 |
| 自由線(縦線)記入 sF9 | `FreeLineVerticalButton_Click` | `TryBeginFreeLineDraft(false)` | 主回路限定 | `IsMainCircuitSheet` | 一致 |
| 縦分岐線記入 sF9 | `VerticalConnectorButton_Click` | `TryBeginConnectorDraft()` | 制御回路限定 | `IsControlCircuitSheet` | 一致 |
| 接続点記入 F10 | `ConnectionDotButton_Click` | `TryPlaceConnectionDot()` | 主回路限定 | `IsMainCircuitSheet` | 一致 |
| 配線分断記入 F10 | `WireBreakButton_Click` | `TryPlaceWireBreak()` | 制御回路限定 | `IsControlCircuitSheet` | 一致 |

`ConsumeToolButtonFocusRestore`/`ToolButtonPreviewKeyDown`の配線パターンも既存8ボタンと完全同型で、コピペミスなし。

### 観点(2) IsMainCircuitSheet/IsControlCircuitSheetの通知一元化

**クリーン。** `CurrentSheet`の実体を変えうる3経路（`CurrentSheetIndex`のsetter126行目・`NotifyCurrentSheetChanged`177行目・`ReplaceDocument`1506行目）全てで`NotifyCurrentSheetDependentPropertiesChanged()`が無条件（`SetProperty`の戻り値でガードせず）呼ばれており、CurrentSheetIndexのSetProperty早期return再発トラップ（T-041既知パターン）対策は実効している。`Document.Sheets`直接操作で`sheet.MainCircuit`を書き換える経路がこの3箇所以外に存在しないこともgrepで確認済み（`MainCircuit`はシート新規作成時のみ設定、生成後に変更するUIなし）。

新規テスト`DeleteCommand_WhenIndexNumberStaysSame_StillNotifiesSheetTypeProperties`は「index数値が変化しないが実体が入れ替わる」という核心シナリオを的確に検証している。

### 観点(3) XAML=裁定との整合・HasProjectガードとのAND合成・T-033配置バー無効化ツリー配下か

**クリーン。** 新設5ボタンは既存7ボタンと同じ`<ToolBar Band="1" BandIndex="0">`内（`MainWindow.xaml`）にあり、これは`MainContentArea`（`IsEnabled="{Binding IsPlacementBarVisible, Converter={StaticResource InverseBool}}"`、`MainWindow.xaml:84-85`）の配下にある。配置バー表示中の無効化ツリーに正しく含まれている。

`HasProject`との明示的なAND合成はしていないが、`IsMainCircuitSheet`/`IsControlCircuitSheet`は`CurrentSheet?.MainCircuit`参照であり、`HasProject=false`（`Document.Sheets.Count==0`）の間は`CurrentSheetIndex`が何であれ`CurrentSheet`は必ずnullになるため、両プロパティは間接的に`HasProject=false`を包含する（XMLdocにも明記の意図的設計）。既存の`HasProject`直接バインドと機能的に等価であり、問題なし。

### 観点(4) RED証明報告と新規5テストの整合

**クリーン、実測で確認。** `dotnet test`を読み取り専用で実行し、Core 14件＋App 220件＝**234件全合格**を確認した（コミットメッセージの主張と一致）。新規テストは5件（`NoProject_BothPropertiesAreFalse`・`NewDocument_DefaultSheetIsControlCircuit`・`Properties_ReflectCurrentSheetMainCircuit`[Theory 2ケース]・`DeleteCommand_WhenIndexNumberStaysSame_StillNotifiesSheetTypeProperties`）で、コミットメッセージの「新規5」と一致。

### 観点(5) code-reviewスキル併用

実施済み（8アングル: 行単位スキャン／削除挙動監査／クロスファイル追跡／再利用性／簡潔化／効率性／設計深度／CLAUDE.md準拠）。結果は2節。

---

## 2. code-reviewスキルで検出した所見

### 所見1（要修正相当、確度: PLAUSIBLE）新設3ボタンのキーボード起点操作で記入モードが継続不能に見える

- **file**: `src/Ecad2.App/MainWindow.xaml.cs:1096-1119`（`ToolButtonPreviewKeyDown`/`ConsumeToolButtonFocusRestore`）、`759,824-853`（矢印キー/Enter確定の`IsCanvasFocused()`ガード）、`1301-1329`（新設3ボタンのClickハンドラ）
- **対象**: 「自由線(横線)記入」「自由線(縦線)記入」「縦分岐線記入」の3ボタン（「接続点記入」「配線分断記入」は即時確定のため対象外）

**事象**: ユーザーがTabキーでツールバーへフォーカス移動し、上記3ボタンのいずれかをEnter/Spaceで押下すると、`ConsumeToolButtonFocusRestore`が「キーボード起因」と判定してキャンバスへフォーカスを戻さない（既存の`懸念4`対応ポリシー、`docs/ecad2-t021-focus-design-consolidation-plan-onmitsu.md`）。記入モード自体は正常に開始されるが、続く矢印キーでの調整・Enterでの確定は`IsCanvasFocused()`を必須条件としており、フォーカスがボタン上に残ったままだと一切発火しない。

**既存ボタンとの違い**: 既存6ボタン（a接点配置等、`BuiltinPlaceButton_Click`）は同じフォーカス保持ポリシーを持つが、「キャンバスをマウスクリックして配置」という代替完了経路がある（`TryPlaceActiveTool()`はクリック起点でも成立）。一方、新設3ボタンの記入モード（自由線・縦コネクタ）は、`LadderCanvasHost`のマウスイベントハンドラが`Tool.Mode==PlaceConnector/PlaceLine`中は何も確定処理をしない設計のため、**マウスによる確定代替手段が皆無**。この非対称性が既存ボタンには無かった新規リスクを生んでいる。

**完全な手詰まりではない**: `CyclePanelFocus()`（Shift+Tabの独自巡回、`SheetNavList→LadderCanvasHost→DeviceTableGrid`）にツールバーは含まれないため、Shift+Tabを2回押せばキャンバスへ到達し継続操作は可能。ただしこれは非自明な回避策であり、素直に矢印キーやマウスクリックを試すユーザーには「無反応」に見える。ステータスメッセージ（「左右キーで長さを調整しEnterで確定」等）も未達成のまま表示され続け、誤解を招く。

**既存記録との関係**: `docs/ecad2-t021-focus-design-consolidation-plan-onmitsu.md`は既存3ボタン（SelectDefault/BuiltinPlace/OpenPartSelection）のみを分析対象としており、新設のようなマウス代替なし記入系ボタンへの適用は検証範囲外。「既知・容認済みの制約」として記録された形跡は見当たらなかった（`docs/ecad2-t047-presurvey-onmitsu.md`も「後続フロー自体は変更不要」と推測記述のみで本件は未検証）。

**推奨**: 忍者の実機検証観点に「Tab+Enterでの3ボタン起動→キーボードのみでの続行可否」を追加するか、侍側で修正（例: 新設3ボタンのみ`ConsumeToolButtonFocusRestore`を使わず常時`FocusCanvas()`を呼ぶ、他10ボタンとは異なる専用ポリシーとする）を検討されたい。

### 所見2（簡潔化提案、確度: 低〜中）5つの新規Clickハンドラが既存のTag集約パターンを踏襲していない

- **file**: `src/Ecad2.App/MainWindow.xaml.cs:1301-1329`（新規5メソッド）、対比: `1074-1085`（`BuiltinPlaceButton_Click`、Tag経由の単一ディスパッチャ）
- 既存6ボタンは`Tag="a接点"`等をXAMLに積み、`BuiltinPlaceButton_Click`1メソッドで捌く設計だが、新設5ボタンは「呼び分け先が違うだけ」のメソッドを5つ個別定義している。将来6つ目の手動配線ボタンが増えるたびに同型メソッドが複製され続ける。ただし呼び出し先メソッドの引数・シグネチャが不揃い（`TryBeginFreeLineDraft(bool)`等）なため、`BuiltinPlaceButton_Click`ほど機械的な1対1の型ではなく、個別メソッド化にも一定の妥当性がある。

### 所見3（簡潔化提案、確度: 低）IsControlCircuitSheetの実装イディオムがIsMainCircuitSheetと不揃い

- **file**: `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:139,143`
- `IsMainCircuitSheet => CurrentSheet?.MainCircuit == true;`に対し`IsControlCircuitSheet => CurrentSheet is Sheet sheet && !sheet.MainCircuit;`とパターンが異なる。`CurrentSheet?.MainCircuit == false`と書けば対称的な1行イディオムに揃う（結果は同一）。機能上のバグではない。

### 所見4（構造的懸念、確度: 中、経過観察向き）シート種別による活性判定ロジックの3重化

- **file**: `MainWindowViewModel.cs:137-143`（新規）、`MainWindow.xaml.cs:738,753`（キー分岐、既存）、`MainWindow.xaml.cs:1163,1199,1226,1266`（Try系ガード、既存）
- 「このシートでこの機能が使えるか」の判定が、キーボード分岐・Try系ガード・XAML `IsEnabled`バインディングの3箇所に独立実装されている。現状`Sheet.MainCircuit`は単純boolのため3箇所とも機械的に一致しているが、コンパイラ・テストによる一致保証はない。将来シート種別が3種以上に拡張された場合、3箇所全ての手作業追随が必要になり、直し忘れ時に「キーボードでは使えるがボタンは無効のまま」等の表示と実挙動の不一致を生みうる。T-047固有の新規リスクというより、T-041由来の既存構造への追随（3つ目のコピー追加）。

### 所見5（将来リスク、確度: 中、経過観察向き）NotifyCurrentSheetDependentPropertiesChangedは規約止まりで構造的強制力がない

- **file**: `MainWindowViewModel.cs:145-154`
- 「CurrentSheet実体を変えうる経路は3箇所」という前提はコメントでの列挙と開発者の記憶に依存しており、コンパイラによる強制はない。現状`Sheet.MainCircuit`はシート新規作成時にしか設定されないため実害はないが、将来「既存シートの種別を後から変換する」機能が追加された場合、その実装者が3経路のいずれも経由せず`Sheet.MainCircuit`を直接書き換えると、CurrentSheetIndexの早期returnトラップ（T-041既知）と同型のバグが対象を変えて再現しうる。今回のdiff自体は3経路を正しく網羅しており不具合ではないが、次に4つ目の変更経路が追加された際の再発条件は据え置かれている。

### 所見6（観察レベル）テストカバレッジの穴・XMLdoc表現の軽微な不正確

- `SheetNavigationViewModel.cs:95`（`AddCommand`の`wasEmpty`経路、空文書への最初のシート追加）が`IsMainCircuitSheet`/`IsControlCircuitSheet`通知を直接検証するテストを持たない（動作は正しいことを確認済み、将来の回帰検知網にやや穴がある）。
- `MainWindowViewModel.cs:137-138`のXMLdocが「`CurrentSheet`がnull(`HasProject=false`)の間は常にfalse」と記述するが、`CurrentSheet is null`と`HasProject is false`は厳密には同値でない（`CurrentSheetIndex`が範囲外なら`HasProject=true`でも`CurrentSheet`はnullになりうる）。現状`CurrentSheetIndex`を書き換える箇所は全て範囲内の値のみ代入するため実害はないが、将来の実装者を誤誘導しうる記述。

### Reuse/Efficiency/Conventionsアングルの結果

- **Efficiency**: 該当なし（`NotifyCurrentSheetDependentPropertiesChanged`の追加コストは低頻度イベントに対するトリビアルなgetter呼び出しのみで実害なし、実測ベースで確認済み）。
- **Conventions**: `CLAUDE.md`の担当パス・依存追加禁止・品質哲学いずれにも明確な違反なし。
- **Reuse**: `IsControlCircuitSheet`は既存`InverseBooleanConverter`で代替できた可能性が指摘されたが、CurrentSheetがnullの際の3値挙動（両方false）を素直に表現するには結局同等の複雑さが必要（Simplificationアングルで検討済み、現状の2プロパティ方式は過剰ではないと判定）。

---

## 3. 総括

機能ロジック（対応表・通知一元化・活性制御の配下関係・テスト網羅）はいずれも正確で、234件のテストも実測で全合格を確認した。**所見1（新設3ボタンのキーボード起点操作でのUX不備）のみ要修正相当**と判断する。他の所見（2〜6）は構造的な将来リスクや簡潔化の余地であり、実害は現状ないため経過観察が妥当と考える。

修正要否・優先順位の仕分けは家老の裁定に委ねる。

---

## 4. 不明点

- 所見1について、忍者の実機（UI Automation経由）でTab+Enter操作を再現した場合に実際にどう見えるか（ステータスバー表示・フォーカスリング等の視覚的挙動）は静的読解のみでは確認できていない。実機検証を推奨する。
