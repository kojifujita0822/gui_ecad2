# T-096（タイマー設定時間・Setpoint入力UI）設計叩き台（侍）

作成日: 2026-07-15
対象: プロパティパネルへタイマー設定時間(Setpoint)入力欄を新設。GuiEcad完全踏襲
（殿裁定）＝NumberBox代替(TextBox)+Slider(0〜10秒クイック設定)の2点セット、双方向同期。

## 0. 前提（隠密着手前調査・殿裁定の確認）

- GuiEcad原本＝NumberBox(0〜9999、整数丸め)+Slider(0〜10秒・1秒刻み)の2点セット、双方向同期
- 「MAX10秒」はSliderの表示レンジであり値へのハード制約ではない(NumberBoxからは9999まで入力可)
- 対象種別＝タイマコイル本体+限時/瞬時接点NO/NC計5種、**接点選択時も同名コイルのSetpointを
  対象にする**(GuiEcad仕様)
- パラメータキー名`Setpoint`はecad2と同名

## 1. Core層の既存仕様との整合確認（重要な手がかり）

`NetlistBuilder.BuildComponents()`(`src/Ecad2.Core/Simulation/NetlistBuilder.cs:315-324`)は
既に「コイル/接点どちらのParams[Setpoint]でも拾うが、同一デバイスで複数あればタイマコイル
(`ElementKind.Timer`)の値を優先する」という設計になっている。UI側の「接点選択時も同名コイルの
Setpointを対象にする」仕様は、このCore層の優先順位と対応させるのが自然——**UIも最初から
「コイルが存在すればコイル側のParamsを編集対象にする」設計にすることで、Core層の実際の評価と
UI表示の食い違いを防ぐ**。

## 2. ViewModel設計

### 2-1. 対象判定・Setpoint編集対象の解決

```csharp
/// <summary>SelectedElementがタイマ関連(コイル本体+限時/瞬時接点NO/NC計5種)か。
/// IsSelectedElementSelectSwitch等と同型、ResolveDeviceClass(既存、DeviceClass.Timerへ
/// 5種すべてがマップ済み、MapToDeviceClass参照)を再利用する。</summary>
public bool IsSelectedElementTimerRelated
    => SelectedElement is ElementInstance selEl && ResolveDeviceClass(selEl) == DeviceClass.Timer;

/// <summary>要素がタイマコイル本体(接点ではない)かを判定する(IsRealContactElementと同型)。
/// DeviceClass.Timerは5種を一括りにするため、コイル本体か接点かの区別にはPartResolver.
/// ComponentKind直接判定が必要。</summary>
private bool IsTimerCoilElement(ElementInstance element)
    => PartResolver.CreatesComponent(element, PartLibrary)
       && PartResolver.ComponentKind(element, PartLibrary) == ElementKind.Timer;

/// <summary>Setpoint編集対象の実要素を解決する(GuiEcad仕様=接点選択時も同名コイルの
/// Setpointを対象にする)。SelectedElement自身がコイル本体ならそれ自身、接点なら
/// CurrentSheet内の同名コイルを検索、見つからなければ選択要素自身へフォールバック
/// (NetlistBuilder側の「コイルが無ければ他要素の値をそのまま使う」優先順位と整合)。</summary>
private ElementInstance? ResolveSetpointTargetElement()
{
    if (SelectedElement is not ElementInstance selEl) return null;
    if (IsTimerCoilElement(selEl)) return selEl;
    if (CurrentSheet is not Sheet sheet || selEl.DeviceName is not string deviceName) return selEl;
    return sheet.Elements.FirstOrDefault(e => e.DeviceName == deviceName && IsTimerCoilElement(e)) ?? selEl;
}
```

### 2-2. Setpoint本体（TextBox/NumberBox代替）

```csharp
/// <summary>タイマー設定時間(秒、T-096、殿裁定=GuiEcad完全踏襲)。0〜9999の整数丸め
/// (GuiEcad仕様、NumberBox代替)。ResolveSetpointTargetElementで解決した実要素の
/// Params[Setpoint]を読み書きする。範囲外・非数値は値を変更せず表示のみ元へ戻す
/// (SelectedElementNotchPositionと同型パターン)。</summary>
public string SelectedElementSetpoint
{
    get
    {
        var target = ResolveSetpointTargetElement();
        return target?.Params.TryGetValue(ParamKeys.Setpoint, out var v) == true ? v : "";
    }
    set
    {
        var target = ResolveSetpointTargetElement();
        if (target is null) return;
        string oldValue = target.Params.TryGetValue(ParamKeys.Setpoint, out var ov) ? ov : "";
        if (!double.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double n)
            || n < 0 || n > 9999)
        {
            OnPropertyChanged(nameof(SelectedElementSetpoint), oldValue);
            return;
        }
        string newValue = Math.Round(n).ToString(CultureInfo.InvariantCulture);
        if (oldValue == newValue) return;

        UndoManager.RecordSnapshot(Document);
        target.Params[ParamKeys.Setpoint] = newValue;
        MarkDirty();
        OnPropertyChanged(nameof(SelectedElementSetpoint), oldValue);
        OnPropertyChanged(nameof(SelectedElementSetpointSliderValue));
    }
}
```

### 2-3. Sliderミラー値

```csharp
/// <summary>Setpointのスライダー用ミラー値(0〜10秒・1秒刻みのクイック設定、GuiEcad仕様)。
/// SelectedElementSetpointと同一実体を指し、setterはSelectedElementSetpointへ委譲することで
/// Undo記録・MarkDirty・通知を一元化する(DRY)。Slider自体のMinimum/Maximum=0/10が範囲を
/// 保証するためクランプ処理はgetter側のみ(既存値が9999等10超過でもSlider表示は10にクランプ)。</summary>
public double SelectedElementSetpointSliderValue
{
    get
    {
        var target = ResolveSetpointTargetElement();
        if (target?.Params.TryGetValue(ParamKeys.Setpoint, out var v) == true
            && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out double n))
            return Math.Clamp(n, 0, 10);
        return 0;
    }
    set => SelectedElementSetpoint = Math.Round(value).ToString(CultureInfo.InvariantCulture);
}
```

### 2-4. 通知箇所への追加

既存4箇所(`SelectedCell`のsetter・`DeleteSelectedElement`・`NotifySelectedElementChanged`・
`Document`のsetter)へ`IsSelectedElementTimerRelated`/`SelectedElementSetpoint`/
`SelectedElementSetpointSliderValue`のOnPropertyChangedを追加する(T-085/T-086と同型の横展開)。

## 3. XAML構造

```xml
<StackPanel Visibility="{Binding IsSelectedElementTimerRelated, Converter={StaticResource BoolToVisibility}}">
    <TextBlock Text="設定時間(秒):" Margin="0,8,0,2"/>
    <TextBox x:Name="SetpointBox" Text="{Binding SelectedElementSetpoint, UpdateSourceTrigger=Explicit}"
             IsEnabled="{Binding CanEditDiagram}"
             LostKeyboardFocus="SetpointBox_LostKeyboardFocus" PreviewKeyDown="SetpointBox_PreviewKeyDown"/>
    <Slider Minimum="0" Maximum="10" TickFrequency="1" IsSnapToTickEnabled="True"
            Value="{Binding SelectedElementSetpointSliderValue, Mode=TwoWay}"
            IsEnabled="{Binding CanEditDiagram}"
            AutomationProperties.Name="設定時間クイック設定(0～10秒)"/>
</StackPanel>
```

TextBox側は既存のDeviceNameBox/NotchPositionBox/LampColorBoxと同型(UpdateSourceTrigger=Explicit
+LostKeyboardFocus/Enterで確定)。Slider側はGuiEcad仕様どおりドラッグ中リアルタイムに反映する
通常のTwoWayバインディング(Explicit化しない、ドラッグ操作自体がクイック設定の主目的のため)。

## 4. コードビハインド

- `SetpointBox_LostKeyboardFocus`/`SetpointBox_PreviewKeyDown`: 既存の
  `NotchPositionBox_LostKeyboardFocus`等と同型、`CommitDeviceNameEdit()`を呼ぶだけ。
- `CommitDeviceNameEdit()`へ`SetpointBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();`
  を追加(DeviceNameBox/NotchPositionBox/LampColorBoxと同列)。

## 5. 家老へ確認したい技術判断（UI/UXそのものではなく実装粒度の判断）

**Slider操作時のUndo記録粒度**：Sliderは`Mode=TwoWay`の通常バインディングのため、ドラッグ中
`ValueChanged`が連続発火し、`SelectedElementSetpointSliderValue`のsetter経由で
`SelectedElementSetpoint`のsetterが都度呼ばれる。値が変化するたびに`UndoManager.RecordSnapshot`
が呼ばれる設計のため、1回のドラッグ操作で複数のUndo履歴が積まれうる（GuiEcad原本も
`CommitSetpoint`が同様の頻度で呼ばれていたかは着手前調査文書に明記なし、恐らく同種の挙動と
推測）。ドラッグ確定(`MouseUp`等)まで記録を遅延させる代替案もあるが、実装複雑化の割に実害は
限定的(既存のUndo履歴が大量に積まれるだけで機能的破綻はない)と判断し、**まずはシンプルな
現行案で実装し、忍者実機確認で使用感に問題が出れば再検討する**方針でよいか確認したい。

## 6. スコープ境界

- 対象は`MainWindowViewModel.cs`・`MainWindow.xaml`・`MainWindow.xaml.cs`のみ。
- Core層(`NetlistBuilder`/`Evaluator`)は既に完備のため無改修(DoD(1)(3)の器は既存)。
- テストモード中の限時接点動作(プリセット時間経過での通電切替、DoD(4))は実機確認が必要
  （UI新設により初めてSetpointが0以外に設定可能になるため、Core層のロジック自体は
  今回変更しないが「実際に機能することの確認」として実機確認スコープに含める）。

## 7. 未確定・家老検分事項

- ラベル文言「設定時間(秒):」は仮案（GuiEcad原本ラベル「設定時間 (秒)」をほぼそのまま踏襲）。
- 5節のUndo記録粒度、家老裁量で決定いただきたい。
