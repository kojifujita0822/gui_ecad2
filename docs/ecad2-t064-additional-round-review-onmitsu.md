# T-064 追加往復修正 再レビュー(隠密・フル観点)

- 対象コミット: `0f9977e`(侍、T-069往復4周目フル観点レビュー指摘・殿裁定2026-07-13に基づく即修正)
- 実施日: 2026-07-13
- 実施者: 隠密
- 方式: DoD整合確認+`code-review`スキル(フル観点、2並列エージェント、定量分析含む)
- スコープ境界: レビューのみ、書き込みなし

## 結論サマリ

今回の修正(`ReplaceDocument`への`CancelImageInsertDraft()`追加)自体は正しく機能しており、幽霊プレビューの原因診断も正確、既存2メソッドとの対称性も完全。RED証明・テスト内容も整合している。

**ただし、同じ`ReplaceDocument`内、今回の修正箇所のわずか6行上に、別の横展開漏れ(CONFIRMED)を発見した**——`SelectedImage`のクリアが漏れている。これはパターン台帳PR-01(新規選択可能状態の横展開漏れ)そのものの典型例。加えて、Altitude観点で「今回の対応が場当たり的である」という構造的指摘も得た。

## DoD達成確認

| 観点 | 結果 |
|---|---|
| 修正が正しく機能しているか | 確認OK。`CancelImageInsertDraft()`は`if(_imageInsertDraft is null) return;`ガードを持ち、`ReplaceDocument`内での呼び出し位置(Document差替後・既存のTool再代入より前)も問題なし。 |
| 幽霊プレビュー原因診断の正確性 | 確認OK。`RedrawCanvas`(`MainWindow.xaml.cs:152-161`)は`ImageInsertDraftPreview`をTool.Mode非依存で無条件にDrawへ渡している。`ViewModel_PropertyChanged`(84-95行目)が`ImageInsertDraftPreview`変更時に正しくRedrawCanvasをトリガーすることも確認。診断は正確。 |
| 既存2メソッドとの対称性 | 確認OK。`ClearConnectorDraftIfAny`/`ClearFreeLineDraftIfAny`/`CancelImageInsertDraft`の3メソッドは構造(nullガード・フィールドクリア・Tool代入・OnPropertyChanged)が完全に対称。 |
| RED証明報告とテスト内容の整合 | 確認OK。新規テスト`ReplaceDocument_ClearsImageInsertDraft_OnNewDocument`は修正前コードでFAIL・修正後PASSすることをコード読解で確認。 |

## 新規発見1(CONFIRMED、重大): `ReplaceDocument`が`SelectedImage`をクリアしていない

**該当**: `MainWindowViewModel.cs:2077-2082`(今回の修正箇所の直前)

`SelectedConnector`/`SelectedWireBreak`/`SelectedFreeLine`/`SelectedConnectionDot`の4つはsetter経由で明示クリアされているが、**`SelectedImage = null;`だけが無い**(`SelectedImage=null`は`SelectedCell`のsetter内にしか存在せず、`ReplaceDocument`は`_selectedCell`を直接代入=setterバイパスのため、この自動クリアも効かない)。

**失敗シナリオ**: 画像を選択中(`SelectedImage`≠null)に確定・キャンセルせず新規/開くを実行すると、`_selectedImage`が旧Documentの`ImageInsert`実体を保持したまま残留する。`HasSelectedImage`/`HasNoPropertySelection`が偽の状態を返し続け、プロパティパネルに実体の消えた画像のプロパティ(トレース用下絵チェックボックス等)が表示・編集可能なまま残る可能性がある。

**パターン台帳との照合**: PR-01(新規選択可能状態の横展開漏れ)そのものの典型例。皮肉なことに、**今回の修正(画像挿入ドラフトのクリア漏れ)のわずか6行上に、同種の別の横展開漏れ(画像選択状態のクリア漏れ)が存在していた**——「指摘された1件のみを塞ぐモグラ叩き」になっていたことを示す実例。台帳PR-01への再発履歴追記を推奨。

## 新規発見2(Altitude、構造的): 一元化ヘルパーが存在するのに再利用されず個別列挙が継続

**該当**: `MainWindowViewModel.cs:2085,2087,2095`(今回の修正) vs `:1482-1487`(既存の`CancelResidualDraftForToolSwitch`、T-069往復4周目で新設)

3種ドラフト全クリアは既に`CancelResidualDraftForToolSwitch()`として一元化済み(`MainWindow.xaml.cs:1672/1909`が利用)にもかかわらず、`ReplaceDocument`はこれを再利用せず個別列挙(`ClearConnectorDraftIfAny`/`ClearFreeLineDraftIfAny`/`CancelImageInsertDraft`)を継続し、今回さらに1行を追加する形で対応した。`ReplaceDocument`の該当3行は`CancelResidualDraftForToolSwitch();`1行へ置換可能(直後にTool=SelectDefaultの再設定があるため挙動差分なしを確認済み)。

定量データ(`_connectorDraft`/`_freeLineDraft`/`_imageInsertDraft`の参照箇所): 各フィールドの「無条件全クリア」は`SelectedCell`setter・`ReplaceDocument`・`CancelResidualDraftForToolSwitch`の**3箇所で個別に名指し列挙**されている。クリア責務は分散したままであり、将来4種目のドラフトが追加されれば今回と同型のクリア漏れが再発しうる構造。

**評価**: 今回の1行追加は「殿裁定の指摘1件を塞ぐ」限りでは正しいが、(a)新規発見1(SelectedImage漏れ)が同じ関数内に現存する、(b)一元化ヘルパーが再利用されていない、の2点から**「十分な深さの修正」ではなく「その場しのぎ」寄り**と判断する。根本修正としては、`ReplaceDocument`と`SelectedCell`setter双方を単一のクリア入口(命名変更を伴うなら`ClearAllDraftsIfAny`等、「ツール切替専用」を含意する現行名`CancelResidualDraftForToolSwitch`は汎用ヘルパーとしては命名が実態と乖離)へ統一することが望ましいが、これは往復5周目以降の判断事項。

## 軽微な指摘(参考)

- テストコメント「ConnectorDraftTests/FreeLineDraftTestsの同型テスト...と対称に揃える」は事実誤認(`ConnectorDraftTests.cs`に`ReplaceDocument`系テストは存在せず、`FreeLineDraftTests.cs`のみ該当)。実害は軽微(コメントのみ)。

## 派生提案の有無

あり(上記「新規発見1」CONFIRMED重大・「新規発見2」構造的指摘)。自らは着手せず家老へ報告のみ。
