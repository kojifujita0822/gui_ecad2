# T-068増分1 重大バグ（2件目）「個別パーツ項目の子メニューが開かない」独立調査（隠密）

家老依頼：忍者再検証（`docs/ecad2-t068-increment1-verification-ninja-round2.md`）で、1件目のバグ
（自作パーツサブメニュー自体が開かない）解消後、個別パーツ項目（例「忍者テストパーツ01」）自体の
子メニュー（編集/削除）が5操作パターン全てで開かず、展開しようとすると親階層ごと閉じてしまうと
判明。侍への同時依頼（Wチェック方式）につき独立性を保ち侍側の結論は参照せず調査。加えて家老より
「モグラ叩き検知（同一機能領域での2件目の重大バグ、動的MenuItem構築という設計自体の是非）」の
俯瞰評価も依頼された。

殿より本件は往復ゲート免除（3周目以降も継続可）の事前許可あり。腰を据えて調査した。

## 結論：個別原因は確度「中」、モグラ叩き俯瞰評価は確度「高」

**個別原因**（完全な確定には至らず、有力仮説を提示）：動的生成された`sub`（各パーツ項目の
`MenuItem`）が`IsLoaded=false`の状態でユーザー操作を受け、`IsSubmenuOpenProperty`の
`CoerceValueCallback`により展開が強制的に無効化されている可能性が高い。ただし「親階層（自作パーツ
サブメニュー自体）まで閉じてしまう」現象の完全なメカニズムは、フォーカス喪失処理まで踏み込む必要
があり本調査では確定に至っていない（動的タイミングに依存する挙動のため、一次ソース精読の限界）。

**モグラ叩き俯瞰評価**：1件目・2件目とも「`SubmenuOpened`イベント発火時に動的にMenuItemツリーを
構築し、直後にユーザー操作を受け付ける」という同一設計アプローチに起因する構造的パターンと判断
する。3件目の類似バグが出る前に、設計アプローチ自体の見直しを推奨する。

## 一次ソース確認（dotnet/wpf）

### 1. `IsSubmenuOpenProperty`には`IsLoaded`依存のCoerceがある（`MenuItem.cs:519-580`）

```csharp
public static readonly DependencyProperty IsSubmenuOpenProperty =
    DependencyProperty.Register("IsSubmenuOpen", typeof(bool), typeof(MenuItem),
        new FrameworkPropertyMetadata(BooleanBoxes.FalseBox,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            new PropertyChangedCallback(OnIsSubmenuOpenChanged),
            new CoerceValueCallback(CoerceIsSubmenuOpen)));

private static object CoerceIsSubmenuOpen(DependencyObject d, object value)
{
    if ((bool) value)
    {
        MenuItem mi = (MenuItem) d;
        if (!mi.IsLoaded)
        {
            mi.RegisterToOpenOnLoad();
            return BooleanBoxes.FalseBox;   // ← IsLoaded=falseなら強制的にfalseへ戻す
        }
    }
    return value;
}

private void RegisterToOpenOnLoad() => Loaded += new RoutedEventHandler(OpenOnLoad);

private void OpenOnLoad(object sender, RoutedEventArgs e)
{
    // Open menu after it has rendered (Loaded is fired before 1st render)
    Dispatcher.BeginInvoke(DispatcherPriority.Input, new DispatcherOperationCallback(delegate(object param)
    {
        CoerceValue(IsSubmenuOpenProperty);   // Loaded後に再度Coerceを試みる
        return null;
    }), null);
}
```

**事実**：`sub`（対象MenuItem）が`IsLoaded=false`の状態で`IsSubmenuOpen=true`にしようとすると、
値は強制的に`false`へ戻され、代わりに`Loaded`イベント発火後に再試行する仕組みになっている。これは
通常のケース（静的にXAML宣言されたメニュー）では問題にならない設計だが、**実行時に動的生成された
直後のMenuItemを即座に操作しようとする場面では、意図せずこの経路に入りうる**。

### 2. 親解決（`ItemsControlFromItemContainer`）自体はレイアウトタイミングに依存しない（`ItemsControl.cs:1144-1166`）

```csharp
public static ItemsControl ItemsControlFromItemContainer(DependencyObject container)
{
    UIElement ui = container as UIElement;
    if (ui == null) return null;
    ItemsControl ic = LogicalTreeHelper.GetParent(ui) as ItemsControl;   // 論理ツリーのみ参照
    if (ic != null)
    {
        IGeneratorHost host = ic as IGeneratorHost;
        if (host.IsItemItsOwnContainer(ui)) return ic;
        else return null;
    }
    ...
}
```

`LogicalTreeHelper.GetParent`は論理ツリー（`Items.Add`実行と同期的に確定する）を参照するため、
`OpenMenu()`（`MenuItem.cs:2287-2308`）内の親解決ロジック自体は`Items.Add(sub)`直後でも正しく
`CustomPartsMenu`を返すはずと判断できる。**つまり1件目のバグ原因（HasItems判定）とは異なり、
`OpenMenu()`自体の親解決が失敗しているわけではなさそう**——問題は`OpenMenu()`成立後の
`CoerceIsSubmenuOpen`（IsLoaded依存）にある可能性が高いと考えられる。

## 有力仮説（確度：中）

`CustomPartsMenu_SubmenuOpened`イベントハンドラ内で、`CustomPartsMenu`が開かれた**その場で**
`sub`（`entry.Definition.Name`ごとの新規`MenuItem`インスタンス）を生成し即座に`Items.Add`している
（`MainWindow.xaml.cs`、1件目バグ修正後も変更なし）。生成直後の`sub`は、ユーザーが素早く操作した
場合まだ一度も`Loaded`イベントを発火していない可能性が高く、この状態で展開しようとすると
`CoerceIsSubmenuOpen`により`IsSubmenuOpen`が強制的に`false`へ戻される。忍者所見「UIAの
`SupportedPatterns`に`ExpandCollapsePattern`が含まれる（`HasItems`はtrueと判定されている）のに
展開後に閉じてしまう」という一見矛盾する現象と整合する。

## 未解明点（正直に明示）

「`sub`自体だけでなく、親階層（`CustomPartsMenu`）まで閉じてしまう」という忍者所見の核心部分は、
今回の調査で完全には解明できていない。`CoerceIsSubmenuOpen`は`sub.IsSubmenuOpen`を`false`へ戻す
だけで、理論上は親階層（`CustomPartsMenu`）の開閉には影響しないはずである。`ClickHeader()`冒頭の
`FocusOrSelect()`（`sub`がまだ完全にレンダリングされていない状態でのフォーカス設定が何らかの
副作用を招く可能性）や、WPFメニューシステムの「フォーカスが階層外に出たら全体を閉じる」仕組み
まで踏み込む必要があるが、これは実行時のタイミング・フォーカス遷移に依存する挙動であり、一次
ソースの静的読解のみでは確定が難しい（動的タイミングの謎は実測が本質的に必要という既存知見と
整合）。侍による診断ログ計装（`sub.Loaded`イベント発火タイミング・`FocusOrSelect()`呼び出し時の
`Keyboard.FocusedElement`実測等）での裏取りを推奨する。

## モグラ叩き検知の観点（家老依頼の俯瞰評価）

1件目（`CustomPartsMenu`自体：XAML上子要素0件でHasItems=false）と2件目（`sub`：動的生成直後の
IsLoaded/Coerceタイミング疑い）は、**技術的な発火メカニズムこそ異なるが、根っこは同一**——
「`SubmenuOpened`イベント発火時に、その場でMenuItemツリー（1件目は1段目、2件目は2段目の入れ子）を
動的構築し、構築直後に即座にユーザー操作を受け付ける」という設計アプローチが、WPFメニューシステム
の複数の暗黙的前提（`HasItems`確定・`IsLoaded`確定等、いずれも「静的にXAML宣言されたメニュー」を
前提として設計された仕組み）と、階層が深くなるほど繰り返し衝突している。

これは偶然の2つの独立したバグではなく、**同一アプローチに起因する構造的パターン**と判断する。
3件目（さらに深い階層、あるいは別の動的メニュー箇所）で同型の症状が再発するリスクが高いと考える。

**対処の方向性（参考、決定は家老・侍・殿に委ねる）**：
- (a) 個別対処の継続：`sub`にも1件目と同様のダミー子要素を静的パターンで持たせる、または生成後に
  明示的な`Loaded`待ち処理を挟む、といった対症療法。ただし3件目のリスクは残る
- (b) 設計変更：自作パーツの一覧・編集・削除を、動的入れ子メニューではなく、既存のダイアログ内
  リスト（例：`PartEditorDialog`を開く前に一覧選択ダイアログを挟む、または専用の「パーツ管理」
  ダイアログでリスト+編集/削除ボタンを持たせる）という、WPFの動的コンテンツ表示として実績のある
  形（`ListBox`+`ItemsSource`バインディング等）に切り替える。メニューの動的入れ子構造という
  ハイリスクな領域自体を避けられる

家老・殿の判断を仰ぎたい（UI/UX分岐点になりうるため、既定方針の変更を伴う場合は殿確認が必要な
論点と考える）。

## 出典

- `MenuItem.cs`（dotnet/wpf main、`IsSubmenuOpenProperty`/`CoerceIsSubmenuOpen`/`OpenMenu`/
  `ItemsControlFromItemContainer`呼び出し元）
- `ItemsControl.cs`（dotnet/wpf main、`ItemsControlFromItemContainer`静的メソッド本体）
- `docs/ecad2-t068-increment1-verification-ninja-round2.md`
- `src/Ecad2.App/MainWindow.xaml.cs`（`CustomPartsMenu_SubmenuOpened`、コミット`74eb164`時点から
  未変更の該当箇所）
