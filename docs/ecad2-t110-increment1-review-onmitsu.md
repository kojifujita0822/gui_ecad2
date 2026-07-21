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

---

# 追補2: (6)対策コミットe45d8b8のレビュー（2026-07-22、家老依頼の観点(a)(b)(c)）

## 結論（先出し）

**(a)=NG、差し戻し要**（対策が隠しているのは別のオーバーレイであり、残留した十字型の実体には
後始末が依然として存在しない——一次ソースで証明）。**(b)=OK**（計装残骸なし）。**(c)=OK**
（c83dc2aの2修正は妥当、軽微観察1点のみ）。修正は1行追加で足りる見込み（下記）。

## (a) 対策の適合性——標的違いを一次ソースで確定

**オーバーレイは2つある**ことがまず前提:
- **A**: `PlacementToolBarDropZoneOverlay`——ecad2自前の破線矩形+案内文言のGrid（T-103）
- **B**: AvalonDock標準の`OverlayWindow`——**十字型ドロップターゲット**（中央の十字ドッキング
  ボタン群を持つ独立Window、DragServiceがドラッグ中に表示）

忍者2.1の残留物は、記述特徴（「オーバーレイ中央のドッキングアイコンへの正確なクリック」を
試行=ボタンを持つのはBのみ）から**B**。しかしe45d8b8がCollapse化したのは**A**である。

**Bに後始末が存在しないことの一次ソース証明**（v4.74.1、scratchpad取得済み）:
1. Bを隠す正規経路は`DragService.Drop()`内の`_currentHost.HideOverlayWindow()`
   （`DragService.cs:218-219`）と`Abort()`内の同呼出（260行）の2つのみ。
2. Drop()の呼出元はAvalonDock自身のWM_EXITSIZEMOVEハンドラ（`LayoutFloatingWindowControl.cs:
   372-384`）——**T-103フックのhandled=trueが丸ごとスキップさせる当の対象**。
3. Abort()の呼出元はWM_LBUTTONUPフォールバック（同395-398行）だが、OSのモーダル移動ループ中は
   LBUTTONUPがループに消費されるため発火しない。
4. フロート窓が閉じる際の`OnClosed`（同517-531行）は`_dragService`を一切後始末**しない**
   （フック解除とContentのDisposeのみ）。ゆえにDock()成功→フロート窓クローズ後もBは表示された
   まま残る——「プロセス再起動でのみ解消」という忍者観測と完全に一致する。

∴ handled=true成立時にBを隠す処置はアプリ側にもAvalonDock側にも存在せず、**e45d8b8適用後も
残留バグは構造上そのまま再現する**（AのCollapse追加は無害だが、Aは従来からClosed経由で
畳まれており実質冗長）。

**推奨修正（1行+コメント）**: handled=true成立箇所（`Dock()`呼出の前）へ
`((AvalonDock.Controls.IOverlayWindowHost)MainDockingManager).HideOverlayWindow();`
を追加する。`IOverlayWindowHost.HideOverlayWindow()`はDockingManagerの明示インターフェース
実装（`DockingManager.cs:1429`）でキャスト経由で呼出可能。DragService.Drop()の実行順
（Hide→DragDrop）に忠実な位置となる。制約コメントを1つ——本呼出が隠すのはMainDockingManagerを
ホストとするBのみであり、将来「複数フロート窓が同時に存在し互いがドロップ先になる」構成に
なった場合は全ホストのHideが要る（現行構成=フロート可は配置ツールバー1窓のみ、では不要）。
e45d8b8のA Collapse追加は防御として残置でよい。

## (b) 診断ログ計装の除去漏れ——なし

`AppendDiagLog`/`DiagLog`/`ecad2-diag`のsrc/tests全域grep結果は4件のみ、いずれもT-110計装とは
無関係（`Diagnostics/TraceLog.cs:8`のT-038時代の経緯コメント1件+テスト基盤DLLのバイナリ偶然
一致3件）。c83dc2aで追加された計装（AppendDiagLog本体・DiagLogLock・DiagLogPath・全呼出17箇所）
はe45d8b8で完全に除去されている。**一時計装の横断洗い出し原則**（memory:
feedback_temp_instrumentation_removal_discipline）にも適合——T-038由来の`TraceLog.cs`は別系統の
恒久機構であり巻き込まれていない。

## (c) 累積修正の通し確認——整合、軽微観察1点

- **(2)起動時選択タブ修正**（c83dc2a、.cs:233-241）: Loaded後にContentIdベースで
  `IsActive=true`。インデックス依存を避けた方式選択は妥当、忍者再検証OK済みとも整合。
  **軽微観察**: `IsActive=true`は選択に加えペインのアクティブ化（青帯・フォーカス管理）も
  伴うため、起動直後は配置ツールバーがアクティブ色で立ち上がるはず。実害はない見込みだが、
  増分2の回帰観察項目（起動直後の各ペイン色）に含めることを推奨。
- **起動時読込退行の修正**（c83dc2a）: `if (savedXml is null) return;`復元を確認（.cs:500）。
  旧実装と同一の意味論に戻り、二段フォールバック構造も維持。指摘どおりの修正。
- **指1（e1c8f73）**・**A-3撤去**・**T-103再配線**との相互矛盾なし。ビルド/テスト全合格
  （侍申告792+131件）とも整合。

---

# 追補3: (6)3周目前の俯瞰評価——介入方式の転換提案（2026-07-22、モグラ叩きゲート対応）

## 自己訂正（先に申告）

追補2の推奨修正（`IOverlayWindowHost`キャスト経由の`HideOverlayWindow()`呼出）は
**コンパイル不能**（CS0122）だった。`IOverlayWindowHost`は`internal interface`
（`IOverlayWindowHost.cs:19`、一次ソース確認済み）であり、外部アセンブリからキャスト自体が
不可能。拙者はメソッドの存在（`DockingManager.cs:1429`）を確認したが**インターフェース自体の
アクセシビリティ確認を欠いた**。以後、外部APIの呼出推奨時は「存在」に加え「呼出可能性
（public性）」まで確認することを教訓とする。

## 俯瞰評価の結論: 「表示後に掃除する」枠を出て「そもそも表示させない」へ——**案D推奨**

2周の不発（1周目=対象取り違え、2周目=API到達不能）は、いずれも「B（OverlayWindow）を
表示させた後で消す」という同一の枠内の試行だった。枠自体を出るのが正解と評価する。

**案D: ルートLayoutPanelへ`CanDock="False"`を設定し、Bの表示自体を封じる**

```xml
<avalonDock:LayoutRoot>
    <avalonDock:LayoutPanel Orientation="Vertical" CanDock="False">
```

一次ソースによる裏付け（いずれもv4.74.1、scratchpad取得済み）:
1. `LayoutPanel.CanDock`は**公開DependencyProperty**（`LayoutPanel.cs:51-71`）、XAML設定可、
   **シリアライズ対応**（同82-101行、False時のみ属性書き出し+読み戻し——保存レイアウト・
   既定XML自己Serializeと自然に整合）。
2. 実行時の消費者は**`DragService.GetOverlayWindowHosts`のゲートただ1箇所**
   （`DragService.cs:282` `if (_manager.Layout.RootPanel.CanDock)`）。Falseならホスト列挙が
   空になり、`_currentHost`は常時null、`ShowOverlayWindow`は一度も呼ばれず**Bは生成すら
   されない**。掃除すべきものが最初から存在しなくなる。
   （GitHubコード検索で全用途を掃引済み——他のヒットは全て`CanDockAsTabbedDocument`の
   部分一致であり、`LayoutPanel.CanDock`の別の消費者は無い。）
3. ライブラリ自身のドキュメントコメント（`LayoutAnchorable.cs:147`）が「`CanMove`・`CanClose`と
   併せて`LayoutPanel.CanDock`を使え」とロックダウン用途を公認している——裁4
   （`CanFloat="False"`）・指1（`CanDockAsTabbedDocument="False"`）と同じ設計語彙の
   正規プロパティであり、**T-110増分1が既に採った封止方針の直系**。
4. 副作用の評価: 標準のドラッグ&ドロップドッキング（十字ターゲット・プレビュー）が
   全面的に効かなくなるが、これは**T-103の設計趣旨そのもの**（標準OverlayWindow/DropTargetの
   位置ズレバグを避け独自ドロップ枠で置き換える）。配置ツールバーの再ドック経路=メニューの
   「ドッキング」（`Dock()`はCanDockを参照しない）とT-103独自枠（同じく`Dock()`呼出）は
   いずれも無傷。T-103ゾーン外でドロップした場合はフロートのまま残る（現状の
   handled=false経路と同じ、清潔な挙動）。

**実装規模**: XAML属性1つ（+e45d8b8のA Collapse行は防御として残置でよい）。
**忍者確認観点**: (1)フロート後のドラッグで十字型が一切表示されないこと (2)ドラッグ再ドック
（T-103枠内ドロップ）とメニュー経由ドッキングが引き続き機能すること (3)保存/復元・Ctrl+Alt+R
往復でCanDock=Falseが維持されること（シリアライズ round-trip）。

## 却下案の評価（家老提示のA/B/C）

- **案A（リフレクションで`_overlayWindow`をClose()+null化）: 非推奨**。正規の
  `HideOverlayWindow`実装は実際には5操作（`_areas=null`／`Owner=null`／`HideDropTargets()`／
  `Close()`／`null`化、`DockingManager.cs:1429-1437`）であり、2操作だけの部分複製は`_areas`残留
  等の中途半端な状態を作る。内部実装詳細への依存はバージョン更新で静かに壊れる。
- **案B（`Application.Current.Windows`走査でClose()）: 侍の非推奨判断に同意・確定的根拠を追加**。
  `ShowOverlayWindow`は`CreateOverlayWindow`後に`_overlayWindow.Show()`を呼ぶ実装
  （1420-1426行）ゆえ、外部からCloseされた`_overlayWindow`キャッシュが残ると次回ドラッグで
  「Close済みWindowのShow」となり`InvalidOperationException`——侍懸念のクラッシュは推測でなく
  実装上確定的。
- **案C（handled=true設計の見直し）: 不要**。案Dが同じ効果（標準経路との衝突解消）を
  属性1つで達成する。handled=trueによるDrop()スキップ自体は、案DでBが存在しなくなれば
  「スキップして困る後始末」も消えるため、現行設計のまま無害。
- 参考・次善（案Dが実機で予期せぬ挙動を示した場合のみ）: **案F**=リフレクションでも
  フィールド手術でなく`Type.GetInterface("IOverlayWindowHost")`+InterfaceMapping経由で
  **ライブラリ自身の`HideOverlayWindow`メソッドを呼び出す**（正規5操作がそのまま実行され
  状態整合の再実装が不要、案Aより安全）。ただし依然internal依存ゆえ案D成立なら不要。

---

# 追補4: 案D実装コミット5123eb3のレビュー（2026-07-22）

## 結論（先出し）

**5123eb3自体は正**（`CanDock="False"`はルートLayoutPanel＝`DragService.cs:282`が読む
`Layout.RootPanel`当体に正しく配置、コメントも正確）。**ただし指2（必須・1行）を追加で要する**
——このままでは**次回起動時に修正が無音で無効化される**罠が既に発動可能な状態にある。

## 指2（必須）: 保存済みレイアウトXMLによる案Dの無効化——実ファイルで確認済み

- `%AppData%\Ecad2\docking-layout\main-layout.xml`が**既に存在する**（2026-07-22 03:24、
  忍者の切り分け実験（メニュー経由保存）の産物＝**5123eb3より前の保存**）。
- 実ファイル検分: `<RootPanel Orientation="Vertical">`——**CanDock属性なし**（grepの
  "CanDock"6ヒットは全て指1の`CanDockAsTabbedDocument`の部分一致）。
- `LayoutPanel.ReadXml`は属性が無ければ既定値`CanDock=true`のまま（`LayoutPanel.cs:97-101`）。
  起動時`LoadDockingLayoutFromFileIfExists`がこのファイルをDeserializeすると**Layout全体が
  置き換わり、XAMLの`CanDock="False"`は消滅**——案Dは無音で無効化され、忍者の最終確認は
  「十字型がまた出る」を観測する（3周目の偽再発を踏む）。さらに以後の保存はモデルの
  `CanDock=true`を焼き付け続け、恒久的に修正が効かない。
- **推奨修正（1行）**: `TryDeserializeDockingLayout`内、`Deserialize`成功直後へ
  `MainDockingManager.Layout.RootPanel.CanDock = false;`（`LayoutRoot.RootPanel`・
  `LayoutPanel.CanDock`とも公開プロパティ、コンパイル可能性確認済み）。
  呼出元3経路（起動時読込・Ctrl+Alt+R保存済み優先・同既定フォールバック）全てが本メソッドを
  通るため、防御は1箇所で足りる。既定XML（XAML自己Serialize産）は`CanDock="False"`属性を
  含む（WriteXmlはFalse時に書き出す）ため本質的リスクは外部永続化ファイルのみだが、
  コード強制なら旧版ファイル・手動編集・将来の版差全てに耐える（読込3層防御と同思想の
  第4の防御）。適用後は次回保存でファイル側も自然に正規化される。
- ※旧4ファイルと同様、既存main-layout.xmlの削除は不要（rm禁止・コード強制で無害化される）。

## 増分1全体の最終通し確認

累積5コミット（a78b802→e1c8f73→c83dc2a→e45d8b8→5123eb3）の整合を通しで再確認:
永続化単一化・裁1〜裁4・指1・(2)修正・読込ガード復元・A-3撤去・T-103再配線・案D、相互矛盾なし。
build/test全合格（侍申告923件）。**指2反映を条件に忍者最終実機確認へ進んでよい**。

## 忍者最終確認項目（指2反映後）

1. フロート後のドラッグで十字型OverlayWindowが一切表示されないこと（案Dの本旨）
2. ドラッグ再ドック（T-103枠内ドロップ）・メニュー経由ドッキングとも機能すること
3. **既存main-layout.xml（CanDock属性なし）が存在する状態で起動→ドラッグしても十字型が
   出ないこと**（指2の実証、最重要——現に03:24産の実ファイルがその条件を満たしている）
4. Ctrl+Alt+R・保存/復元の往復後も十字型不出現が維持されること（round-trip）
5. 保存後のmain-layout.xmlに`CanDock="False"`属性が書かれていること（正規化の確認）
6. 起動直後の配置ツールバーのアクティブ色（追補2(c)観察事項）
7. 増分1の主要回帰（アクティブ色一元化・起動時選択タブ・ElementPlacementBar位置）の
   スポット再確認

## 追補2出典

- コミット`e45d8b8`/`c83dc2a`全差分＋現物（.cs:226-251/500現況確認）
- AvalonDock v4.74.1一次ソース（scratchpad取得）: `DragService.cs:207-263`（Drop/Abort）・
  `LayoutFloatingWindowControl.cs:365-398/505-531`（WM_EXITSIZEMOVE/WM_LBUTTONUP/InternalClose/OnClosed）・
  `DockingManager.cs:44/1396-1439`（IOverlayWindowHost実装・HideOverlayWindow明示実装）
- `docs/ecad2-t110-increment1-verification-ninja.md`2.1（残留物の特徴記述）

## 出典

- コミット`a78b802`全差分＋現物（`src/Ecad2.App/MainWindow.xaml`/`MainWindow.xaml.cs`/
  `tests/Ecad2.App.Tests/T058Increment4LayoutFileNameTests.cs`、本文中に行番号）
- `docs/ecad2-t110-increment1-pretask-check-onmitsu.md`（突き合わせ台）
- AvalonDock v4.74.1一次ソース（scratchpad）: `LayoutAnchorable.cs:135-142,237-238,259`
  （CanDockAsTabbedDocument）・`Generic.xaml:404-560/536-540`
- `docs/ecad2-t110-poc-review-onmitsu.md`（軽1〜軽3・追補）・`docs/ecad2-t110-poc-verification-ninja.md`（2.3-2.5）
