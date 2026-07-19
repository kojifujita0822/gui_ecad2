# T-067(5) 右クリックメニュー実機確認（忍者、2026-07-19）

## 判定サマリ

| 観点 | 判定 | 所見 |
|---|---|---|
| 1. 右クリックメニュー表示 | OK | 「線種」▶（実線/破線/点線）+「削除」が正しく表示・展開される |
| 2. 線種変更3種の反映 | **NG** | 内部モデルは正しく変更されるが、変更直後の画面再描画が反映されない |
| 3. 削除 | OK | 右クリック→削除で枠が正しく消える |
| 4. Undo/Redo回帰なし | OK | 削除→Undo→Redoが正常動作 |
| 5. 境界ケース（rowInRangeガード） | **保留・確認難航** | 確実な再現環境の構築に至らず、詳細は下記参照 |

## 観点2（NG）詳細

枠を右クリック→「線種」→「破線」を選択したが、**CopyFromScreen方式で確認しても枠線は実線の
ままで変化が見られなかった**（PrintWindow既知の罠を疑い、CopyFromScreenでも同一結果を確認
済み。手法起因ではなく実際に未反映）。続けて「点線」を試したが同様に無反応。

しかし、その後**「削除」→Undo(Ctrl+Z)を実行したところ、復元された枠が正しく「点線」として
描画された**（拡大画像でドットパターンを確認、証跡`t067-5-undo-topborder-zoom.png`）。これは
**内部モデル（`GroupFrame.BorderStyle`）は正しく`Dotted`に変更されている**ことを示す一次証拠。
Redo(Ctrl+Y)も正常動作（削除状態に戻る）。

一次ソース確認（`MainWindow.xaml.cs` 1790-1796行目）：
```csharp
var styleItem = new MenuItem { Header = label };
styleItem.Click += (_, _) =>
{
    if (_viewModel.SetSelectedFrameBorderStyle(style)) RedrawCanvas();
};
```
`SetSelectedFrameBorderStyle`（`MainWindowViewModel.cs` 1429-1436行目）は変更が実際にあった
場合のみ`true`を返し`frame.BorderStyle = style`を実行する。Undo後に正しい線種で描画された
事実から、この呼び出し自体は成功し`RedrawCanvas()`も呼ばれているはずだが、**その直後の画面
反映だけが機能していない**——「モデル変更は成功、直後の再描画のみ視覚的に効かない」という
表示バグと判断する。削除処理（既存`DeleteMenuItem_Click`を流用）は正しく即時反映されており、
両者の実装経路の違い（新規実装のインラインClickラムダ vs 既存の共有ハンドラ）が関係している
可能性があるが、原因の一次確認は侍・隠密に委ねる。

## 観点5（保留）詳細

隠密指摘の「rowInRangeガードにより範囲外の枠境界線で右クリック無反応」を狙い、ズーム40%で
グリッド全体を表示し最下行付近まで届く枠を作成→境界線を右クリックしたが、**無反応という結果は
得られたものの、後の確認でズーム40%表示時のグリッド罫線の切れ目がシートの実際の行数上限
（既定22行）なのか、単にキャンバスの表示・スクロール可能範囲の限界（未使用領域は描画されない
可能性）なのかを、確実に切り分けられなかった**。垂直スクロールバーを最大値まで動かしても
行10程度までしか表示されず、シートの`Rows=22`という設定値に対応する行21までスクロールで
到達できているか確認が取れていない。

この観点は、忍者側でこれ以上座標を手探りで追い込むよりも、正確なグリッド原点・セル間隔の
理論値（`MarginMm=20mm`, `CellMm=9mm`、`Sheet.Rows`既定22）を踏まえた侍・隠密側の計算、または
殿代行操作での確認が効率的と判断し、これ以上の深追いを避けた。**「無反応だった」という一度の
観測結果はあるが、それが境界ケード起因か検証環境の制約かを断定できないため、NGともOKとも
報告しない**。

## 証跡ファイル
- 観点1: `t067-5-frame-created.png`, `t067-5-after-rightclick.png`
- 観点2: `t067-5-dashed-copyfromscreen.png`, `t067-5-dotted-copyfromscreen.png`,
  `t067-5-frame-topborder-zoom.png`（実線のまま）, `t067-5-undo-topborder-zoom.png`（点線で復元）
- 観点3・4: `t067-5-after-delete.png`, `t067-5-after-undo.png`, `t067-5-after-redo.png`
- 観点5: `t067-5-boundary-frame.png`, `t067-5-boundary-rightclick1.png`（行操作メニュー誤ヒット）,
  `t067-5-boundary-rightclick2.png`（無反応）, `t067-5-scrolled-bottom.png`（スクロール限界）

## 範囲外検出
なし。
