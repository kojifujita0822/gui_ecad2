# T-080 往復2周目 マウス経路不具合の根本原因調査(隠密、独立系統)

- 対象: (a) 物理ダブルクリックで行コメントエディタが開かない (b) エディタ表示中に他所を物理
  クリックしても閉じない(`RungCommentBox_LostKeyboardFocus`不発火)
- 手法: 静的読解のみ(共有mainへの一時注入なし)。侍とは独立系統、本調査中は侍の調査内容を
  参照していない。
- 対象コミット: `c4dd2b1`時点(T-080往復1周目適用後)の`src/Ecad2.App/MainWindow.xaml(.cs)`・
  `ViewModels/MainWindowViewModel.cs`

## 結論(見立て): (a)と(b)は同根の可能性が高い、ただし(a)単独の別要因も残る

**主仮説(高確度、コードで実証可能)**: 往復1周目の指摘F修正(`IsMainContentEnabled`による
`MainContentArea`の無効化)が、行コメントエディタの「フォーカスロスト=確定して閉じる」という
既存設計(GuiEcad踏襲)と構造的に矛盾している。

### 根拠

1. `MainWindow.xaml:99-100`: `MainContentArea`(メニュー・ツールバー・メインキャンバス・
   出力パネルを内包するGrid)の`IsEnabled`は`IsMainContentEnabled`にバインドされている。
   `MainWindowViewModel.cs:84`: `IsMainContentEnabled => !IsPlacementBarVisible && !IsRungCommentEditorVisible`。
   つまり**行コメントエディタ表示中は`MainContentArea`全体(キャンバスを含む)が無効化される**
   (往復1周目指摘Fの意図どおり、マウス素通し防止のため)。

2. `RungCommentEditor`(Border)・`RungCommentBox`(TextBox)自体は`MainContentArea`の**外**
   (`RootLayoutGrid`直下、Grid.Row="2")にあるため無効化されず、編集は継続可能。

3. `RungCommentBox`の閉じる経路は`LostKeyboardFocus="RungCommentBox_LostKeyboardFocus"`
   (`MainWindow.xaml:683`)のみで、これが`CommitRungCommentEditor(restoreFocus: false)`を
   呼ぶ(`MainWindow.xaml.cs:1740`)。「他所をクリックしてフォーカスが移る」ことでこのイベントが
   発火し、確定・クローズする設計(コメント「フォーカスロスト=確定扱い、GuiEcad踏襲」)。

4. **しかし、他所(=キャンバス・メニュー・ツールバー)は`MainContentArea`配下にあり、今まさに
   `IsEnabled=false`で無効化されている**。WPFでは無効化された要素はキーボードフォーカスを
   受け取れないため、無効化された要素を物理クリックしても`RungCommentBox`からフォーカスが
   移動せず、`LostKeyboardFocus`が発火しない。結果、エディタは**物理クリックでは絶対に
   閉じられない**(Enter/Tab/Escapeのキーボード経路のみが有効)。これが症状(b)の直接的な
   コード上の説明。

5. **この設計矛盾はRungCommentEditor固有の新規問題**であることをT-033(ElementPlacementBar)
   との対比で確認した。`PlacementDeviceNameBox`(ElementPlacementBarの入力欄)には
   `LostKeyboardFocus`ハンドラが一切配線されていない(`grep`で確認、0件)。PlacementBarは
   `PlacementOkButton_Click`/`PlacementCancelButton_Click`という**明示ボタンでのみ**閉じる設計で、
   「フォーカスロストで閉じる」経路を最初から持たない。ゆえにPlacementBar表示中に
   `MainContentArea`を無効化しても自己矛盾が起きなかった。往復1周目の指摘F修正は、この
   「無効化」の仕組みをPlacementBarからそのまま流用してRungCommentEditorへ適用したが、
   RungCommentEditorだけが持つ「フォーカスロストで閉じる」という別の既存設計との相性を
   検討していなかったとみられる。

### 症状(a)との関係

上記4により、**一度エディタが開くと物理クリックでは二度と閉じられない**(キーボードのみ有効)。
この状態のまま`MainContentArea`(キャンバス含む)が無効化され続けるため、**その後の物理
ダブルクリックはキャンバスに到達すらしない**(無効化された要素はヒットテスト・イベント発火の
対象外)。つまり、もし殿の検証手順が

1. (何らかの経路で)一度エディタが開いた状態になる
2. 他所を物理クリックして閉じようとする → 閉じない(症状b)
3. 別の行で改めて物理ダブルクリックを試す → 反応しない(症状a、実際にはキャンバス自体が
   無効化されたまま反応不能になっている)

という順であれば、(a)(b)は**単一のバグ(Fの過剰な無効化範囲)が異なる形で現れた同じ現象**と
説明できる。

一方、殿の検証が「一度もエディタを開いたことがない真っさらな状態でいきなり物理ダブルクリック」
であった場合、この主仮説だけでは(a)を説明できない(`MainContentArea`は無効化されていない
はずのため)。この場合の(a)への往復1周目diffの影響を調べたが、ダブルクリックのヒットテスト
経路(`HitTestRungCommentRow`)への今回の変更は指摘G(`if (sheet.MainCircuit) return null;`)
のみで、これは殿がMainCircuitシートで検証しない限り影響しない(F2側も同条件のため、F2成功の
報告と整合すればMainCircuitシートではないと判断できる)。その他のヒット領域判定
(`xMm > RightBusX(columns)`・行範囲)は本往復で変更されておらず、静的読解の範囲では
不具合を確認できなかった。この場合(a)は本往復の変更由来ではない、より以前からの潜在不具合
(ヒット領域の視認性・スクロール位置等)である可能性が残る。

## 推奨する切り分け

殿・忍者へ、検証時の**操作順序**(エディタを一度でも開いた後にダブルクリックを試したか、
それとも真っさらな状態で最初からダブルクリックが失敗したか)の確認を推奨する。ただし
主仮説(Fの過剰無効化)はいずれにせよ**コードから実証可能な確定的な欠陥**であり、操作順序に
関わらず修正が必要。

## 推奨する修正の方向性(参考、実装は侍マター)

`IsMainContentEnabled`による無効化の対象から、少なくとも「他所へのクリックでフォーカスを
移せる経路」を除外する必要がある。案:
- RungCommentEditor表示中は`MainContentArea`を無効化しない(マウス素通し防止は個別ハンドラの
  ガードに委ねる。ただしこれは往復1周目指摘Fが「個別ハンドラへの後追いガードはT-021の轍のため
  不採用」とした方針の逆行になるため要検討)
- または、クリックアウトでの確定処理を`LostKeyboardFocus`依存から、Window全体の
  `PreviewMouseDown`でRungCommentEditorの矩形外クリックを検知して明示的に
  `CommitRungCommentEditor`を呼ぶ方式へ変更する(PlacementBarと同じ「フォーカス非依存の
  確定契機」に統一する)

## 検証の限界

本調査は静的読解のみ(共有mainへの一時注入禁止)。WPFの無効化要素のフォーカス・ヒットテスト
挙動に関する記述は一般的なWPFの仕様理解に基づく推論であり、実機での実測(忍者マター)による
裏取りが必要。侍の独立系統調査との突合は家老に委ねる。

## 追記(家老経由の殿回答を受けて): 真っさらな状態での(a)単独原因の絞り込み

殿回答=「F2を一度も使わぬ真っさらな状態でダブルクリックし、開かなかった」。これにより上記
「(b)の無効化残留の巻き添え」仮説は(a)には適用できない(真っさらな状態では`IsRungCommentEditorVisible`
は初期値false、`MainContentArea`は無効化されていないはず)。独立原因を、クリック位置の
2ケースで整理する。

### ケース1: ダブルクリックがヒット領域内(`xMm > RightBusX(columns)`、有効行)に着弾した場合

イベント経路を`LadderCanvasHost_PreviewMouseLeftButtonUp`の先頭から辿り直したが、**静的読解の
範囲ではコード欠陥を発見できなかった**。根拠:

- 真っさらなシートでは`SelectedConnector`/`SelectedWireBreak`/`SelectedFreeLine`/
  `SelectedConnectionDot`がいずれもnull(何も配置・選択されていない)ため、`LadderCanvasHost_
  PreviewMouseLeftButtonDown`(393-458行)の`CaptureMouse()`系分岐は1つも成立せず、Down側は
  実質何もしない(状態を変更しない)。ダブルクリック2発分のDown/Upとも、ドラッグ確定の早期
  return(520-576行)には引っかからない。
- 1発目のUp(ClickCount==1)は`e.ClickCount==2`条件(582行)を満たさず読み飛ばされ、
  Select分岐(595行〜)で該当ヒットが無ければ最終的に626行`_viewModel.SelectedCell =
  ToGridPos(position); TryPlaceActiveTool();`のみを実行する。`TryPlaceActiveTool()`は
  `Tool.Mode != PlaceElement`なら即return(705-708行、既定のSelectモードでは無害)。この
  単発クリックの副作用がSelectedCellの単純な代入のみであり、2発目の判定に影響する経路は
  見当たらない。
- `Window_PreviewMouseLeftButtonDown`(1345行)は`_toolButtonKeyboardClickSource = null`の
  みで無関係。Window側にPreviewMouseLeftButtonUpの横取りハンドラは存在しない
  (grep: `Window_PreviewMouseLeftButtonUp`という名のハンドラ自体が無い)。
- ズーム(`CanvasScale`、Ctrl+ホイール)は`LadderCanvasHost`自身への`ScaleTransform`
  (`MainWindow.xaml:439`)で、`e.GetPosition(LadderCanvasHost)`はWPFの標準変換により
  対象要素自身の変換を含めて逆算されるため、ズーム率に関わらず`ToMm`後の値は一貫するはず
  (T-041等の既存ドラッグ機能が同じ変換で既に実績あり、本往復固有の変更なし)。
- 本往復(往復1周目)がこの経路に加えた変更は指摘Gの`if (sheet.MainCircuit) return null;`
  のみで、F2側も同条件のため、F2成功の報告と整合するなら対象シートはMainCircuitではないと
  判断でき、Gはこのケースの説明にならない。

→ **ケース1が真実なら、静的読解だけでは原因を特定できておらず、実機ログ計測(診断ログ注入)
   が必要**という結論になる(往復1周目までの範囲では見つからない、より古い/別種の欠陷の
   可能性)。

### ケース2: ダブルクリックがヒット領域外に着弾した場合(現時点で最有力視)

真っさらなシート(`sheet.RungComments.Count == 0`)では、`DrawRungComments`
(`DiagramRenderer.cs:1073`)の先頭ガード`if (sheet.RungComments.Count == 0) return;`により
**行コメント欄は画面上に一切描画されない**(文字も背景も枠線も無い、真っ白な余白と区別が
つかない)。すなわち「右母線のさらに右、`RungCommentXOffsetMm`(2.0mm)だけ離れた位置から」
という**ヒット領域そのものを示す視覚的な手がかりが、初回入力時には何も無い**。

殿がこの状況で(既存コメントが無い行への)ダブルクリックを試みる場合、視覚的な手がかりが
無いままヒット領域(右母線のすぐ右側のごく狭い帯)を正確に狙う必要があり、実際には
右母線そのものの上、または最終列セル内(=`xMm <= RightBusX(columns)`の側)をクリックして
しまう可能性が高い。この場合`HitTestRungCommentRow`(`LadderCanvas.cs:218`)の
`if (xMm <= _renderer.RightBusX(sheet.Grid.Columns)) return null;`により**意図通りnullを
返しているだけ**で、コード上の不具合ではなく、**UX上の発見可能性(discoverability)の欠如**
が真因となる。

→ こちらは「バグ」ではなく「視覚的な手がかりの欠如」という設計課題であり、GuiEcad踏襲の
   仕様自体がそうだった可能性もある(前回レビューでは踏襲確認のみで、この発見可能性の論点は
   検証対象に含まれていなかった)。

### 切り分けの提案

ケース1・2のどちらであるかを静的読解だけで一意に決定できない。以下いずれかで切り分け可能:

1. **F2で先に1件コメントを入力させ、画面上にコメント文字を表示させた状態にしてから、
   その文字の上を物理ダブルクリックする**(既存コメントの編集を試す)。これで開けば
   ケース2(発見可能性の問題)が濃厚、これでも開かなければケース1(コード側の未知の欠陥)が
   濃厚——という切り分けが可能。ただしF2を使うとこの往復の前提(真っさらな状態)を崩すため、
   殿には「これは切り分け目的の別条件のテスト」と明示した上で依頼されたい。
2. または、殿にダブルクリックした**おおよその画面座標・シートの列数設定**を伺えれば、
   `RightBusX(columns)`の値と比較して着弾位置がヒット領域内か外か機械的に判定できる
   (家老采配の追伸で「正確な位置は未回答」とあったため、追加で伺えれば一意に判定可能)。

## 総括(更新)

(b)は主仮説(F修正とLostKeyboardFocus設計の構造矛盾)がコードから確定的に実証済みで不動。
(a)は真っさらな状態での発生が確定したことで(b)との単純な巻き添え説明が崩れ、独立原因を
要する。静的読解の範囲では「ヒット領域内なら未知の欠陥(要実機ログ)」「ヒット領域外なら
UX発見可能性の課題(バグではない)」の2択に絞り込めたが、これ以上の一意特定には殿の
着弾位置情報、またはF2併用での切り分けテストが必要。

## 追記2(往復2周目、忍者厳密再実測を受けての訂正): (b)主仮説は反証済み

忍者が厳密条件(ecad2ウィンドウをアクティブに保ったまま、無効化されたツールバー領域=
`MainContentArea`内を殿が物理クリック)で再実測した結果、`RungCommentBox_LostKeyboardFocus
(NewFocus=null)`が正しく発火し、`CommitRungCommentEditor`→`CloseRungCommentEditor`まで
確定的にクローズすることが確認された(`docs-notes/ecad2-t080-ninja-verification-round2.md`
末尾)。

すなわち**`IsMainContentEnabled`による`MainContentArea`無効化下でも、WPFは物理クリックで
キーボードフォーカスを正しく外し、`LostKeyboardFocus`ハンドラは発火する**——本調査書の
主仮説(「無効化された要素はフォーカスを受け取れないため、他所クリックではフォーカスが
移らずLostKeyboardFocusが不発火」)は、この一次実測により**反証された**。無効化要素への
物理クリックであっても、WPFはキーボードフォーカス自体は(無効化要素に移すのではなく)
単に現フォーカス要素から外す形で処理する模様(推測、無効化要素の内部挙動の一般論としては
妥当だが、本件固有の検証はしていない)。

(b)は現行コードの挙動が正常と実測確定したため、修正対象から外す方向で家老が殿に諮る予定。
一次情報(実測)を静的読解より優先し、率直に訂正する([[feedback_self_correct_with_primary_sources]]
の教訓どおり)。

## 追記3(最終実機確認を受けての再訂正): 追記2の反証はさらに撤回、当初主仮説が正しいと確定

`docs-notes/ecad2-t080-ninja-final-verification.md`観点6により、殿の直接証言で「ecad2ウィンドウ
内側のクリックだけでは`RungCommentBox`は確定せず開いたまま、チャットへ戻るためのクリック
(ウィンドウ非アクティブ化)で確定する」ことが判明した。すなわち**本調査書の当初主仮説
(`MainContentArea`無効化により、無効化された要素はキーボードフォーカスを受け取れず
`LostKeyboardFocus`が不発火する)が正しかった**ことが最終確定した。

追記2で「無効化下でもWPFはフォーカスを正しく外しLostKeyboardFocusは発火する」と訂正したのは、
忍者round2実測(`docs-notes/ecad2-t080-ninja-verification-round2.md`)が「窓外クリック(ウィンドウ
非アクティブ化)」を「アプリ内クリック」と誤認識したまま報告したことに起因する誤訂正だった。
窓内クリックでは実際には確定しない(=当初主仮説どおり)、窓非アクティブ化時のみ確定する、
というのが最終結論。詳細は`docs-notes/ecad2-t080-ninja-verification-round2.md`末尾の再訂正節・
`docs-notes/ecad2-t080-ninja-final-verification.md`観点6を参照。

殿裁定(2026-07-12): (b)は修正せず**現状を仕様化**する。「行コメントエディタのクローズ経路は
Enter/Tab確定・Esc取消のみ。窓内クリックでは閉じない(誤操作防止の無効化を優先)。ウィンドウ
非アクティブ化時はフォーカスロストで確定」という仕様として`docs/spec/`配下へ追記する(隠密が
別途対応、`docs/ecad2-t080-doubleclick-root-cause-onmitsu.md`と対の作業として実施)。

一次情報(実機)の上でさらに一次情報が訂正される、という二段階の訂正を経た。都度その時点で
得られる最良の一次情報に基づき率直に訂正する姿勢自体は変えず、今後も同様に対応する
([[feedback_self_correct_with_primary_sources]])。
