# T-096（タイマー設定時間入力UI＋残り時間リアルタイム表示）静的レビュー（隠密）

レビュー日: 2026-07-15
対象コミット: 06d4a22
対象diff: `src/Ecad2.App/MainWindow.xaml`（14行）・`src/Ecad2.App/MainWindow.xaml.cs`（11行）・`src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（82行）・`src/Ecad2.Core/Rendering/DiagramRenderer.cs`（41行）・テスト2ファイル新規（25件）
レビュー深度: 軽量既定（1周目、karo.md方針）。ただし殿ご自身の実機確認をもって完全決着となる旨の申し送りを受け、通常より丁寧に検証した。
併用: code-reviewスキル（low effort）

## 総合判定

**軽微な指摘1件（severity低、cleanup系）のみ。実装・テストとも設計叩き台どおりで正しく、致命的欠陥なし。**

## (a) 台帳DoDとの整合

DoD(1)〜(4)いずれも確認：
- (1) プロパティパネルへの設定時間入力欄新設（TextBox+Slider双方向同期）：`MainWindow.xaml`確認OK。
- (2) 限時接点のみ表示（瞬時接点は非表示）：`IsSelectedElementTimerRelated`が`ElementKind.TimerContactNO or ElementKind.TimerContactNC`のみを対象とすることを確認（後述、コメント不整合を除く）。
- (3) `DiagramRenderer.DrawTimerCountdowns`新設：確認OK、GuiEcad原本の色指定（後述）を含め忠実な移植。
- (4) テストモード中の実機確認：殿ご自身のサンプル回路実機確認記録あり（コミットメッセージ）。

## (b) code-reviewスキル併用（low effort）

diffのhunkから正しさのバグ・重複・デッドコードは見当たらない。

### 指摘1（severity低、cleanup系）：XAML側コメントが実装と食い違う

`MainWindow.xaml:658`付近のコメント「T-096: タイマ接点(限時/瞬時NO/NC計4種)限定の設定時間入力欄」は、設計叩き台初版（5種対象案）の名残と考えられ、実装（`IsSelectedElementTimerRelated`は限時接点2種のみが対象、瞬時接点は対象外）と食い違う。実装自体は正しくDoD(2)を満たしているが、コメントが古いまま残っており、将来の保守者が誤解しうる。修正するなら「タイマ接点のうち限時NO/NC計2種限定」等への訂正を推奨する。

## (c) 狙い撃ち観点

### スコープ訂正（タイマーコイル本体不要）の反映確認
設計叩き台初版は「接点選択時も同名コイルのSetpointを対象にする」複雑なロジック（`IsTimerCoilElement`・`ResolveSetpointTargetElement`内での同名コイル検索）を提示していたが、実装では**殿訂正（タイマーコイル本体は仕様として存在しない）を受け、`ResolveSetpointTargetElement() => SelectedElement`という単純な実装に置き換えられている**ことを確認した。`IsTimerCoilElement`等の設計叩き台記載メソッドはコミットに一切含まれておらず、デッドコードの残存もない。訂正が正しく反映されている。

### 瞬時接点への非表示ロジック
`IsSelectedElementTimerRelated`は`PartResolver.ComponentKind`の直接判定で`TimerContactNO or TimerContactNC`のみを対象とし、`ResolveDeviceClass`経由の`DeviceClass.Timer`（5種一括り、限時/瞬時を区別できない）を意図的に使わない設計であることを確認した。テスト（`T096SetpointTests.cs`）でも瞬時接点2種・限時接点2種・他種別(ContactNO)の3方向を分けて検証しており、対照ケースとして妥当。

### Core層統合（App層無改修）の設計判断の妥当性
`DrawTimerCountdowns`は`DiagramRenderer.Render()`内、既存の要素描画ループ直後（`if (sim is not null) DrawTimerCountdowns(...)`）から呼ばれ、既存の`OnRealtimeTick`（100ms DispatcherTimer）→`RedrawCanvas()`→`Render()`という経路にそのまま乗る設計を確認した。App層への変更は本当に不要（diffにも該当箇所の変更なし）。GuiEcad原本はApp層に描画ロジックを持つ構造差異があるが、ecad2は既に通電色分け描画がCore層に統合済みという既存構造との一貫性を優先した判断であり、妥当と考える。

### `Color(A,R,G,B)`引数順序の確認
`new Color(230, 255, 246, 200)`（バッジ背景）・`new Color(255, 235, 170, 70)`（枠線）について、`Color`構造体の定義（`IRenderer.cs:9`、`record struct Color(byte A, byte R, byte G, byte B)`）と照合し、R高・G高・B低という黄色系の色相になることを確認した。GuiEcad原本の記述「淡黄色バッジ」と整合する。

### RED先行証明25件（18+7）の経路網羅性
- `T096SetpointTests.cs`（18件）：`IsSelectedElementTimerRelated`の3方向（限時/瞬時/他種別）、`SelectedElementSetpoint`の境界値（0・10・9999・小数丸め、範囲外・非数値・空文字）、Undo記録粒度（値変化時のみ記録）、`SelectedElementSetpointSliderValue`のクランプ・双方向反映。境界値分析の観点から網羅的と判断する。
- `DiagramRendererTimerCountdownTests.cs`（7件）：計時中・未経過・時限到達（ちょうど0になる境界）・非励磁・瞬時接点2種・`sim=null`（PDF出力/通常表示）。設計叩き台5節の検証観点と完全一致。
- 両ファイルとも件数がコミットメッセージ（18件・7件）と一致することを確認した。

### 通知箇所への追加（横展開）
`IsSelectedElementTimerRelated`/`SelectedElementSetpoint`/`SelectedElementSetpointSliderValue`のOnPropertyChangedが、既存の`IsSelectedElementLamp`/`SelectedElementLampColor`（T-085）と全く同一の4箇所（`SelectedCell`のsetter・`DeleteSelectedElement`・`NotifySelectedElementChanged`・`Document`のsetter）に追加されていることを確認した。T-085/T-086で確立したパターンの正しい横展開であり、PR-01（新規選択可能状態の横展開漏れ）チェックリストに沿う。

### `CommitDeviceNameEdit()`への`SetpointBox`追加
既存の`DeviceNameBox`/`NotchPositionBox`/`LampColorBox`と並び、4つ目として`SetpointBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();`が追加されていることを確認した。PR-02（未確定編集の確定漏れ）対策として妥当。

## スコープ確認
diff対象は`MainWindowViewModel.cs`・`MainWindow.xaml`・`MainWindow.xaml.cs`・`DiagramRenderer.cs`＋テスト2ファイル、侍自己申告と一致。範囲外変更なし。

## 総括
致命的指摘なし。軽微なコメント不整合1件のみ、実害はない。忍者実機確認は省略され本レビューをもって完全決着との申し送りを受けたため、通常の1周目レビューよりも仔細に検証したが、それでも指摘は上記1件に留まった。**完全決着で問題ないと判断する**（コメント不整合は次回同箇所に触れる際の修正で足り、単独修正までは不要と考える）。
