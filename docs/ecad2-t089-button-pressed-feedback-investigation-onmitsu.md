# T-089実機確認「押下フィードバックが変化ゼロ」の一次ソース調査（隠密、家老並行独立調査）

調査日: 2026-07-18　調査者: 隠密　依頼元: 家老（殿実機確認でBackground=#33000000オーバーレイが「変化を感じない」と判明、
侍修正と並行の独立一次ソース調査）

## 依頼内容(DoD)

(1) WPF既定Button(Aero2テーマ)のControlTemplateで使われる`ButtonChrome`要素が、Style経由のBackground
設定をIsPressed時の実描画に反映する構造か、それとも別ロジックが視覚を決定しBackground設定を素通り
させる構造か
(2) MainWindow.xamlの3スタイル(ToolBarButtonStyle等)のBasedOnチェーンが静的に正しく解決されるか

## 結論(先出し)

**(1) 素通り確定。増分7(SubMenuBorder)・T-099(SelectedContent)に続く3例目の「値は正しいが実描画に
反映されない」構造的罠と断定できる。** ただし`ButtonChrome`という名前の要素は現行(.NET Core系)WPFの
Aero2テーマには存在しない（家老の想定は.NET Framework時代のClassic/Luna/Royaleテーマの名残と見られる、
現行は単純な`Border`）。機序はButtonChromeの独自ロジックではなく、**WPFの依存関係プロパティ値優先
順位そのもの**——既定ControlTemplateが持つ`ControlTemplate.Triggers`（`TargetName="border"`の直接
Setter）が、ecad2側`Style.Triggers`のSetterより優先順位が高いため、後者の値はBorder要素の実描画に
一切到達しない。Microsoft公式ドキュメントが**ほぼ同一の症状を専用サンプルで警告**している。

**(2) BasedOnチェーンの静的解決自体は正しい。** 3スタイルとも構文上の問題なし、実描画に反映されない
原因は(1)側にある。

---

## (1) 技術的根拠：一次ソース

### 1.1 Aero2既定Button ControlTemplateの構造

出典: [dotnet/wpf](https://github.com/dotnet/wpf) `main`ブランチ `src/Microsoft.DotNet.Wpf/src/Themes/XAML/Button.xaml`（2026-07-18取得、rawファイル直接取得・全文確認）。

Aero2.NormalColorセクションの`BaseButtonStyle`（`TargetType="{x:Type ButtonBase}"`、Button/ToggleButton
共通の基底テンプレート）：

```xml
<ControlTemplate TargetType="{x:Type ButtonBase}">
    <Border x:Name="border"
        BorderThickness="{TemplateBinding BorderThickness}"
        Background="{TemplateBinding Background}"
        BorderBrush="{TemplateBinding BorderBrush}"
        SnapsToDevicePixels="true">
        <ContentPresenter x:Name="contentPresenter" ... />
    </Border>
    <ControlTemplate.Triggers>
        <Trigger Property="Button.IsDefaulted" Value="true">
            <Setter Property="BorderBrush" Value="{DynamicResource {x:Static SystemColors.HighlightBrushKey}}" TargetName="border" />
        </Trigger>
        <Trigger Property="IsMouseOver" Value="true">
            <Setter Property="Background" Value="{StaticResource Button.MouseOver.Background}" TargetName="border" />
            <Setter Property="BorderBrush" Value="{StaticResource Button.MouseOver.Border}" TargetName="border" />
        </Trigger>
        <Trigger Property="IsPressed" Value="true">
            <Setter Property="Background" Value="{StaticResource Button.Pressed.Background}" TargetName="border" />
            <Setter Property="BorderBrush" Value="{StaticResource Button.Pressed.Border}" TargetName="border" />
        </Trigger>
        <Trigger Property="ToggleButton.IsChecked" Value="true">
            <Setter Property="Background" Value="{StaticResource Button.Checked.Background}" TargetName="border" />
            <Setter Property="BorderBrush" Value="{StaticResource Button.Checked.Border}" TargetName="border" />
        </Trigger>
        <Trigger Property="IsEnabled" Value="false">
            <Setter Property="Background" Value="{StaticResource Button.Disabled.Background}" TargetName="border" />
            ...
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>
```

要点：
- `ButtonChrome`は存在しない。単純な`Border x:Name="border"`のみ、背景を持つ他の要素もなし。
- `border.Background`の**既定値**は`{TemplateBinding Background}`（Button自身のBackgroundプロパティを反映）。
- しかし`ControlTemplate.Triggers`内に、`IsMouseOver`/`IsPressed`/`ToggleButton.IsChecked`/`IsEnabled`の
  それぞれについて、**`TargetName="border"`を明示した直接Setter**が別途存在し、border要素のBackgroundを
  直接上書きする。`ToggleButton.IsChecked`トリガーがButton用の共通テンプレートに既に含まれている点から、
  **このテンプレートはButton/ToggleButton両方の暗黙的StyleでTemplateとして共用されている**とみられる
  （ToggleButton専用のXAMLファイルはdotnet/wpfに存在せず、Web調査でも「ToggleButton/RepeatButtonの
  暗黙的Style定義は全テーマ共通セクションに一括定義」との情報のみ得られ、BasedOn先の完全確証には
  至らなかった＝下記「不明点」参照)。

### 1.2 実際の色値（視覚的にほぼ同一という副次要因）

| リソース | 色コード | 色味 |
|---|---|---|
| Button.MouseOver.Background | `#FFBEE6FD` | 薄い水色 |
| Button.Pressed.Background | `#FFC4E5F6` | 薄い水色（MouseOverとほぼ同系統） |
| Button.Checked.Background | `#FFBCDDEE` | 薄い水色（同上） |

**通常のクリック操作（マウスを乗せてから押す）では、`IsMouseOver=true`が先行して既に薄い水色になっており、
そこから`IsPressed=true`に遷移してもほぼ同系統の薄い水色にしか変わらない**。仮に(1)の優先順位問題が
存在しなくても、素のAero2既定フィードバック自体が「クリック時に体感できるほどの変化」を生まない配色で
ある点が、症状を後押ししている可能性が高い（推測、実機での色差の主観評価は忍者領分）。

### 1.3 WPF依存関係プロパティ値優先順位（公式仕様、根本原因の確定根拠）

出典: [Dependency property value precedence - WPF | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/dependency-property-value-precedence)（2026-07-18取得）

公式の優先順位リスト（抜粋、高→低）：

1. Property system coercion
2. Active animations
3. **Local values**
4. **TemplatedParent template property values** — 「テンプレート内では: (1) Triggers (2) Property sets」の順
5. Implicit styles（Styleプロパティ自体にのみ適用）
6. **Style triggers**（page/applicationに存在する明示的・暗黙的スタイル内のtrigger。**"Triggers in
   default styles have a lower precedence"**＝既定テーマスタイル内のtriggerはこの6位ではなく9位へ格下げ）
7. Template triggers
8. Style setter values
9. **Default styles (theme styles)** — 「(1) Active triggers (2) Setters」の順

**このリストが直接示す構造**：border要素（TemplatedParent=Buttonが適用するControlTemplate）にとって、
`ControlTemplate.Triggers`内のIsPressedトリガー（`TargetName="border"`）は**項目4「TemplatedParent
template property values」の1番目=Triggers**に該当し、優先度4位。一方、border要素の
`Background="{TemplateBinding Background}"`という既定バインディング自体は同じ項目4の**2番目=Property
sets**（優先度は4位の中で下位）。ecad2側の`Style.Triggers`（`IsPressed=True→Background=#33000000`、
Button自身に適用）は**項目6「Style triggers」**に該当し、優先度6位。

結論：**border要素の実描画Backgroundは「項目4の1番目(ControlTemplate.Triggers)」が「項目4の2番目
(TemplateBinding経由でButton.Backgroundの値=#33000000を反映するはずだった経路)」を上書きする形で
確定する。ecad2のStyle.Trigger自体はButtonの`Background`プロパティには正しく反映される（コードで
`button.Background`を読めば`#33000000`が返る）が、その値がborder要素の実描画へ到達する経路自体が、
より高優先度のControlTemplate.Triggersによって完全にブロックされる。**

### 1.4 公式ドキュメントが提示する「ものずばり」の警告例

同ページの冒頭サンプル（"Dependency properties set in multiple places"節）は、今回の状況とほぼ同型の
構成（`Style.Triggers`でIsMouseOver時にBackgroundを変えようとする例）を挙げ、次のように明記している：

> "The example replaces the button's default ControlTemplate **because the default template has a
> hard-coded mouseover Background value**."

＝「既定ControlTemplateはハードコードされたmouseover Background値を持つため、それを回避するには
テンプレート自体を置き換える必要がある」と、公式ドキュメントが直接、今回と同じ落とし穴を警告して
いる。これは増分7・T-099と同じく「調べれば一次ソースに明記されている構造的罠」の3例目。

---

## (2) BasedOnチェーンの静的解決確認

`src/Ecad2.App/MainWindow.xaml`をGrepし、`BasedOn`を含む全12件のうち、暗黙的Button/ToggleButtonを
参照する3件を確認：

| スタイル | TargetType | BasedOn | 解決先 |
|---|---|---|---|
| `ToolBarButtonStyle` | Button | `{StaticResource {x:Type Button}}` | App.xaml Application.Resourcesの暗黙的Buttonスタイル |
| `TestModeToolBarButtonStyle` | ToggleButton | `{StaticResource {x:Type ToggleButton}}` | App.xaml Application.Resourcesの暗黙的ToggleButtonスタイル |
| `PlacementToolBarButtonStyle` | Button | `{StaticResource ToolBarButtonStyle}` | ToolBarButtonStyle経由で同上 |

MainWindow.xaml内に`{x:Type Button}`/`{x:Type ToggleButton}`という暗黙的スタイル（x:Keyなし）が別途
存在しないことも確認済み（Grep、12件のBasedOnはすべて名前付きキー参照のみ）。**StaticResourceの
検索順序（要素→Window.Resources→Application.Resources→テーマ）に従い、MainWindow.Resources内に
遮蔽するキーが存在しない以上、3スタイルとも構文上正しくApp.xamlの新設暗黙的スタイルへ解決される**。
BasedOnチェーン自体に不整合はない。

## 追加所見：`TestModeToolBarButtonStyle`のMultiTriggerも同型の罠に該当する可能性

`MainWindow.xaml`差分で追加された複合状態対応（`IsPressed=True && IsChecked=True`→
`Background=#CC5A2E00`のMultiTrigger）も`Style.Triggers`内にあるため、同じく優先順位6位止まり。
既定ControlTemplateの`ToggleButton.IsChecked`単体トリガー（4位）に上書きされる可能性が高く、
テストモードON中の押下フィードバック（本来このMultiTriggerが担うはずの複合表現）も同様に実描画へ
反映されない懸念がある（**推測、ToggleButton側のControlTemplate完全一致は未確証、下記不明点参照**）。

---

## 対処案（推測、判断は侍・家老に委ねる）

公式ドキュメントの結論と同じく、**Style.Triggersでの上書きでは原理的に対処不能**。増分7が
`SubMenuBorder`に対して行ったのと同型のアプローチ（ControlTemplateをコピーし、`TargetName="border"`
の各Setter値をecad2独自のもの、またはDynamicResource化した色に差し替えた派生テンプレートを新設）が
必要になる可能性が高い。既存のButton/ToggleButton双方（ContentPresenter・Trigger群を含む）を
コピーする規模になるため、T-089着手前調査書が触れていなかった追加コストとして家老・侍へ申し送る。

## 不明点

- ToggleButton専用のControlTemplateがButton用`BaseButtonStyle`と完全に同一リソースを共有しているか
  （`ToggleButton.IsChecked`トリガーがButton用テンプレートに存在する事実から強く示唆されるが、
  ToggleButtonの暗黙的Style定義自体の`BasedOn`/`Template`セッターをdotnet/wpfソースから直接確認する
  には至らなかった。Web検索・WebFetch双方で該当ファイルの特定に至らず）。侍が実機で
  `DependencyPropertyHelper.GetValueSource`を使えば最終確認できる（増分7の侍実測と同手法）。
- 実際の色差（#FFBEE6FD vs #FFC4E5F6）が人間の目にどの程度知覚できるかは実機の話であり、静的調査の
  範囲外（忍者領分）。
- Application.Resourcesに`Template`セッターを持たない暗黙的Style（今回のT-089新設スタイル）を新設した
  場合、Buttonの`Template`プロパティ自体がどこから供給されるかは、公式ドキュメントの優先順位リスト
  （項目9「Default styles」がStyleそのものの適用元にもなりうる旨の記載）から「テーマの既定スタイルが
  Templateを供給し続ける」と考えられ、実際にecad2のボタンが今回の変更後も通常表示を保っている事実とも
  整合するが、断定するには実機のVisualTree確認（忍者領分）が望ましい。

## 派生提案の有無

範囲外の新規提案なし。

---

## 出典

- [dotnet/wpf `main`ブランチ](https://github.com/dotnet/wpf) `src/Microsoft.DotNet.Wpf/src/Themes/XAML/Button.xaml`（2026-07-18、rawファイル直接取得・全文確認、Aero2.NormalColorセクション`BaseButtonStyle`）
- [Dependency property value precedence - WPF \| Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/dependency-property-value-precedence)（2026-07-18取得、優先順位リスト全文・冒頭サンプル引用）
- [ToggleButton control - WPF \| Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/togglebutton-styles-and-templates)（参照のみ、具体的ControlTemplateソースは非掲載）
- アプリ側コード（2026-07-18実測、作業ツリー未コミット差分）：`src/Ecad2.App/App.xaml`70-96行目付近（T-089新設Button/ToggleButton暗黙的スタイル）、`src/Ecad2.App/MainWindow.xaml`27-95行目付近（3スタイルのBasedOn化・TestModeToolBarButtonStyleのMultiTrigger）
- 既存調査書（同型パターンの先例、手法の参考）：`docs/ecad2-t083-zoubun7-menu-dark-redesign-survey-onmitsu.md`（SubMenuBorderのStaticResource固定）、`docs/ecad2-t099-tanaoroshi-shinsou-chousa-onmitsu2.md`（SelectedContentのコンテナ未生成）
