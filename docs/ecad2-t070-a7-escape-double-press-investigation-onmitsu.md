# T-070 A-7再発「Escape二度押し」独立調査(隠密、2026-07-14)

対象: 検索バー+部品配置バー同時表示状態でEscapeを1回押しても両方無反応、2回目で初めて両方閉じる
(忍者実機確認NG疑い)。Wチェック原則(karo.md)に基づき、侍とは独立に静的読解で根本原因を調査した。

## 結論

**根本原因を特定した**。`MainWindow.xaml.cs:1264-1270`(A-7往復2周目修正箇所)が
`e.Handled = true`を設定して`return`することで、WPF標準機構である配置バーの`IsCancel`ボタン
(`PlacementCancelButton`、`MainWindow.xaml:795`)のEscapeキー検出処理が**1回目のEscapeでは
実行されなくなる**。A-7往復2周目の修正自体は「配置バー優先で検索バーのEscapeが機能しない」問題を
正しく解消したが、その副作用として今度は「検索バー優先で配置バーのIsCancelキャンセル機構が
1回目は反応しない」という、新しい非対称な副作用を生んでいる。

## 該当コード

`MainWindow.xaml.cs:1257-1271`(`Window_PreviewKeyDown`、Tunnelingで最上位=Window自体に登録):

```csharp
if (_viewModel.IsPlacementBarVisible)
{
    // T-070隠密レビュー指摘A-7: ...
    if (e.Key == Key.Escape && _viewModel.Find.IsVisible)
    {
        _viewModel.Find.IsVisible = false;
        FocusCanvas();
        e.Handled = true;   // ← ここが今回の根本原因
    }
    return;
}
```

`MainWindow.xaml:795`(配置バーのキャンセルボタン、WPF標準の`IsCancel`機構でEscape押下時に
`Click`が発火する設計):

```xml
<Button Content="キャンセル" ... IsCancel="True" Click="PlacementCancelButton_Click" .../>
```

## 根本原因の技術的裏付け(WPF内部機構)

WPFの`IsCancel="True"`ボタンは、`Button`クラスのコンストラクタ相当処理で
`AccessKeyManager.Register("\x001B", button)`(`\x001B`=Escapeキーの文字コード)を呼び、
Escapeキーを疑似アクセスキーとして登録する仕組みになっている(出典1)。

`AccessKeyManager`は`InputManager.Current.PostProcessInput`イベント(Tunneling/Bubbling
双方の通常ルーティングが完了した**後**に発火する後処理フック)で`Keyboard.KeyDownEvent`を
監視しており、`PostProcessInput`内で最初に以下のチェックを行う(出典2、GitHub本体確認済み):

```csharp
if (e.StagingItem.Input.Handled) return;
```

つまり、**そのキー入力(RoutedEventArgs)が最終的にHandled=trueであれば、
AccessKeyManagerのEscape検出処理自体が丸ごとスキップされる**。これはTunneling/Bubbling
どちらのフェーズで`Handled=true`にされたかを問わない(PostProcessInputは全ルーティング完了後の
最終状態を見る)。

`Window_PreviewKeyDown`はXAML上`Window`自体に`PreviewKeyDown="Window_PreviewKeyDown"`
(`MainWindow.xaml:11`)としてTunnelingの最上位に登録されており、Tunnelingは常にWindowから
子孫要素へ伝播するため、本ハンドラは必ず最初に発火する。ここで1264-1269行が
`e.Handled = true`にすると、以降の同一キー入力に対するルーティング(子要素のPreviewKeyDown/
KeyDown等)は一切発火しなくなり(出典3)、`PostProcessInput`時点でも`Handled=true`のままなので
`AccessKeyManager`のEscape検出(=`PlacementCancelButton`の`Click`発火)も実行されない。

## 症状との整合性

1. **1回目のEscape**: `IsPlacementBarVisible=true`かつ`Find.IsVisible=true`のため1264の
   条件を満たし、`Find.IsVisible = false`(検索バーは閉じる)、`e.Handled = true`。
   → `AccessKeyManager`のEscape検出がスキップされ、配置バーの`IsCancel`機構
   (`PlacementCancelButton_Click`)は**発火しない**。配置バーは開いたまま残る。
2. **2回目のEscape**: `Find.IsVisible`は既にfalseのため1264の条件を満たさず、1270行の
   `return`のみ実行される(`e.Handled`は`false`のまま)。→ `PostProcessInput`到達時点で
   `Handled=false`のため`AccessKeyManager`のEscape検出が実行され、`PlacementCancelButton_Click`
   が発火して配置バーが閉じる。

「1回目で両方無反応に見えた」という忍者の観察は、実際には検索バーは1回目で閉じているが
配置バーが画面上に残り続けるため、複合UIとして見た目の変化が乏しく感じられた可能性が高いと
推測する(検索バー単体の消失が配置バーの陰で目立ちにくい、または2回連続で押した後にまとめて
画面確認した、等)。ここは推測であり断定はしない。いずれにせよ「2回目で初めて両方閉じる」
という中核症状は、上記機構で過不足なく説明できる。

## 対処に向けた参考(断定はしない、侍の実装裁量)

1264-1269ブロックで検索バーを閉じる際に、配置バー自身も同時に明示的に閉じる処理
(`PlacementCancelButton_Click`相当のロジック呼び出し、または両方をこのブロック内で処理し
`IsPlacementBarVisible`も併せて`false`にする)を追加すれば、1回のEscapeで両方閉じる対称的な
挙動になると考えられる。`e.Handled`をfalseのままにする対処は、`AccessKeyManager`が
`PlacementCancelButton_Click`を発火させる一方で検索バー側の処理(1266-1268)と二重処理に
ならないか等の副作用検討が要るため、明示呼び出し案の方が安全と見る。実装方針の最終判断は
侍・家老に委ねる。

## 出典

- [#588 – If You Handle PreviewKeyDown Event, KeyDown Won't Fire | 2,000 Things You Should Know About WPF](https://wpf.2000things.com/2012/06/26/588-if-you-handle-previewkeydown-event-keydown-wont-fire/)
- [dotnet/wpf: AccessKeyManager.cs (PresentationCore)](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Input/AccessKeyManager.cs)
- [dotnet/wpf: Button.cs (PresentationFramework)](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Button.cs)

## 不明点

- `AccessKeyManager`の登録タイミング(Windowロード時か、配置バーXAML評価時か)が
  `Window_PreviewKeyDown`のXAML登録より先か後かは未確認だが、`PostProcessInput`の
  `Handled`チェックは登録順序に依らず成立するため、結論への影響はない。
