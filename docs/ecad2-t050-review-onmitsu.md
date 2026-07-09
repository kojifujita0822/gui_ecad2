# T-050 静的レビュー（隠密）

対象コミット: `3190226`（TraceLogの全角ラテン文字正規化統一と旧値null化漏れ修正）
対象ファイル: `AssemblyInfo.cs`／`Diagnostics/TraceLog.cs`／`ViewModels/DeviceTableViewModel.cs`／`ViewModels/SheetNavigationViewModel.cs`／新規`tests/Ecad2.App.Tests/TraceLogTests.cs`（5ファイル、54行追加15行削除）

## 結論

**軽微な修正を推奨2件、経過観察向け新規気づき1件。コミット自体の目的（P-014/P-015対応）は達成されている。**

---

## 観点1: P-014側RED証明とテスト内容の整合・FormKC正規化の副作用範囲

**整合性は妥当。** `TraceLogTests.cs`の8ケース（全角ラテン小文字/大文字・全角数字・半角非変換・空文字）は全角ラテン文字の経路を正確に突いている。FormKC正規化の副作用範囲（合成文字・丸囲み数字等への広い作用）はDisableEnvValuesとの完全一致判定という利用文脈では実害なしと判断（意図した方向＝より多くの入力を正しく無効化リストに一致させる、へ働く）。

**ただし新規リスクを1件発見（要修正候補）**：`string.Normalize(NormalizationForm.FormKC)`はUTF-16の不対サロゲートを含む不正な文字列に対し`ArgumentException`を投げる（Angle A・B独立発見・実測で確認済み）。旧`NormalizeFullWidthDigits`（char単位ループ）はこの入力でも例外を投げなかった。`TraceLog.Initialize()`内のこの呼び出しはtry/catch非保護（`TraceLog.cs`内の他の全メソッドWrite/LogPropertyChangedは「トレースログの失敗が本来の処理を道連れにしてはならない」という自己言明のベストエフォート方針でtry/catch隔離されているのに、ここだけ不変条件が破れている）。

検証の結果、`App.xaml.cs`の`DispatcherUnhandledException`購読は`TraceLog.Initialize()`呼び出しより前に配置済み（過去レビューfinding2対応）で、実測でも例外は捕捉されると確認。ただし捕捉後も`base.OnStartup`（MainWindow生成）へは戻らず、MessageBox表示後にMainWindowが生成されない「見えないプロセス残留」という別種の起動失敗に至る。到達経路（`ECAD2_TRACE_LOG`環境変数への不対サロゲート混入）は通常操作では非到達、悪意/異常データ経由のみで可能性は低い。**判定: PLAUSIBLE。** 対処は容易（`NormalizeFullWidth`呼び出し箇所をtry/catchで囲みベストエフォート化するだけ）で、既存のTraceLog.cs内の設計一貫性のためにも修正を推奨。

## 観点2: P-015側「RED証明不可」申告の妥当性

**妥当と判断。** `ViewModelBase.OnPropertyChanged(string, object?)`オーバーロードが渡す`oldValue`は`TraceLog.LogPropertyChanged`のログ出力にのみ現れ、`PropertyChangedEventArgs`（標準.NET、`PropertyName`のみ保持）やUIバインディングには一切影響しない。ゆえに通常のPropertyChangedイベント購読によるユニットテストでは検出不可能。`TraceLog`はinternal staticクラスで`IsEnabled`もInitialize経由でしか変更できずモック困難、テストのために実ファイル書き込み（%TEMP%\ecad2-trace.log）を伴う統合テスト化も望ましくない。既存の同型finding3修正（自動テスト無し・隠密静的レビューのみで検証）と一貫した扱い。

## 観点3: 旧値null化修正2箇所がP-015原所見と過不足なく一致するか

**過不足なく一致。** `docs/proposed.md`のP-015原文は「兄弟2ファイル（`DeviceTableViewModel.cs`の`Refresh()`、`SheetNavigationViewModel.cs`の`SelectedSheet`setter）」と明記しており、T-050の修正範囲と完全一致。コミットメッセージもP-015の文言を忠実に踏襲。

**ただしP-015対応自体に新規の実バグを1件発見（CONFIRMED）**：`SelectedSheet`セッタで`AddCommand`経由のSheets数0→1遷移（起動直後の空プロジェクトへ最初のシートを追加する操作）において、`Sheets.Add(sheet)`が`SelectedSheet = sheet`の代入（`_dispatcher.BeginInvoke`による遅延実行）より先に同期完了しているため、セッタ内`oldValue = SelectedSheet`（getter呼び出し）の時点でgetterは既に追加済みの新シート自身を返し、`oldValue == value`（old==new）になる。実行順序・getter実装（`CurrentSheetIndex`既定値0のまま不変）から確定的に立証。実害は`TraceLog`診断ログ上で「無選択→初回選択」という意味のある変化が「変化なし」に見えることに限定（PropertyChangedEventArgs自体・UI表示には影響なし）。`SheetNavigationViewModelTests.cs`の既存テストは全て`NewDocument()`（1シート入り）起点のため、この0→1遷移パスは未カバー。**T-050の目的（旧値の正確な捕捉）が、まさにこの経路で機能していない**点は今後の診断ログ調査を誤誘導しうるため、修正を推奨（優先度中、既定OFFのオプトイン機能限定）。

## 観点4: InternalsVisibleTo追加の妥当性

**妥当、露出拡大の実害なし。** `TraceLogTests.cs`のみが恩恵を受けており（`TraceLog`がinternal staticのため`typeof(TraceLog)`自体の型解決にIVTが必要、既存の`MapToDeviceClass`等のリフレクションテストは対象クラスがpublicのためIVT不要という差異は正しい説明）、他の既存テストによる悪用は確認されず。副作用として`Ecad2.App`の全internalメンバー（`LadderCanvas`のHitTest系等）がテストプロジェクトから見えるようになる点は将来の実装詳細結合の呼び水になりうるが、現時点では設計上の注意点に留まる（findingとしては見送り）。

---

## code-reviewスキル（medium、8角度）で新規発見・生存した所見

| 所見 | 判定 | 対処 |
|---|---|---|
| `NormalizeFullWidth`の`ArgumentException`未保護（観点1参照） | PLAUSIBLE | try/catch追加を推奨 |
| `SelectedSheet`セッタold==newバグ（観点3参照） | CONFIRMED | 修正を推奨（優先度中） |
| `SheetNavigationViewModel.cs`内に1引数版`OnPropertyChanged(nameof(SelectedSheet))`が3箇所残存（`RefreshSelectedSheet()`49行目=高頻度経路、`ResetSheets()`56行目、`DeleteCommand`122行目=旧値取得コスト実質ゼロ） | PLAUSIBLE | T-050のスコープ外（P-015原文に含まれず）。新規気づきとして`docs/proposed.md`への起票を提案 |
| 旧値退避パターンのヘルパー化提案（8→9箇所） | REFUTED | 不採用。捕捉が条件付き（TraceLog.IsEnabled分岐によるボクシング回避最適化）・mutate部が非均質で、ヘルパー化はむしろ可読性を下げる。KISS方針にも反する |

---

## 不明点

なし（各判定の根拠は実測・行番号付きで確認済み）。

## 派生提案の有無

あり——`SheetNavigationViewModel.cs`の3箇所（`RefreshSelectedSheet()`・`ResetSheets()`・`DeleteCommand`）の旧値null化残存を、P-015の兄弟課題として新規P番号で`docs/proposed.md`へ起票することを提案。特に`DeleteCommand`はローカル変数`sheet`が既に手元にあり対処コストが実質ゼロ。着手判断は家老・殿に委ねる。
