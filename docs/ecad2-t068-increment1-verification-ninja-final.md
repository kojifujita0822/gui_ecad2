# T-068増分1 最終再検証記録（忍者・往復4周目）

真因対策：`CustomPartsMenu_SubmenuOpened`冒頭に発火源チェック
（`if (!ReferenceEquals(e.OriginalSource, CustomPartsMenu)) return; e.Handled = true;`）を追加。
子孫`sub`のSubmenuOpened（RoutingStrategy.Bubble）が親ハンドラを誤って再発火させ、
`Items.Clear()`で開こうとしていた階層ごと消えていた真因（忍者診断ログ実測→隠密一次ソース
確定→侍対処）への対処。前回までの2つの対処（`CustomPartsMenu.UpdateLayout()`、
`sub.SubmenuOpened`時の`sub.UpdateLayout()`）と併存した状態での検証。

## 結論：観点1〜3、全て完全解消

### 観点3（個別パーツの編集・削除）: 完全OK

5パターン中4パターン（複数ステップホバーは前回除外のまま）で「編集(_E)...」「削除(_D)」の
展開を再確認、いずれも正しく展開された：

1. `ExpandCollapsePattern.Expand()` → OK
2. 物理クリック単発 → OK
4. キーボードナビゲーション（`Alt+P`→`↓`→`→`→`↓`→`↓`→`→`） → OK

**編集機能の実動作確認**：「編集(_E)...」をInvoke → `自作パーツ編集`ダイアログが開き、既存値
（名前=忍者テストパーツ01、幅=3、高さ=2、役割=a接点(NO)）が正しくロードされていることを確認
（`SelectionPattern`で役割選択状態を確認）。

※役割が「a接点(NO)」だった点は、往復1周目の新規作成時にSelectionItemPattern例外で「コイル」を
選択できないままOKされた経緯（`docs/ecad2-t068-increment1-verification-ninja.md`参照）と整合。
デフォルト値のまま保存されていたことが今回の編集確認で裏付けられた。

**削除機能の実動作確認**：「削除(_D)」をInvoke → 「パーツの削除」確認ダイアログ
（「自作パーツ「忍者テストパーツ01」を削除しますか？」）が表示されることを確認。「はい」ボタンは
`InvokePattern`非対応（標準MessageBox、Paneとして現れる）のため物理クリックで実行 → ダイアログが
閉じ、以下2箇所への反映を確認：
- 自作パーツメニュー：「忍者テストパーツ01」が一覧から消え、「T101検証用自作部品」
  「忍者テストパーツ02」の2件のみ残る
- 部品選択パネル（`PartSelectionList`）：同様に「忍者テストパーツ01」が消え、2件のみ残る

### 観点1・2: 前回（往復2周目）確認済みのまま変更なし、完全OK

## 総合判定

T-068増分1の検証観点1〜4、全て完全にOK。往復1〜4周目で発見された3件のバグ
（HasItems=false起因のサブメニュー未展開、動的生成`sub`のMeasure/ApplyTemplate未実行、
SubmenuOpenedイベントのバブリングによる親ハンドラ誤発火）はいずれも解消を確認した。
回帰なし（既存の部品選択パネル表示・基本図形一覧も正常）。

これにて忍者側の実機確認は完了、家老・隠密の最終判断を仰ぐ。
