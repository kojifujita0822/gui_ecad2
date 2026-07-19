# T-089 配置ツールボタン「選択中の恒久的インジケーター不在」切り分け調査（隠密）

調査日: 2026-07-19
調査者: 隠密
委任元: 家老（忍者の詳細原因分析を受けた切り分け依頼、task_id=T-089）

## 発端（忍者所見の再掲、家老経由）

配置ツールボタン（`PlacementToolBarButtonStyle`、F5〜F10・a接点配置等）には選択中（`ViewModel.Tool`）を示す恒久的インジケーターが無く、色変化はMouseOver/Pressedのみに依存する一時的なもの。殿の「押下後、水色から元に戻る」という所見と符合する。

## 依頼内容

これがT-089実装（ControlTemplate自作転換、Style.Triggers移設等）で失われた新規回帰か、元々未実装だった仕様欠落かの切り分け。

## 結論

**T-089による新規回帰ではない。T-089着手前から一貫して存在しなかった、元々の仕様欠落である。**

## 根拠

### (1) T-089着手前（HEAD、コミット`3165d49`）時点でも同一構造

```
git show HEAD:src/Ecad2.App/MainWindow.xaml | grep -n "PlacementToolBarButtonStyle\|Tool\.Mode\|Binding.*Tool"
```
`Tool.Mode`へのバインディングはステータスバーのテキスト表示（`<TextBlock Text="{Binding Tool.Mode, StringFormat='ツール: {0}'}"/>`）1箇所のみ。`PlacementToolBarButtonStyle`定義（HEAD時点）にTool.Mode関連のトリガーは存在しない。

### (2) 配置ツールボタン群の実質原型＝T-040導入時点（コミット`8d10684`、GX Works3様式化）でも同一構造

```
git show 8d10684:src/Ecad2.App/MainWindow.xaml | grep -n "PlacementToolBarButtonStyle" -A 8
git show 8d10684:src/Ecad2.App/MainWindow.xaml | grep -n "Tool\.Mode\|IsChecked"
```
`PlacementToolBarButtonStyle`はWidth/Heightのみの単純な派生スタイルで、Tool.Mode関連のトリガーは無し。`Tool.Mode`バインディングはこの時点でも既にステータスバーの1箇所のみ。**T-040導入時点から一貫して恒久的インジケーターは存在しなかった。**

### (3) 構造的要因（ボタン型そのものに選択状態を保持する仕組みが無い）

配置ツールボタンは`Button`（`Click`イベントのみ）であり、`ToggleButton`ではない。`IsChecked`のような恒久的な選択状態プロパティ自体が型として存在しないため、そもそも「選択中を色で示す」ための土台（DataTrigger等のバインド先）が無い。テストモードボタン（`TestModeToolBarButtonStyle`）は`ToggleButton`で`IsChecked`トリガーによる恒久表示を持つが、配置ツールボタン群は最初から異なる型で実装されている。

## 評価

忍者所見「押下後、水色から元に戻る」は、T-089が追加した押下フィードバック（`IsPressed`依存、瞬間的）が仕様通り動作しているに過ぎない。恒久的インジケーター機能自体が最初から存在しないため、「選択中の恒久表示が無い」ように見えるのは当然の帰結であり、**T-089のバグではない**。

## 派生提案

配置ツールボタンに「選択中ツールの恒久的ハイライト表示」を新設するか否かは、UI/UX判断を伴う新規機能要望であり、本調査の範囲外。`docs/proposed.md`へ記録し、実装要否は殿確認を経てから判断すべきと考える（本調査書とは別途、家老采配によりproposed.md記載済み）。

## 不明点

なし（コミット比較による確定調査、推測を含まない）。
