# T-086調査: セレクトSWノッチ位置設定UI(隠密、2026-07-14)

対象: セレクトSWのノッチ番号(`ParamKeys.Position`)を設定するUIがApp層に皆無という問題
(殿実機指摘・家老grep確認・侍`docs-notes/ecad2-t086-position-ui-absence-samurai.md`で実測込み裏付け)。
GuiEcad側の実装形式調査・ecad2への実装方針検討・影響範囲洗い出し。

## 結論(要約)

GuiEcadは**プロパティパネル内、要素種別で条件分岐する専用数値入力欄**(タイマ「設定時間」と
同型パターン)で実装している。ecad2は同種のKind別条件分岐プロパティUIを1件も持たないため、
本件がその最初の実装例になる。WinUI専用の`NumberBox`はWPFに存在しないため直接移植はできず、
既存の機器名編集欄(`DeviceNameBox`)と同型のTextBoxパターンで代替するのが自然。

## DoD1: GuiEcad側の実装形式

出典: `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Properties.cs:165-189,298-303`

- `BuildProperties`相当のメソッド内、`_selected.Kind == ElementKind.SelectSwitch`の場合のみ、
  「ノッチ位置」ラベル+`NumberBox`(WinUI、`Minimum=0`/`Maximum=99`/`SmallChange=1`/
  `SpinButtonPlacementMode=Compact`)+説明文(「同名接点に異なる番号を設定し、テストモードで
  クリックして切り替えます」)をプロパティパネルへ動的追加する。
- 直後の`else if`ブロック(190-192行)がタイマ(`ElementKind.Timer`等)の「設定時間」プロパティで、
  全く同型の構造(Kind分岐→専用NumberBox→ハンドラでコマンド実行)。GuiEcad内で確立済みの
  横展開パターンと分かる。
- 値変更時`OnPositionBoxChanged`(298-303行): `_history.Execute(new SetParamCommand(_sheet,
  _selected, ParamKeys.Position, ((int)args.NewValue).ToString()))` → `Canvas.Invalidate()`。
  `SetParamCommand`はGuiEcad独自のUndo/Redo**個別コマンド**パターン(ecad2とは設計が異なる、
  DoD2参照)。

## DoD2: ecad2の既存プロパティパネルで足りるか

**足りない(新規UI要素が要る)が、既存パターンの横展開で対応可能。**

- ecad2のプロパティパネル(`MainWindow.xaml:564-583`)は現状、`HasSelectedElement`時に
  「種別」表示+「デバイス名」`TextBox`(`DeviceNameBox`)のみ。Kind別の追加プロパティ分岐
  (GuiEcadのSelectSwitch/Timer相当)は**ecad2に1件も実装されていない**
  (`MainWindowViewModel.cs`grep確認、`Setpoint`ヒット0件)。本件がecad2最初のKind別条件分岐
  プロパティUIになる。
- GuiEcadの`NumberBox`はWinUI専用コントロールでWPFには存在しない。ecad2は既存の
  `DeviceNameBox`と同型の`TextBox`(`UpdateSourceTrigger=Explicit`+`LostKeyboardFocus`で確定、
  `MainWindow.xaml:572-574`)を踏襲し、数値バリデーション(`int.TryParse`+範囲チェック、
  不正入力時は表示を戻す)を確定処理側で行う形が自然。GuiEcadの`Minimum/Maximum`によるUI側
  制約はecad2では持てないため、確定処理内でのバリデーションに置き換える必要がある。
- Undo対応: GuiEcadは`SetParamCommand`という値ごとの個別コマンドクラスで実装しているが、
  ecad2のUndo/Redoは`UndoManager.RecordSnapshot(Document, ...)`という**スナップショット方式**
  (`MainWindowViewModel.cs`各所、`SelectedElementDeviceName`等の確定処理と同型)。個別
  Commandクラスの移植は不要で、確定タイミング(`LostKeyboardFocus`相当)で`RecordSnapshot`を
  呼ぶ既存パターンを踏襲すればよい。

## DoD3: 影響範囲洗い出し

1. **`ParamKeys.Position`書き込み経路の新設**: `MainWindowViewModel.cs`に新規プロパティ
   (例`SelectedElementNotchPosition`)を追加し、`SelectedElement.Params[ParamKeys.Position]`へ
   書き込む。`SelectedElementDeviceName`セッター(1721-1757行)と同型のガード構造
   (`SelectedElement is not ElementInstance el`早期return、値未変化なら早期return)を踏襲する。
2. **対象要素の判定条件**: `ResolveDeviceClass(SelectedElement) == DeviceClass.SelectSwitch`
   (T-061 A-1で確立済み判定の再利用)。`HasSelectedElement`と同様の新規bool算出プロパティ
   (例`IsSelectedElementSelectSwitch`)をXAML側`Visibility`バインド用に追加する必要がある。
3. **XAML側**: プロパティパネル(`MainWindow.xaml:569-575`)の`StackPanel`内に、
   `IsSelectedElementSelectSwitch`をゲートにした条件付き表示ブロック(ラベル+TextBox)を追加。
4. **CanEditDiagramガード【MUST、T-061確立の統一ゲート】**: `DeviceNameBox`と同様
   `IsEnabled="{Binding CanEditDiagram}"`を付す(テストモード中の回路データ改変禁止、
   T-061 A-2/A-3で既に機器名編集欄・画像挿入メニューに横展開済みのパターン、本件も対象漏れ
   させないこと)。
5. **RecordSnapshotタイミング**: 確定処理(LostKeyboardFocus相当)で`UndoManager.RecordSnapshot`
   を呼ぶ。既存の値未変化ガード(no-op時はRecordSnapshotしない、T-070 A-5と同型の配慮)も
   踏襲すべき。
6. **既存バグとの関連(副次効果)**: 侍メモ(`docs-notes/ecad2-t086-position-ui-absence-samurai.md`)
   のとおり、`CycleSelectSwitch`の`positions`列挙は現状常に空集合で`ToggleInput`フォールバックに
   必ず落ちている。本UI実装によりPosition値が書き込まれるようになれば、複数ノッチ切替の本来
   機能が初めて動作可能になる(=本UIの実装自体が別バグの間接的な解消条件でもある)。
7. **未解明点の持ち越し**: 侍メモの「忍者の自動/手動2接点同時導通目視と理論の矛盾」は、
   Position設定UI実装後の実機確認で選択ハイライトと通電ハイライトの見間違いか否か切り分け
   られる見込み(本調査の範囲外、実装後の忍者確認へ持ち越し)。

## 出典

- GuiEcad原本: `MainPage.Properties.cs:165-189`(UI構築)・`:298-303`(`OnPositionBoxChanged`)
- ecad2既存パターン: `MainWindow.xaml:564-583`(プロパティパネル)・
  `MainWindowViewModel.cs:1721-1757`(`SelectedElementDeviceName`、確定処理の参考実装)
- 侍メモ: `docs-notes/ecad2-t086-position-ui-absence-samurai.md`

## 不明点

- GuiEcadの`Minimum=0`/`Maximum=99`という範囲制約の根拠(ノッチ数の実運用上限)は
  GuiEcad側にコメント等の説明がなく不明。ecad2側で同じ範囲を踏襲すべきか、殿確認が要るか
  判断材料が無い(実装時に相談要、または無制限int許容でも実害は薄いと考えられる)。
