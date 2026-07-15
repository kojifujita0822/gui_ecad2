# T-096（Setpoint）・T-097（LabelDy）着手前調査（隠密）

調査日: 2026-07-15
調査者: 隠密
委任元: 家老（殿裁定＝Setpoint・LampColor・LabelDyを順次進める）

## T-096（Setpoint）着手前調査：GuiEcad原本のタイマー設定UI形式

### 結論
殿情報「GuiEcadはタイマー時間設定（MAX10秒）で作動できた」との符合を確認した。GuiEcad原本には**NumberBox＋Sliderの2点セット**が存在し、「MAX10秒」はスライダーの表示レンジ（クイック設定用）を指すと判断できる。

### 根拠（GuiEcad原本実物確認、`C:\Users\kojif\Desktop\生産物\gui_ecad\`）

- ファイル：`GuiEcad.App/MainPage.Properties.cs`、`RefreshPropertiesPanel()`内190〜240行目。
- 対象種別：タイマコイル本体に加え、限時接点NO/NC・瞬時接点NO/NCの計5種いずれを選択してもこのUIが出る（接点選択時は同名タイマコイルのSetpointを対象にする仕組みあり）。
- **NumberBox**（205〜215行目）：ラベル「設定時間 (秒)」、`Minimum=0`・`Maximum=9999`・`SmallChange=1`・`LargeChange=10`・`SpinButtonPlacementMode=Compact`、整数丸め表示。
- **Slider**（216〜225行目）：コメント「0〜10秒のスライダー（手早い設定用・1秒刻み）」、`Minimum=0`・`Maximum=10`・`StepFrequency=1`、NumberBoxと双方向同期。
- **「MAX10秒」はスライダーの表示上限であり、値そのものへのハード制約ではない**（事実）。NumberBoxからは9999まで直接入力可能、10秒超過を拒否する検証ロジックはコード上存在しない。
- 確定処理：`CommitSetpoint`（306〜312行目）、`Math.Round(value)`で秒単位に丸め、Undo/Redo対応の`SetParamCommand`で確定。単位表記は「設定時間 (秒)」ラベルのみ、コントロール自体に単位サフィックスは無い。
- パラメータキー名：`GuiEcad.Core/Model/Element.cs`の`ParamKeys.Setpoint`、ecad2の`ParamKeys.Setpoint`と**同名**（命名・意味論とも一致）。

### T-096実装への申し送り
- `NumberBox`はWinUI3専用コントロールでWPFに存在しない（既存調査`docs/ecad2-t086-select-switch-position-ui-survey-onmitsu.md`39行目でも既知）。ecad2側は`SelectedElementNotchPosition`（T-086、`TextBox`+`UpdateSourceTrigger=Explicit`+`LostKeyboardFocus`/`PreviewKeyDown`確定方式）と同型のTextBox方式が代替として妥当と考えられる（Slider相当のクイック設定UIまで移植するかは規模判断、家老・侍の設計判断に委ねる）。
- 対象種別（タイマコイル＋限時接点NO/NC＋瞬時接点NO/NC計5種）・「接点選択時も同名コイルのSetpointを対象にする」仕組みも、GuiEcad原本の仕様として設計時に踏襲を検討する価値がある。

---

## T-097（LabelDy）着手前検証：コイル機器名は丸の中心にあるか

### 結論
**理論計算上、現状の`DefaultLabelDy(Coil)=-5.5`はコイルの丸の中心とほぼ一致する（ズレ量は理論値でごく僅か）。ただしフォントメトリクス（アセント/ディセンダーの非対称性）を考慮すると、視覚的な文字の重心は中心よりやや上にズレている可能性がある（推測、実測未了）。**

### 幾何計算（事実、コードから直接算出）

1. **コイルの丸の中心・半径**（`SymbolGlyphs.cs:182-186`、`Coil()`メソッド）
   - `C(r, s, cx, cell, 0, 0, 0.420)` → ローカル座標(0,0)、半径0.420（セル単位）。
   - `SymbolGlyphs`のローカル座標系は「行中心線=y0」（クラスコメント、`SymbolGlyphs.cs:6`）で、`DiagramRenderer.cs:995`の`PushTransform(X(lb), YRow(e.Pos.Row))`によりワールド座標へ変換される。
   - **よって丸の中心Y座標 = `YRow(e.Pos.Row)`（行中心線そのもの）**。半径 = 0.420 × CellMm(9.0) = **3.78mm**。

2. **ラベルのY座標**（`DiagramRenderer.cs:1079`、`DrawElementLabel`）
   - `yn = YRow(e.Pos.Row) - Cell * 0.50 - dy`
   - Coilの`dy = ElementCatalog.DefaultLabelDy(Coil) = -5.5`
   - `yn = YRow(row) - 9.0×0.50 - (-5.5) = YRow(row) - 4.5 + 5.5 = YRow(row) + 1.0`

3. **テキストの垂直基準**（`DrawingTheme.cs:72`・`WpfRenderer.cs:98-104`）
   - `TextRole.DeviceName`のスタイルは`VAlign: VAlign.Bottom`（`DrawingTheme.cs:72`）。
   - WPF実装：`VAlign.Bottom => position.Y * K - ft.Height`（`WpfRenderer.cs:101`）。`ft`はWPFの`FormattedText`、`ft.Height`はフォントの行高さ全体（Ascent+Descent+LineGap相当）。
   - つまり`yn`は**テキスト矩形（行高さ全体）の下端**を指す。FontSizeMm=2.0mm（`DrawingTheme.cs:72`）。

4. **理論上のズレ量（矩形中心ベース）**
   - テキスト矩形下端 = `yn` = `YRow(row) + 1.0`
   - テキスト矩形上端 ≈ `yn - ft.Height`。`ft.Height`は一般に`FontSizeMm`の約1.15〜1.2倍程度（フォント依存、正確な値は未実測）。仮に1.2倍とすると`ft.Height ≈ 2.4mm`、上端 ≈ `YRow(row) - 1.4`。
   - 矩形中心 ≈ `(YRow(row)+1.0 + YRow(row)-1.4)/2 = YRow(row) - 0.2`
   - **丸の中心`YRow(row)`からのズレ ≈ 0.2mm（理論値、ごく僅か）**

### 推測（フォントメトリクスの非対称性、実測未了）

機器名は英数字主体（例："CR11"）で降下文字（g/j/p/q/y等）を含まないことが多く、実際に描画される文字グリフは`ft.Height`が想定するディセンダー領域をほとんど使わない。一般的なフォントの目安（Ascent≈FontSize×0.8、Descent≈FontSize×0.2、正確な値はフォント依存で未実測）で試算すると、文字の視覚的重心はテキスト矩形の中心よりもさらに**上に0.3〜0.8mm程度**ズレる可能性がある（推測）。これを合算すると、視覚的な文字の重心は丸の中心から**上に0.5〜1.0mm程度**ズレている可能性が推測される。

**この推測値は静的解析の限界であり、正確なズレ量はフォント実測（実機スクリーンショットでのピクセル計測、または忍者への実測依頼）でなければ確定できない。**

### 補正値の提案

- 理論計算上のズレは小さく（矩形中心ベースで0.2mm、視覚的重心を考慮しても1mm未満と推測）、**大幅な補正は不要と考えられる**が、殿が「ズレている」と感じられた実際の見え方との整合を取るには実測が望ましい。
- UI仕様（殿指示：「現在の高さを0として+-で上下させる」相対オフセット方式）を踏まえると、**T-097の実装では現状の`-5.5`をそのまま新UIの基準0点として採用し**、実測で追加のズレが確認された場合のみ、基準点そのもの（`DefaultLabelDy`の値）を補正する、という段取りが妥当と考える。
- 補正が必要と判明した場合の方向性：視覚的重心が中心より上にあるなら、`dy`をやや大きくする（`yn`を下げる、すなわち`dy`を-5.5より大きい値、例えば-5.0〜-4.5程度）方向の補正が理論的には整合する（暫定値、実測前提）。

### 副次的気づき（今回のスコープ外、参考情報）
`ElementKind.Timer`（タイマコイル本体）は`ElementCatalog.DefaultLabelDy`のswitch式にcaseが無く、既定の`0.0`が適用される（`ElementCatalog.cs:44-53`）。Coilと異なり中心補正がされていない。今回はCoil（コイル要素）のみが調査対象のため深追いしないが、将来的にタイマコイルの機器名表示も同様の検証が必要になりうる（気づきのみ、派生タスク化はしない）。

## 派生提案
なし（両調査とも家老采配の範囲内で完結）。
