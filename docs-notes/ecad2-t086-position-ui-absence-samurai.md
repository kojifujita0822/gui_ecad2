# T-086調査メモ: Position設定UI不在の裏付け(侍、2026-07-14)

T-061 A-1診断ログ調査(SS1のトグル不審挙動)の過程で得た副産物。T-086の実装状況の裏付け資料として記す。

## 確認事実

1. サンプル図面`sample\basic-test.gcad`のSS1要素2つ(id=ed3cdff3.../id=70b6df42..., row3col1・row5col1)は
   いずれも`"params": {}`——ノッチ`Position`値が未設定。
2. コードベース全体をgrep(`ParamKeys.Position`)した結果、参照箇所は以下の読み取り専用2箇所のみ:
   - `MainWindowViewModel.cs` `CycleSelectSwitch`(ノッチ列挙・巡回)
   - `NetlistBuilder.cs`(`switchPos`読み取り、Component化)
   Position値を書き込む・設定するUI(プロパティパネル・右クリックメニュー等)はApp層に一切なし。
3. 帰結: `CycleSelectSwitch`の`positions`列挙は常に空集合となり、`ToggleInput`フォールバック
   (GuiEcad由来の安全弁)に必ず落ちる。複数ノッチ切替の本来機能は一度も実際に使われたことが無い。

## 未解明点(留保、T-086調査へ引き継ぎ)

`ToggleInput`は`SimState.Inputs`のみ変更し`SimState.Positions`には触れないため、
`Evaluator.IsConducting`のSelectSwitch判定(`s.Positions.TryGetValue(c.DeviceName, ...)`失敗→false)は
理論上「常に非導通」になるはず。しかし忍者実機確認では「自動/手動2接点が同時に導通する」との目視
報告があり、理論と矛盾する。選択ハイライト(青、MouseUpフォールスルーバグ由来)と通電ハイライトの
見間違いの可能性が高いが未確認。T-086調査時、Position設定UI新設後の実機確認で併せて解消されたい。

## 参照

- [[ecad2_setproperty_early_return_trap]]とは無関係の別種の欠落(値の書き込み経路自体が存在しない)
- 診断ログ本体・SS1 MouseUpガード修正の経緯は`docs-notes/handover-next-session.md`(侍セクション)参照
