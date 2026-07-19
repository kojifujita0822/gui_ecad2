# T-067(4) フォーカスロスト確定バグ 実機確認記録（忍者、2026-07-19）

## 前提
侍が診断ログ計装済み（`%TEMP%\ecad2-diag.log`、`AppendDiagLog`呼び出し）。忍者はUI操作で
再現しつつログを実測した。

## 検証手順・結果

1. シート追加 → 「グループ枠記入」でキャンバス上に枠を作成 → 枠の上辺境界線をダブルクリック
   （`HitTestFrame`は境界線近傍のみヒットする実装のため、枠内部ではなく境界線を狙う必要あり）
   → `OpenFrameLabelEditor`正常発火、`FrameLabelBox.IsFocused=True`をログで確認。
   **PrintWindow撮影ではこのTextBoxオーバーレイが写らなかった**（キャンバス上のオーバーレイ
   描画に関する既知の罠と同型、`CopyFromScreen`方式で裏取りし正しく表示されていることを確認
   ——`ecad2-ui-automation`スキル6節に既存の教訓通り）。

2. **フォーカスロスト確定（NG再現）**：TextBoxへ"TEST1"を入力後、以下2パターンで別要素への
   マウスクリックを試行：
   - キャンバス上の枠外（相対800,500）を物理クリック → ログ変化なし、TextBox表示のまま
   - ツールバー「選択ツール」ボタンを物理クリック → ログ変化なし、TextBox表示のまま
   いずれも`FrameLabelBox_LostKeyboardFocus`が発火せず、`CommitFrameLabelEditor`も呼ばれない。
   **殿の実機操作でも確定されないという報告と一致する再現に成功**。

3. **対照実験(1) Tab確定**：Tabキー送信 → ログに`FrameLabelBox_LostKeyboardFocus: fired,
   OldFocus=TextBox, NewFocus=null` → `CommitFrameLabelEditor` → `CloseFrameLabelEditor`が
   正常な順序で発火。**Tab確定は正常動作**（ハンドオーバーメモの既知情報と一致）。

4. **対照実験(2) 原因の手がかり＝クリックそのものが他要素に届いていない疑い**：再度ラベル編集を
   開いた状態で、「a接点配置(F5)」ボタンを物理クリックしたところ、**ステータスバーのツール表示は
   `Select`のまま変化せず**（正常ならクリックで`PlaceElement`等へ変化するはず）、診断ログにも
   一切変化がなかった。同様に「選択ツール」ボタン・キャンバス枠外クリックのいずれも、ボタンの
   機能自体が働いた形跡（ツール状態変化・ログ出力）が一切見られなかった。

   **これは「マウスクリックによるフォーカス移動が失敗している」のではなく、「TextBox表示中は
   マウスクリックそのものが他のUI要素にルーティングされていない」ことを強く示唆する**。
   ハンドオーバーメモに記載の実装方針（「配置バー・行コメントエディタと同様にMainContentAreaの
   IsEnabled(IsMainContentEnabled)と連動させる」）を踏まえると、FrameLabelEditor表示中は
   MainContentArea全体がIsEnabled=Falseになっており、WPFの入力ルーティング上、無効化された
   コントロールへのマウス入力は処理されない（ヒットテスト自体が素通りする）ため、ユーザーが
   別要素をクリックしても何も起きず、結果としてLostKeyboardFocusも発火しない、という説明が
   有力と考えられる。

5. **対照実験(3) Esc取消**：Escキー送信 → `CloseFrameLabelEditor` → `FrameLabelBox_
   LostKeyboardFocus(NewFocus=LadderCanvas)`の順で正常発火（Tabとは逆順＝Escはプログラム側が
   明示的にフォーカスをLadderCanvasへ戻す実装と読める）。**Esc取消は正常動作**。

## 判定まとめ

| 確定契機 | 結果 |
|---|---|
| ダブルクリックで開く | OK |
| Enter確定 | 未実施（ハンドオーバーメモにより既知OK） |
| Tab確定 | OK（本検証で再確認） |
| Esc取消 | OK（本検証で再確認） |
| フォーカスロスト確定（マウスクリックで他要素へ） | **NG再現**。原因の強い手がかり＝
  TextBox表示中はMainContentArea無効化によりマウスクリックが他要素へ届いていない疑い |

## 侍・隠密への申し送り
上記4の所見（マウスクリックがTextBox表示中は他要素に届いていない疑い）が事実なら、これは
「フォーカスロストイベントの発火漏れ」という当初の理解とは異なり、**「そもそもユーザーが
別要素をクリックする操作自体が成立していない」という、より根本的な設計上の問題**である
可能性がある。`IsMainContentEnabled`と`FrameLabelEditor`（および同型の他エディタ）の
バインディング実装箇所の確認を推奨する。ただし、この解釈は忍者の実機観測（ツール状態・
ログの無反応）からの推論であり、コード上の一次確認は行っていない（担当外のため）。

## 証跡ファイル
- `%TEMP%\ecad2-diag.log`（追記式、本検証区間は該当タイムスタンプ`19:56:34`〜`20:02:01`）
- `%TEMP%\claude\ecad2\t067-4-copyfromscreen-check.png`：ラベル編集オープン直後（CopyFromScreen）
- `%TEMP%\claude\ecad2\t067-4-after-outside-click.png`：枠外クリック後もTextBox残存
- `%TEMP%\claude\ecad2\t067-4-after-toolbar-click.png`：ツールバークリック後もTextBox残存

## 回帰の有無
Tab/Enter/Esc確定・ダブルクリックオープンには回帰なし。フォーカスロスト確定のみ引き続きNG。

## 範囲外検出
なし。
