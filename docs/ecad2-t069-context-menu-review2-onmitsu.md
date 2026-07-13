# T-069 右クリックメニュー 事後レビュー(隠密・往復2周目、フル観点)

- 対象コミット: `95f5e36`(1周目レビュー指摘4件への修正)
- 実施日: 2026-07-13
- 実施者: 隠密
- 方式: DoD(a)(b)(c)突合+テストコード網羅性点検(d、往復2周超ゆえ適用)+`code-review`スキル併用(e、effort相当高、10角度並列finder→集約)
- スコープ境界: レビューのみ、書き込みなし
- 注記: 実測`dotnet test`は他役(侍)がT-064等を並行作業中で作業ツリーに未コミット変更が入っており(`ImageRectDip`未解決エラー)実行不能だった。侍申告(582件全合格)を根拠として扱う。

## 結論サマリ

**新たな要修正3件を発見。往復3周目が必要と判断する。** 修正1(HitTestElement新設)は判定ロジック自体は正しいが、**メニュー表示と実行(削除・機器名変更)が別々の判定ロジックを使っており、実行側が旧来のまま放置されたため、メニューは出るのに操作が黙って失敗する**という新たな連鎖バグを生んだ。修正2(CommitDeviceNameEdit追加)は要素・縦コネクタ分岐では正しく機能するが、**行操作メニュー経由の間接消失経路には未対応**。修正3+4(Tool.Modeガード)は記入中ドラフト保護そのものは達成したが、**ガードの粒度が粗く、「連続配置」という常用ワークフロー中を含め、記入中ドラフトと無関係な操作(行操作メニュー全般・PlaceElement中の削除等)まで長時間巻き添えで機能低下している**。

## 要修正(最重要、CONFIRMED、3系統独立検出+自己裏取り済み)

### 1. SelectedElement再解決失敗——メニューは出るが操作が黙って失敗する

**該当**: `MainWindowViewModel.cs:1505-1506`

```csharp
public ElementInstance? SelectedElement
    => SelectedCell is { } pos && CurrentSheet is Sheet sheet ? sheet.Elements.FirstOrDefault(el => el.Pos == pos) : null;
```

修正1で新設した`HitTestElement`(区間交差判定、`el.Pos.Column <= pos.Column && pos.Column <= el.Pos.Column + el.CellWidth - 1`)は右クリック時のメニュー表示判定にのみ使われる。しかし、メニュー項目実行時に参照される`SelectedElement`ゲッターは**旧来の単純一致判定(`el.Pos == pos`)のまま放置**されており、両者が整合していない。

**失敗シナリオ**: Motor(Pos=(0,1)、CellWidth=3、列1-3占有)の非アンカーセル(列2)で右クリック→`HitTestElement`が正しくMotorを検出しメニュー(削除・機器名変更・コメント編集)を表示→`SelectedCell=(0,2)`が代入される→

- 「削除」選択: `DeleteMenuItem_Click`→`DeleteSelectedElement()`(`MainWindowViewModel.cs:1596-1612`)は`SelectedElement is not ElementInstance el`ガードで即`false`を返す(`el.Pos==(0,1)`≠`SelectedCell==(0,2)`)。Connector/WireBreak/FreeLine/ConnectionDot系の削除も該当なしで全てfalse。**Motorは削除されず盤面に残るが、ユーザーには何も起きたか分からない**(RedrawCanvasも呼ばれない)。
- 「機器名変更」選択: `DeviceNameBox.Focus()`はバインド先`SelectedElementDeviceName`(`SelectedElement`ベース)が空文字を返すため**入力欄が空欄表示になる**(本来の"M1"が消えたように見える)。ユーザーが新名を入力しEnter/フォーカス喪失で`CommitDeviceNameEdit()`が走っても、setter側が`SelectedElement is not ElementInstance el`で即returnし、**変更は一切反映されない**。エラー表示も無いため、ユーザーは変更が成功したと誤認する。

**根拠**: `DeleteSelectedElement()`(1598行目)・`SelectedElementDeviceName`のgetter/setterは、いずれも`SelectedElement`ゲッター(1505行目)を経由する。`GridPos`は`readonly record struct`ゆえ`==`は値の完全一致のみtrueであり、区間交差の概念を持たない。

**評価**: これは1周目の要修正1(CellWidth判定漏れ)を修正しようとした結果、**表示は直ったが実行が伴わない新たな連鎖バグ**を生んだもの。「メニューが出るのに何も起きない」というのは、1周目の「メニューが出ない」より発見しづらく、ユーザーにとって誤解を招きやすい点でむしろ悪化している側面がある。

**修正案(参考、判断は侍・家老に委ねる)**: `SelectedElement`ゲッター自体を`HitTestElement`ベースに統一する(`SelectedCell is {} pos ? HitTestElement(pos) : null`)のが最も一貫するが、`SelectedElement`は左クリック選択・矢印キー移動等の既存機能でも広く参照されているグローバルな概念のため、挙動変更が他機能に影響しないか要検討。代替として、右クリックハンドラ側で「非アンカーセルで検出した場合は検出要素のアンカー位置(`el.Pos`)を`SelectedCell`に設定する」という局所的な修正も考えられる。

**検出経路**: code-reviewスキルAngle C・Dの2系統独立検出+隠密が`SelectedElement`/`DeleteSelectedElement`/`SelectedElementDeviceName`の実装を読解し裏取り済み。

## 要修正(CONFIRMED、複数角度独立発見)

### 2. Tool.Modeガードの粒度が粗く、記入中ドラフトと無関係な機能まで一律剥奪

**該当**: `MainWindow.xaml.cs` `LadderCanvasHost_PreviewMouseRightButtonDown`冒頭の`if (_viewModel.Tool.Mode != ViewModels.ToolMode.Select) return;`

このガードは記入中ドラフト保護を目的に導入されたが、右クリックハンドラ**全体**の入口に置かれたため、記入中ドラフトと無関係な処理まで一律ブロックしている。

- **行操作メニュー(else節、行の挿入/追加/削除)**: この分岐は`SelectedCell`/`SelectedConnector`への代入を一切行わない(コード確認済み)ため、そもそも「メニューを開いただけでドラフトが消える」という導入理由が当てはまらない。にもかかわらず一律ブロックされており、**根拠のない機能剥奪**になっている。
- **部品配置モード(PlaceElement、記入中ドラフトを持たない静的な状態)**: 1周目(4e049c6)時点では、配置モード中でも既存要素の削除・既存縦コネクタの削除・行操作メニューは正常に機能していた(「機器名変更」のみが無反応だった)。今回のガードでこれら**全てが使えなくなった**(配置スペースを空けるため既存要素を削除したい、という自然な操作列がEscで一旦ツールを捨てないと行えなくなった)。
- **F2キーとの非対称**: コメント編集の既存キーボード経路(F2、`MainWindow.xaml.cs`)は`Tool.Mode`を問わずSelectedCellの有無のみで動作するが、右クリックメニュー経由のコメント編集だけが`Tool.Mode==Select`限定に狭められた。同一機能が操作経路によって到達可否が割れている。

**評価**: 殿裁定(記入中ドラフト保護を優先)に基づく実装ではあるが、実際の影響範囲は「記入中ドラフトを実際に持つモード(PlaceConnector/PlaceLine/PlaceImage)」を超え、ToolMode全8種([Select, PlaceElement, PlaceConnector, PlaceFrame, PlaceLine, PlaceDot, PlaceWireBreak, PlaceImage])を一枚岩でブロックしている。ガードの粒度をより細かくする(例えば「SelectedCell/SelectedConnectorへの代入を伴う要素・縦コネクタ分岐のみ」をTool.Modeでガードし、「行操作メニュー(else節)」は常時許可する)ことで、ドラフト保護を維持しつつ機能剥奪を最小化できる可能性がある。殿裁定の意図がどこまでの範囲を許容するものだったか、確認を推奨する。

**実害の具体化(Angle A追加発見)**: PlaceElementモードは「記入中ドラフト」を一切持たない静的な状態だが、T-021分岐A(殿裁定)により**要素配置後もTool/SelectedCellが意図的に保持され続ける「連続配置」がこのアプリの常用ワークフロー**(ツールバーのa接点ボタン等を押すと配置後も解除されない設計)。この間ずっとTool.Mode==PlaceElementのままになるため、連続配置作業中は既存要素の削除・行操作・縦コネクタ削除といった右クリックメニュー機能全般が長時間にわたり封じられ、Escで一旦ツールを解除しない限り到達できない。これは「記入中ドラフト保護」という目的からは説明できない副作用であり、実害の大きさが当初の想定より深刻。

**検出経路**: code-reviewスキルAngle A・B・Iの3系統独立検出、隠密がelse節の実装(SelectedCell代入無し)およびT-021分岐A(連続配置)の既存仕様を確認し裏取り済み。

### 3. 行操作メニュー実行後、コマンド内部のSelectedCell行シフトで未確定編集が別経路で消失

**該当**: `MainWindow.xaml.cs`行操作メニュー分岐(else節)に`CommitDeviceNameEdit()`が無い。`MainWindowViewModel.cs`の`InsertRowBeforeCommand`/`DeleteRowAtCommand`内部

行操作メニュー分岐自体は`SelectedCell`/`CommitDeviceNameEdit()`のいずれにも触れないため一見安全に見えるが、選択された`InsertRowBeforeCommand`/`DeleteRowAtCommand`の**コマンド実行内部**で`SelectedCell = sc with { Row = sc.Row ± 1 }`のような行シフトが行われ、これが`SelectedCell`のsetterを経由して`SelectedElementDeviceName`のPropertyChangedを発火させる。

**失敗シナリオ**: 既存要素の機器名編集中(`DeviceNameBox`に未確定テキストあり)に、その要素より上の行の空セルを右クリック→「行を削除」を選択→コマンド内部の行シフトで`DeviceNameBox`の表示がソース値(未確定前の値)へ巻き戻る→未確定入力が警告なく消失する。要素・縦コネクタ分岐には修正2で`CommitDeviceNameEdit()`が追加されたが、行操作メニュー経由の間接的な消失経路には対処されていない。

**検出経路**: code-reviewスキルAngle A発見(コマンド内部実装まで踏み込んだ追跡)。

## テストコード網羅性点検(DoD d、往復2周超ゆえ適用)

修正1(`HitTestElement`)の回帰テスト4件(アンカー/非アンカー/範囲外/別行)は境界値分析として適切。ただし:

- 上記要修正1の実害(`SelectedElement`との不整合)を検出できるテストが無い——`HitTestElement`単体のテストのみで、「`HitTestElement`が検出した要素を`DeleteSelectedElement()`が正しく削除できるか」というEnd-to-Endの検証が抜けている。これは境界値分析でいう「関数単体は正しいが結合点が抜けている」典型パターン。
- 修正2・3+4はView層イベントハンドラのためテスト不可(妥当な判断、既存の限界)。

## code-reviewスキル併用(10角度、経過観察)

- **IsOccupiedとHitTestElementの重複**(Angle F・G、2系統独立検出、具体的統合案あり): 両者は数式的に完全等価な区間交差判定を別々に実装している。共通ヘルパー(`FindOccupyingElement`等)への統合を推奨——「将来同種の判定式修正が入った際に片方だけ更新され再び先祖返りする」という指摘は、まさに今回1周目→2周目で実際に踏んだ経緯(T-071の教訓が一度失われた)を踏まえると説得力が高い。
- **CommitDeviceNameEdit呼び出しの重複**(Angle G): 要素分岐・縦コネクタ分岐の2箇所で同一呼び出しが複製されている。冪等な呼び出しのため、分岐前に1回呼べば足りる(軽微)。
- **「呼び出し元の自己申告に依存する設計」**(Angle E・I、2系統独立検出): Tool.Modeガード・CommitDeviceNameEdit呼び出しのいずれも、setter自体ではなく個々の呼び出し元コードビハインドに実装されている。将来GroupFrame右クリックメニュー等の新経路が追加された際、これらのガード・呼び出しを個別に複製し忘れると同種のバグが再発する(T-082でも同型の経過観察を指摘済み)。
- テスト4件のフィクスチャ生成コードの軽微な重複(Angle G)。
- CLAUDE.md規約違反なし(Angle J)。

## 派生提案の有無

なし(全指摘T-069の実装範囲内)。
