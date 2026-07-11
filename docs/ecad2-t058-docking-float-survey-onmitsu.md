# T-058 パネル（ツールバー含む）のドック化・フロート配置機能 調査（隠密）

殿直接指示（2026-07-11、範囲拡大版）。第一段＝隠密調査（WPF標準機能の限界確認・外部ライブラリ候補の
要否/コスト/ライセンス・GX Works3等参考UIでの類似事例・対象パネル候補の棚卸しと優先度叩き台）。
**本調査は実装を伴わない。外部ライブラリ導入の採否は改めて殿へゲートする（`docs/todo.md`T-058節）。**

---

## 1. WPF標準機能の限界（技術確認）

### 事実（既存確認、家老検分2026-07-11 `docs/todo.md`T-058節より）
`ToolBarTray`/`ToolBar`は**同一`ToolBarTray`内でのバンド間並べ替えのみ**が標準機能で、別ウィンドウとして
切り離す「真のフロート化」能力は無い。ecad2の`MainWindow.xaml:141-142,206`は`IsLocked="True"`で
このドラッグ並べ替え自体も明示的に無効化済み（意図的な選択、フロート機能とは無関係と家老確認済み）。

`DockPanel`（`MainWindow.xaml:380,450,470,481,514`で使用中）はあくまで**レイアウトパネル**（子要素の
上下左右配置を決めるだけ）であり、ウィンドウ分離・再配置の機構ではない。`GridSplitter`（同397,437,462,507）
はGrid行/列境界のドラッグリサイズのみで、位置の入れ替え・別ウィンドウ化は対象外。

### 自前実装した場合の技術的見通し（所見）
WPF標準APIのみで「パネルを別ウィンドウへ切り離し、再度メインウィンドウへ戻す」こと自体は理論上可能
（`Window`を動的生成し`Grid.Children.Remove`で既存要素を親から外して新規`Window.Content`へ再アタッチ、
逆操作で再ドッキング）。ただし、GX Works3的なドッキングUIとして実用に足るには追加で以下が必要になり、
いずれもゼロからの実装コストが大きい：
- ドラッグ中のドロップ先プレビュー表示（Adorner Layer活用等）
- 複数フロートウィンドウ間のタブ化（1つのタブグループに複数パネルをまとめる）
- レイアウトの永続化（アプリ再起動時に前回のフロート配置を復元）
- フロートウィンドウとメインウィンドウ間のフォーカス・Zオーダー管理

これらは外部ドッキングライブラリ（2節）が標準機能として持つものであり、自前実装は「車輪の再発明」に
近いコスト構造になる。**所見: 自前実装(a)よりも外部ライブラリ導入(b)の方が実装コスト対効果に優れる
と考えられる**（最終判断は殿へゲート）。

---

## 2. 外部ドッキングライブラリ候補調査

Web調査（一次情報：GitHub API・NuGet.org・各ベンダー公式ドキュメント）による比較。

| ライブラリ | ライセンス | 保守状況 | .NET 8対応 | 商用利用 | 導入コスト所感 |
|---|---|---|---|---|---|
| **AvalonDock**（`Dirkster.AvalonDock`、Dirkster99フォーク） | **MS-PL**（準パーミッシブ、商用クローズドソース配布可） | 非常に活発（調査時点2日前にpush、★1,700/Fork 348） | `net8.0-windows`明示ターゲットは無いが`net5.0-windows7.0`アセットへのフォールバックで動作する見込み（**要PoC実機確認**） | 可 | **低**。NuGet一発、MVVM対応API、RoslynPad等の実績あり |
| Xceed WPF Toolkit（Extended WPF Toolkit） | v4.0.0以降**Community License=非商用限定**、商用は別途Plus版購入要 | ★4.2k | 無償版は.NET 8対応の明記なし | **不可（無償版）** | ecad2の有償配布方針とは無償版が不適合 |
| DevExpress WPF（DockLayoutManager） | 商用プロプライエタリ | 継続更新 | 対応 | 可（有償） | 単一開発者$1,078/年〜（スイート全体） |
| Telerik UI for WPF（RadDocking） | 商用プロプライエタリ（永久ライセンス新規販売終了、サブスクのみ） | 継続更新 | 対応 | 可（有償） | $850〜$1,650/年 |
| Infragistics WPF（XamDockManager） | 商用プロプライエタリ | 継続更新 | 対応 | 可（有償） | $1,599/年〜 |

**GPL/LGPL系ライブラリは主要候補に見当たらず**、CLAUDE.md「不要な外部依存を追加しない」原則との
抵触が懸念されるライセンス上の罠は今回の調査範囲では該当しなかった。注意すべきは商用配布時の
Xceed無償版の非商用限定条項。

### 推奨
**AvalonDock（`Dirkster.AvalonDock`）を最有力候補として推奨する**（隠密所見、採否は殿へゲート）。
MS-PLで商用配布方針と矛盾せず、保守が極めて活発、導入コストがほぼゼロ。商用スイート（DevExpress等）は
年間$1,000〜1,600/開発者の継続コストが発生し、ドッキングUIのみのためにスイート全体を導入する費用対
効果は薄い。唯一の懸念（.NET 8明示対応の有無）は**導入前に小規模PoCで実機確認**することを推奨する。

---

## 3. GX Works3等参考UIでの類似事例

既存調査書（`docs/ecad2-gxworks3-uiux-survey-onmitsu.md`・`-part2.md`、恒久保存済み・memory
`ecad2_uiux_reference_surveys`参照）より抜粋・補足。

- GX Works3は「メニューバー→ツールバー→作業領域→ドッキングウィンドウ→ステータスバー」の
  クラシック構成で、**全ウィンドウがドッキング可能**（左にプロジェクトツリー、下部に出力/エラー
  パネル——ecad2の現行構成と意匠的に近い）。
- 技術構成：ネイティブC++（MFC/ATL）コアに.NET Framework（WinForms）プラグイン群をロードする
  ハイブリッド構成（WPFではない）。`WorkWindowPlugin`/`DialogPlugin`/`DockingWindowPlugin`/
  `CommandPlugin`/`FrameWindowPlugin`等100以上のプラグインDLLで構成。
- **ドッキング機能の実現手段は商用UIコンポーネント併用の可能性が高い**：`GXW3.exe.config`から
  Infragistics v13.1（`UltraWinToolbars`/`UltraWinStatusBar`）・DevExpress v10.2
  （`XtraGrid`/`XtraEditors`/`XtraLayout`）の併用が判明済み（本命調査書part2節、2節と同じ
  ベンダー）。どちらの製品がドッキング機能そのものを担っているかは未確認（Infragistics
  `UltraDockManager`かDevExpress `XtraLayout`系のいずれかと推測されるが、GXW3.exe.config単独では
  特定に至らず）。
- **示唆**：業界の先行製品（GX Works3）も自前実装ではなく商用UIコンポーネントに依存してドッキング
  機能を実現している。これは2節の「自前実装よりライブラリ導入」という所見を補強する参考事実。
  ただしGX Works3はWinForms、ecad2はWPFのため製品そのものの流用はできず、あくまで「業界の設計判断の
  参考」に留まる。

---

## 4. ecad2側 対象パネル候補の棚卸しと優先度叩き台

`MainWindow.xaml`実物確認（全650行）による現状構造。

| # | 対象 | 現状構造 | ドック化の実装難易度所感 |
|---|---|---|---|
| 1 | ツールバー1段目（新規/開く/保存/元に戻す/やり直し/PDF/行±） | `ToolBarTray Grid.Row="1"` 内`ToolBar Band="0"`、`IsLocked="True"` | 中。`IsLocked`解除＋AvalonDock`DockingManager`への差し替えが必要（既存Command配線はそのまま流用可） |
| 2 | ツールバー2段目（選択ツール/F5〜F10/部品、GX様式意匠） | 同`Band="1"` | 同上。ただし配置系コマンドは使用頻度が高く、フロート化してもキャンバス脇への係留運用が主になると想定 |
| 3 | 左パレット（シートナビゲーション） | `Border LeftPaletteArea`（`Grid.Column="0"`、幅220固定）+`DockPanel`+`ListBox`+ボタン4つ | **低**。単純な構造で自己完結、ドック化の第一候補になりうる |
| 4 | 右パネル（機器表/プロパティ⇔部品選択） | `Border RightPanelArea`（`Grid.Column="4"`、幅280固定）内で**既に上下2分割**（`GridSplitter`）＋下段は状況依存切替（プロパティ/部品選択、`IsPartSelectionVisible`） | **高**。「右パネル全体を1ペイン」とするか「機器表」「プロパティ」を独立ペインに割るかの粒度判断が必要。状況依存切替ロジック（T-026段階4-7）との整合も要設計 |
| 5 | 下部出力パネル（DesignRuleCheck結果） | `DockPanel OutputPanelArea`（`Grid.Row="4"`）+`DataGrid`、T-059でGridSplitterドラッグ調整対応済み | **低**。単純なDataGrid1つ、ドック化しやすい |

### 参考：既存の非モーダル浮動オーバーレイ実装（ElementPlacementBar）

`MainWindow.xaml:587-648`（T-033）は、Popupを使わず同一Window内オーバーレイとして配置後入力バーを
浮動表示する既存実装（`docs/archive/ecad2-t033-ui-automation-impact-survey-onmitsu.md`に設計判断根拠あり）。
これはAvalonDock導入前でも「同一ウィンドウ内での動的表示位置制御」の先行パターンとして参考になるが、
「別ウィンドウへの切り離し」を意味する今回のフロート化とは性質が異なる点に注意。

### 優先度叩き台（隠密所見、決定は殿）

実装難易度が低く独立性の高い**3（左パレット）・5（出力パネル）を先行候補**とし、1・2（ツールバー）は
殿の当初の問いの発端でありニーズが強い一方`ToolBarButtonStyle`等の既存意匠を保ったままの移行検証が
要る中間難度、4（右パネル）は既存の複雑な状況依存切替との整合設計が必要な最難関、という順で段階
導入するのが妥当と考える。ただし実際の優先順位は殿の実用上のニーズ（「何を動かしたいのか」）に
依存するため、上記はあくまで実装コスト観点の叩き台であり、決定は殿へ委ねる。

---

## 5. 総合所見・次の1手

1. **外部ライブラリ導入の採否**（AvalonDock推奨）は、CLAUDE.md依存関係変更ゲート・karo.md該当節に
   従い、改めて殿へ諮る必要がある【未実装】。
2. 採用が決まれば、導入前に**小規模PoC**（`Dirkster.AvalonDock`をNuGet導入し、`DockingManager`を
   1つ配置して.NET 8プロジェクトで警告なくビルド・動作するか実機確認）を推奨する。
3. 対象パネルの範囲・優先順位は4節の叩き台をもとに殿確認【MUST、UI/UX分岐】。
4. 本調査はスコープどおり調査のみで実装は行っていない。

---

## 出典
- `docs/todo.md` T-058節（起票背景・殿裁定経緯）
- `docs/ecad2-gxworks3-uiux-survey-onmitsu.md`／`-part2.md`（GX Works3構成・技術基盤）
- `src/Ecad2.App/MainWindow.xaml`（全650行、現状構造）
- Web調査（AvalonDock/Xceed/DevExpress/Telerik/Infragistics、出典URLは調査エージェント原文に列挙、
  主要一次情報：GitHub API `api.github.com/repos/Dirkster99/AvalonDock`、NuGet.org、各ベンダー公式）
