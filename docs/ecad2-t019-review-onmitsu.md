# T-019 静的レビュー（隠密）

対象4コミット: 715e0e6(段階1 Document差し替え基盤) → 4b34585(段階2 保存) →
7cb7aa0(段階3 開く) → ece2525(段階4 新規+CurrentSheet nullable化防御)。
`code-review`スキル(medium、8観点finder→1-vote verify)を併用。

## 家老指定5観点への回答

| # | 観点 | 判定 |
|---|------|------|
| 1 | 旧インスタンスへのイベント購読解除漏れ | **該当なし**。`Ecad2.Core.Model`配下（LadderDocument/Sheet/DeviceTable）はイベント発行機構を持たない素朴なPOCOのため、購読残留・二重発火は原理的に起こらない。 |
| 2 | CurrentSheet nullable化のnull参照取りこぼし | 描画(RedrawCanvas)・ナビツリー(SheetNavigation)・HasProjectは適切に対応済み。**DRC出力パネルのみ対応漏れ**（下記#1参照、CONFIRMED）。 |
| 3 | Load/Save例外のMessageBox変換の網羅 | TrySaveToFile・OpenButton_Click双方で網羅されている。ただし`catch (Exception)`がI/O例外に限らずプログラミングバグ由来の例外も一律握り潰す点はPLAUSIBLE（下記#5）。 |
| 4 | Ctrl+N/O/Sと既存キーバインドの衝突 | 衝突なし。Window_PreviewKeyDownの既存ケース（F5-F8/矢印/Delete/Enter/Escape）とはCtrl修飾の有無で完全に分離。 |
| 5 | OnMenuRestartバグ相当の構造欠陥 | **該当なし**。New/Open/Saveはメニュー・ツールバー・Ctrl+N/O/Sいずれも`ReplaceDocument`という単一ゲートウェイに集約されており、GuiEcadの入口分散(再起動系だけDirty確認漏れ)は再現していない。 |

## code-reviewスキル所見（verify後）

| # | 判定 | 内容 |
|---|------|------|
| 1 | **CONFIRMED** | `ReplaceDocument`が`OutputPanelViewModel.Diagnostics`(DRC結果)・`SelectedDiagnostic`をクリアしない。旧文書でDRC実行済みの状態で新規/開くを実行すると、新文書に切り替わった後も古いDRC結果が出力パネルに残り続ける。その行をクリックすると`JumpTo`が新Document.Sheetsに対してPageNumber一致検索を行い、偶然一致すれば無関係な位置へ誤ジャンプ、不一致なら無反応（沈黙するため気づかれにくい）。 |
| 2 | **CONFIRMED（軽微）** | `ReplaceDocument`が`StatusMessage`をクリアしない。配置エラー等のメッセージ表示中に新規/開くを実行すると、新文書表示後もステータスバーに旧文書時点のメッセージが残る。 |
| 3 | PLAUSIBLE（中程度） | `ReplaceDocument`が`Tool`状態をリセットしない。配置ツール選択中（自作パーツ等）に新規/開くを実行すると`Tool`が残留し、新文書でセル選択→Enterを押すと前文書のツール種別が初期選択された配置ダイアログが開く。ただしOKボタンで確定するワンクッションがあるため、即誤配置には至らない。 |
| 4 | PLAUSIBLE（将来リスク） | `ReplaceDocument`内で`SelectedCell`のsetter（依存プロパティ通知カスケード持ち）をバイパスし`_selectedCell=null`と直接代入した上で、同じ通知を手書きで重複列挙している。現時点では内容は一致(食い違いなし)だが、将来setter側にのみ通知が追加されると`ReplaceDocument`側が追随せず、新規/開く直後だけ特定表示が更新されないバグを生みうる。 |
| 5 | PLAUSIBLE（許容範囲内寄り） | `TrySaveToFile`/`OpenButton_Click`の`catch (Exception)`はI/O例外に限らず全例外を「保存/読み込みエラー」に変換する。設計意図（隠密調査T-024節推奨）に基づく既定方針であり、UI層最外殻のcatch-allとして一般的な範囲内。プログラミングバグ由来の例外まで握り潰す点は理論上の懸念に留まる。 |
| 6 | REFUTED | 「Clear()後もDiagramRenderer.Geometryが古いシートの値を保持しヒットテストが誤動作する」という候補は誤り。Geometryはコンストラクタ時のCellMm/MarginMmのみで決まる不変値であり、かつClear()後はWidth/Height=0でヒットテスト自体が発生しない。 |

## 隠密所見

要修正候補として家老の判断を仰ぐべきは**#1(DRC残留)**。実際に誤ジャンプ・情報不整合を招きうる。
**#2(StatusMessage残留)**は軽微だが`ReplaceDocument`に1行足すだけで解消できるため合わせて対応が
望ましい。#3(Tool未リセット)はワンクッションあるため経過観察でも良いが、#1・#2と同じ
`ReplaceDocument`内の話なので併せて対応すると効率的。#4・#5は設計メモ、#6は却下。
