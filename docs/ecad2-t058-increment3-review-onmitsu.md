# T-058増分3（右パネルAvalonDock化）静的レビュー（隠密）

レビュー日: 2026-07-15
対象コミット: 02d8bad（実装）・7bdd56c（設計叩き台記録）
対象diff: `src/Ecad2.App/MainWindow.xaml`・`src/Ecad2.App/MainWindow.xaml.cs`（侍自己申告どおり2ファイルのみ、確認済み）
レビュー深度: 軽量既定（1周目、karo.md方針）
併用: code-reviewスキル（medium effort、8角度finder→3件verify）

## 総合判定

**設計叩き台どおりの実装であり、致命的欠陥なし。** ただし増分2から持ち越された既存の未修正欠陥が今回複製されている点が1件CONFIRMED。cleanup系の経過観察2件を付記する。

## (a) 台帳DoDとの整合

- 3つ目の独立DockingManager新設（案C踏襲）：確認OK。`RightPanelDockingManager`が`AllDockingManagers`（`MainWindow.xaml.cs:107`）へ追加され、`RegisterDockingContents`/`SerializeDefaultDockingLayouts`/`ResetDockingLayoutToDefault`の既存汎用実装がそのまま対応（改修不要、設計叩き台2.5節どおり）。
- ContentId命名`"DeviceTable"`/`"RightPanelBottom"`：既存`"LeftPalette"`/`"OutputPanel"`と非衝突を確認（grep、4件とも重複なし）。
- Orientation混在ネストなし：単一`Orientation="Vertical"`の`LayoutPanel`一つのみ、隠密指摘(3)の既知バグ（v4.72.0まで）には非該当。
- CanFloat/CanAutoHide：明示設定なし（増分2の既定方針を踏襲、事後救済＝Ctrl+Alt+R）。

## (b) code-reviewスキル併用（medium effort）

8角度finder（correctness系3・cleanup系3・altitude・conventions）を実行、3件をverify。

### 指摘1（CONFIRMED、severity中）: レイアウトリセット後のタイトル/内容不整合

`ResetDockingLayoutToDefault()`（`MainWindow.xaml.cs:217-234`）はデシリアライズ後に`UpdateOutputPanelTitle()`/`UpdateRightPanelBottomTitle()`を呼び直さない。既定レイアウトXMLは起動時に初期値のTitle（"プロパティ"/"出力"）で焼き付けられているため：

- 部品配置モード中（タイトル「部品選択」）にCtrl+Alt+Rを押すと、Titleは「プロパティ」に巻き戻るが、表示中身（`IsPartSelectionVisible`のVisibilityバインディング）は部品選択リストのまま変化しない → タイトルと中身が食い違う。

**重要**: これは今回の増分3が新規に持ち込んだバグではない。増分2（出力パネル、コミット79a60b2）の時点で`UpdateOutputPanelTitle()`を追加した際も`ResetDockingLayoutToDefault()`側は未対応のままで、既に同型の不整合（検索結果表示中にリセット→タイトルだけ「出力」に戻る）が存在していた。増分3はこの既存の穴をそのまま複製した形。

**パターンの疑いあり**：「新設した状態同期処理に、既存の横断的リセット処理（レイアウトリセット）が追従しない」という型は、パターン台帳PR-05（状態リセット処理の横展開漏れ、Document/Sheet構成変更時の責務追従漏れ）と対象領域は異なるが構造は類似する。台帳に完全一致する型はないため新規パターン候補としての提示に留める（断定はしない）。

対処するなら`ResetDockingLayoutToDefault()`のforeachループ後に`UpdateOutputPanelTitle()`・`UpdateRightPanelBottomTitle()`を呼び直すのが素直（増分2の既存欠陥も同時に解消できる）。

### 指摘2（CONFIRMED・cleanup、severity低）: Title同期メソッドの構造的重複

`UpdateOutputPanelTitle()`（増分2）と`UpdateRightPanelBottomTitle()`（増分3）が完全に同一形状（`Descendents().OfType<LayoutAnchorable>().FirstOrDefault(ContentId==...)` → null check → `.Title`条件代入）。ただし現時点で2箇所のみであり、既存運用基準（rule of three＝3箇所到達で正式パターン化）には未達。増分4・5の設計文書からは3箇所目が生まれる具体的根拠は読み取れない（推測の域を出ない）。**今すぐ抽出必須とは言えず、経過観察が妥当。**

### 指摘3（REFUTED寄り、severity低）: MinHeight制約の縮小

旧Grid実装の`MinHeight="80"`が新AvalonDock構造では`DockMinHeight`未設定。当初「ほぼ0まで縮小可能」という懸念を立てたが、AvalonDock（Dirkster.AvalonDock 4.74.1）のreflection実測で`LayoutAnchorablePane`の既定`DockMinHeight`/`DockMinWidth`が25pxであり、ドラッグ処理自体がこの値でクランプされていることを確認（無制限縮小ではない、REFUTED）。ただし意図値80px→既定値25pxへの縮小という軽微な差異は残る。25pxはタブヘッダー分でほぼ食い潰される可能性があり、実機での見え方は忍者確認事項に加えるのが望ましい（静的解析の範囲外）。

### その他（Efficiencyほか、severity低、経過観察のみ）

- `UpdateRightPanelBottomTitle()`が`IsPartSelectionVisible`変更のたびLayout全体を再走査（`RegisterDockingContents()`で取得済みのanchorable参照を保存せずContentのみ保存しているための二度手間）。増分2の既存パターンの踏襲であり今回新規の問題ではない。将来的にはanchorable自体をDictionaryキャッシュする設計が望ましいが、緊急性なし。
- `AllDockingManagers`（3要素のハードコード配列）・Title同期メソッドの今後の増殖懸念（Altitude角度）：増分4・5でDockingManagerがさらに増えた場合の一般化検討材料として記録のみ。

## (c) 狙い撃ち観点

- **§3のBinding罠の回避**：`UpdateRightPanelBottomTitle()`はコードビハインドでの`PropertyChanged`購読→`Title`直接更新方式を正しく踏襲。Binding使用箇所なし、罠には該当せず。横展開として妥当。
- **ContentId命名の非衝突**：確認済み（上記(a)参照）。
- **スコープ境界**：diffは`MainWindow.xaml`・`MainWindow.xaml.cs`の2ファイルのみ、侍自己申告と一致。範囲外変更なし。

## 総括・家老への申し送り

- 致命的指摘なし、1周目レビューとして経過観察でよい水準。
- **指摘1（リセット後タイトル不整合）は増分2からの既存欠陥の複製**である点を明記して報告する。今回のみの対応か、増分2側も遡って直すかは家老・侍の判断に委ねる。
- 指摘2・3は経過観察のみ、今回の修正は不要と判断する。

## 追記（2026-07-15）: 指摘1修正（コミット4aebaa6）のレビュー

`ResetDockingLayoutToDefault()`（`MainWindow.xaml.cs:217-238`）のforeachループ末尾（Deserialize完了後、`_viewModel.StatusMessage`代入の直前）へ`UpdateOutputPanelTitle()`・`UpdateRightPanelBottomTitle()`の呼び出しを追加。増分2（出力パネル）・増分3（右パネル）双方の不整合を同時に解消する対処であることを確認した。

- **狙い撃ち観点**：両メソッドとも呼び直されていることを確認（追加はforeachループの外、1回のみ）。両メソッドはnullチェック付き（anchorable未検出時は何もしない）で、デシリアライズ直後の新規`LayoutAnchorable`インスタンスに対しても`ContentId`一致で正しく辿れる設計（XMLシリアライズ/デシリアライズで`ContentId`属性は保持される）。他の副作用（`_viewModel.StatusMessage`設定順序等）に変化なし。
- **スコープ境界**：修正は`ResetDockingLayoutToDefault()`メソッド内に閉じている。範囲外の変更なし。
- **RED証明省略の妥当性**：侍説明どおり、`ResetDockingLayoutToDefault()`はAvalonDockの実`DockingManager`インスタンス・`XmlLayoutSerializer`に依存するView層コードビハインドであり、既存の`UpdateOutputPanelTitle`/`UpdateRightPanelBottomTitle`自体も同じ理由でユニットテスト対象外（既存のテスト資産もViewModel層中心）。RED証明不可という説明は妥当と判断する。

**判定：問題なし、クリーン。忍者の実機確認（検索結果表示中のケース含む）を待つのみ。**
