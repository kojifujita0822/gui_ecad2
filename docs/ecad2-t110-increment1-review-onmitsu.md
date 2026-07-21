# T-110 増分1（骨格統合）静的レビュー（隠密）

レビュー日: 2026-07-22　担当: 隠密　委任元: 家老
対象: コミット`a78b802`（MainWindow.xaml 724行変更/MainWindow.xaml.cs 293行変更/テスト1本、
359行追加683行削除）。レビュー深度: フル観点。突き合わせ台:
`docs/ecad2-t110-increment1-pretask-check-onmitsu.md`（A〜F全項目と1対1で照合）

## 結論（先出し）

**指摘1件（指1=F-3の即時封止、侍へ小差し戻し推奨）を除き全項目合格**。家老ご懸念のB-2
（期待集合へのキャンバスContentId）は**問題なし**——侍はLayoutDocument走査を実装済みで、
"Canvas"は期待集合に自然に含まれる。PR-21全数確認も合格（統合スタイル2本とも全Setter完備）。
指1の反映（属性6箇所の追加、機械的変更）を経て忍者実機確認へ進むことを推奨する。

## 1. 着手前チェックとの1対1突き合わせ結果

| 項目 | 判定 | 根拠（現物確認箇所） |
|---|---|---|
| A-1 案1トポロジ | OK | MainWindow.xaml:735-1340（縦: ツールバー123/横: 220+Doc+280縦2分割/出力160） |
| A-2 ShowHeader方式 | OK | 1091行`LayoutDocumentPane ShowHeader="False"`（属性1つ、テンプレコピー無し。「ShowHeaderはPane側の公開プロパティ」の注意コメントも正確） |
| A-3 案Y撤去 | OK | ContentDocking購読・ResetPlacementToolBarLayoutToDefault消滅、孤立参照なし（.cs:255-261コメント）。T-103ヒットテスト成立時も標準`Dock()`へ統一（.cs:336-341、候補aと一貫） |
| A-4 CanFloat封止 | OK | LeftPalette:1049/DeviceTable:1184/RightPanelBottom:1203/OutputPanel:1334の4箇所+MainToolBar:761。PlacementToolBar:852はフロート可維持（裁4どおり） |
| A-5 Items.Count==1トリガー | OK | 321-325行（一次ソースGeneric.xaml:536-540と一致） |
| A-6/E-3 ContentId分岐 | OK（残置判断妥当） | 373-381行コメントに根拠明記。トリガーは**案E忠実形**（ContentId AND `IsDirectlyHostedInFloatingWindow=False`のMultiDataTrigger、627-634行相当）——PoCレビュー軽1指摘の採用を確認 |
| B-1 単一ファイル名 | OK | `DockingLayoutFileName="main-layout.xml"`定数（.cs:131）、旧4ファイル放置の方針コメントあり |
| **B-2 期待集合にCanvas** | **OK（家老懸念は解消）** | `RegisterDockingContents`（.cs:389-403）が`OfType<LayoutAnchorable>`に加え**`OfType<LayoutDocument>`も走査**し、`expectedIds.Add(document.ContentId)`（400行）で"Canvas"を含める。`HasExpectedContent`（410-417）も`OfType<LayoutContent>`で両者を包含 |
| B-3 ループ単一化 | OK | 全永続化メソッドが`MainDockingManager`直接参照へ縮退 |
| B-4 Ctrl+Alt+R/S | OK | クラスハンドラ（.cs:144-164）無変更 |
| B-5 タイトル動的更新 | OK | 検索先MainDockingManagerへ切替（.cs:365-384）、リセット後再呼出維持（517-518） |
| C-1 T-103フィルタ・再配線 | OK | `isPlacementToolBar`フィルタ（.cs:289-290）が全ペインフロート発火への防御として機能。オーバーレイはManager全体へ重ねる形へ変更（XAML:1395-1412、意味変化は仮実装として許容の旨コメント——観2参照） |
| C-2 T-104タブナビ | OK | LayoutAnchorSideControl暗黙スタイル（Manager.Resources内）+`DisableFocusOnAutoHideSideItemsControl`4方向（.cs:226-241） |
| C-3 テーマ適用単一化 | OK | `ApplyDockingManagerThemes`（.cs:737-749）、統合タイトルスタイル登録、出し分け分岐撤去 |
| C-4 座標変換 | OK | `PositionPlacementBar`のクランプ基準を`CanvasDocumentGrid`へ変更（.cs:3336-3361、`TranslatePoint`でRootLayoutGrid座標へ変換） |
| C-5 FindBar内包 | OK | CanvasDocumentGrid内（XAML:1093-1132） |
| C-6 IsMainContentEnabled | OK | MainContentAreaラッパー（Row0-1、Auto/*）が単一Manager+メニューを包含、配置バー・ステータスバーは外（殿裁定どおり） |
| D-1/D-2 PR-21全数確認 | **合格** | 下記§2 |
| E-1 起動時選択タブ | 未対応（申し送りどおり） | 忍者確認項目へ（§4） |
| E-2 クリック基準 | 忍者確認項目へ | §4 |
| F-1 テスト追随 | OK | `T058Increment4LayoutFileNameTests`を定数検証へ書き換え（旧switch式ロジック自体が消滅のため妥当）。build/test全合格（侍申告131+792件） |
| F-3 DockAsDocument封止 | **指1（下記§3）** | 一次ソース確認により「実装困難でない」と確定したため、忍者送りでなく増分1内での封止を推奨 |

## 2. PR-21全数確認（D節、レビュー最重点）

- **UnifiedAnchorablePaneControlStyle**（201-343行）: Style本体Setter=Foreground/Background/
  TabStripPlacement/Template/ItemContainerStyle/**ItemTemplate**（329-335）/**ContentTemplate**
  （336-342、`LayoutAnchorableControl`ラップ）——**全Setter完備**。増分0(f)で露見した
  ContentTemplate欠落型の再発なし。
- **UnifiedAnchorablePaneTitleStyle**（382-643行）: 旧`AnchorablePaneTitleNoDragHandleStyle`の
  完全コピー（Style本体Setter2本・IsAutoHidden/CanClose/IsActive系に加えホバー/押下時の
  ボタン色トリガー群まで、旧スタイルの全トリガーを維持）+旧`PlacementToolBarAnchorablePaneTitleStyle`
  の案Eトリガーを**忠実形**（ContentId AND ドッキング時のみ）で統合——**旧2スタイルの和集合が
  漏れなく引き継がれている**（D-2合格）。
- 旧スタイルキー（NoDragHandleStyle/PlacementToolBar*Style）・旧Manager x:Name
  （LeftPalette/RightPanel/OutputPanel*）の残骸参照ゼロ（grep確認。"PlacementToolBarDockingManager"
  の2出現は経緯コメント内のみ）。

## 3. 指摘

### 指1（推奨・侍へ小差し戻し）: DockAsDocument経路の封止は属性1つで可能——増分1内での対応を推奨

F-3（着手前チェック）は「封止可否は一次ソース確認要、実装困難なら忍者送り」としていたが、
確認の結果**実装は容易**と確定した:
- `LayoutAnchorable.CanDockAsTabbedDocument`は**v4.74.1に実在する公開プロパティ**
  （`LayoutAnchorable.cs:135-142`、変更通知・シリアライズ対応(237-238行)・259行で
  コマンド抑止に使用——ライブラリ正規の封止機構）。
- 放置した場合のリスク（静的分析による指摘、実機未確認）: `ShowHeader="False"`の文書ペインへ
  アンカラブルを「タブ付きドキュメントとしてドッキング」（タイトルバーのメニューから全ペインで
  到達可能）すると、**タブUIが見えない文書ペイン内で選択が新規文書へ移り、キャンバスがマウス
  操作では戻せない形で隠れる**恐れがある（Ctrl+Alt+Rで復旧可能だが、殿の通常操作圏内にある罠）。
- **推奨対応**: 全6アンカラブル（MainToolBar/PlacementToolBar/LeftPalette/DeviceTable/
  RightPanelBottom/OutputPanel）へ`CanDockAsTabbedDocument="False"`を追加（機械的な属性追加のみ、
  ロジック変更なし）。反映後の忍者確認で「メニューに『タブ付きドキュメントとして
  ドッキング』項目が出ない/実行できない」ことを確認項目へ。

## 4. 軽微所見（修正不要、忍者確認・増分先送り）

- 観1: A-6残置コメントの根拠記述「ドッキング中はタブUIがホストしAnchorablePaneTitle自体は
  使われず」は、旧実装で配置ツールバー上段に帯（AnchorablePaneTitle）が表示されていた実績
  （T-099帯・T-100ハッチング対応の対象そのもの）と食い違う可能性がある。挙動の正否は実機でしか
  確定できないため、忍者確認項目「配置ツールバー上段の帯の有無とラベル非表示の実効」で観察する
  （どちらに転んでも実害はない——帯が出るならラベルは案Eどおり隠れ、出ないなら分岐は単に
  発火しないだけ）。
- 観2: T-103ドロップ枠の判定範囲がManager全体へ拡大（旧: ツールバー段の空き領域のみ）。
  「どこへ落としても元位置へ戻る」方向の変化で、仮実装スコープとして許容とのコメントあり——
  忍者確認後の家老報告時に**殿への周知事項**として一言添えるのが望ましい（UI/UXの体感変化のため）。
- 観3: 上段ツールバーペインは旧Auto高さ（内容フィット）から固定`DockHeight="123"`へ変化。
  実ツールバー（PoCのダミーより内容が多い）で欠け・余白過多が無いかは実機確認事項。

## 5. 忍者実機確認項目（指1反映後に実施）

前提: セカンドモニタ・画素採取判定・PrintWindow色不正確性への注意（既存MUST群）。

1. 起動・全ペイン表示（シート/キャンバス/機器表/プロパティ/出力/ツールバー2タブ）・
   ドキュメントタブが出ていないこと（裁2）
2. **起動直後の選択タブが「配置ツール」か**（E-1、PoCでは「基本機能」になった実績あり——
   NGなら侍へ差し戻し、Loaded後設定等の対処）
3. 上段ツールバーの高さ（観3: ボタン欠け・過大余白なし）
4. アクティブ色一元化: 各ペインの**中身**（セル/行クリック基準、E-2）を順にクリックし
   青帯が常に1つ（T-110発端の本実装での実証、画素採取）
5. ペイン境界リサイズ（AvalonDock内蔵リサイザーの操作感、旧GridSplitterとの差の所見)
6. 配置ツールバーのFloat→Dock往復（メニュー経由+T-103ドラッグ&ドロップ枠経由の両方、
   2〜3周、タブ自己複製・縦長化・空白化なし）——**本実装での候補a最終実証**
7. 観1: 配置ツールバー上段の帯の有無・ラベル非表示の実効
8. 観2: ドロップ枠の表示範囲と「元位置へ戻る」挙動
9. レイアウト保存（Ctrl+Alt+S/終了時）→再起動復元、Ctrl+Alt+R（保存済み優先→既定）、
   旧4ファイル環境からの初回起動で「レイアウトを既定の状態に更新しました」系の動作
10. ダークモード切替（タブ色・タイトル帯色・キャンバス、画素採取）
11. Tab巡回・AutoHide（ピン留め収納・復帰、サイド領域が全域に及ぶ見え方）
12. ElementPlacementBar表示位置（CanvasDocumentGrid基準クランプ、右端・下端はみ出しなし）
13. 指1反映後: 「タブ付きドキュメントとしてドッキング」がメニューから消えている/実行不能なこと
14. 検索バー（FindBar）表示・出力パネル「検索結果」タイトル切替・部品選択切替（B-5回帰）

## 出典

- コミット`a78b802`全差分＋現物（`src/Ecad2.App/MainWindow.xaml`/`MainWindow.xaml.cs`/
  `tests/Ecad2.App.Tests/T058Increment4LayoutFileNameTests.cs`、本文中に行番号）
- `docs/ecad2-t110-increment1-pretask-check-onmitsu.md`（突き合わせ台）
- AvalonDock v4.74.1一次ソース（scratchpad）: `LayoutAnchorable.cs:135-142,237-238,259`
  （CanDockAsTabbedDocument）・`Generic.xaml:404-560/536-540`
- `docs/ecad2-t110-poc-review-onmitsu.md`（軽1〜軽3・追補）・`docs/ecad2-t110-poc-verification-ninja.md`（2.3-2.5）
