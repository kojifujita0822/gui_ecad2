# T-083増分7再挑戦: ドロップダウンメニュー背景ダーク対応の方針再調査(隠密)

調査日: 2026-07-17　調査者: 隠密（key=1784276632853）　依頼元: 家老（T-083増分7、殿指示2026-07-16による再挑戦）

## 依頼内容(DoD)

1. MenuItem用ControlTemplate完全自作(Role別=TopLevelHeader/SubmenuItem等)の要否・規模見積
2. 代替アプローチの有無(Web一次情報含む比較)
3. 推奨案の提示

## 結論(先出し)

1. **ControlTemplate自作は必要**。ただし「完全自作」ではなく、**WPF既定(Aero2テーマ)のControlTemplateをコピーし、色参照のみDynamicResourceへ置換する「派生テンプレート」**で足りる。対象は実質2種（`TopLevelHeaderTemplateKey`・`SubmenuItemTemplateKey`）——現行メニュー構造（`MainWindow.xaml:142-186`）が2階層のみ（サブサブメニュー無し）のため、`TopLevelItemTemplateKey`・`SubmenuHeaderTemplateKey`は現状未使用。
2. **根本原因を特定**：ドロップダウン全体を包む背景要素`SubMenuBorder`（`TopLevelHeaderTemplateKey`内、Popup内）は`Background="{StaticResource Menu.Static.Background}"`（WPF Aero2テーマDLL内蔵の**StaticResource**）を参照しており、これはXAMLロード時に一度だけ解決されテーマDLLの固定値になる。**アプリ側でSystemColors.MenuBrushKey等をいくらDynamicResourceオーバーライドしても原理的に届かない**（StaticResourceは動的リソース差し替えの対象外）。侍の実測（`DependencyPropertyHelper.GetValueSource`）が指した「TemplateBinding経由で親MenuItem自身のBackgroundを参照する」要素は、別の役割を持つ`SubmenuItemTemplateKey`内の`templateRoot`（各サブメニュー項目自体の背景）だったとみられる——これは個別MenuItemのBackgroundプロパティを設定すれば正しく反映される（構造は健全）。両者を合わせて見ると、「ドロップダウンの外枠だけがStaticResourceで固定され動かせない」というのが構造的制約の正体であり、増分1層B（AvalonDock）とは異なり**今回は検証ミスではなく真の構造的制約**と判断する。
3. **代替アプローチ（サードパーティテーマライブラリ、Fluent ThemeMode、OS API）はいずれも非推奨**。ControlTemplate部分自作が唯一の現実的解。
4. **推奨案**：既定Aero2の`TopLevelHeaderTemplateKey`・`SubmenuItemTemplateKey`をコピーし、`SubMenuBorder`等の色参照をDynamicResource化した派生テンプレートを新設。既存の未使用キー`MenuBarBackgroundBrush`/`MenuBarForegroundBrush`（Theme.Light/Dark.xamlに定義済みだが増分7でMenu要素から参照を外され現在デッドコード）を活用しつつ、新規ブラシキー約7種を追加。規模は中程度（XAML約250〜350行、侍1セッション+忍者1〜2周検証が目安）。

---

## 1. 技術的根拠：WPF既定(Aero2)MenuItemテンプレートの一次ソース解析

出典: [dotnet/wpf](https://github.com/dotnet/wpf) `main`ブランチ、`src/Microsoft.DotNet.Wpf/src/Themes/XAML/MenuItem.xaml`・`Menu.xaml`（2026-07-17取得、rawファイル直接取得・全文確認）。

### 1.1 使用テーマの特定

同ファイルは`<!-- [[Aero.NormalColor, Aero2.NormalColor, ...]] -->`のようなコメントでテーマ別セクションに分かれている。ecad2の実行環境（memory記録: Windows 11 26200.0、ハイコントラスト無効）ではWPF既定テーマは**Aero2.NormalColor**が選択される（.NET Core/.NET系WPFがWindows 7以降を検出した場合の標準選択、Classic/Luna/RoyaleはXP以前・明示選択時のみ）。よって`Aero2.NormalColor`セクションを対象に解析した。

### 1.2 `SubMenuBorder`(ドロップダウン全体の背景) — StaticResourceで固定

`TopLevelHeaderTemplateKey`（`MenuItem.xaml` 2186〜2260行付近、Aero2セクション）:

```xml
<Popup x:Name="PART_Popup" ...>
    <Border x:Name="SubMenuBorder"
        Background="{StaticResource Menu.Static.Background}"
        BorderBrush="{StaticResource Menu.Static.Border}"
        BorderThickness="1" Padding="2">
        ...
    </Border>
</Popup>
```

`Menu.Static.Background`は`Menu.xaml`のAero2セクションで`<SolidColorBrush x:Key="Menu.Static.Background" Color="#FFF0F0F0" />`と定義された**StaticResource**（テーマDLL内蔵、Light固定値）。StaticResourceはXAML解析時に一度だけ解決される性質上、実行時に`Application.Resources.MergedDictionaries`を差し替えても再解決されない。これがT-083増分2・増分7で「`SystemColors.MenuBrushKey`をオーバーライドしても効果なし」と観測された現象の技術的根拠——過去の増分1層Bのような検証手法のミス（座標取り違え等）ではなく、**WPFのリソース解決方式そのものに起因する原理的な制約**である。

### 1.3 `SubmenuItemTemplateKey`(各サブメニュー項目自体) — TemplateBindingで可変

`SubmenuItemTemplateKey`（`MenuItem.xaml` 2281〜2363行、Aero2セクション。ecad2の「新規」「開く」等、サブサブメニューを持たない項目に適用されるRole）:

```xml
<Border x:Name="templateRoot"
    BorderThickness="{TemplateBinding BorderThickness}"
    Background="{TemplateBinding Background}"
    BorderBrush="{TemplateBinding BorderBrush}">
```

こちらは`{TemplateBinding Background}`——各`MenuItem`インスタンス自身の`Background`依存関係プロパティを参照する。個別MenuItemの`Background`を設定すれば正しく反映される。ただし`Background`はWPFで**継承されない依存関係プロパティ**のため、親`Menu`の`Background`を設定しても子`MenuItem`群へは伝播しない（`Foreground`のみ継承される。これが増分7で「Foregroundは効くがBackgroundは効かない」という非対称の一因でもある）。

侍実測「`SubMenuBorder`がTemplateBinding経由で親MenuItem自身のBackgroundを参照する」は、名称としては`SubMenuBorder`ではなく機能的にはこの`SubmenuItemTemplateKey`側の`templateRoot`を指しているとみられる（両者の役割の違いを踏まえると解釈上の食い違いであり、実測データ自体と矛盾しない）。

### 1.4 `Menu`自体(トップバー本体)の背景 — ローカル値で上書き可能なはず

`Menu.xaml`のAero2セクション:

```xml
<Style TargetType="{x:Type Menu}">
    <Setter Property="Background" Value="{StaticResource Menu.Static.Background}" />
    ...
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="{x:Type Menu}">
                <Border Background="{TemplateBinding Background}" ...>
                    <ItemsPresenter .../>
                </Border>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

Style既定値は`StaticResource Menu.Static.Background`だが、ControlTemplate自体は`{TemplateBinding Background}`を使っている。WPFの依存関係プロパティ値決定優先順位は「ローカル値 > テンプレートトリガー > スタイルセッター」であるため、XAML側で`<Menu Background="{DynamicResource MenuBarBackgroundBrush}">`と明示すればローカル値としてStyle既定値より優先され、理論上は機能するはずである。**この点は増分1層Bと同型の「値は正しいが描画に反映されない」という侍の報告があったが、当時は隠密2の調査で座標取り違えの検証ミスと判明した先例がある**（`docs/ecad2-t083-zoubun1-layerb-shinsou-chousa-onmitsu2.md`）。メニューバー本体（トップバー自体）に限っては、SubMenuBorderと異なりStaticResourceの直接的な壁は無いため、再検証の価値がある（下記4節）。

---

## 2. 現状コードの実態確認

- `MainWindow.xaml:142` `<Menu x:Name="MenuBarArea" Grid.Row="0">` — Background/Foreground属性なし（増分7で削除、コミット`ec0707a`）。
- `MainWindow.xaml:143-186` メニュー構造は2階層のみ（トップレベル6項目「ファイル/編集/図面/表示/ツール/ヘルプ」→各直下に単純項目、サブサブメニューなし）。`IsCheckable="True"`の項目3件（グリッド表示・ダークモード・テストモード）が増分7の実害（白地白文字）の直接の発生源だった。
- `Theme.Light.xaml:16-17`・`Theme.Dark.xaml:16-17` — `MenuBarBackgroundBrush`/`MenuBarForegroundBrush`キー自体は**増分2で定義されたまま残存**（Light=`#F0F0F0`/`#000000`、Dark=`#FF2D2D30`/`#FFF0F0F0`）。ただしMenu要素から参照が外れているため現在は**デッドコード**（どこからも参照されていない）。
- `SystemColors.MenuBarBrushKey`/`MenuBrushKey`/`MenuTextBrushKey`のオーバーライドは増分7で完全削除済み（`Theme.Light/Dark.xaml`）。

---

## 3. DoD(1): ControlTemplate自作の要否・規模

### 要否

**必要**。理由は1.2節の通り、`SubMenuBorder`のStaticResource参照はDynamicResourceオーバーライドで解決不可能なため、ControlTemplate自体の書き換え以外に手段がない。

### 規模見積もり

| 対応 | 内容 | 規模 |
|---|---|---|
| 必須 | `TopLevelHeaderTemplateKey`派生（`SubMenuBorder`等をDynamicResource化） | ControlTemplate 1個・約80〜100行 |
| 必須 | `SubmenuItemTemplateKey`派生（`templateRoot`等をDynamicResource化） | ControlTemplate 1個・約60〜80行 |
| 任意（将来の拡張耐性） | `TopLevelItemTemplateKey`・`SubmenuHeaderTemplateKey`派生 | 現状未使用。省略した場合、将来ネストメニュー等でこのRoleが使われるとLight固定のまま取り残される |
| 新規ブラシキー | `MenuPopupBackgroundBrush`/`BorderBrush`/`ForegroundBrush`/`SeparatorBrush`、`MenuItemHighlightBackgroundBrush`/`BorderBrush`、`MenuItemHighlightDisabledBackgroundBrush`/`BorderBrush`、`MenuDisabledForegroundBrush`等 | Theme.Light/Dark.xamlへ計7〜8種追加（既存`MenuBarBackgroundBrush`等は再利用） |
| Style結線 | `MenuItem`暗黙的Style（`Role`トリガーで上記テンプレート切替）、`MenuItem.Background`既定値をDynamicResource化 | 小〜中 |

**工数感**：侍1セッション（XAML約250〜350行）+ 忍者1〜2周検証。Trigger（`IsHighlighted`/`IsEnabled`/`IsChecked`/ホバー+無効化の複合）を完全に踏襲しないと見た目が劣化する回帰リスクがあり、往復2周超の可能性は中程度（増分3のTextBox白浮き対応と同程度の複雑度と見積もる）。

---

## 4. DoD(2): 代替アプローチの比較(Web一次情報含む)

| アプローチ | 評価 | 理由 |
|---|---|---|
| **ControlTemplate部分自作**（推奨） | 適 | 新規外部依存なし、既存の見た目・挙動を最大限保持しつつ色のみ差し替え可能。WPF標準機構の範囲内で技術的に確実 |
| サードパーティWPFテーマライブラリ（MahApps.Metro/ModernWpf/MaterialDesignInXaml等） | 非推奨 | 影響範囲が全UIコントロールに及び過大、既存の`Dirkster.AvalonDock.Themes.VS2013`との統一感が崩れるリスク、新規外部依存追加（品質哲学「不要な外部依存を追加しない」に反する懸念） |
| Fluent ThemeMode（.NET標準） | 不採用（既存結論維持） | `docs/ecad2-t083-honjissou-3layer-design-survey-onmitsu.md`で2026-07-16時点「`[Experimental(WPF0001)]`のまま」と確認済み。本調査時点（2026-07-17）との間隔は1日のみで状況変化の可能性は低いと判断し再調査は省略した（**推測**、必要なら次回念のため再確認を推奨） |
| OS `DwmSetWindowAttribute`(`DWMWA_USE_IMMERSIVE_DARK_MODE`)等 | 対象外 | WPFの`Menu`/`MenuItem`は完全に自前描画（GDI/DirectXベースの独自レンダリング）であり、OSネイティブメニュー描画ではないためDWM APIの影響を受けない。タイトルバーのダーク化にのみ有効（増分1層B調査書の参考記載と同じ整理） |
| Microsoft Learn公式ドキュメント | 参考情報のみ | [Menu - WPF \| Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/menu-styles-and-templates)を確認したが、「既定テンプレートを変更してカスタム外観にできる」という一般論のみで、`SubMenuBorder`個別の色指定手法への言及はなし。ControlTemplateコピー&カスタマイズという手法自体は同ページが示す標準的なアプローチと合致する |

---

## 5. DoD(3): 推奨案

**実装順序を含めた推奨手順**（侍実装時の参考、最終的な設計判断は侍・家老に委ねる）：

1. **`SubMenuBorder`／`SubmenuItemTemplateKey`のControlTemplate自作を先に完了させる**（3節の必須2種）。新規ブラシキーはTheme.Light/Dark.xamlへ追加し、`MenuItem`暗黙的Styleの`Background`既定値もDynamicResource化する（個別`<MenuItem>`要素への属性追加は不要、Style Setterで一括反映できる——20箇所超のMenuItem要素を編集せずに済む）。
2. **①完了後に、メニューバー本体（`Menu`要素自体）の`Background`/`Foreground`を`MenuBarBackgroundBrush`/`MenuBarForegroundBrush`（既存の未使用キー）で復活させる**。順序を逆にすると増分7と同じ「白地白文字」が再発するため必須の順序。
3. **忍者検証は画素採取・正しいUIA座標（y≧32、`ecad2-ui-automation`スキル追記済みの罠を踏まえる）を用いる**。確認観点：(a)トップバー本体の背景・文字色 (b)ドロップダウン背景 (c)各項目のホバー/選択/無効化/チェック時の見た目、の3系統×Light/Dark双方。
4. `TopLevelItemTemplateKey`・`SubmenuHeaderTemplateKey`（現状未使用の2Role）は、今回のスコープでは**省略可**と考える（**推測**、UI/UX上の要否判断ではなく実装範囲の技術的判断のため、着手前に家老の裁量確認を推奨）。将来的にサブサブメニュー（例：「最近使ったファイル」等）を追加する計画がある場合はこの限りでない。

---

## 不明点

- Fluent ThemeModeの最新状況（1日前の既存結論からの変化有無）は再調査していない（**推測**に基づき変化なしと判断、時間対効果を鑑み省略）。
- `TopLevelItemTemplateKey`・`SubmenuHeaderTemplateKey`を省略する設計判断が実装範囲として適切かは、実装着手前に家老・侍で最終確認することを推奨（本調査は技術的な要否分析に留め、実装範囲の最終決定はスコープ外）。
- .NET 10ランタイムに実際に同梱されるAero2テーマDLLのBAMLを直接逆コンパイルしての裏取りは行っていない（dotnet/wpf `main`ブランチのソースで代替、MenuItemテンプレート構造はWPFの歴史を通じて大きな変更がないという一般知識に基づく判断、**推測**）。侍が実装時に`DependencyPropertyHelper.GetValueSource`で実機実測すれば最終確認できる。

## 派生提案の有無

範囲外の新規作業提案なし。

---

## 出典

- [dotnet/wpf `main`ブランチ](https://github.com/dotnet/wpf) `src/Microsoft.DotNet.Wpf/src/Themes/XAML/MenuItem.xaml`・`Menu.xaml`（2026-07-17、rawファイル直接取得・全文確認、Aero2.NormalColorセクション該当箇所を精読）
- [Menu - WPF \| Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/menu-styles-and-templates)
- `docs/todo.md` T-083節（増分7打ち切り経緯、行131-140）
- `docs-notes/handover-next-session.md`（侍実測引き継ぎ内容）
- `docs/ecad2-t083-zoubun1-layerb-shinsou-chousa-onmitsu2.md`（増分1層Bの「検証ミスvs構造的制約」の先例、UIA座標取り違えの罠）
- `docs/ecad2-t083-honjissou-3layer-design-survey-onmitsu.md`（Fluent ThemeMode不採用の既存結論）
- アプリ側コード（2026-07-17実測）：`src/Ecad2.App/MainWindow.xaml`(142-186行)、`src/Ecad2.App/Themes/Theme.Light.xaml`・`Theme.Dark.xaml`全文、コミット`ec0707a`の差分（`git show ec0707a`）
