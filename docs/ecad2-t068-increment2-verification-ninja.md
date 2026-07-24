# T-068増分2 実機確認記録（忍者）

最終更新: 2026-07-25

## 対象

- コミット: `e5d54e4`（PartEditorDialog 端子タブ、RowOffset/BoundaryOffsetのリスト形式追加・削除）
- 家老委譲: 2026-07-24 22:23（隠密静的レビュー完了後の号令）
- 検証観点は家老予告どおり4点（下記）

## 環境・準備

- `ecad2-ui-automation`スキルに従いセカンダリモニタで`Start-Ecad2App`起動、build事前確認（警告0・エラー0）。
- **判定手段はUIA機械読取に統一**（`GridPattern.GetItem`でセル取得、`DataItem`列挙で行データ確認）。目視断定はしていない。
- 検証用に「T068検証部品」（本編検証、増分1機能から継続保有）・「T068削除用テスト」（削除確認専用、検証後削除済み）の2件を作成。既存の「T101検証用自作部品」「忍者テストパーツ02」（過去セッションの遺物）は無変更のまま残置。

## 判定結果

| 観点 | 判定 | 詳細 |
|---|---|---|
| 1. 行追加・削除・値編集 | **OK** | 「追加」で自動命名(P1→P2→P3)・RowOffset/BoundaryOffset初期値0の行を追加。`GridPattern.GetItem(row,col).ValuePattern.SetValue()`で値編集(P1:RowOffset=3/BoundaryOffset=5、P2:RowOffset=7/BoundaryOffset=1)。行(DataItem)の`SelectionItemPattern.Select()`→「削除」ボタンで対象行のみ削除、他行は無傷(3行→2行、削除対象=P3のみ消失)。 |
| 2. バリデーション(両側) | **OK** | (a)役割=「a接点(NO)」・ポート1点で保存 → 拒否、`ErrorText`="接続点(ポート)を2つ以上配置してください。"がVisible=True、ダイアログは閉じず。(b)役割を「非シミュレート」に変更・同じポート1点のまま保存 → 拒否されずダイアログが正常に閉じ、再度開いて`Saved role='非シミュレート'`・`Saved port row count=1`を確認。両側とも期待どおり。 |
| 3. 保存時並べ替え(BoundaryOffset昇順) | **OK** | 保存前=P1(BoundaryOffset=5)→P2(BoundaryOffset=1)の順(降順)で保存、保存後に再度開くと P2(BoundaryOffset=1)→P1(BoundaryOffset=5) の順に並べ替え済み。before/after両方を実際に確認(T-121教訓踏襲)。 |
| 4. 回帰確認(増分1機能) | **OK** | 名前編集(`NameBox`="T068検証部品"が保存後も保持)・幅高さ編集(`WidthBox`=3/`HeightBox`=2でOK保存、ダイアログ正常終了)・役割編集(a接点(NO)→非シミュレートの切替が保存に反映)を確認。パーツメニュー新規作成(「パーツ(P)」→「新規作成(N)...」)・編集(自作パーツサブメニュー→対象パーツ→「編集(E)...」)・削除(同→「削除(D)」→標準MessageBox「はい(Y)」物理クリックで確定)の3導線とも正常動作、削除後は自作パーツ一覧から対象が消失していることをUIAで確認。 |

## 操作手法

- 追加・削除ボタン、タブ切替、コンボボックス選択: `InvokePattern`/`SelectionItemPattern`/`ExpandCollapsePattern`(UIA標準パターン、フォーカス非占有)。
- DataGridセル値編集: `GridPattern.GetItem(row,col)`で取得したセルの`ValuePattern.SetValue()`(標準パターン、非占有)。
- パーツメニュー(「パーツ(P)」「自作パーツ(C)」サブメニュー)の展開・項目実行: `ExpandCollapsePattern.Expand()` + `InvokePattern.Invoke()`。**座標クリック(`Invoke-Ecad2ScreenClick`)は途中まで使用したが不安定と判明したため本編ではInvokePattern経由に統一**(詳細は下記所見1)。
- 標準MessageBox(削除確認ダイアログ)の「はい(Y)」ボタン: `Invoke-Ecad2ScreenClick`による物理クリック相当(合成マウスイベント、`InvokePattern`非対応のため。スキル既知の罠どおり)。

## 範囲外の気づき

- 隠密指摘の3点((a)`CommitEdit()`戻り値未確認 (b)自動命名`P{n}`重複しうる (c)値域・重複チェック無し)について、今回の検証操作中には該当する異常症状には遭遇しなかった。ただし(b)の重複再現条件(追加→削除→追加)は本検証で偶然近い操作(P3追加→削除)を行ったが、その後P3を再度追加する操作はしておらず、重複有無は未確認(**判別つかず**、次回追加検証の余地あり)。

## 検証中に発見した新規の罠(スキルへの追記候補、家老へ別途報告)

1. **AvalonDockタブ切替式ツールバー(「基本機能」/「配置ツール」)は、非選択タブの内容がUIA要素ツリーから完全に外れる**(T-104増分2でツールバーがLayoutAnchorableのタブ内へ移設された影響とみられる)。「配置ツール」タブ選択中は「新規作成(Ctrl+N)」等1段目ツールバーのボタンが`FindAll`で一切ヒットしない(0件ではなく該当ボタンだけ非存在)。対策: 操作前に対象タブ(`SelectionItemPattern.Select()`)を明示的に選択してから要素探索すること。
2. **メニューPopup表示中に`Invoke-Ecad2ScreenClick`(内部で`Set-Ecad2Foreground`→`SetForegroundWindow`)を使うと、Popup自体は`EnumWindows`上に存在し続ける(かつPrintWindow撮影でも正常に描画される)のに、クリックが実行されず、かつメニュー項目も実行されない「見た目は開いたまま・操作が効かない」状態になることがあった**(3回再現)。原因未特定(SetForegroundWindowによるメニュー捕捉状態への干渉が疑わしいが断定はしない)。対策: メニュー項目の実行は座標クリックでなく`ExpandCollapsePattern`/`InvokePattern`のUIA標準パターン経由に統一すると安定した。

## 総括

**T-068増分2、検証観点1〜4すべてOK。回帰なし。** バリデーション・並べ替えとも実装どおりに機能している。範囲外の気づき(b)は判別つかずのまま留保。新規の罠2点は別途家老へ報告しスキル追記を検討されたい。
