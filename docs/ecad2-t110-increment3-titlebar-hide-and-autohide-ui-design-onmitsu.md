# T-110 増分3 実装設計書：単一ペインタイトルバー完全非表示＋AutoHide代替UI（隠密）

作成日: 2026-07-22　作成者: 隠密　委任元: 家老（殿裁定2026-07-22＝裁5付帯裁定「AutoHide機能を残す（代替UIを新設）」を受けた増分3設計依頼）
本書は設計のみで実装には着手しない。実装は侍、レビューは隠密、実機確認は忍者（検証パイプライン既定順）。

---

## 0. 要旨（先出し）

- **タイトルバー完全非表示（裁5=案A）は、LayoutAnchorableControlの暗黙スタイル差し替え（テーマ既定コピー+Header Border層のContentId分岐Collapse）で実装する**（§2）。PoC(h)で実証済みの手法を、無条件Collapseから**対象4ペイン名指しのDataTrigger 4本**へ変えて本実装化する（フェイルセーフ化、§2.3）。
- **T-099の罠（Collapse化で対話機能ごと死ぬ）は3層の根拠で回避**（§2.5）。特にAutoHideフライアウトのピン復帰UIは、AvalonDockの明示Style機構（`LayoutAutoHideWindowControl.AnchorableStyle`）により**ecad2側の非表示スタイルが構造的に届かず温存される**ことを一次ソースで確認した。
- **AutoHide代替UIは3案を提示**（§3）。隠密の推奨は**案1（表示メニューにサブメニュー「パネルを自動的に隠す」+ペイン別トグル4項目）**。UI/UX分岐ゆえ最終選定は殿裁定。
- 技術的裏付け: `LayoutAnchorable.ToggleAutoHide()`は**public**（呼出可能）、`DockingManager.ExecuteAutoHideCommand`の中身も同メソッド呼出のみ（§3.1）。前回教訓（存在確認+アクセシビリティ確認）に基づき可視性まで確認済み。
- 旧保存レイアウトファイルとのround-trip（PR-25の型）は**非該当**（本増分はLayoutモデルの属性・構造を一切変えない、§2.7）。
- 不明点3件（2タブペインのタイトル帯の有無・フライアウト実表示・IsAutoHidden変更通知）は正直に「不明」とし、設計をそれらに**依存しない形**に組んだ上で、忍者実機確認項目に含めた（§5・§6）。

### 殿裁定を要する事項

| # | 論点 | 選択肢 | 隠密の推奨 |
|---|---|---|---|
| 増3裁1 | AutoHide代替UIの方式 | 案1=表示メニューにペイン別サブメニュー / 案2=「アクティブパネルを自動的に隠す」単一項目 / 案3=ペイン中身の右クリックメニュー | 案1（§3.5） |
| 増3裁2 | （案1採用時）ショートカットキーの追加要否 | 追加しない / 後日別途検討 | 今回は追加しない（§3.6） |

**殿裁定（2026-07-22）＝増3裁1=案1（表示メニューにサブメニュー4項目）採用、増3裁2=ショートカットキーは
今回追加しない。いずれも隠密推奨どおり。**（`docs/todo.md` T-110節参照）

---

## 1. 前提（確定済み裁定と増分1完了時点の実装状態）

- 裁5=案A（タイトルバー完全非表示）・裁6=許容（アクティブ表示喪失、代替視覚表現なし）・付帯裁定=AutoHide機能は残す（代替UI新設）【いずれも殿裁可済み、`docs/todo.md` T-110節】。
- 対象は**単一ペイン4領域**: シート（LeftPalette）・機器表（DeviceTable）・プロパティ（RightPanelBottom）・出力（OutputPanel）。配置ツールバーペイン（MainToolBar/PlacementToolBar、2タブ同居）は**対象外**（殿確認済み、`docs/todo.md` T-110依頼内容(6)）。
- 増分1完了時点の実装状態（本設計の土台）:
  - 単一`MainDockingManager`+案1トポロジ（`src/Ecad2.App/MainWindow.xaml:735-`）。
  - 4ペインとも`CanClose="False" CanFloat="False" CanDockAsTabbedDocument="False"`（`MainWindow.xaml:1063/1198/1217/1348`）。`CanAutoHide`は未指定=既定true（AutoHide機能は現在も生きている）。
  - タイトルスタイルは`ApplyDockingManagerThemes`（`MainWindow.xaml.cs:762-`）で`MainDockingManager.Resources[typeof(AnchorablePaneTitle)]`へ`UnifiedAnchorablePaneTitleStyle`を登録（AutoHideピン=`PART_AutoHidePin`、`LayoutItem.AutoHideCommand`束縛、`MainWindow.xaml:474-481`）。
  - `LayoutAnchorableControl`のスタイルは未登録（テーマ既定のまま）＝タイトルバー帯は現在表示中。

---

## 2. 実装方式(1): タイトルバー完全非表示

### 2.1 手法の骨子

VS2013テーマ既定の`LayoutAnchorableControl`スタイル（AvalonDock v4.74.1 `Generic.xaml:1712-1800`、全89行・Style本体Setterは`Template`の1本のみ）を**完全コピー**し、テンプレート内の`Header` Border（`Generic.xaml:1726-1728`、`AnchorablePaneTitle`を包む層）に対し**対象ContentIdのDataTriggerでVisibility=Collapsed**を加えたスタイル（仮称`TitleBarHiddenAnchorableControlStyle`）を`MainWindow.xaml`のWindow.Resourcesへ定義する。

これはPoC(h)（`poc/t110-single-dockingmanager-poc/.../MainWindow.xaml:336-375`、実機動作確認済み）の本実装化であり、テーマ自身が「フロートウィンドウ直接ホスト時にHeaderをCollapse」する同型トリガーを内蔵する（`Generic.xaml:1755-1761`）**テーマ公認パターン**である（隠密プラン§6.2）。

### 2.2 原典からの完全移植（PoC軽2の解消を含む）

PoC実装は原典トリガー5本のうち2本へ簡略化していた（PoCレビュー軽2: ドッキング時`BorderThickness`が1,0,1,1のままとなり上辺が欠ける）。**増分3では原典の5本を全て移植する**:

| # | 原典（Generic.xaml） | 内容 | 増分3での扱い |
|---|---|---|---|
| 1 | 1755-1761 | フロート直接ホスト時 Header Collapse | そのまま移植（配置ツールバーのフロート時に必要） |
| 2 | 1762-1771 | Model null時（仮想化）Header Collapse | そのまま移植 |
| 3 | 1774-1779 | IsFloating=False時 Bd BorderThickness=1 | そのまま移植（**上辺欠け防止の要、軽2解消**） |
| 4 | 1781-1786 | IsDirectlyHostedInFloatingWindow=False時 Bd=1 | そのまま移植 |
| 5 | 1788-1795 | フロート直接ホスト+Items.Count==1時 Bd=0 | そのまま移植 |
| 追加 | — | **対象4ペインContentIdのDataTriggerでHeader Collapse（4本）** | 新規（§2.3） |

Style本体のSetterが`Template`1本のみであることは一次ソースで確認済み（`Generic.xaml:1712-1800`に`Background`等の既定値Setterは無く、テンプレート内で`TemplateBinding Background`とDynamicResource直参照により賄われている）。**PR-21（Style本体Setter転記漏れ）のリスク面は従来3例より小さい**が、レビュー時は原典との1対1突き合わせを必ず行う（§6.1）。

### 2.3 対象限定はContentId名指しのDataTrigger 4本（PoCの無条件方式から変更）

PoC(h)は`Header`のVisibilityを**無条件**でCollapsedにし、実機で「単一ペイン4領域は消え、配置ツールバーは見た目変化なし」を観測した。しかし配置ツールバー（2タブペイン）が対象外になった**機序は未確定**である（§5-1参照。「2タブペインではタイトル帯自体が表示されない」可能性が高いが、一次ソースの`AnchorablePaneControlStyle`の`ContentTemplate`（`Generic.xaml:553-`）は2タブでも`LayoutAnchorableControl`を生成する構造であり、帯が出ない機序を静的に特定し切れなかった）。

よって本実装では実測偶然に依存せず、**明示的に対象を限定する**:

```xml
<!-- 概念形。既定はテーマ原典どおり（Header表示）、対象4ペインのみ非表示 -->
<ControlTemplate.Triggers>
    <!-- 原典トリガー5本（§2.2）をここに完全移植 -->
    <DataTrigger Binding="{Binding Model.ContentId, RelativeSource={RelativeSource Self}}" Value="LeftPalette">
        <Setter TargetName="Header" Property="Visibility" Value="Collapsed"/>
    </DataTrigger>
    <DataTrigger Binding="{Binding Model.ContentId, RelativeSource={RelativeSource Self}}" Value="DeviceTable">
        <Setter TargetName="Header" Property="Visibility" Value="Collapsed"/>
    </DataTrigger>
    <DataTrigger Binding="{Binding Model.ContentId, RelativeSource={RelativeSource Self}}" Value="RightPanelBottom">
        <Setter TargetName="Header" Property="Visibility" Value="Collapsed"/>
    </DataTrigger>
    <DataTrigger Binding="{Binding Model.ContentId, RelativeSource={RelativeSource Self}}" Value="OutputPanel">
        <Setter TargetName="Header" Property="Visibility" Value="Collapsed"/>
    </DataTrigger>
</ControlTemplate.Triggers>
```

**名指し方式（既定=表示、4ペインのみ非表示）を推奨する理由**:
1. **フェイルセーフ**: 将来ペインを追加した場合、黙ってタイトルバーが消えるのでなく「表示される」側に倒れる（消したければ明示的に1本足す）。
2. **機序不明への頑健性**: 配置ツールバーの帯が実は存在する状況（例: 想定外の単独ドッキング化）が生じても、名指し外なので巻き込まない。
3. **既存実装との一貫性**: `UnifiedAnchorablePaneTitleStyle`のContentId分岐（案E由来、`MainWindow.xaml`）・T-100/案Eの「既定コピー+標的差し替え」思想と同型。

なお`Model.ContentId`を条件にしたDataTrigger分岐が`LayoutAnchorableControl`層で機能すること自体はPoC(e)類似構造+（h)の複合で実機実証済み（PoC統合タイトルスタイルのContentId分岐と同じバインドパス形式。ただし§5-1の限定に留意）。

### 2.4 適用方法: `ApplyDockingManagerThemes`でのResources登録（既存パターンと一貫）

```csharp
// ApplyDockingManagerThemes内、UnifiedAnchorablePaneTitleStyle登録の直後に追加
MainDockingManager.Resources[typeof(AvalonDock.Controls.LayoutAnchorableControl)] =
    (Style)FindResource("TitleBarHiddenAnchorableControlStyle");
```

- PoC(h)と同じ暗黙スタイル登録方式（`poc/.../MainWindow.xaml.cs:74`）。
- 既存の`AnchorablePaneTitle`スタイル登録（`MainWindow.xaml.cs:772`）と同じ場所・同じ機構に揃え、テーマ切替（Light/Dark）時も同一経路で再登録される（スタイル自体はDynamicResource参照ゆえテーマ非依存だが、コードの読みやすさ・増分1との一貫性を優先）。
- XAML側`DockingManager.Resources`への静的定義でも技術的には成立するが、スタイル登録の置き場が2系統に割れるため採らない。

### 2.5 T-099の罠（`wpf_collapse_visibility_hides_interaction`）の再発防止

家老依頼の明示観点。3層で確認した:

1. **消す階層が違う**: T-099の事故は`AnchorablePaneTitle`コントロール自体のCollapse化でマウス処理（フロート化ドラッグ）ごと殺した件。本設計はその**一段上のHeader Border層**を消す（テーマ自身がフロート時に行うのと同じ層・同じTargetName）。`AnchorablePaneTitle`のスタイル（`UnifiedAnchorablePaneTitleStyle`）には一切触れない。
2. **失われる機能は全て代替済みか棚卸しした**（隠密プラン§6.1の表の増分3時点版）:

   | 機能 | 増分3後の状態 |
   |---|---|
   | #1 ペイン名表示 | 喪失を許容（裁5=案A、ペイン中身で自明） |
   | #2 ドラッグでフロート化 | 機能自体なし（裁4=CanFloat="False"、対象4ペイン） |
   | #3 クリックでアクティブ化 | 中身クリックで代替（PoC(d)・増分1(4)で実機実証済み） |
   | #4 標準メニュー（Float/Dock/AutoHide/Hide） | Float/Dock/Hideは機能自体封止済み。AutoHideのみ代替UI新設（§3） |
   | #5 AutoHideピン | 代替UI新設（§3）+フライアウトのピンで復帰（下記3） |
   | #6 閉じる | 機能自体なし（CanClose="False"、従来から） |
   | #7 右クリックメニュー | #4と同一内容ゆえ同じ扱い |

3. **AutoHideフライアウトのピン復帰UIは構造的に温存される**（一次ソース確認済み、本設計の安全性の要）:
   - フライアウト（`LayoutAutoHideWindowControl`）は内部の`LayoutAnchorableControl`を**明示Style付き**で生成する: `_internalHost = new LayoutAnchorableControl { Model = _model, Style = AnchorableStyle }`（`LayoutAutoHideWindowControl.cs:278`）。
   - この`AnchorableStyle`はVS2013テーマが`LayoutAutoHideWindowControl`の暗黙スタイルで設定している（`Generic.xaml:2465-2474`、BorderBrush/BorderThicknessのみの簡素Style）。
   - WPFのスタイル解決規則上、**Styleプロパティに明示値がある要素には暗黙スタイル（Resources[typeof(...)]登録）は適用されない**。ゆえに§2.4の非表示スタイルはフライアウト内の`LayoutAnchorableControl`には届かず、そのTemplateはThemeStyle（AvalonDock本体`generic.xaml:863-927`）へフォールバックする。同Templateは`Header`（`AnchorablePaneTitle`、877-879行）を含み、Collapseトリガーはフロート時とModel null時のみ（901-922行）——**AutoHide中のフライアウトにはタイトルバー（ピン付き）が表示され、ピンでのドッキング復帰が可能**。
   - （帰結）AutoHideへ入れた後、メニューを使わずともフライアウトのピンで戻れる。メニュー側の復帰動線（§4）と二重化される。
   - ※これは静的解析による結論であり、実表示は忍者実機確認項目に含める（§6.2-4）。

### 2.6 PR-21（Style本体Setter転記漏れ）対策

- 原典スタイルのStyle本体Setterは`Template`1本のみ（§2.2で確認済み）。`ItemTemplate`/`ContentTemplate`/`ItemContainerStyle`等の機能Setterは**このスタイルには存在しない**（それらは`AnchorablePaneControlStyle`側の話で、増分1で移植済み）。
- レビュー時（§6.1）は`Generic.xaml:1712-1800`と実装XAMLの1対1突き合わせを行い、テンプレート内部構造（Bd/Header/ContentPresenterの3要素+Binding 3本）とトリガー5本+追加4本の全数を確認する。

### 2.7 レイアウト永続化との整合（PR-25の型の点検）

本増分は**Layoutモデルの構造・属性を一切変更しない**（スタイルとメニューのみの変更。`CanAutoHide`も既定trueのまま指定しない）。ゆえに:
- 増分1で保存された`main-layout.xml`とのround-trip問題（PR-25: 旧版ファイルのDeserializeがXAML修正を無音上書き）は**構造的に非該当**。
- AutoHide状態自体は従来からシリアライズ対象（`LayoutAnchorable.cs` WriteXml:249-258、`AutoHideWidth`等）であり、増分3で挙動が変わる要素は無い。AutoHide状態での終了→再起動の復元は忍者確認項目とする（§6.2-6、増分3の新規リスクではなく代替UI経由でAutoHideが日常操作になることへの備え）。

---

## 3. 実装方式(2): AutoHide代替UI（3案、殿選定用）

### 3.1 全案共通の技術的裏付け（一次ソース確認済み）

1. **発動・復帰とも`LayoutAnchorable.ToggleAutoHide()`（public）を呼べばよい**（`LayoutAnchorable.cs:429`。ドッキング中→AutoHide化、AutoHide中→ドッキング復帰のトグル動作）。タイトルバーのピンが実行する`DockingManager.ExecuteAutoHideCommand`は`internal`だが、中身は`_anchorable.ToggleAutoHide()`の1行のみ（`DockingManager.cs:1943`）——**同一実体に公開経路で到達できる**。前回教訓（T-110増分1(6): 存在確認+アクセシビリティ確認をセットで）に基づき、publicであることまで確認済み。
2. **対象`LayoutAnchorable`の取得はContentId検索とする**: `MainDockingManager.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault(a => a.ContentId == "...")`。増分1既存コードの確立パターン（`UpdateOutputPanelTitle`等、`MainWindow.xaml.cs:238/354/380/393`）と同一。
   - **【重要・罠】XAMLの`x:Name`参照は使わない**: レイアウトのDeserialize（起動時読込・Ctrl+Alt+R）でXAML生成のモデルツリーは丸ごと差し替えられるため、`x:Name`フィールドは古いインスタンスを指し続ける。T-099(c)の教訓（Layout差し替えが正規機構）と同根であり、必ず都度Descendents()検索する。
3. **実行可否条件**: 標準ピンの`CanExecuteAutoHideCommand`は「非フロート かつ CanAutoHide」（`LayoutAnchorableItem.cs:105-110`）。対象4ペインはCanFloat="False"かつCanAutoHide=既定trueゆえ常時実行可能。代替UI側でも同条件（実際にはnullチェックのみで足りる）とする。
4. **チェック状態の同期は「メニューを開いた時に都度評価」とする**: AutoHide中か否かは`LayoutAnchorable.IsAutoHidden`（public、`Parent is LayoutAnchorGroup`の計算プロパティ、`LayoutAnchorable.cs:161`）で判定できるが、**変更通知（PropertyChanged）が発行されるかは未確認**（§5-3）。通知に依存せず、親MenuItemの`SubmenuOpened`イベントで4項目の`IsChecked`を都度設定する方式なら確実（メニューは開いた瞬間の状態が見えれば足りる）。

### 3.2 案1: 表示メニューにサブメニュー「パネルを自動的に隠す」+ペイン別トグル4項目【隠密推奨】

```
表示(V)
├─ グリッド表示(G)                Ctrl+G      （既存）
├─ ダークモード(作図色)(D)                    （既存）
├─ ────────────
├─ パネルを自動的に隠す(A)                    （新設サブメニュー）
│   ├─ ☑ シート(S)
│   ├─ ☐ 機器表(D)
│   ├─ ☐ プロパティ(P)
│   └─ ☐ 出力(O)
└─ 現在のレイアウトを既定として保存(L) Ctrl+Alt+S （既存）
```

- チェックON=AutoHide中。クリックで`ToggleAutoHide()`（発動も復帰も同じ項目）。
- アクセスキーは既存と衝突しない（表示メニュー直下は G/D/L 使用済み、_A は空き。サブメニュー内は独立スコープ）。
- **得**: (a)対象ペインが名指しで明確、誤操作しにくい (b)発動と復帰が同じ場所で完結（AutoHide中のペインはタイトルバーが無くともメニューから戻せる） (c)キーボード到達可能（Alt+V→A→S等、キーボードファースト方針と整合） (d)実装が最小（メニュー5項目+ハンドラ1本+SubmenuOpened同期）。
- **失**: (a)メニュー階層が1段深い (b)「今見ているペインを隠す」には対象名を選ぶ一手間。

### 3.3 案2: 「アクティブパネルを自動的に隠す」単一項目（Visual Studio流）

表示メニューへ単一項目を置き、`MainDockingManager.ActiveContent`から対象を解決して`ToggleAutoHide()`。

- **得**: (a)メニュー1項目のみで最小の見た目 (b)VSの「ウィンドウ>自動的に隠す」に馴染んだ利用者には直感的。
- **失**: (a)キャンバス（LayoutDocument）がアクティブな時は対象が無く、無効化制御（CanExecute相当）が必須 (b)**復帰動線が回りくどい**——AutoHide中のペインを戻すには、サイドタブをクリックしフライアウトを出してから同メニューを再実行（またはフライアウトのピン）という2段操作になる (c)「どのペインに効くか」が状態依存で、誤って意図しないペインを隠すリスク (d)裁6=アクティブ表示喪失許容の環境では「今どれがアクティブか」の視覚手掛かりが乏しく、(c)のリスクが増幅される。
- 実装コスト: 小〜中（ActiveContent解決+無効化制御）。

### 3.4 案3: ペイン中身の右クリックコンテキストメニューに「自動的に隠す」

4ペインの中身（ListBox/DataGrid等）へContextMenuを追加（または既存ContextMenuへ項目追加）。

- **得**: (a)ペインとの対応が最も直感的（そのペインの上で右クリック） (b)タイトルバー右クリックメニュー（旧機能#7）の自然な代替。
- **失**: (a)中身コントロールの既存・将来のContextMenuと競合しうる（機器表DataGridの行操作メニュー等を将来設ける場合、AutoHide項目が同居して煩雑化） (b)4箇所へ分散実装（保守点が増える） (c)**AutoHide中の復帰ができない**——中身が画面から消えている（サイドタブ化）ため右クリック対象が無く、復帰はフライアウトのピンのみに依存 (d)発見性が低い。
- 実装コスト: 中（4ペインの中身XAML変更+既存ContextMenu有無の個別確認）。

### 3.5 得失比較と推奨

| 観点 | 案1（メニュー4項目） | 案2（アクティブ単一） | 案3（右クリック） |
|---|---|---|---|
| 発動の明確さ | 高（名指し） | 中（状態依存） | 高（場所依存） |
| **復帰動線** | **同じメニューで完結** | 回りくどい（2段） | 不可（ピン頼み） |
| キーボード到達 | 可（Alt+V,A,...） | 可 | 弱（Shift+F10） |
| 誤操作リスク | 低 | 中（裁6環境で増幅） | 低 |
| 実装・保守 | 最小・1箇所 | 小〜中 | 中・4箇所分散 |
| 既存UIとの競合 | なし | なし | 将来competing懸念 |

**隠密推奨=案1**。決め手は復帰動線の完結性（タイトルバーが無い世界でAutoHideの入りと出が同じUIで済む唯一の案）と、キーボードファースト方針との整合。案2はVS踏襲の馴染みはあるが、裁6（アクティブ表示なし）環境との相性が悪い。UI/UX分岐ゆえ最終選定は殿に委ねる。

### 3.6 ショートカットキー（増3裁2）

キーボードファーストの観点では専用ショートカット（例: アクティブペインをAutoHideするCtrl+Alt+A等）も考えうるが、今回は**追加しない**ことを推奨する:
- 案1のメニュー経由で既にキーボード到達可能（Alt+V,A,S等）。
- アクティブペイン依存のショートカットは案2と同じ曖昧さ（裁6環境）を持ち込む。
- Alt絡みショートカットは検証手法上の制約も既知（増分1(9): 合成キー入力が配送されない。実機確認は物理操作かメニュー経由となる）。
- 必要性が実運用で見えてから追加しても遅くない（YAGNI）。

---

## 4. 復帰動線の全体像（案1採用時）

AutoHide中のペインへの到達経路は二重化される:

1. **サイドタブ→フライアウト→ピン**: AutoHide化するとウィンドウ端にサイドタブ（`LayoutAnchorControl`）が出る。クリックでフライアウト表示、フライアウト上部のタイトルバー（テーマ既定・ピン付き、§2.5-3で温存根拠確認済み）のピンでドッキング復帰。
2. **メニュー**: 表示>パネルを自動的に隠す>該当ペインのチェックを外す（フライアウトを出さずに復帰可能）。

---

## 5. 不明点（静的調査の限界の明示、実測必要）

1. **2タブペイン（配置ツールバー）のタイトル帯が表示されない機序**: PoC(h)実測で「無条件Collapseでも配置ツールバーは見た目変化なし」、PoC(e)実測で「ContentId分岐（AnchorablePaneTitle層のラベル非表示）がドッキング時にも発火場面なし」（忍者2.5）が観測された。テーマの`AnchorablePaneControlStyle`は2タブでも`ContentTemplate`経由で`LayoutAnchorableControl`（Header帯持ち）を生成する構造（`Generic.xaml:553-`）であり、帯が出ない機序を一次ソースから特定し切れなかった。**本設計は名指し方式（§2.3）によりこの不明点に依存しない**が、増分1申し送り(5)（案E相当のラベル非表示がどの層で効いているか）と同件として、増分3実機確認で「配置ツールバーペインの上部帯の有無」を観察・記録する（§6.2-2）。
2. **フライアウトのタイトルバー実表示**: §2.5-3は静的解析（明示Style機構+ThemeStyleフォールバック）による結論。実表示は忍者確認で裏取りする（§6.2-4）。
3. **`IsAutoHidden`の変更通知の有無**: 未確認。設計は通知に依存しない（SubmenuOpened都度評価、§3.1-4）ため実装上の支障はないが、侍実装時にもし通知ベースのバインディングを検討する場合は一次ソース（`LayoutContent.OnParentChanged`系）の確認を必須とする。

---

## 6. 検証計画

### 6.1 隠密静的レビュー観点（侍実装後）

1. `TitleBarHiddenAnchorableControlStyle`と原典`Generic.xaml:1712-1800`の**1対1突き合わせ**（テンプレート構造3要素・Binding 3本・トリガー5本+追加4本の全数。PR-21観点=Style本体Setterの全転記確認を含む——本件はTemplate 1本のみ）。
2. ContentId 4値（LeftPalette/DeviceTable/RightPanelBottom/OutputPanel）の正確性（XAML定義との突き合わせ）。
3. `AnchorablePaneTitle`層（`UnifiedAnchorablePaneTitleStyle`）に触れていないこと（T-099罠の階層確認）。
4. 対象取得がContentId検索（Descendents()）であり`x:Name`参照でないこと（§3.1-2の罠）。
5. メニューのアクセスキー・既存項目との衝突有無、SubmenuOpened同期の実装。
6. 本増分がLayoutモデルの属性・構造を変更していないこと（§2.7の保証の実装確認）。
7. 着手前チェック（本書§2.2の移植表・§6.1自身）との1対1突き合わせ（`onmitsu.md`調査ワークフロー既定）。

### 6.2 忍者実機確認項目（画素採取【MUST】: 色・枠線系）

1. **タイトルバー消失**: 4ペイン全てで帯が完全に消えること（Light/Dark両テーマ、画素採取）。
2. **配置ツールバーペインは不変**: 上部帯の有無を含む見た目が増分2完了時点と同一であること（§5-1の観察記録を兼ねる。帯が「元々無い」ならその旨を記録し、増分1申し送り(5)を同時に解消する）。
3. **枠線**: ドッキング時の上辺を含む4辺が欠けないこと（軽2解消の確認、BorderThickness=1、画素採取）。
4. **AutoHide一巡**: 代替UI発動→サイドタブ化→フライアウト表示→**フライアウトにタイトルバー+ピンが表示されること**（§5-2の裏取り）→ピンで復帰。4ペイン全てで実施。
5. **メニュー復帰**: AutoHide中にフライアウトを出さず、メニューのチェック解除で復帰できること。チェック状態が実状態と常に一致すること（発動・復帰の前後でメニューを開き直して確認）。
6. **永続化round-trip**: AutoHide状態で終了→再起動で状態が復元されること。復元後もメニューのチェック状態が一致すること。
7. **Ctrl+Alt+R**: リセットでAutoHide状態も既定（全ペインドッキング）へ戻ること、その後のタイトルバー非表示が維持されること。
8. **アクティブ化経路**: タイトルバー消失後も、各ペインの中身クリックでフォーカス移動できること（機能#3の代替の回帰）。
9. **配置ツールバーFloat→Dock往復**: 従来どおり正常（トリガー1の移植確認、1〜2周でよい）。
10. **Tabキー巡回**: 破綻しないこと（増分1確認項目の回帰）。
11. 検証操作の注意: ToggleButton系はUIA Toggle()でなく物理クリック相当で（`ecad2-ui-automation`スキル既知の罠）。Alt絡みショートカットは合成キー不達のためメニュー経由で。

### 6.3 実装規模の目安（家老の采配材料）

- XAML: スタイル約95行（原典89行相当+追加トリガー4本）+メニュー約8行（案1採用時）。
- C#: Resources登録1行+メニューハンドラ+SubmenuOpened同期で計約40行。
- テスト: 挙動がUI層に閉じるため新規ユニットテストは最小（ContentId定数の突き合わせ程度を侍判断で）。既存テストへの影響なし見込み（Layoutモデル・永続化コードは無変更）。

---

## 7. 実装順序の提案

1. 増分2（回帰総点検）完了を待つ（増分3の変更が回帰確認の基準面を汚さないため。家老プラン§2の既定順どおり）。
2. 増3裁1（代替UI方式）・増3裁2（ショートカット要否）の殿裁定。
3. 侍実装（タイトルバー非表示+代替UIを1コミットずつ分けることを推奨——問題発生時の切り分け）。
4. 隠密静的レビュー（§6.1）→忍者実機確認（§6.2）。

---

## 出典

- `src/Ecad2.App/MainWindow.xaml`（474-481/673-723/735-/1063/1106/1198/1217/1348行、増分1完了時点）
- `src/Ecad2.App/MainWindow.xaml.cs`（178/238/354/378-407/762-772行、同上）
- `poc/t110-single-dockingmanager-poc/T110SingleDockingManagerPoc/MainWindow.xaml`（204-323/336-375行）・同`MainWindow.xaml.cs`（65-80行）
- AvalonDock v4.74.1一次ソース（GitHub Dirkster99/AvalonDock タグv4.74.1、2026-07-22 curl取得・scratchpad保存）:
  - `AvalonDock.Themes.VS2013/Themes/Generic.xaml`: 404-560（AnchorablePaneControlStyle・ContentTemplate:553-）/1712-1800（LayoutAnchorableControl既定スタイル・Header:1726-1728・トリガー5本:1749-1795）/2465-2475（LayoutAutoHideWindowControlスタイル・AnchorableStyle Setter）
  - `AvalonDock/Layout/LayoutAnchorable.cs`: 123-130（CanAutoHide）/161（IsAutoHidden）/249-258（WriteXml）/429（ToggleAutoHide、public）/607
  - `AvalonDock/Controls/LayoutAutoHideWindowControl.cs`: 33/63-75（AnchorableStyle、既定null）/278（_internalHost生成、明示Style付与）
  - `AvalonDock/Controls/LayoutAnchorableItem.cs`: 79-112（AutoHideCommand・CanExecute:105-110・Execute:112）
  - `AvalonDock/DockingManager.cs`: 1523（GetLayoutItemFromModel、public）/1943（ExecuteAutoHideCommand、internal・中身はToggleAutoHide()のみ）
  - `AvalonDock/Themes/generic.xaml`: 863-927（LayoutAnchorableControlのThemeStyle、Header:877-879・トリガー:900-923）
- 既往文書: `docs/ecad2-t110-single-dockingmanager-unification-plan-onmitsu.md`（§6.1-6.5）・`docs/ecad2-t110-implementation-plan-karo.md`（§1-§2）・`docs/ecad2-t110-poc-review-onmitsu.md`（§3・軽1〜軽3・追補）・`docs/ecad2-t110-poc-verification-ninja.md`（2.1-2.5）・`docs/todo.md` T-110節
- memory: `wpf_collapse_visibility_hides_interaction`・`t110_increment1_moguratataki_overview_effect`・`feedback_verify_before_acting_on_diagnosis`（アクセシビリティ確認）・PR-21/PR-25（`docs-notes/pattern-recurrence-log.md`）
