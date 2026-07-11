# T-063「名前を付けて保存」「削除」メニュー露出 差分レビュー（隠密）

対象: ブランチ`main`（マージ後）、コミット`4f8ea7f`。家老指定3観点の手動確認を実施。対象2ファイル
（`MainWindow.xaml`/`MainWindow.xaml.cs`）。

**結論：DoD1・DoD3はクリーン。DoD2（削除メニューの無効化条件なし判断）で、侍が懸念点として自ら
明示した箇所に実害の想定できる論点を発見。要修正推奨1件。**

## DoD1：「名前を付けて保存」の結線とSaveDocumentAs()呼び出しパターンの整合

`SaveDocument()`（`MainWindow.xaml.cs:214-229`）と`SaveAsMenuItem_Click()`（同240-245行）を並べて
確認した。

```csharp
private void SaveDocument()
{
    if (!_viewModel.HasProject) return;
    CommitDeviceNameEdit();
    if (_viewModel.CurrentFilePath is string path) TrySaveToFile(path);
    else SaveDocumentAs();
}

private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
{
    if (!_viewModel.HasProject) return;
    CommitDeviceNameEdit();
    SaveDocumentAs();
}
```

`HasProject`チェック・`CommitDeviceNameEdit()`確定処理は完全に同一。差分はコメントどおり「パス
確定済みでも常に`SaveDocumentAs()`へ進む」点のみ。**整合性OK、指摘なし。**

## DoD2（重点）：「削除」メニューの無効化条件なし判断の妥当性

### 侍の主張と検証結果

侍の説明：「Key.Delete case（`MainWindow.xaml.cs:892-903`）と同じ削除ロジックをそのまま流用。
`IsCanvasFocused()`判定はメニュークリックには不要、選択が無ければ各`DeleteSelected*`系は何もせず
`false`を返すため無効化バインディングも付けていない」。

**各`DeleteSelected*`メソッドの「選択なしでfalseを返しno-op」という主張は事実として正しい**
（`DeleteSelectedElement`/`DeleteSelectedConnector`/`DeleteSelectedWireBreak`/
`DeleteSelectedFreeLine`/`DeleteSelectedConnectionDot`の5メソッドすべてを
`src/Ecad2.App/ViewModels/MainWindowViewModel.cs`で確認、いずれも先頭で対象なしなら
即`return false`）。**`IsCanvasFocused()`がメニュークリックに不要という判断も妥当**（この判定は
キー入力の宛先がキャンバスかを見るものであり、メニューコマンドはフォーカス位置に依存しない）。

### 発見：Key.Delete caseとの「表面的な整合」では説明しきれない新しいリスク

`Key.Delete` case（892行）は`when noModifier && IsCanvasFocused()`という条件を持つ。この
`IsCanvasFocused()`判定により、**キャンバス以外（例：プロパティパネルの`DeviceNameBox`）に
キーボードフォーカスがある間は、この分岐に到達すること自体が構造的にあり得ない**（フォーカスは
同時に2箇所に存在しないため）。つまり既存のKey.Delete caseは、「デバイス名編集中に削除ロジックが
割り込む」という状況を、フォーカス排他性によって自動的に回避していた。

一方`DeleteMenuItem_Click`はメニュークリックというフォーカス非依存のトリガーで発火するため、
**「`DeviceNameBox`で機器名を未確定編集中（まだ`LostFocus`/`Enter`を経ていない）に、マウスで
『編集』メニューから『削除』をクリックする」という、Key.Delete caseでは原理的に起こり得なかった
新しい操作経路が生まれる**。

この経路をコードで追跡すると：

1. `SelectedElementDeviceName`（`MainWindowViewModel.cs:1218-1220`）のgetterは
   `SelectedElement?.DeviceName ?? ""`——**編集対象は常に選択中の要素そのもの**（削除対象と
   編集対象が一致する）。
2. `DeleteMenuItem_Click`は`CommitDeviceNameEdit()`を呼ばずに直接`DeleteSelectedElement()`を実行する。
3. `DeleteSelectedElement()`（同1263-1279行）は要素を削除した後、
   `OnPropertyChanged(nameof(SelectedElementDeviceName))`（1276行）を明示的に発火する。
4. `DeviceNameBox`は`UpdateSourceTrigger=Explicit`（`MainWindow.xaml.cs:156-161`のT-049コメントで
   確認、この設定は「入力→ソース」の伝播タイミングのみを制御し「ソース→表示」の伝播には影響しない）
   ため、3.の通知を受けてWPFの標準バインディング機構により**表示テキストが強制的に新しい値
   （要素削除後のため空文字相当）へ上書きされる**。

**結果：未確定で入力中だった機器名の文字列は、確定されることなく画面上から消え、モデルへも
反映されずに失われる。** これは`SaveDocument`/`SaveAsMenuItem_Click`/Undo（953行）/Redo（958行）が
軒並み処理の入口で`CommitDeviceNameEdit()`を呼んでいるのと比較すると、`DeleteMenuItem_Click`だけが
これを欠いている非対称な実装になっている。

**評価**：クラッシュ・データ破損等の重大な不具合ではなく「未確定入力が黙って破棄される」という
UX上の実害に留まるが、侍自身が懸念点として明示した箇所から実際に具体的な再現シナリオを特定できた
ため、**要修正推奨とする**。修正案は`DeleteMenuItem_Click`冒頭に`CommitDeviceNameEdit();`を1行
追加するのみ（`SaveAsMenuItem_Click`と同型）で足りる小規模な対応と見積もる。

## DoD3：他の類似メニュー項目との一貫性

実装済みで比較可能な既存メニュー項目のIsEnabled有無を確認：

| 項目 | IsEnabled |
|---|---|
| 新規 | なし（常時有効） |
| 開く | なし（常時有効） |
| PDF出力 | なし（常時有効） |
| 上書き保存 | `{Binding HasProject}` |
| 名前を付けて保存（今回） | `{Binding HasProject}` |
| 削除（今回） | なし（常時有効） |

「意味のある無効化条件（プロジェクト未オープン等）がある場合のみバインディングを付け、そうでない
場合は付けない」という一貫したスタイルの範囲内にあり、**「削除」だけが特別扱いされているわけでは
ない**。「切り取り」「コピー」「貼り付け」は今回の差分に含まれず、`Click`ハンドラ自体が未実装
（器のみ）のため直接比較はできないが、実装時は同様のスタイルになると推測される。**一貫性の観点は
問題なし。**

## まとめ

| 観点 | 判定 |
|---|---|
| DoD1: SaveDocumentAsパターン整合 | OK |
| DoD2: 削除メニュー無効化条件なし判断（重点） | 要修正推奨1件（`CommitDeviceNameEdit()`欠落） |
| DoD3: 他メニュー項目との一貫性 | OK |

## 要修正推奨

**`MainWindow.xaml.cs:980`付近（`DeleteMenuItem_Click`）**：冒頭に`CommitDeviceNameEdit();`を
追加し、未確定のデバイス名編集を削除実行前に確定させる（`SaveAsMenuItem_Click`と同型の対応）。

## 往復2周目レビュー（commit `0910658`）

侍が要修正指摘を修正。差分4行のみ：

```diff
     private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
     {
+        CommitDeviceNameEdit();
         if (_viewModel.DeleteSelectedElement() || _viewModel.DeleteSelectedConnector()
```

`SaveAsMenuItem_Click`と同型で`CommitDeviceNameEdit()`を冒頭に追加。以下を確認した：

- `CommitDeviceNameEdit()`（`UpdateSource()`呼び出し）は`SelectedElementDeviceName`セッターを
  経由するが、これは`el.DeviceName`（デバイス名文字列）を変更するのみで`SelectedElement`
  （選択中の要素インスタンス自体）は変化しない。よって続く`DeleteSelectedElement()`は確定処理の
  前後で同一の要素を正しく対象にする（削除対象がずれる懸念なし）。
- `CommitDeviceNameEdit()`はフォーカスの有無に関わらず安全に呼べる設計（`SaveDocument()`等
  既存箇所と同じ呼び出しパターン）であり、`DeviceNameBox`にフォーカスが無い通常の削除操作
  （キャンバスからの操作等）に対する副作用もない。
- Key.Delete case側（883行付近）には変更なし、影響範囲は`DeleteMenuItem_Click`に閉じている。

指摘は正しく解消された。新規の問題も見当たらない。

**結論：クリーン確定。忍者の実機確認へ回して差し支えない。**
