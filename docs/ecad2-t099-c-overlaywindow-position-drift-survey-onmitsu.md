# T-099(c) 追加調査: 十字型UI（OverlayWindow/DropTarget）の位置決めロジックと案Yの関連切り分け（隠密）

調査日: 2026-07-20　調査者: 隠密　委任元: 家老（殿実機報告=十字型UIの表示位置ずれ、忍者実機再現と並行調査）
手法: 一次ソース確認（GitHub `Dirkster99/AvalonDock` master、v4.74.1相当）。静的読解のみ、実機検証は行っていない。

---

## 前提: 既存調査書の再確認結果

`docs/ecad2-t099-c-overlaywindow-droptarget-and-attachdrag-survey-onmitsu.md`（2026-07-19作成）の
「調査1」は、十字型UI（Left/Top/Right/Bottom/Intoの各ドロップターゲット）を**中央のみに絞り込む
表示制御**（Visibility切替）の実現可否を扱っており、**「表示位置がずれる」という今回の観点（位置決め
ロジックそのもの）に関する記述は無い**。よって一次ソースを新規に調査した。

---

## 調査結果: 十字型UI（OverlayWindow）の位置決めロジック

### 結論

**カーソル追従ではない。ドラッグ対象を保持する`DockingManager`自身の画面上の位置・実測サイズに
ぴったり重ねて配置される設計**（対象ペイン全体を覆うオーバーレイ、家老の選択肢でいえば
「対象ペイン中央固定」に近い——正確には「対象DockingManager全体を覆うオーバーレイウィンドウ」で
あり、その内部にXAMLレイアウトで配置された十字アイコン群が乗る構造）。

### 根拠（一次ソース、`DockingManager.cs`）

`CreateOverlayWindow()`（`DockingManager.cs:2575-2595`）:

```csharp
private void CreateOverlayWindow(LayoutFloatingWindowControl draggingWindow = null)
{
    if (_overlayWindow == null)
    {
        _overlayWindow = new OverlayWindow(this);
    }
    if (draggingWindow?.OwnedByDockingManagerWindow ?? true)
        _overlayWindow.Owner = Window.GetWindow(this);
    else
        _overlayWindow.Owner = null;

    var rectWindow = new Rect(this.PointToScreenDPIWithoutFlowDirection(new Point()), this.TransformActualSizeToAncestor());
    _overlayWindow.Left = rectWindow.Left;
    _overlayWindow.Top = rectWindow.Top;
    _overlayWindow.Width = rectWindow.Width;
    _overlayWindow.Height = rectWindow.Height;
}
```

- `this`は`DockingManager`自身（ecad2では`PlacementToolBarDockingManager`等、複数DockingManager
  構成のうちドラッグ操作の起点となったマネージャ）。
- `PointToScreenDPIWithoutFlowDirection(new Point())`＝DockingManagerの左上端(0,0)をスクリーン座標へ
  変換した値。`TransformActualSizeToAncestor()`＝実測サイズ（`ActualWidth`/`ActualHeight`系）。
- **この2値をその場でキャプチャし、OverlayWindow（十字型UIを含む透明ウィンドウ）の`Left/Top/Width/
  Height`へ1回だけ設定する**。ドラッグ中にカーソルが動いてもOverlayWindow自体の位置は再計算されない
  （マウス追従はDropTarget群の`HitTestScreen`判定のみ、既存調査書「調査1」根拠3参照）。
- 個々の十字アイコン（`PART_AnchorablePaneDropTargetLeft/Top/Right/Bottom/Into`等）は、
  OverlayWindowのControlTemplate内でXAMLレイアウトにより相対配置され、`GetScreenArea()`
  （`TransformExtentions.cs`、実測位置ベース）で画面座標を動的取得する（既存調査書の記載どおり）。

### 重要な含意

**OverlayWindow自体の位置・サイズは「`CreateOverlayWindow()`が呼ばれた瞬間の`DockingManager`の
実測値」に依存する一発キャプチャ**である。呼び出しタイミングでDockingManagerのレイアウトパスが
未完了（実測値が古い/過渡的）だと、OverlayWindow全体（＝十字型UIごと）が対象ペインからズレた位置に
表示されうる、という構造が一次ソースから読み取れる。

---

## 案Y実装（コミット`61ecdfd`）との関連切り分け

### 結論: 直接の変更範囲外。ただし間接的な経路は理論上否定できない

- `git show --stat 61ecdfd`＝`src/Ecad2.App/MainWindow.xaml.cs`のみ、98追加/45削除。変更内容は
  `ContentDocking`/`ContentFloating`ハンドラの簡素化・`ResetPlacementToolBarLayoutToDefault`新設・
  `HasExpectedContent`関連（前回セッション持ち越し分、既レビュー済み）・診断ログ除去のみ。
- `CreateOverlayWindow`・`OverlayWindow`・`DragService`等、十字型UIの表示ロジックを構成する
  AvalonDock側のコードにecad2は一切手を加えていない（ecad2はAvalonDockをNuGetパッケージとして
  参照するのみ、フォークやソース同梱ではない）。**コードとして直接触れている範囲ではない**。

### 間接的な経路（未検証、推測）

1. **タイミング仮説**：`ResetPlacementToolBarLayoutToDefault()`は`ContentDocking`イベント内で
   `TryDeserializeDockingLayout`を同期呼び出しし、レイアウトツリーを丸ごと差し替える（設計書
   「同期呼び出しの安全性」節）。この差し替え直後、`PlacementToolBarDockingManager`の
   `ActualWidth`/`ActualHeight`がWPFの次回レイアウトパスを経るまで旧サイズ（差し替え前の値、
   または不定）のまま残る可能性がある。この状態でユーザーが即座に再度ドラッグを開始すると、
   `CreateOverlayWindow`が古い/過渡的な実測値をキャプチャし、十字型UIがズレて見える、という
   経路は一次ソースの構造上あり得る（案Yがこの不整合を「新規に生んだ」のではなく、レイアウト
   差し替えという操作自体が本来的に持つタイミング窓——案Y以前の対症療法でも同種のレイアウト
   差し替えは発生していたため、案Y固有の問題とは断定できない）。
2. **T-099要件3（幅動的フィット、案Yとは別コミット）との複合**：配置ツールバーはコンテンツ幅へ
   動的フィットする実装済み（ToolBar実幅1244px→523px、家老采配メッセージでは案Yに含めていない
   別要件）。DockingManager自体の実測サイズが以前（固定幅）より変動しやすい構造になっているため、
   `CreateOverlayWindow`の一発キャプチャとサイズ変動タイミングが噛み合わない場面が増えている
   可能性がある。**これは案Y単体でなくT-099要件全体の副作用として疑うべき観点**。

### 切り分けられない理由

一次ソース精読だけでは「実際にどのタイミングでドラッグが開始されたか」「その瞬間の
`ActualWidth`/`ActualHeight`が実際に古かったか」は確認できない。実機での再現条件の特定が必須。

---

## 忍者への申し送り（再現条件の切り分け観点）

1. **再ドッキング直後・即座の再ドラッグ vs 間を置いてからのドラッグ**：ズレが前者でのみ発生し
   後者で発生しないなら、上記タイミング仮説（レイアウト差し替え直後の実測値不整合）が濃厚。
2. **ズレの方向・量**：十字型UIが配置ツールバーの「旧位置」（幅動的フィット前のサイズ・位置、
   または直前のドッキング状態の位置）にズレるなら、実測値キャプチャの遅延を強く示唆する。
   ランダムな方向・量なら別要因（DPI・マルチモニタ座標変換等）を疑うべき。
3. **配置ツールバー以外のDockingManager（左パレット・出力・右パネル）でも同型のズレが起きるか**：
   案Y実装は`PlacementToolBarDockingManager`専用のハンドラ配線のみのため、他パネルで再現しないなら
   案Y固有の関与を示唆（ただし他パネルは今回の再発防止策・Deserialize経路の対象でもあるため、
   完全な対照群にはならない点に留意）。
4. **再現の再現性**：毎回100%か、確率的か。過渡的な実測値のタイミング窓に依存する仮説なら、
   確率的な再現になるはず。

## 不明点

- OverlayWindowのControlTemplate（十字アイコン群のXAMLレイアウト定義）がAvalonDock本体
  `Generic.xaml`側かテーマパッケージ（`AvalonDock.Themes.VS2013`等）側のどちらにあるかは
  未特定（前回調査書から持ち越しの不明点、今回も追跡していない——アイコン自体の配置ロジック
  ではなく「ウィンドウ全体の位置決め」を優先して調査したため）。
- GitHub issueでの既知不具合報告の有無は未検索（家老依頼の主眼＝位置決めロジックの解明と
  案Yとの関連切り分けを優先、時間関係で後回し。必要なら追加調査可）。
