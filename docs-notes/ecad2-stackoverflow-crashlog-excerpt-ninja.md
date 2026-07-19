# Stack Overflowクラッシュ調査 補足報告（忍者）

報告日: 2026-07-18　報告者: 忍者

## 家老よりの追加確認(1)(2)(3)への回答

### (1) 例外の型・メッセージ本文

`%TEMP%\ecad2-ui-automation-stderr.log`（拙者の起動試行分）冒頭は文字通り`Stack overflow.`
という固定文字列で、これは通常のtry/catchでは捕捉不能な`StackOverflowException`をCLRの
クラッシュハンドラがstderrへ書き出してからプロセスを強制終了する際の定型出力と見られる。

しかし、これとは別に`%TEMP%\ecad2-crash.log`（アプリの`DispatcherUnhandledException`
ハンドラが記録する専用ログ、全セッション共通の追記式）を確認したところ、**真の一次例外は
下記の`System.InvalidOperationException`**と判明した：

```
System.InvalidOperationException: '{DependencyProperty.UnsetValue}' は、プロパティ
'BorderBrush' の有効な値ではありません。
   at System.Windows.DependencyObject.GetEffectiveValue(EntryIndex entryIndex, DependencyProperty dp, RequestFlags requests)
   at System.Windows.Controls.Border.ArrangeOverride(Size finalSize)
   at System.Windows.FrameworkElement.ArrangeCore(Rect finalRect)
   at System.Windows.UIElement.Arrange(Rect finalRect)
   at System.Windows.ContextLayoutManager.UpdateLayout()
   at System.Windows.ContextLayoutManager.UpdateLayoutCallback(Object arg)
   at System.Windows.Media.MediaContext.FireInvokeOnRenderCallbacks()
   at System.Windows.Media.MediaContext.RenderMessageHandlerCore(Object resizedCompositionTarget)
   at System.Windows.Media.MediaContext.RenderMessageHandler(Object resizedCompositionTarget)
   at System.Windows.Threading.ExceptionWrapper.InternalRealCall(Delegate callback, Object args, Int32 numArgs)
   at System.Windows.Threading.ExceptionWrapper.TryCatchWhen(Object source, Delegate callback, Object args, Int32 numArgs, Delegate catchHandler)
```

同一の例外・同一のスタックトレースが、2026-07-18T18:43:57.97台〜18:43:58.xx台の**約1秒足らずの間に
少なくとも3285回連続記録**されていた（crash.log全体でのgrep一致件数）。

### (2) クラッシュ直前の例外ダイアログ

画面を目視する間もなく即座にクラッシュしたため、実際に例外ダイアログが画面表示されたかは
**未確認（不明）**。ただし下記の状況証拠から、以下の連鎖が起きていたと推定する（推定であり
断定はしない）：

1. `Border.ArrangeOverride`内で`BorderBrush`プロパティが`{DependencyProperty.UnsetValue}`
   のまま評価され`InvalidOperationException`発生
2. `Ecad2.App.App.OnDispatcherUnhandledException`ハンドラが捕捉し`MessageBox.Show`を呼ぶ
   （拙者のstderr.logスタックトレースにこの呼び出し連鎖が記録されていた）
3. `MessageBox.Show`が新たな`Dispatcher.Invoke`によるレイアウトパス（`UpdateLayout`）を
   誘発し、そこで**再び同じBorderBrush評価が走り同じ例外が発生**
4. 2〜3が高速に繰り返され（1秒未満で3000回超）、最終的にネイティブスタック領域を使い果たし
   `StackOverflowException`（キャッチ不能）でプロセス強制終了

### (3) ログ全文の共有

**`ecad2-ui-automation-stderr.log`は確認しようとした時点で既に消失していた**
（存在しない、`Test-Path`で`False`）。全セッションが`%TEMP%\ecad2-ui-automation-stderr.log`
という同一固定パスを使うため、他セッションの再起動試行で上書き・再作成された可能性が高い
（複数セッション共存時のログパス競合、`ecad2-ui-automation`スキルの新たな注意点になりうる）。

代わりに`%TEMP%\ecad2-crash.log`（全セッション共通蓄積、削除していない）に該当連鎖の記録が
残っており、上記(1)に抜粋を記載した。**ただしこのファイルも全セッション共通のため、
2026-07-18T18:43:57台の記録が拙者自身の試行によるものか、他セッション（侍・隠密の原因調査）
によるものかは時刻だけでは断定できない**（拙者自身の起動試行は2回とも18:36台であった）。
いずれにせよ同一の一次原因（BorderBrush UnsetValue）を示す記録である可能性が高いと考える。

## 補足所見（推測、断定しない）

App.xamlで新設されたButton/ToggleButtonの暗黙的ControlTemplate（`Border x:Name="border"
... BorderBrush="{TemplateBinding BorderBrush}"`）が疑わしい。`TemplateBinding`は、
バインド元プロパティ（この場合`Control.BorderBrush`）に明示的な値もデフォルト値も無い場合
`UnsetValue`になりうる。既定WPFテンプレート（Aero2 BaseButtonStyle等）ではBorderBrushに
既定値が用意されているのが通常だが、今回自作したStyleのSetterでBorderBrushの既定値が
設定し忘れられている可能性がある。原因特定・修正は侍・隠密の領分のため、これ以上は深追い
しない。
