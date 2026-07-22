# T-110 増分2 所見A・B調査書（隠密）

調査日: 2026-07-22　調査担当: 隠密　委任元: 家老（増分2忍者実機確認で検出された重大所見2件、バグ対応Wチェック）
本書は調査のみで修正には着手しない（所見Bは侍が並行修正中、家老が両診断を突合する）。

---

## 所見A: メニューバー無効時ライト固定色 — 範囲判定

### 結論

1. **範囲判定=増分1範囲外（既存事象）がほぼ確実**。増分1はメニュー関連資産に一切触れておらず、無効化の構造も増分1前後で不変（根拠1・2）。
2. **家老仮説（Aero2既定のIsEnabled=false固定色Trigger）は一次ソースで否定**。Aero2の無効時トリガーは文字色のみで、背景を変えるトリガーはどの層にも存在しない（根拠3〜5）。
3. 観測色#EEF4F9の出所は**静的に特定できず**（不明）。ecad2ソース・WPF既定テーマ・VS2013テーマのいずれにも定義が無い（根拠6）。実測切り分け案を後述する。

### 根拠（すべて一次ソース実測）

1. **無効化の構造は増分1前後で不変**: メニューバーは`IsMainContentEnabled`束縛ラッパー内にT-033増分2以来ずっと配置されている。増分1直前（`git show a78b802~1`: 891行ラッパー・908行Menu）と現在（`MainWindow.xaml:659-660`ラッパー・674行Menu）で同一構造。「増分1で单一ラッパーへ含まれた影響」という切り分け候補は**成立しない**（元から含まれていた）。
2. **増分1以降の全7コミット（a78b802〜58b1a47）でメニュー関連変更ゼロ**: `git diff a78b802~1..HEAD`でMainWindow.xamlのMenu定義部（`<Menu`/MenuBar/MenuItem）にヒット無し、App.xaml差分ゼロ。Theme.Light/Dark.xamlへの各4行追加はT-111の`WarningMessageBrush`（ステータスバー警告文字色）のみでメニュー無関係。
3. **Aero2のMenu既定スタイルに無効時トリガーは無い**（dotnet/wpf main `Themes/XAML/Menu.xaml:205-228`、Aero2セクション）。Backgroundは`Menu.Static.Background`(#FFF0F0F0)のStyle Setterのみで、テンプレートは`TemplateBinding Background`。ecad2は`MainWindow.xaml:674-675`でBackgroundをローカル値（`DynamicResource MenuBarBackgroundBrush`）指定しており、依存関係プロパティ優先順位で常に勝つ（無効時もダーク時は#FF2D2D30のはず）。
4. **Aero2のMenuItem各テンプレートのIsEnabled=Falseトリガーは文字色のみ**（`MenuItem.xaml` Aero2セクション1998-2514行の4テンプレート全て: `TextElement.Foreground`/`Glyph Fill`へ`Menu.Disabled.Foreground`(#FF707070)を設定するのみ、Background Setter無し）。MenuItem既定StyleのBackgroundは`Transparent`（2490行〜）。
5. **ecad2自作TopLevelHeaderテンプレート（T-083増分7、App.xaml:797-800）の無効時トリガーも文字色のみ**（`MenuDisabledForegroundBrush`、Light/Dark両対応済み）。templateRoot Backgroundは`TemplateBinding Background`で無効時に触れない。
6. **#EEF4F9はどこにも定義されていない**: ecad2 `src/`・`poc/`全体、WPF既定テーマ（Menu.xaml/MenuItem.xaml/ToolBar.xaml）、VS2013テーマ（Generic.xaml/LightBrushs.xaml）の全grepでゼロ件。静的定義由来の色ではない（半透明合成または採取対象のずれを示唆。ただし半透明合成ならLight/Dark下地で結果が変わるはずで「両テーマ同色」観測と緊張関係がある——これも実測確認事項）。
7. **過去のダークモード検証は無効状態メニューを確認していない**: T-107/T-108の検証記録4ファイルに「メニュー」「無効」「要素配置」への言及ゼロ。「既存だが未観測」と整合する。

### 検討して棄却した仮説

- **配置バー（ElementPlacementBar）の誤配置がメニューバーに重なって見えている説**: 増分1で`PositionPlacementBar`の座標基準がCanvasDocumentGridへ書き換えられた直後のため疑ったが、配置バー背景は`DialogBackgroundBrush`（ダーク時#FF2D2D30=暗色）であり「ダークでもライト固定色」の観測と矛盾。可能性低（ただし完全排除は実測待ち）。

### 実測切り分け案（忍者へ、家老経由）

1. **試金石**: ライト・通常時（有効状態）のメニューバーを画素採取——Theme.Lightの`MenuBarBackgroundBrush`=#F0F0F0が出るはず。もし通常時から#EEF4F9なら「無効化が原因」ではなく採取対象・採取位置の問題（別要素の矩形を測っている）へ切り替わる。
2. 4状態マトリクス（Light/Dark × 有効/無効）でMenuBarArea矩形をUIAで取り直して画素採取。
3. **決定打（範囲判定の直接証拠）**: 増分1直前コミット`a78b802~1`のビルドで同操作（要素配置ツール選択→メニューバー画素採取）を再現確認。既存事象なら増分1前でも同色が出る。

---

## 所見B: ダークモードでアクティブペインタイトル文字消失 — 独立診断

### 結論

1. **静的機序は全経路で不発見**——スタイルの写し・テーマ辞書・辞書マージ構成・ecad2側辞書のどこにも「ダークのみアクティブ文字が消える」を説明する静的構造は無い（根拠1〜6）。
2. **重要事実: 両テーマ辞書とも`ToolWindowCaptionActiveText`=#FFFFFF（白）・`ToolWindowCaptionActiveBackground`=#007ACC（青）で同一値**（DarkBrushs.xaml:234-237／LightBrushs.xaml:231-234）。「ダーク辞書の色定義が悪い」のではない。
3. **増分0のPoCでは再現していない**（`docs/ecad2-t110-poc-verification-ninja.md`(e): ダークでも「4パネルのラベル表示は両テーマとも維持」と実測記録あり）——PoCと本実装は同型のスタイル+同じVs2013DarkThemeを使っており、**本実装固有の実行時要因**の線が濃い。
4. 静的構造の謎ではなく動的（実行時の値解決・描画）の謎であり、**実測が本質的に必要**な段階（`memory: feedback_static_vs_dynamic_investigation`の型）。切り分け実測案を後述。

### 根拠（すべて一次ソース実測）

1. **写しは本家と同一**: `UnifiedAnchorablePaneTitleStyle`のIsActiveトリガー（`MainWindow.xaml:535-539`）と本家VS2013テーマ既定スタイル（`Generic.xaml:716-720`）を1対1突合。キー（`ToolWindowCaptionActiveText`等）・構造とも一致。意図的差分（VS2013内部キーのStaticResource→DynamicResource化・ToolTip日本語化）は文字色経路に無関係。文字への伝搬経路（Header ContentPresenterの`TextElement.Foreground="{TemplateBinding Foreground}"`、418行）も本家597行と同一。
2. **キーは両辞書に実在し同値**（結論2のとおり）。`ResourceKeys.cs:100/103`でComponentResourceKey定義も確認。
3. **非アクティブ（ダーク）が正常である事実**が、Style Setter→TemplateBinding→TextBlockの経路自体はダークでも機能していることを示す（InactiveText=#D0D0D0が正しく出ている）。同一トリガー内でBackground（青、効いている）とForeground（消える）が分かれる静的機序は原理的に存在しない——ゆえに「トリガー不発」でも「辞書不良」でもない何かが実行時に起きている。
4. **ecad2側辞書は潔白**: Theme.Dark.xamlはブラシ定義60行のみ。`ToolWindowCaption`系の上書き・TextBlock暗黙スタイルとも無し（App.xaml含めgrep確認）。
5. **辞書マージ構成に後勝ち上書き無し**: DarkTheme.xaml→[DarkBrushs.xaml, Generic.xaml]の順でマージ（後着Generic.xamlが優先解決される構成だが）、Generic.xamlとその内包3辞書（OverlayButtons/MenuItem.xaml/IconGeometry）に`ToolWindowCaption`再定義ゼロ件。
6. 既定タイトルテンプレート（`AvalonDockThemeVs2013AnchorableTitleTemplate`、Generic.xaml:1280-1285）のTextBlockは色指定なし＝`TextElement.Foreground`継承頼み（本家設計どおり、写し側も同じ）。

### 切り分け実測案（侍の並行診断と突合用）

1. **ライト+アクティブの文字色を画素採取**——ここが最大の分岐点:
   - **#FFFFFF（白）なら**: トリガー・辞書は正常でダーク時のみ実行時に何かが壊れる（テーマ切替タイミング・DynamicResource再解決の問題等）。
   - **#444444（濃灰=LightのInactiveText）なら**: 実は**両テーマともIsActiveトリガーのForegroundが効いておらず**、ライトは「濃灰 on 青」で偶然読めていただけ（ダークはInactiveText=#D0D0D0…の解決にも別の何かが乗る、等）。「ライトは正常」の観測が目視なら要再確認（`memory: feedback_screenshot_visual_misjudgment_thin_lines`の型）。
2. ダーク+アクティブ帯の文字位置ピクセル採取: 「背景と同色で塗られている」のか「文字が描画されていない」のかの判別。
3. 診断ログ（侍領分、`memory: feedback_diagnostic_log_escalation`）: アクティブ化時に`AnchorablePaneTitle`の`Foreground`実値と`DependencyPropertyHelper.GetValueSource`、およびHeader内TextBlockの実効Foregroundをダンプ。ライト/ダークで比較すれば値解決層が一撃で確定する。
4. 参考: ダーク切替の操作経路（起動時からダークか、起動後にトグルしたか）で差が出るかも記録されたい（テーマ差し替えは`ApplyDockingManagerThemes`+`ApplyUiChromeTheme`の2系統があり、DynamicResource再解決のタイミング依存を疑う場合の材料になる）。

---

## 出典

- `src/Ecad2.App/MainWindow.xaml`（382-620行 UnifiedAnchorablePaneTitleStyle・659-676行 ラッパー/Menu・1490行 ElementPlacementBar）
- `src/Ecad2.App/MainWindow.xaml.cs`（762-789行 ApplyDockingManagerThemes/ApplyUiChromeTheme・3425-3441行 PositionPlacementBar）
- `src/Ecad2.App/App.xaml`（701-810行 自作Separator/TopLevelHeaderテンプレート）・`Themes/Theme.Light.xaml`・`Theme.Dark.xaml`（全域）
- git実測: `git show a78b802~1:src/Ecad2.App/MainWindow.xaml`・`git diff a78b802~1..HEAD`
- AvalonDock v4.74.1一次ソース（2026-07-22 curl取得・scratchpad保存）: `AvalonDock.Themes.VS2013/{DarkBrushs,LightBrushs,DarkTheme,LightTheme,OverlayButtons}.xaml`・`Themes/{Generic.xaml,ResourceKeys.cs,Menu/MenuItem.xaml}`
- dotnet/wpf main一次ソース（同上）: `src/Microsoft.DotNet.Wpf/src/Themes/XAML/{Menu,MenuItem,ToolBar}.xaml`
- `docs/ecad2-t110-poc-verification-ninja.md`（(e)両テーマ実測）・`docs/ecad2-t107-*` `t108-*` 検証記録4件（無効状態メニュー言及ゼロの確認）
