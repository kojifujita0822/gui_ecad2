# T-104 TimerPause機能削除、削除範囲の洗い出し（侍への引き継ぎメモ・隠密）

調査日: 2026-07-20　調査者: 隠密　委任元: 家老（殿裁定=TimerPause機能自体を廃止）
手法: `TimerPause`/`_timerPaused`の全文grep（`src/`・`tests/`両方）＋周辺コードの精読による
「削除すべき箇所」と「残すべき箇所」の切り分け。

---

## 結論（先出し）

TimerPause機能は**App層（`MainWindow.xaml`/`MainWindow.xaml.cs`）に閉じた機能**。Core層
（`Ecad2.Core`）に対応する概念は無く、テストコード（`tests/`配下）にも参照なし。削除範囲は
以下5箇所のみ、横展開漏れの懸念は小さいが、**602-603行は削除順序を誤るとビルドエラーになる
ため要注意**（後述）。

---

## 削除対象（5箇所）

### 1. `MainWindow.xaml` 1019-1029行: `TimerPauseButton`要素本体

```xml
<!-- T-061第五歩: 実時間タイマパネル(GuiEcad TimerPauseBtn踏襲)。テストモード中のみ
     有効化する(タイマ命令を使わない回路では無害、殿裁定=段階導入5段階を今回スコープに含める)。 -->
<ToggleButton x:Name="TimerPauseButton" Style="{StaticResource TestModeToolBarButtonStyle}"
              ToolTip="タイマ一時停止" AutomationProperties.Name="タイマ一時停止"
              Click="TimerPauseToggleButton_Click"
              IsEnabled="{Binding IsTestMode}">
    <StackPanel>
        <Path Style="{StaticResource ToolBarIconStyle}" Data="M6,4 L6,14 M12,4 L12,14"/>
        <TextBlock Style="{StaticResource ToolBarKeyLabelStyle}" Text="一時停止"/>
    </StackPanel>
</ToggleButton>
```

直前のコメント（1019-1020行）ごと削除。直後の`テストモード`ToggleButton（1010-1018行）や
`</ToolBar>`（1030行）は無関係、残す。

### 2. `MainWindow.xaml.cs` 97行: `_timerPaused`フィールド宣言

```csharp
private bool _timerPaused;
```

### 3. `MainWindow.xaml.cs` 602行: `StartRealtimeTimer()`内の初期化行

```csharp
private void StartRealtimeTimer()
{
    _timerPaused = false;              // ← この行を削除
    TimerPauseButton.IsChecked = false; // ← この行も削除(次項参照)
    _realtimeClock.Restart();
    ...
```

### 4. `MainWindow.xaml.cs` 603行: `TimerPauseButton.IsChecked = false;`

**【削除順序の注意】** 項目1（XAML側`x:Name="TimerPauseButton"`削除）と同時に削除しないと、
この行が存在しない名前を参照してビルドエラーになる。項目1と3-4は必ずセットで削除すること。

### 5. `MainWindow.xaml.cs` 630-645行: `TimerPauseToggleButton_Click`メソッド全体

```csharp
// 一時停止/再開: 実時間カウントを止める。再開時は経過の起点をリセットして時間飛びを防ぐ
// (GuiEcad OnTimerPauseToggle踏襲。Stopwatch自体は一時停止中も動き続け、再開時に_lastTickMsを
// その時点の経過時間へ合わせることで一時停止中の経過をdtに含めない)。
private void TimerPauseToggleButton_Click(object sender, RoutedEventArgs e)
{
    _timerPaused = sender is System.Windows.Controls.Primitives.ToggleButton { IsChecked: true };
    if (_timerPaused)
    {
        _realtimeTimer?.Stop();
    }
    else if (_viewModel.Mode == ViewModels.AppMode.Test)
    {
        _lastTickMs = _realtimeClock.ElapsedMilliseconds;
        _realtimeTimer?.Start();
    }
}
```

直前のコメント（630-632行）ごと削除。

---

## 残すもの（誤って削除しないよう明記——TimerPauseとは独立の基盤）

`StartRealtimeTimer`/`StopRealtimeTimer`/`OnRealtimeTick`は、**タイマ命令（限時接点等）の
実時間シミュレーション駆動そのもの**であり、TimerPause（一時停止トグル）とは別物。これらを
誤って削除すると、テストモード中のタイマー命令シミュレーション自体が壊れる。

- `MainWindow.xaml.cs` 91-96行: `_realtimeTimer`/`_realtimeClock`/`_lastTickMs`フィールド
  （項目2の`_timerPaused`のみ削除、他3フィールドは残す）
- `MainWindow.xaml.cs` 599-616行: `StartRealtimeTimer()`（項目3-4を除去した残り）・
  `StopRealtimeTimer()`（無変更）
- `MainWindow.xaml.cs` 618-628行: `OnRealtimeTick()`（無変更、`_timerPaused`参照なし）
- `MainWindow.xaml.cs` 486-490行: テストモードIn/Out時の`StartRealtimeTimer()`/
  `StopRealtimeTimer()`呼び出し（無変更）
- `MainWindow.xaml` 89行: `TestModeToolBarButtonStyle`（`x:Key`付き共有スタイル、「テストモード」
  ToggleButton（1010行）と共用のため**削除禁止**——TimerPauseButtonだけがこのスタイルを
  使っているわけではない）
- `MainWindow.xaml` 1010-1018行: 「テストモード」ToggleButton本体（無変更、TimerPauseButtonの
  直前にある別要素、混同注意）

---

## 横展開確認（削除漏れチェック）

- `grep -rn "TimerPause" C:\ECAD2` → 一致は`docs/`配下の過去調査書・台帳（履歴として保持、
  修正不要）と`src/Ecad2.App/MainWindow.xaml`・`MainWindow.xaml.cs`の2ファイルのみ。
- `grep -rn "_timerPaused" C:\ECAD2` → `MainWindow.xaml.cs`の3箇所（項目2・3・5）のみ、他ファイル
  への漏れなし。
- `tests/`配下（`Ecad2.App.Tests`・`Ecad2.Core.Tests`）に`TimerPause`/`_timerPaused`の参照なし
  （削除すべきテストコードは無い）。
- `Ecad2.Core`にTimerPause相当の概念は存在しない（App層に閉じた機能と確定）。

## build/test確認の勘所

削除後、`dotnet build`で以下を確認されたい：
- `TimerPauseButton`（XAML名前）への参照が全て消えていること（項目1〜4の同時削除漏れがあると
  ここでコンパイルエラーになるはず）。
- `TimerPauseToggleButton_Click`未使用の警告が出ないこと（XAML側の`Click=`属性ごと削除済みなら
  そもそも参照が残らない）。
