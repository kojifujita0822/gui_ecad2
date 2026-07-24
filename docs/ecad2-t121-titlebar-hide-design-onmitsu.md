# T-121 配置ツールバーペインのタイトルバー（青い帯）常時非表示・省スペース化 設計書（隠密）

作成日: 2026-07-24　作成者: 隠密　委任元: 家老（殿直接指示2026-07-22、T-099(c)・T-110増分3と同じ手順＝隠密設計書作成→殿裁定→侍実装→隠密レビュー→忍者実機確認）
本書は設計のみで実装には着手しない。実装は侍、レビューは隠密、実機確認は忍者（検証パイプライン既定順）。

---

## 0. 要旨（先出し）

- **帯全体（Header Border層）のドッキング時非表示化は、既存のT-110増分3`TitleBarHiddenAnchorableControlStyle`の延長で確実に実現できる**。対象4ペイン（LeftPalette/DeviceTable/RightPanelBottom/OutputPanel）と同じ「既定コピー+ContentId名指しDataTrigger」方式を`MainToolBar`/`PlacementToolBar`にも拡張する（§2.1）。
- **【本書最大の発見・要検討事項4への回答】「フロート時は帯を表示する」という殿裁定は、`DockingManager.Resources`への暗黙的スタイル登録という現行方式のままでは実現できない可能性が高い**（§2.2）。2件の独立した一次ソース調査（`LayoutAnchorableFloatingWindowControl.OnInitialized`・`DockingManager.CreateUIElementForModel`を実コード確認）により、フロートウィンドウ内で生成される`LayoutAnchorableControl`へ`Resources`を明示的に橋渡しするコードが存在しないことを確認した。フロートウィンドウは独立したロジカルツリールート（別`Window`）であり、`MainDockingManager.Resources`に登録した暗黙的スタイル（`TitleBarHiddenAnchorableControlStyle`）はそもそも解決経路上に無い——ゆえにフロート時はテーマ（VS2013）既定スタイルへフォールバックし、その原典トリガー（フロート直接ホスト時Header Collapse）がそのまま効き、殿裁定に反して帯は表示されないと推測される（確度高、完全な確証ではない）。**実装方式そのものが変わる分岐点のため、殿裁定を要する（T121裁1、§3）**。
- **要検討事項2（MenuDropDownButton喪失・AttachDragガードの影響）は決着済みの実測で解消**：通常のタブドラッグによるフロート化は、帯（AnchorablePaneTitle）とは完全に別経路（`AnchorablePaneTabPanel`/`LayoutAnchorableTabItem`、隠密調査書`ecad2-t110-increment2-finding-c-investigation-onmitsu.md`で一次ソース確定）であり、かつ**殿ご自身の物理操作で機能することが実機確認済み**（`docs/todo.md` T-110増分2所見C、2026-07-22決着）。帯を消してもフロート化の入口（タブドラッグ）自体は失われない（§2.3）。
- **新規の気づき（要検討事項に無かった論点）**：帯にはMenuDropDownButtonだけでなくPART_AutoHidePin（自動的に隠すピン）も同居しており、帯を消すとドッキング中のAutoHide操作の入口も失われる。追加要否は殿確認事項とする（T121裁2、§4）。
- **CanFloat非対称性（要検討事項3）は帯Collapseの適否に影響しない**。両タブとも同じ「ドッキング時常時非表示」で統一してよい。CanFloat自体は無変更（§2.4）。

### 殿裁定を要する事項

| # | 論点 | 選択肢 | 隠密の推奨 |
|---|---|---|---|
| T121裁1 | フロート時のDock/AutoHide操作の入口をどう確保するか | 案イ=ecad2独自メニューでFloat/Dock操作を代替提供（帯そのものの復元は追求しない） / 案ロ=`Application.Resources`へスタイル登録場所を移し暗黙的スタイル伝播を試みる（実現性は別途技術検証が必要） / 案ハ=フロートウィンドウ生成イベントをフックし動的にResourcesを注入（実装規模中、実現可能性は要検証） | **案イ**（§3.1、確実・低リスク・小規模） |
| T121裁2 | AutoHide代替UIへ配置ツールバー2項目（基本機能/配置ツール）を追加するか | 追加する / 追加しない（既存4項目のまま） | 追加しない（§4、実用上の意味が薄い） |

**殿裁定（2026-07-24、家老経由）＝T121裁1=案イの簡略版（Float発動のみをメニュー項目として実装し、Dock項目は含めない。ドッキング復帰は既存のタブドラッグ（§2.3で機能確認済み）に任せる。これにより§3.1で触れたT-099(c)調査5「Dock自己複製バグ」の現構成での再現有無の再検証は不要となる）。T121裁2=「追加しない」。いずれも隠密推奨どおり。**（`docs/todo.md` T-121節参照）

---

## 1. 前提（現状実装の確認、一次ソース・実コード確認済み）

- `MainToolBar`: `CanClose="False" CanFloat="False" CanDockAsTabbedDocument="False"`（`MainWindow.xaml:985`）。フロート不可。
- `PlacementToolBar`: `CanClose="False" CanDockAsTabbedDocument="False"`（`MainWindow.xaml:1076`、`CanFloat`未指定=既定`True`）。フロート可能。
- 現在の非対称挙動の直接原因は`UnifiedAnchorablePaneTitleStyle`内のMultiDataTrigger（`MainWindow.xaml:719-725`）：`ContentId="PlacementToolBar"` かつ ドッキング時（`IsDirectlyHostedInFloatingWindow=False`）の場合のみ、`AnchorablePaneTitle`内部の**ラベルTextBlock**（`Header`という名前、510-515行）をCollapseする（T-099(c)案E方式の名残）。`MainToolBar`はこの条件に該当せずラベルが常時表示される——これが「基本機能選択時は帯が見え、配置ツール選択時はラベルだけ消える」という非対称の実体（帯そのものの有無ではなく、帯内部のラベル文字の有無の差だった点に注意）。
- T-110増分3で導入済みの`TitleBarHiddenAnchorableControlStyle`（`MainWindow.xaml:742-826`）は、**帯全体**（`LayoutAnchorableControl`のHeader Border層、`AnchorablePaneTitle`インスタンス自体を包含）をContentId名指しでCollapseする方式。現在の対象は`LeftPalette`/`DeviceTable`/`RightPanelBottom`/`OutputPanel`の4ペインのみ（807-821行）で、`MainToolBar`/`PlacementToolBar`は対象外（設計書`ecad2-t110-increment3-titlebar-hide-and-autohide-ui-design-onmitsu.md`§1「配置ツールバーペインは対象外（殿確認済み）」）。この4ペインは全て`CanFloat="False"`なので、フロート時の挙動は一度も実地検証されていない（本書§2.2の問題が今回初めて顕在化する理由）。
- `TitleBarHiddenAnchorableControlStyle`は`ApplyDockingManagerThemes`（`MainWindow.xaml.cs:808-809`）で単一`MainDockingManager.Resources[typeof(LayoutAnchorableControl)]`へ暗黙的スタイルとして登録され、Light/Dark両テーマで再適用される。

---

## 2. 実装方式：帯全体Collapse方式（要検討事項1・2・3への回答）

### 2.1 対象ペイン追加は「既定コピー+ContentId名指し」方式の単純拡張（要検討事項1）

`TitleBarHiddenAnchorableControlStyle`のControlTemplate.Triggers末尾（現行822行の直前）へ、`MainToolBar`/`PlacementToolBar`用の`DataTrigger`を追加する（既存4ペインと同型、ドッキング/フロートの区別なく常時Collapseでよいかは§2.2参照——結論としては案イ採用ならこの単純形のままでよい）。

これにより「Header文字部分のみ非表示では高さの縮小効果がほぼ無い」問題（要検討事項1、T-099(c)当時の約21px見積もり）を解消する——Header Border層ごと消すため、支配要素だったボタン列（MenuDropDownButton等15px+Grid Margin）も含めて丸ごと消え、真の省スペース化になる。

### 2.2 【本書最大の発見】フロート時の帯表示は現行方式のままでは実現困難——一次ソース確認結果

T-110増分3の`TitleBarHiddenAnchorableControlStyle`は、VS2013テーマ既定スタイル（一次ソース `docs-notes/vendor-reference/avalondock-v4.74.1/.../Generic.xaml:1755-1761`）の**原典トリガー1**（フロート直接ホスト時Header Collapse）をそのまま移植している。これはVS2013テーマの設計思想として妥当——単一ペインがフロートウィンドウに直接ホストされる場合、フロートウィンドウ自体のOSタイトルバーが既にタイトルを表示するため、内部の帯（`AnchorablePaneTitle`）は冗長という判断。既存の対象4ペインは`CanFloat="False"`ゆえこのトリガーが実質発火せず、問題化しなかった。

T-121の殿裁定は逆方向——「フロート時はタイトルバー表示、ドッキング操作はフロート化後のタイトルバー経由で行う想定」。当初はこれを、原典トリガー1より後ろへ「ContentId名指し・フロート時はVisible復帰」というトリガーを追加すれば実現できると想定していた（WPFのトリガー後着優先、`UnifiedAnchorablePaneTitleStyle`の既存パターンと同型）。

**しかし、この前提そのものが崩れる可能性が高いことが一次ソース確認で判明した**。2件の独立したExplore調査で以下を確認した：

1. `LayoutAnchorableFloatingWindowControl.OnInitialized`（一次ソース、AvalonDock v4.74.1）：
   ```csharp
   protected override void OnInitialized(EventArgs e)
   {
       base.OnInitialized(e);
       var manager = _model.Root.Manager;
       Content = manager.CreateUIElementForModel(_model.RootPanel);
       ...
   }
   ```
   フロートウィンドウの中身は`manager.CreateUIElementForModel(...)`というファクトリメソッドで生成される。
2. `DockingManager.CreateUIElementForModel`（同、1563-1687行）の実装は、対象モデルの型ごとに`new LayoutAnchorablePaneControl(...)`等をnewして返すだけの単純なファクトリで、**生成したコントロールの`Resources`へ何かをマージ・代入する処理、および`DockingManager`自身の論理ツリーへ追加する処理（`AddLogicalChild`等）のいずれも存在しない**。

`LayoutAnchorableFloatingWindowControl`自体が独立した`Window`（別のロジカルツリールート）であり、暗黙的スタイル解決（`TargetType`ベースの`Resources`探索）はロジカルツリーの祖先を辿る仕組みである以上、上記の橋渡しコードが無ければ`MainDockingManager.Resources`に登録したカスタムスタイル（`TitleBarHiddenAnchorableControlStyle`）はフロートウィンドウ内の`LayoutAnchorableControl`には届かない。フロートウィンドウ内ではテーマ（VS2013）の既定スタイルへフォールバックし、その中の原典トリガー1（フロート時Header Collapse）がそのまま働くと推測される——これは「ecad2側でどんなトリガーを追加しても、フロートウィンドウ内のコントロール自体が別スタイルを使っているため効かない」ことを意味する。

**確度**：2件の独立調査（`LayoutAnchorableFloatingWindowControl.cs`・`DockingManager.CreateUIElementForModel`の実コード読解）が同じ結論を補強しており確度は高いが、`LayoutFloatingWindowControl`基底クラス側の`UpdateThemeResources`等、未確認の経路が皆無とは言い切れないため「不明点」に留め置く（§6）。

**結論**：フロート時に元の帯（MenuDropDownButton・PART_AutoHidePin含む）をそのまま復元させる設計は、実装コストと不確実性が見合わない可能性が高い。§3で代替方針を提示し、殿裁定を仰ぐ。

### 2.3 MenuDropDownButton・AttachDragガードへの回答（要検討事項2）——決着済みの実測で解消

要検討事項2は「通常のタブドラッグによるフロート化が実際に機能するかを検証してから帯全体除去に踏み切ること」だったが、**この検証は既に完了し決着している**（`docs/todo.md` T-110増分2所見C、2026-07-22）：

1. **経路の分離**（一次ソース確定、`docs/ecad2-t110-increment2-finding-c-investigation-onmitsu.md`）：配置ツールバー（2タブ構成）のタブからのフロート化は、単一ペインの帯ドラッグ（`AnchorablePaneTitle`のマウス処理）とは**完全に別の実装経路**（`LayoutAnchorableTabItem`＋`AnchorablePaneTabPanel.OnMouseLeave`→`StartDraggingFloatingWindowForContent`）。帯（`AnchorablePaneTitle`）を消しても、この経路のコード自体には一切触れない。
2. **実機で機能することを確認済み**：UIA合成ドラッグでは不成立に見えたが、**殿ご自身の物理マウス操作で「アプリ内でのドラッグは実際にはフロート化する」ことを確認**（`docs/todo.md`556-579行）。ただし増分1由来の別課題（「ドロップ枠判定範囲の全域拡大」という仮実装）により、アプリ内で手を離すと即座に再ドッキングされてしまうため、フロート状態を維持するにはアプリウィンドウの外まで運ぶ必要がある——これは**T-121のスコープ外の既知課題**（増分2以降へ申し送り済み）であり、帯の消去可否とは独立。
3. `MainToolBar`（CanFloat="False"）方向のドラッグは`StartDraggingFloatingWindowForContent`冒頭のガード（`DockingManager.cs:1701-1705`、一次ソース確認済み）で無害に弾かれる（既存所見Cで確認済み）。

**結論**：帯を消してもフロート化の入口（タブドラッグ）自体は独立して機能し続ける。§3で提示する代替UIと組み合わせれば、Dock/AutoHide操作へのアクセス手段は確保できる。

### 2.4 CanFloat非対称性への回答（要検討事項3）

帯Collapseの適否とCanFloatの値は独立した軸。`MainToolBar`はそもそもフロートしない。`PlacementToolBar`はフロート可能だが、§2.2の発見によりフロート時の帯表示は別問題として扱う（§3）。**両タブとも「ドッキング時は常時非表示」で統一してよく、CanFloat自体の変更は不要**。

---

## 3. T121裁1：フロート時のDock/AutoHide操作入口をどう確保するか（殿裁定事項）

### 3.1 案イ（隠密推奨・殿裁定採用）：ecad2独自メニューでFloat操作を代替提供

T-110増分3のAutoHide代替UI（表示メニュー「パネルを自動的に隠す」サブメニュー、`Tag`ベースの汎用実装）と同じ設計思想で、表示メニューへ「配置ツールバーをフロート化」相当の項目を追加する。対象は`PlacementToolBar`のみ（`MainToolBar`はCanFloat="False"のため対象外）。

- 発動：`LayoutAnchorable.Float()`（`LayoutContent.cs`、public、T-099(c)調査4で存在・アクセシビリティとも確認済み）を呼ぶ。
- **【殿裁定確定・簡略版】Dock項目はメニューに含めない**。ドッキング復帰は既存のタブドラッグ（§2.3で機能確認済み、アプリ内→再ドッキング領域への手動ドラッグ）に一本化する。これにより、T-099(c)調査5「メニューDockでのタブ自己複製バグ」（`InternalDock`のフォールバック探索にフロートウィンドウ除外フィルタが無い、4分割DockingManager時代の調査）が現在の単一`MainDockingManager`構成でも再現するかの再検証は**不要**となった（メニューのDock項目自体を作らないため、当該バグの発火経路そのものが存在しない）。
- **得**：技術的に確実（`DockingManager.Resources`の伝播問題を回避、§2.2の不確実性に依存しない）。実装規模小（Float発動のみでDockハンドラ不要、さらに小さくなった）。AutoHide代替UIとの一貫性が高く、利用者が「表示メニューを見ればパネル操作は揃っている」と学習しやすい。Dock自己複製バグの再検証が不要になった分、実装・レビューの手間もさらに減った。
- **失**：フロートウィンドウ自体の見た目（帯）は変わらない（VS2013既定のまま、Collapseされた状態が続く可能性がある）。「タイトルバーが表示される」という殿裁定の字面どおりの実現ではなく、機能面（Floatへのアクセス手段確保）での代替になる。ドッキング復帰はタブドラッグのみに依存するため、§2.3の既知課題（アプリ内で手を離すと即座に再ドッキングされる仮実装）の影響をそのまま受ける（T-121のスコープ外の既存課題として許容）。

### 3.2 案ロ：`Application.Resources`へスタイル登録場所を移す

`TitleBarHiddenAnchorableControlStyle`（フロート時Visible復帰トリガー込み）を`MainWindow.xaml`の`Window.Resources`から`App.xaml`の`Application.Resources`へ移動する。暗黙的スタイルは`Application.Resources`に登録すればプロセス内の全`Window`（フロートウィンドウ含む）に対して解決対象になるため、理論上はフロートウィンドウ内の`LayoutAnchorableControl`にも届く可能性がある。

- **得**：実現すれば「フロート時に実際に帯が表示される」という殿裁定の字面どおりの実現に近づく。
- **失**：(a) 未検証の推測に基づく変更であり、実際に暗黙的スタイルが届くかは実装後の実機確認まで確定しない（§2.2の不確実性がそのまま実装リスクに転化する）。(b) 対象4ペイン（LeftPalette等）が使う既存の同スタイルも巻き込むため、影響範囲がT-121の対象外（既存の安定動作）にまで及ぶ——回帰リスクが増す。(c) `MainDockingManager`が複数存在した場合（現状は単一だが）に備の区別ができなくなる懸念（現状は単一なので実害なしだが、設計as-isの純度が下がる）。
- 実装規模：中（移動自体は小さいが、影響範囲の再検証・全ペインの回帰確認が必要）。

### 3.3 案ハ：フロートウィンドウ生成イベントのフック

AvalonDockには`LayoutFloatingWindowControlCreatedEventArgs`という型が存在する（GitHub一次ソースのファイル一覧で確認済み、`source/Components/AvalonDock/LayoutFloatingWindowControlCreatedEventArgs.cs`）ことから、`DockingManager`がフロートウィンドウ生成時に発火するイベントがあると推測される（未確認、シグネチャ等は今回未調査）。もし存在すれば、ecad2側でこのイベントを購読し、生成された`LayoutAnchorableFloatingWindowControl`インスタンスへ`fwc.Resources.MergedDictionaries.Add(...)`のような形で明示的にリソースを注入できる可能性がある。

- **得**：案ロと違い対象4ペインを巻き込まず、`PlacementToolBar`のフロートウィンドウだけを狙い撃ちできる（影響範囲を絞れる）。
- **失**：イベントの存在・シグネチャ・発火タイミング（`OnInitialized`より前か後か、間に合うか）が未調査であり、実現可能性そのものが不明。追加の一次ソース調査が必要（本書のスコープでは未実施）。
- 実装規模：中（イベント調査次第で変動）。

### 3.4 得失比較と推奨

| 観点 | 案イ（独自メニュー） | 案ロ（Application.Resources） | 案ハ（イベントフック） |
|---|---|---|---|
| 確実性 | 高（既存パターンの踏襲） | 低〜中（未検証） | 低（未調査） |
| 影響範囲 | 小（PlacementToolBar限定） | 中〜大（対象4ペインも巻き込む） | 小（狙い撃ち可能） |
| 実装規模 | 小 | 中 | 中（要追加調査） |
| 殿裁定の字面との一致度 | 中（機能は満たすが見た目の帯復元はしない） | 高（実現すれば） | 高（実現すれば） |
| Dock自己複製バグ（T-099(c)調査5）との関係 | メニューにDock項目を含める場合は対処要 | 既存の帯機構をそのまま使うため対処不要 | 同左 |

**隠密推奨＝案イ**。理由：(1)技術的な確実性が最も高く、§2.2で判明した不確実性（フロートウィンドウへのリソース伝播）に実装が依存しない。(2)AutoHide代替UI（T-110増分3、既に稼働実績あり）と同じ設計パターンで、利用者の学習コストが増えない。(3)「フロート時にタイトルバー表示」という殿裁定の**目的**（ドッキング操作をフロート化後にできるようにする）は満たしつつ、**手段**（帯そのものの復元）に固執しない。ただし、これはUI/UXの分岐であり最終判断は殿に委ねる。

---

## 4. 新規の気づき：AutoHide操作の入口喪失（要検討事項に無かった論点、殿裁定＝追加しない）

帯（`AnchorablePaneTitle`）には`MenuDropDownButton`だけでなく`PART_AutoHidePin`（自動的に隠すピン、`MainWindow.xaml:571-593`）も同居する。帯全体をCollapseすると、ドッキング中はこのピンも同時に消え、AutoHide操作の入口を失う（`PART_HidePin`＝閉じるボタンは`CanClose="False"`のため元々非表示なので影響なし）。

これは対象4ペイン（T-110増分3）で先に踏んだのと同型の問題で、その時は表示メニュー「パネルを自動的に隠す」サブメニュー（`MainWindow.xaml:907-911`、`Tag`ベースの汎用実装）を新設して対処した。同じ実装パターンで`MainToolBar`/`PlacementToolBar`用の項目2つを追加するのは小規模（MenuItem2個+`AutoHideSubmenu_SubmenuOpened`へ2行）で技術的には容易。

ただし、**配置ツールバー自体をAutoHide（自動的に隠す）する実用上の意味は薄い可能性がある**——配置ツールバーは配置操作そのものの入口であり、隠すと配置操作ができなくなる（シート/機器表等の補助パネルとは性質が異なる）。隠密の推奨は「追加しない」だが、UI/UX判断であり殿確認事項とする（T121裁2）。

なお、追加しない場合でも機能自体（`CanAutoHide`既定True）は無効化しない限り技術的には有効なままである点に注意——もし将来「配置ツールバーはAutoHide不可にすべき」という判断になった場合は`CanAutoHide="False"`の明示指定を別途検討する（本書のスコープ外、気づきとして記録のみ）。

---

## 5. 既存トリガー（719-725行）の扱い

`UnifiedAnchorablePaneTitleStyle`内の719-725行（`PlacementToolBar`限定のラベルテキストCollapse、T-099(c)案Eの名残）は、本設計適用後は**ドッキング時、帯全体（`AnchorablePaneTitle`インスタンス自体）が先に消えるため実質発火しない**（内側のラベルトリガーが評価される前に外側のHeader Borderごと非表示になる）。フロート時は§3の裁定次第（案イ採用なら、フロート時の帯の見た目は無変更＝719-725行の条件`ContentId="PlacementToolBar" かつ ドッキング時`にも該当しないため、いずれにせよ発火しない）。

T-100本体コメント（`MainWindow.xaml:460-468`）の先例（「発火しない場合の実害はゼロ、削除しても既存の1トリガー分のコード削減にとどまる」という判断基準）に倣い、**削除せず残置を推奨**する。

---

## 6. 不明点（静的調査の限界の明示、実測必要）

1. **フロートウィンドウ内の`LayoutAnchorableControl`が`MainDockingManager.Resources`の暗黙的スタイルを引き続き受け取るか**（§2.2）：2件の独立調査で「届かない」可能性が高いと判断したが、`LayoutFloatingWindowControl`基底クラス側の未確認経路が皆無とは言い切れない。**殿裁定で案イ（Float発動のみ）採用が確定したため、本不明点は実装に影響せず解消不要**（フロートウィンドウ内の見た目がVS2013既定のままでも案イの実装は成立する）。

（旧・不明点2「T-099(c)調査5 Dock自己複製バグの現構成再現性」は、殿裁定によりメニューへDock項目を含めないことが確定したため、当該バグの発火経路自体が存在せず、確認不要になった。）

---

## 7. 検証計画（殿裁定＝T121裁1=案イ簡略版・T121裁2=追加しない、確定済み）

### 7.1 隠密静的レビュー観点（侍実装後）

1. `TitleBarHiddenAnchorableControlStyle`への追加トリガーがContentId 2値（`MainToolBar`/`PlacementToolBar`）とも正確であること。
2. `UnifiedAnchorablePaneTitleStyle`（719-725行含む）に触れていないこと（帯全体Collapseと帯内部ラベルCollapseは別レイヤー、混同注意）。
3. Float発動メニュー項目の実装、`Tag`ベースの対象取得（`x:Name`参照でないこと、T-099(c)の教訓）。**Dock項目が実装されていないこと**（殿裁定＝簡略版、ドッキング復帰はタブドラッグのみに一本化）。
4. AutoHide代替UIへの項目追加が行われていないこと（殿裁定＝追加しない）。
5. 本設計がLayoutモデルの属性・構造を変更していないこと（スタイルのみの変更、T-110増分3§2.7と同型の保証）。
6. 着手前チェック（本書§2.2・§6）との1対1突き合わせ（`onmitsu.md`調査ワークフロー既定）。

### 7.2 忍者実機確認項目（画素採取【MUST】: 色・枠線系）

1. **ドッキング時の帯消失**：基本機能・配置ツール両タブで帯が完全に消えること（Light/Dark両テーマ、画素採取）。高さの縮小効果を実測。
2. **フロート時の見た目**：`PlacementToolBar`をタブドラッグでアプリウィンドウ外まで運びフロート化し、フロートウィンドウの見た目（帯の有無、VS2013既定のままCollapseされている想定）を観察・記録（§6不明点1の実地裏取りを兼ねる）。
3. **メニューからのFloat発動**：表示メニューの新設項目からフロート化が正常に成立すること。
4. **タブドラッグでのフロート化・ドッキング復帰**：物理マウスでアプリ内→アプリ外へドラッグしフロート化が成立すること（既に決着済みだが本改修後の回帰確認）、およびアプリ内へドラッグで戻すと再ドッキングされること。
5. **MainToolBar（CanFloat=False）のドラッグ無害性**：基本機能タブをアプリ外へドラッグしても何も起きない（フロート化しない）ことを確認（回帰確認）。
6. **Ctrl+Alt+Rリセット**：既定レイアウトへ戻った後も、両タブのドッキング時帯消失が維持されること。
7. **Tabキー巡回**：破綻しないこと。

### 7.3 実装規模の目安（家老の采配材料）

- XAML: `TitleBarHiddenAnchorableControlStyle`へDataTrigger2本追加（約8行）、表示メニューへFloat項目1個（約3行）。
- C#: Floatハンドラ（約8行、AutoHide代替UIの`AutoHideMenuItem_Click`と同型、`Tag`からContentId取得→Descendents()検索→`Float()`呼出）。
- テスト: UI層のみの変更のため新規ユニットテストは不要見込み（既存テストへの影響なし）。

---

## 8. 実装順序の提案

1. 侍実装（帯全体Collapseを1コミット、Float発動メニュー実装を別コミットに分けることを推奨）。
2. 隠密静的レビュー（§7.1）→忍者実機確認（§7.2）。

---

## 出典

- `src/Ecad2.App/MainWindow.xaml`（380-430/460-468/469-730/732-826/907-911/975-1076行）
- `src/Ecad2.App/MainWindow.xaml.cs`（405-430/793-809行）
- AvalonDock v4.74.1一次ソース（`docs-notes/vendor-reference/avalondock-v4.74.1/`ローカル保存分、および本調査で追加取得した`LayoutAnchorableFloatingWindowControl.cs`・`LayoutFloatingWindowControl.cs`）:
  - `AvalonDock.Themes.VS2013/Themes/Generic.xaml`: 1712-1800（`LayoutAnchorableControl`既定スタイル・原典トリガー5本）
  - `AvalonDock/Controls/LayoutAnchorableFloatingWindowControl.cs`: 171-182（`OnInitialized`、`Content = manager.CreateUIElementForModel(...)`）
  - `AvalonDock/DockingManager.cs`: 1563-1687（`CreateUIElementForModel`、Resources橋渡し・論理ツリー追加のいずれも無し）・1701-1712（`StartDraggingFloatingWindowForContent`のCanFloatガード）
  - `AvalonDock/Controls/LayoutAnchorableTabItem.cs`: 89-158
  - `AvalonDock/Controls/AnchorablePaneTabPanel.cs`: 84-97
  - `source/Components/AvalonDock/LayoutFloatingWindowControlCreatedEventArgs.cs`（存在確認のみ、内容未調査、§3.3）
- 既往文書:
  - `docs/ecad2-t110-increment3-titlebar-hide-and-autohide-ui-design-onmitsu.md`（帯全体Collapse方式の原設計）
  - `docs/ecad2-t099-c-paneltitle-label-only-hide-design-onmitsu.md`（ラベルのみ非表示方式・T-099(c)案E）
  - `docs/ecad2-t099-c-overlaywindow-droptarget-and-attachdrag-survey-onmitsu.md`（AttachDragガード調査3・4・5、Dock自己複製バグ調査5・6）
  - `docs/ecad2-t110-increment2-finding-c-investigation-onmitsu.md`（タブドラッグ経路の一次ソース確定）
  - `docs/todo.md` T-121節・T-110増分2所見C決着記録（556-582行）
- memory: `wpf_collapse_visibility_hides_interaction`・`feedback_geometric_transform_endpoint_oversight`・`ecad2_comparison_target_identity_pitfall`
