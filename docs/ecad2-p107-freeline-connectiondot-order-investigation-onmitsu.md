# P-107 FreeLine/ConnectionDot判定順序逆転 実害調査（隠密）

日付: 2026-07-22
契機: `docs/proposed.md` P-107。T-067(1)ヒットテスト静的レビューでGuiEcad原本との突合中に発見。
ecad2既存コードのFreeLine/ConnectionDot判定順序（FreeLine先）がGuiEcad原本の順序（Dot先）と
逆転している件、実害の有無を調査（家老采配2026-07-22）。

## 結論

**実害あり（可能性が高い）**。ConnectionDot（接続点）が自由線(FreeLine)の交点上に配置される
通常の使い方をしている限り、ConnectionDotを直接クリックで選択することが事実上できず、選択・
移動・削除の各操作に波及する。ただし実機確認（忍者領分）は未実施であり、机上のコード読解に
基づく判定である。

## 根拠

### GuiEcad原本：Dotを先に判定する明示的な設計意図

`C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Pointer.cs`354行:

```csharp
else if (HitTestDot(xMm, yMm) is ConnectionDot hitDot)   // 線の交点上に置かれるため自由直線より先に判定
```

コメントに理由が明記されている。当たり判定半径（同ファイル`MainPage.xaml.cs`583,596行）:
- `HitTestDot`：`_geo.CellMm * 0.25`
- `HitTestFreeLine`：`_geo.CellMm * 0.3`

両者は同程度の広さで、ConnectionDotがFreeLine線分上に乗っていれば当たり判定範囲は確実に重複する。
原本の設計者は、この重複を見越して「Dot優先」の順序を意図的に選んでいる。

`docs/archive/ecad2-guiecad-code-survey-onmitsu.md`101行が記す原本の優先順位一覧（if-elseチェーン
の記述順＝優先順位）も「要素→縦コネクタ→枠→接続点→自由直線→画像」と、**接続点が自由直線より先**
であることを裏付ける。

### ecad2：順序が逆転、GuiEcad由来の理由への言及なし

`src/Ecad2.App/MainWindow.xaml.cs`1768-1780行（T-041増分5）:

```csharp
// T-041増分5: 自由線・接続点(主回路シート)の選択。同じ排他クリア順序に倣う。
if (LadderCanvasHost.HitTestFreeLine(position, sheet) is Ecad2.Model.FreeLine freeLine)
{
    _viewModel.SelectedCell = null;
    _viewModel.SelectedFreeLine = freeLine;
    return;
}
if (LadderCanvasHost.HitTestConnectionDot(position, sheet) is Ecad2.Model.ConnectionDot dot)
{
    _viewModel.SelectedCell = null;
    _viewModel.SelectedConnectionDot = dot;
    return;
}
```

コメントは「同じ排他クリア順序に倣う」（＝`SelectedCell=null`を先に呼ぶという別の規約への言及）
のみで、GuiEcad由来の優先順位理由（線の交点上に置かれるため）への言及が無い。同じ関数内の
Connector/Frame（1746-1759行）には「GuiEcad優先順位に倣い」という明示的な参照コメントがあるのに
対し、FreeLine/ConnectionDotの追加時（T-041増分5）だけ、この参照が欠けている。

`src/Ecad2.App/Views/LadderCanvas.cs`116-117行の当たり判定半径：
`FreeLineHitToleranceMm = 2.0`、`ConnectionDotHitToleranceMm = 2.0`（いずれも固定mm値、原本の
セル比率とは表現が異なるが、同程度の重複可能性を持つ広さ）。

`docs/archive/ecad2-t041-increment5-review-onmitsu.md`（増分5の隠密レビュー記録）を確認したが、
許容誤差の値がPoCと一致するかの確認（56-64行）のみで、**クリック時の判定順序（優先順位）自体は
レビュー観点に含まれていなかった**。T-041増分5導入時、GuiEcad原本の該当順序との突合が行われな
かったと見られる。

### 失敗シナリオ

`docs/spec/ecad2-spec-wiring.md`22行の用途定義（「接続点記入(F10)＝主回路限定」）とGuiEcad原本の
設計意図から、ConnectionDotは主回路シートでFreeLine同士（または配線）の交差点を示すために配置
される。ユーザーが以下の操作をした場合：

1. 主回路シートでFreeLine（自由線）を1本以上配置する。
2. その自由線の交点上（または線上）にConnectionDot（接続点、F10）を配置する。
3. その交点付近をクリックして選択しようとする。

ecad2の現在の実装順序では、まず`HitTestFreeLine`が判定される。ConnectionDotの座標は自由線の
線分上（距離ほぼ0）にあるため、`FreeLineHitToleranceMm=2.0mm`の許容誤差に確実に入り、**常に
FreeLineの方が選択されてしまう**。ConnectionDotが選ばれる条件（`HitTestFreeLine`が`null`を
返すこと）は、その座標が自由線の当たり判定範囲外にある場合に限られるが、それは「自由線の交点上
に置く」という接続点本来の用途と矛盾するため、通常の使い方では到達しない分岐になっている。

この結果、接続点を自由線の交点上に置く通常の運用をしている限り、`SelectedConnectionDot`経由の
操作（選択・矢印キー移動`MoveSelectedConnectionDotByKey`・ドラッグ`BeginDragConnectionDot`・
削除`DeleteSelectedConnectionDot`）がいずれも実質的に到達不能になる可能性が高い。

## 不明点

- 実機での再現確認は未実施（隠密の静的レビュー範囲を超えるため、忍者領分）。理論上は
  `HitTestFreeLine`のマージン(2.0mm)内に必ず入るはずだが、実際のクリック座標の丸め・DIP変換
  誤差等で稀に閾値をわずかに外れるケースがあるかは実測が必要。
- 接続点を「自由線から意図的に離れた孤立点」として使う運用が実際にあるか（あるならその場合は
  問題が顕在化しない）は仕様上の想定次第で、本調査では確認できていない。

## 派生提案の有無

対処自体は極めて単純（`MainWindow.xaml.cs`1769-1780行の2ブロックの前後を入れ替えるのみ、
GuiEcad原本と同じ順序に戻す）と見受けられるが、対処の要否・実装可否の判断は家老・侍に委ねる。
本調査は「実害の有無」の判定に留める。

## 出典

- `docs/proposed.md` P-107
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Pointer.cs`354行
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.xaml.cs`580-604行
- `docs/archive/ecad2-guiecad-code-survey-onmitsu.md`100-103行
- `src/Ecad2.App/MainWindow.xaml.cs`1744-1788行
- `src/Ecad2.App/Views/LadderCanvas.cs`116-117,447-469,471-490行
- `docs/spec/ecad2-spec-wiring.md`22,96行
- `docs/archive/ecad2-t041-increment5-review-onmitsu.md`56-64行
