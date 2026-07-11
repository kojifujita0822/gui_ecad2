# T-033増分1 再レビュー（往復1周目）：IsEnabledカスケード化（隠密）

> 2026-07-07 隠密レビュー。対象コミット `4d73864`（`fix(app): T-033増分1 - 隠密レビュー指摘(重大)を反映(往復1周目)`）。
> 家老指定の観点(1)〜(6)＋`code-review`スキル（medium、8角度finder→7件候補・うち1件REFUTED）併用。

---

## 結論：クリーン。忍者の実機確認へ進めてよい

前回レビュー（`docs/ecad2-t033-review-onmitsu.md`）で指摘した重大な欠陥（マウス経路6系統の
素通し）は、`IsPlacementBarVisible`（ViewModel単一の真実源）へのIsEnabledバインドにより
**恒久的に解消された**ことをXAML構造・WPFのIsEnabled合成仕様の両面で確認した。残る指摘は
いずれも軽微（cleanup系・将来の拡張時リスク低減提案）で、往復完了の妨げにはならない。

---

## 観点別結果

### (1) 前回指摘のマウス経路6系統すべてが無効化ツリーの配下に実際に入るか — **CONFIRMED（網羅済み）**

| # | 経路 | 実体 | 所属コンテナ | 判定 |
|---|---|---|---|---|
| 1 | キャンバスクリック | `LadderCanvasHost` | メイン作業域Grid（258行目、バインド有） | カバー済み |
| 2 | 部品選択リスト | `PartSelectionList` | メイン作業域Grid配下`RightPanelArea` | カバー済み |
| 3 | DRC出力パネル行 | `OutputGrid` | `OutputPanelArea`（393-394行目、直接バインド） | カバー済み |
| 4 | シートナビ | `SheetNavList`他 | メイン作業域Grid配下`LeftPaletteArea` | カバー済み |
| 5 | 選択ツールボタン | ツールバーボタン | `ToolBarArea`（109-110行目、直接バインド） | カバー済み |
| 6 | 新規/開くボタン | メニュー・ツールバー | `MenuBarArea`/`ToolBarArea` | カバー済み（二重保護） |

`StatusBar`(Row=4)のみ対象外だが対話要素を持たないため実害なし。全経路のモレは見つからなかった。

### (2) バー自身が無効化スコープの外にあるか — **確認済み・意図通り（懸念は該当しない）**

XAML構造を実読した結果、`ElementPlacementBar`（457行目）はルートGridの直接の子であり、
`IsEnabled`バインドが追加された「メイン作業域Grid」（258行目、同じくGrid.Row="2"）とは
**兄弟要素**であって子ではない。したがって家老懸念（「IsEnabledは子へ継承するゆえ、バーが
無効化Gridの子だとバー自体も死ぬ」）は該当しない。バー自身（OK/キャンセルボタン・ComboBox・
TextBox）は`IsEnabled`バインドを持たず常時操作可能であり、これは意図通りの安全な設計。

### (3) T-019案BのHasProject連動グレーアウトとの共存 — **確認済み・競合なし**

WPFの`IsEnabled`は祖先要素とローカル値のAND合成（祖先=falseなら子はローカル値に関わらず
実効的に無効化）という標準機構であり、既存の`IsEnabled="{Binding HasProject}"`（個々のボタン）
と今回追加した親レベルの`IsPlacementBarVisible`連動は「いずれかがfalseなら無効」という二重
ゲートとして正しく共存する。

なお`code-review`のAngle Cが提起した「`HasProject=false`かつ`IsPlacementBarVisible=true`が
同時成立しうるのでは」という候補は**verify段階でREFUTEDと判定**した：ツールバーの配置ボタン群
には既に`IsEnabled="{Binding HasProject}"`（個別バインド）があり、キーボード経路(`TryPlaceBuiltin`)
にも`HasProject`の明示ガードが既存（コミット`ae2db4b`、T-019、本コミットより前）で入っている
ため、`Tool.Mode=PlaceElement`へ遷移する経路自体が`HasProject=false`の間は構造的に存在しない。
懸念は成立しない。

### (4) Window_PreviewKeyDown早期リターンとの併存整合（キー経路） — **確認済み・整合、むしろ改善**

`if (ElementPlacementBar.Visibility == Visibility.Visible) return;`から
`if (_viewModel.IsPlacementBarVisible) return;`への変更は、UI要素の実装詳細（Visibility）
ではなくViewModelプロパティを直接参照するようになった点で意味的に等価かつ改善（Reuse指摘への
対応も兼ねる）。`SetProperty`→`PropertyChanged`は同期発火のため、バインディング伝播のタイミング
ズレも生じない。

### (5) Visibility直操作の残骸なし＝単一情報源の徹底 — **確認済み・徹底されている**

`grep`で`ElementPlacementBar.Visibility`の直接参照が完全に消えていることを確認した
（`_viewModel.IsPlacementBarVisible = true/false`への置換が徹底）。`_placementIsOr`も
`bool?`化され`ClosePlacementBar`で`null`リセットされており、前回のSimplification指摘にも
対応済み。

### (6) 初期フォーカスBeginInvoke化の妥当性 — **概ね妥当、軽微な指摘2件（PLAUSIBLE）**

`Dispatcher.BeginInvoke(..., DispatcherPriority.Loaded)`への変更はMicrosoft Learn公式の推奨
（`Loaded`/`Dispatcher.BeginInvoke`経由の初期フォーカス設定）に沿っており妥当。ただし2点、
確度は低いが記録に値する指摘があった：

- **フォーカス遅延中のTab競合**（PLAUSIBLE）: `PlacementDeviceNameBox.Focus()`が遅延する間、
  バー自身はIsEnabled対象外のため操作可能なままで、ごく短い window でTabキー等により想定外の
  フォーカス順序になりうる。`DispatcherPriority.Loaded(6) > Input(5)`のため実害は小さいと見られる。
- **DeviceNameBox編集中の再入**（PLAUSIBLE）: 既存要素のプロパティパネル編集中に別セルへF5等で
  新規配置をトリガーすると、`IsPlacementBarVisible=true`代入がメイン作業域Gridを無効化し、
  フォーカスを持つ`DeviceNameBox`が強制的にフォーカスを失い`LostKeyboardFocus`が`TryPlaceElement`
  実行中に再入的に発火する。実害は軽微（早期の無害な再描画1回が挟まる程度）と見られる。

いずれも忍者の実機確認で挙動を確かめる価値はあるが、往復完了の妨げにはならない軽微な所見。

---

## `code-review`スキル併用の追加指摘（medium、Simplification/Reuse）

- **Simplification（CONFIRMED）**: 同一の`IsEnabled="{Binding IsPlacementBarVisible, ...}}"`が
  4箇所（Menu/ToolBarTray/メイン作業域Grid/OutputPanelArea）にコピペされている。ラッパーGrid
  1つに束ねて1バインドへ一元化する余地があり、将来5つ目の領域が追加された際の貼り忘れリスクを
  軽減できる。技術的な衝突要因（GridSplitter配置・ZIndex等）は無いことを確認済み。
- **Reuse（PLAUSIBLE）**: 新規`InverseBooleanConverter`（bool→bool反転）はViewModel計算
  プロパティでも代替可能だが、既存の`InverseBooleanToVisibilityConverter`も同じ「View側で反転」
  という流儀を踏襲しており、このコードベースの既存パターンとの整合性の観点では優劣が付けにくい。
  優先度は低い。

---

## 総評・推奨

往復1周目としてクリーン。前回の重大な欠陥（マウス経路6系統の素通し）は完全に解消されている。
残る指摘（4箇所コピペの一元化、フォーカス関連の軽微なPLAUSIBLE2件）は次の増分または余力がある
時の改善事項として記録するに留め、**T-033増分1は忍者の実機確認へ進めてよい**と判断する。

忍者への申し送り: 実装プランの忍者検証観点（Enter/Esc/マウスクリックの三経路対称性、キャンセル
時の原子的取消、画面端でのバー位置、モーダル非ネスト規約）に加え、上記(6)の2点（バー表示直後の
Tab連打での初期フォーカス順、既存要素編集中に別セルへF5等で新規配置をトリガーした際の見た目の
ちらつき有無）を軽く確認できると尚可。

---

## 出典・参照

- 対象コミット `4d73864`（`git show`で全差分確認）
- `src/Ecad2.App/MainWindow.xaml`, `src/Ecad2.App/MainWindow.xaml.cs`
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`IsPlacementBarVisible`）
- `src/Ecad2.App/Converters/InverseBooleanConverter.cs`（新規）
- コミット`ae2db4b`（T-019、既存の`HasProject`ガードの導入元、観点3の裏付け）
- `docs/ecad2-t033-review-onmitsu.md`（前回レビュー、往復0周目相当）
- `code-review`スキル（medium、finder 8角度→verify 4件、CONFIRMED1・PLAUSIBLE3・REFUTED1）
