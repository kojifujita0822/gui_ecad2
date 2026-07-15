# T-058増分5 再ドッキング時パネル高さ縮小 原因調査（隠密）

調査日: 2026-07-15
調査者: 隠密
委任元: 家老（殿裁定＝原因調査、忍者実機確認で発覚した新規事象）

## 症状（忍者報告の再掲）
「配置ツール」パネル（`PlacementToolBarDockingManager`）をフロート化後、再ドッキングした直後にパネル高さがタイトルバーのみ（19px）まで縮み、手動の境界ドラッグでは拡大できない（Ctrl+Alt+Rで即座に復旧）。

## 結論

### (a) 根本原因
**AvalonDock側の既知の設計上の癖（バグ）と判断する。ecad2側のXAML設定起因ではない。**

一次情報（GitHub Issue #337「LayoutAnchorablePane does not honour DockHeight settings on deserialize」、2022-03-11報告、**現在もOpen（未解決）**）に、今回の症状と酷似する報告が存在する：「ペインがヘッダーのみ表示される最小化状態になる（only the header is visible）」。

Issue #337の報告する根本原因：`LayoutGridControl.OnSizeChanged`メソッドで、`ActualWidth`または`ActualHeight`が0.0になるタイミング（ウィンドウのライフサイクル上、生成直後や再配置直後に一時的に発生しうる）で、サイズ計算・初期化処理（`UpdateChildren()`・`AdjustFixedChildrenPanelSizes()`）がスキップされてしまい、結果としてペインが直前の最小状態（タイトルバーのみの高さ）のまま留まる、という挙動。報告者はOnSizeChanged内へ`ActualWidth > 0.0 && ActualHeight > 0.0`の条件チェックを追加する回避策を提案しているが、正式な修正PRの有無・マージ状況は一次情報からは確認できなかった。

Issue本文のタイトルは「on deserialize」だが、根本原因（ActualWidth/Heightが一時的に0になるタイミングでの初期化スキップ）はデシリアライズに限定される話ではなく、**再ドッキング時のコントロール再構築でも同種のタイミング条件が成立しうる**と考えられる（推測、Issue本文はデシリアライズシナリオでの報告のみ）。

**AvalonDockのバージョン確認**：ecad2が採用する`Dirkster.AvalonDock`4.74.1のリリースノートを確認したところ、v4.73.0で「Fixed crash in DockingManager.OnSizeChanged if layout hasn't been loaded」という近縁の修正が存在するが、これは**クラッシュ対策であり、Issue #337が報告するサイズ計算スキップ問題そのものの修正ではないと判断される**（Issue #337が今なお開いたままであることから、この推測を裏付けられる）。4.70〜4.74.1のリリースノート範囲で、Issue #337に直接対応する修正の記載は見当たらなかった。

**ecad2側のXAML設定の確認**：増分5の`PlacementToolBarDockingManager`は`LayoutAnchorablePane`に`DockHeight`/`DockWidth`を明示指定しておらず、単一ペイン構成でコンテンツ（`ToolBar`）のAutoサイズに追従する標準的な使い方（忍者所見どおり）。これはAvalonDockの一般的な用法であり、ecad2固有の設定ミスに起因するものではないと判断する。

### (b) 対処の可否・難易度
**対処は困難、経過観察（Ctrl+Alt+Rによる事後救済で足りる）が妥当と判断する。**

- 根本原因はAvalonDock本体（NuGetパッケージ内部）のバグであり、正式な修正がリリース済みか確認できない（Issueは2022年からOpenのまま）。
- 回避策（`OnSizeChanged`への条件チェック追加）はAvalonDockのソースコード自体の改変を要し、ecad2側から直接パッチを当てるにはフォーク・独自ビルドが必要でコストが高い。
- ecad2側での代替回避策（再ドッキングイベントを検知して`DockHeight`を明示的に再設定する等）も考えられるが、AvalonDockの内部タイミング（`OnSizeChanged`発火順序）に依存する対症療法であり、複雑度が高く確実性も低い。
- Ctrl+Alt+Rによる即座の復旧手段が既に存在し（T-058増分1〜4で確立済みの機構）、実害は「フロート化→再ドッキングという能動的操作をした場合のみ」に限定される（通常の起動・使用では発生しない）。

## 一次情報の出典
- [Issue #337: LayoutAnchorablePane does not honour DockHeight settings on deserialize](https://github.com/Dirkster99/AvalonDock/issues/337)（症状が酷似、Open）
- [Issue #140: How to adjust FloatingHeight/Width to desired size of Content?](https://github.com/Dirkster99/AvalonDock/issues/140)（関連度は低いが、フロートウィンドウのサイズ計算全般に既知の課題があることを裏付ける傍証、Open）
- [Dirkster99/AvalonDock Releases](https://github.com/Dirkster99/AvalonDock/releases)（v4.73.0の近縁修正を確認、Issue #337への直接対応は確認できず）
- [LayoutAnchorablePane Wiki](https://github.com/Dirkster99/AvalonDock/wiki/LayoutAnchorablePane)（`DockHeight`/`FloatingHeight`の仕様記載は「初期値」程度に留まり、未設定時の自動追従挙動の詳細は非公開）

## 不明点
- Issue #337の正式な修正PRの有無・マージ状況（GitHub UI上のコメント欄を直接確認できなかったため）。
- 今回の症状が「フロート化→再ドッキング」操作固有か、UIA合成ドラッグ特有の癖かは、一次情報だけでは判別不可（忍者所見と同様、実操作での再現性確認が必要）。

## 家老への申し送り
一次情報からAvalonDock側の既知の設計上の癖である可能性が高いと判断できたため、**「経過観察（Ctrl+Alt+Rによる事後救済で足りる）」の裁定を推奨する**。対処を試みる場合はAvalonDockのフォーク・独自ビルドという大きなコストを伴うため、費用対効果は低いと考える。
