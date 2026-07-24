# T-068増分1 再検証記録（忍者・往復2周目）

対象コミット: `a12736c`（重大バグ修正、ダミー子項目でHasItems=true確保）
前回記録: `docs/ecad2-t068-increment1-verification-ninja.md`

## 再検証結果

### 観点1: 完全OK（役割選択の留保も解消）
名前・幅・高さのValuePattern.SetValue（UIA非占有）に加え、役割コンボは**物理クリック**
（`SetCursorPos`+`mouse_event`合成、ドロップダウンを開いて「コイル」項目を直接クリック）で
選択に成功。`SelectionPattern.Current.GetSelection()`で「コイル」が選択されたことを確認。
OKで保存も成功（ダイアログが閉じることを確認）。前回`SelectionItemPattern`が使えなかった点は
物理クリックで解決した。

### 観点2後半: OK（今回の修正で解決）
「パーツ(_P)」→「自作パーツ(_C)」をExpandCollapsePattern（UIA非占有）で展開したところ、
`SupportedPatterns`に`ExpandCollapsePatternIdentifiers.Pattern`が含まれるようになり（前回は
未サポートで例外）、実際に展開すると「T101検証用自作部品」「忍者テストパーツ01」の2件が
一覧表示されることを確認。前回の重大バグは解消された。

### 観点3: 依然として未確認（新たな類似症状）

個別パーツ項目（例：「忍者テストパーツ01」）自体の子メニュー（「編集(_E)...」「削除(_D)」）を
開こうとしたところ、以下5パターンいずれでも**メニュー階層全体が閉じてしまい**、編集/削除項目に
到達できなかった：

1. `ExpandCollapsePattern.Expand()`（対象項目の`SupportedPatterns`には`ExpandCollapsePattern`が
   含まれることを確認済み＝HasItemsはtrueと思われるにもかかわらず展開後に全階層が閉じる）
2. 物理クリック（単発、`SetCursorPos`+`mouse_event`）
3. 複数ステップでのカーソル移動によるホバー連続性確保＋クリック（この過程で意図しない別要素へ
   カーソルが逸れる副作用も観測）
4. キーボードナビゲーション（`Alt+P`→`↓`→`→`→`↓`→`↓`→`→`）
5. Right送信直後を0ms/100ms/500ms間隔でEnumWindowsサンプリング → 送信直後(0ms)には既にポップアップ
   ウィンドウ数が「パーツ→自作パーツ」の2階層分から1つ減っており、3階層目のポップアップが
   一瞬でも出現した形跡なし

対象コードは`CustomPartsMenu_SubmenuOpened`内の以下の部分（`MainWindow.xaml.cs`、行番号は
`74eb164`時点、今回未変更）：

```csharp
var sub = new MenuItem { Header = entry.Definition.Name };
var editItem = new MenuItem { Header = "編集(_E)...", Tag = entry };
editItem.Click += EditPartMenuItem_Click;
sub.Items.Add(editItem);
var deleteItem = new MenuItem { Header = "削除(_D)", Tag = entry };
deleteItem.Click += DeletePartMenuItem_Click;
sub.Items.Add(deleteItem);
CustomPartsMenu.Items.Add(sub);
```

コード上は`sub`がツリー（`CustomPartsMenu.Items`）へ追加される前に既に2つの子（editItem/
deleteItem）を`Items.Add`済みのため、今回のバグ修正（XAML静的宣言でダミー子項目が無くHasItems=false
になっていた問題）とは構造が異なる。しかし実際の症状は前回と酷似しており（サブメニューが一切
展開されない）、忍者の確度としては「同型の問題が動的生成コードにも起きている可能性」を疑うが、
断定はできない（推測報告禁止のため）。

## 総合判定・要判断

- 観点1: 完全OK
- 観点2: 完全OK（前回の重大バグは解消確認）
- 観点3: **未確認のまま**。個別パーツ項目の子メニュー（編集/削除）展開が同型の症状で開けない
- 観点4: 前回確認済み（今回変更なしのため再確認省略）

侍・隠密による追加の一次ソース確認（動的生成`MenuItem`の`Items.Add`タイミングと`HasItems`判定の
関係、または`ItemContainerGenerator`のPopup生成タイミング）を推奨する。
