# T-033 増分1 PoC — 非モーダル浮動インラインバーのフォーカス制御検証

作成: 2026-07-07（侍）。家老采配「T-033増分1(非モーダル化の骨格)のPoC先行」に対する成果。

## 目的

現行`ElementPlacementDialog`（モーダル`Window`、`ShowDialog()`）を非モーダル化した場合に、
(1)TextBoxフォーカス中のEnter確定/Esc取消（IsDefault/IsCancel）が機能するか、
(2)バーを閉じた後のキャンバスへのフォーカス復帰が確実に起きるか、
(3)バー表示中に他のグローバルショートカット（F5相当）が誤って反応しないか、
を本実装（src/）に入る前に最小構成で検証する。GuiEcad(WinUI3)がキーボード配置で9ラウンド格闘した
教訓（`docs/ecad2-keyboard-requirements.md`）とT-021のフォーカス制御モグラ叩き前歴を踏まえる。

## 技術選定の経緯

当初Popupで着手したが、隠密のUIA影響調査（`docs/ecad2-t033-ui-automation-impact-survey-onmitsu.md`）
でPopup特有のリスク（別HWND化によるWin32フォーカス到達の懸念）を指摘されたため、**同一Window内
オーバーレイ（Grid直下にBorder+Visibility+Margin位置決め）へ切り替えた**。現在のソースはこの
オーバーレイ版のみ（Popup版のコードは上書き済み、経緯は`docs/ecad2-t033-poc-result-samurai.md`参照）。

## 検証シナリオ

- **自動ループ×3(OK確定)**: 選択セルEnter→配置→非モーダルバー表示→機器名入力→Enter確定
  （IsDefault経由）→キャンバスへフォーカス復帰→続けて矢印移動、を3回連続で回す。
  比較条件はバーを閉じた後の`Keyboard.Focus(canvas)`明示復帰の「あり」「なし」。
- **Escキャンセル確認**: バー表示中にEscで取消（IsCancel経由）→要素が生成されないか
  （原子的取消）→フォーカス復帰を確認。
- **バー表示中F5無反応確認**: バー表示中にF5（グローバルショートカット代表）を押しても
  反応しないか（家老の仮既定＝モーダル同等に効かない）を確認。

## 実行方法

- **手動（GUI）**: `dotnet run --project T033InlineBarPoc` で起動。
- **無人（自動判定）**: `dotnet run --project T033InlineBarPoc -- --auto --out <ログ出力先>`。

## 結果（2026-07-07 侍の自動実行、`poc-result.txt`参照）

| 検証項目 | 結果 | 判定 |
|---|---|---|
| IsDefault(Enter確定)/IsCancel(Esc取消) | 3/3・Esc発火とも成立 | PASS |
| バー閉鎖後のフォーカス復帰 | 明示`Keyboard.Focus(Canvas)`ありなら成立、暗黙委譲では戻らない | 明示復帰必須 |
| バー表示中F5無反応 | **反応してしまった**（仮既定と相違） | 要注意・殿確認事項 |

## 考察（侍。「確認事実」と「推測」を区別）

- **確認事実**: 同一Window内オーバーレイでは、IsDefault/IsCancelが既存Windowと同一の
  `AccessKeyManager`機構にそのまま乗るため無変更で機能した。
- **確認事実**: フォーカス復帰はT-021と同じ教訓どおり、暗黙委譲に頼らず明示的な呼び出しが必須。
- **確認事実**: モーダルWindowが持つ「背後への入力ブロック」機構は非モーダル化で失われるため、
  グローバルショートカットは実装側で明示的にガードしない限りバー表示中でも素通しで発火する。
- **限界**: 本PoCの自動検証はWPFイベント注入（`InputManager.ProcessInput`）ベースの補助計器であり、
  実際のUI Automation（`SendKeys.SendWait`・`InvokePattern.Invoke()`）経由での到達性そのものの
  実測ではない。**最終判定は増分1の本実装後、忍者の`ecad2-ui-automation`経由の実機確認が必要。**

## 侍の推奨（本実装への引き継ぎ案）

1. 技術選定は同一Window内オーバーレイで確定。OK/キャンセルはClickハンドラ一本のまま維持する
   （隠密推奨、T-021型のPreview系マウス/キーボード分離は適用しない）。
2. バーを閉じた直後、明示的にキャンバスへフォーカス復帰する処理を必須で実装する。
3. **バー表示中のグローバルショートカット無効化ガードが必要と判明。仕様（全部止めるか一部許すか）
   は殿の確認を仰いでから実装する**（詳細は`docs/ecad2-t033-poc-result-samurai.md`）。
4. 本PoCは`poc/`に隔離したまま残す（本体src/とは分離）。

## 構成

- `T033InlineBarPoc/PocCanvas.cs` — 本体LadderCanvasのフォーカス機構・CellRectDipを最小模倣したキャンバス
- `T033InlineBarPoc/MainWindow.xaml(.cs)` — 非モーダルバー（同一Window内オーバーレイ）＋
  フォーカス所在計器＋自動判定（OK確定ループ・Escキャンセル・グローバルショートカット確認）
