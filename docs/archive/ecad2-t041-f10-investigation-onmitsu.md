# T-041増分3 F10キー無反応バグ 独立調査（隠密）

> 2026-07-07 隠密調査。家老采配（忍者発見「F10押下でWireBreak記入がされずメインメニューへ
> フォーカスが移る」、忍者仮説=WPFのKey.System/SystemKey特有仕様、Wチェック並行采配）。
> 観点(1)〜(3)をWeb一次情報＋現行コード比較で検証した。

---

## 結論：**忍者の仮説はCONFIRMED（一次情報で裏付け済み）。侍が既に正しい修正を作業ツリーへ
適用済み（未コミット）であることも確認した——独立に同一結論へ収束**

WPFにおいてF10キーは、Alt同様「システムキー」としてWin32の`WM_SYSKEYDOWN`経由で配送される
既知の仕様があり、`KeyEventArgs.Key`には`Key.F10`ではなく`Key.System`が入る（実際に押された
キーは`e.SystemKey`側に入る）。既存の`switch (e.Key) { case Key.F10 when noModifier: ... }`は
`e.Key`が常に`Key.System`になるためこの`case`に到達せず、WPF既定の「F10→メインメニューへ
フォーカス移動」という挙動へ素通しされていた。これは忍者観測（F10無反応・メニューへフォーカス
移動）と完全に一致する。

作業ツリーを確認したところ、`src/Ecad2.App/MainWindow.xaml.cs`（未コミット）は既に
`case Key.System when noModifier && e.SystemKey == Key.F10:`へ修正済みであり、この対処方法は
一次情報が示す標準的な回避策と一致する。

---

## 観点(1): WPFにおけるF10キーの既知動作 —— **一次情報で確認**

Syncfusionフォーラムの解説（後述の出典）によれば：

> When you press F10 in WPF, e.Key == Key.System, not Key.F10 as you might expect.
> ... F10 has special system-level behavior in Windows, which causes it to be treated
> similarly [to Alt]. The SystemKey property will tell you which System key was pressed.

Windowsの伝統的なUI規約で、F10は（Altキーと同様）「メニューバーへフォーカスを移す」システム
キーとして扱われる（DOS/古典Win32時代からの規約）。これはAlt併用の有無に関わらずF10単体でも
発生する（Alt+何かのキーが`Key.System`になるのと同じ扱いをF10自身も受ける、という一次情報の
説明と一致）。対処は`e.Key == Key.System`かつ`e.SystemKey == Key.F10`の組み合わせで判定する
のが標準的な回避策と確認した。

## 観点(2): 既存F5〜F9ハンドラとの実装差異・忍者仮説の妥当性 —— **妥当性を確認、原因を特定**

`MainWindow.xaml.cs`の`Window_PreviewKeyDown`内`switch (e.Key)`を比較した：

- `case Key.F5 when noModifier:`〜`case Key.F8 when noModifier:`、`case Key.F9 when shift:`
  （増分2、sF9）はいずれも`e.Key`で直接判定しており、正しく機能する（F5〜F9は通常キーで
  Win32の特別扱いを受けないため）。
- **F10のみ**、上記のシステムキー特有仕様の対象であり、`case Key.F10 when noModifier:`という
  他のFキーと同型の書き方では**到達不能**になる。これは「F10だけがF5〜F9と異なる挙動を示す」
  という忍者の観測を、実装パターンの単純な比較だけでも裏付ける（F5〜F9のいずれのケースにも
  今回のバグは起きていない、と忍者報告にある通り）。

`Window_PreviewKeyDown`は`PreviewKeyDown`（トンネリング、最初に発火）にバインドされており、
イベント経路自体（Preview/バブリングの選択）は問題の原因ではない——`Key.System`/`SystemKey`の
仕様はWin32メッセージ（`WM_SYSKEYDOWN`）由来でRoutedEvent戦略とは独立のため、`KeyDown`に
差し替えても同じ問題が起きたはずである（この点は忍者仮説には無かった追加確認事項）。

## 観点(3): 対処方法の一般的なプラクティス —— **侍の修正が標準的な回避策と一致することを確認**

一次情報が示す標準パターン：

```csharp
case Key.System:
    if (e.SystemKey == Key.F10) { /* F10処理 */ }
    break;
```

現行の作業ツリー（`MainWindow.xaml.cs`、未コミット）は`switch (e.Key)`のcaseパターンマッチ内で
`case Key.System when noModifier && e.SystemKey == Key.F10:`という形に書き換えられており、
上記標準パターンをC#のパターンマッチ構文（`when`節）で簡潔に表現した等価な実装になっている。
`e.Handled = true`も既存通り維持されており、`PreviewKeyDown`（トンネリング）でこの`case`に
到達し`e.Handled=true`が設定されれば、後続のWPF既定メニューフォーカス処理より先に消費される
ため、原理的に正しく症状を解消できると判断する。

`noModifier`（`Keyboard.Modifiers == ModifierKeys.None`）は実際の修飾キー押下状態を見るため、
F10単体（Alt等を併用しない）押下でも正しく`true`になり、他のシステムキー組み合わせ
（Alt単体・Alt+アクセラレータ文字等）は`e.SystemKey == Key.F10`条件で除外されるため、
意図しない副作用は無いと判断する。

---

## 総括・申し送り

- 忍者の仮説（Key.System/SystemKey特有仕様）は一次情報でCONFIRMED、既存F5〜F9との実装比較でも
  裏付けられた。
- 侍の修正（作業ツリー、未コミット時点で確認）は一次情報が示す標準的な回避策と一致しており、
  技術的に妥当と判断する。コミット後、忍者による実機再検証（F10押下でWireBreakが記入され
  メニューへフォーカスが移らないこと）を推奨する。
- 副次的な確認事項として、`PreviewKeyDown`/`KeyDown`のイベント選択自体は原因ではないことを
  明記した（将来同種のシステムキー絡みの不具合診断で「イベント経路を疑う」誤り筋を防ぐ材料と
  なる）。

---

## 出典・参照

- [How to NOT HANDLE the F10 key? | WPF Forums | Syncfusion®](https://www.syncfusion.com/forums/97612/how-to-not-handle-the-f10-key)（`e.Key == Key.System`・`e.SystemKey`によるF10判定の標準パターン）
- `src/Ecad2.App/MainWindow.xaml.cs`（作業ツリー、未コミット時点。`Window_PreviewKeyDown`内
  F5〜F9・F10の`switch (e.Key)`ケース比較）
- `docs/ecad2-t041-key-flow-proposal-samurai.md`（4節、F10キー割当の原案）
