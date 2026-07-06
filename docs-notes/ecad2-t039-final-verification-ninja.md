# T-039 操作トレースログ基盤 実機確認記録（忍者）

検証日: 2026-07-06
対象コミット: main 464cd92（隠密再々レビューCLEAN後、往復2周完了）
検証環境: Ecad2.App（`dotnet run --project src/Ecad2.App`）、セカンドモニタにて実施

## 結論

観点1〜4すべてOK。回帰なし。旧CRITICAL（ログ書込失敗が本来処理を巻き込む問題）は
実機再現テストで解消を確認した。範囲外の気づき2件あり（下記）。

## 観点別結果

### 観点1: 起動スイッチ2系統 — OK

- `--trace-log` 引数: 正常起動、`%TEMP%\ecad2-trace.log` にセッション区切り行
  （`==== session start ... ====`）を確認。
- `ECAD2_TRACE_LOG=1` 環境変数: 同様に正常動作。

### 観点3: 既定OFF時の無生成・無副作用 — OK

フラグ・環境変数なしで起動し、新規作成・ツール切替（a接点配置↔選択ツール）・
キャンバスクリック・要素配置ダイアログの開閉を一通り実施。`ecad2-trace.log` は
生成されず、動作にも異常なし。

### 観点2: フォーカス/Binding(旧値)/Click/ツール切替のログ突合 — OK

実際のログ抜粋（ツール切替、`docs-notes/.../t039-trace-scenario2-devicename.log` に保存）:

```
event=Click element="a接点配置 (F5)" type="Button"
event=PropertyChanged source="MainWindowViewModel" property="Tool"
  old="ToolState { Mode = Select, ... }" new="ToolState { Mode = PlaceElement, ... }"
```

**隠密指摘MEDIUM-HIGH（カスタムsetter系で旧値が常にnull）の実機確認**——配置済み要素
のデバイス名をプロパティパネルで "Y002" → "Y099" に変更したところ:

```
event=PropertyChanged source="MainWindowViewModel" property="SelectedElementDeviceName"
  old="Y002" new="Y099"
```

旧値が正確に記録されており、侍の対応（ViewModelBase.OnPropertyChangedオーバーロード
経由の明示的な旧値渡し）が実機で機能していることを確認した。

フォーカス遷移（ボタン→キャンバス、ダイアログ→TextBox等）もWPFの実際の遷移順どおりに
正確に記録されていた。

### 観点4: 複数インスタンス同時起動時の頑健性 — OK（CRITICAL解消の直接確認）

2つのEcad2.Appプロセスを同時起動し、高頻度で交互にツール切替操作を実施——両インスタンス
のイベントがミリ秒単位で密接するタイミングでも双方正常動作。

さらに踏み込んで、**ログファイルを別プロセスから完全排他ロック（FileShare.None）した
状態**で両インスタンスに対しツール切替操作（コイル配置・端子台配置ボタンのInvoke）を
実施:

- 両インスタンスとも Click 実行・Tool の PropertyChanged 発火・ステータスバー表示更新が
  すべて正常に完了（UIフリーズなし）。
- ロック前後でログファイルの行数は 637 → 637 のまま変化なし。つまりロック中に書き込む
  はずだった該当イベント（Click/PropertyChanged/フォーカス遷移、本来7〜8行相当）は
  ファイル共有違反により静かに欠落した。

これは「ログ書込失敗時はベストエフォードで欠落するのみで、本来の処理
（PropertyChanged発火・T-036修正・Click/Command実行）を一切巻き込まない」という
CRITICAL対策の設計意図どおりの動作を、実際にファイル共有違反を発生させて直接実証した
結果である（旧実装ならここでUIごと機能不全に陥っていたはずの経路）。

証跡: `docs-notes/../scratchpad`ではなく検証ログは会話内添付、再現手順は本ファイル参照。

## 範囲外の気づき（2件・修正はしていない）

1. **モーダルダイアログ表示中の `Send-Ecad2Keys` が届かないことがある**
   （ecad2-ui-automationスキルのヘルパー内部制約、T-039の欠陥ではない）。
   `Send-Ecad2Keys` は内部で `Set-Ecad2Foreground`（`SetForegroundWindow(MainWindowHandle)`）
   を呼ぶが、モーダルダイアログ表示中にこれを呼ぶと、フォーカスが一瞬メインウィンドウ側
   （キャンバス等）へ奪われ、その隙に送ったキー入力がダイアログのテキストボックスに届かない
   事象を実機で再現した（トレースログにも
   `LostKeyboardFocus DeviceNameBox → GotKeyboardFocus LadderCanvasHost → (自動で)GotKeyboardFocus DeviceNameBox`
   という往復が記録され、フォーカス奪取の実態が裏付けられた）。
   忍者記録`ecad2-t039-trace-log-field-feedback-ninja.md`にも既知の罠として言及あり。
   **回避策**（今回確立・実証済み）: ダイアログ自身の `AutomationElement.Current.NativeWindowHandle`
   を取得し、そちらへ直接 `SetForegroundWindow` してから `[System.Windows.Forms.SendKeys]::SendWait`
   を呼ぶ（`Send-Ecad2Keys` ヘルパーは経由しない）。スキル文書（`ecad2-ui-automation`）への
   反映を提案するが、判断は家老・侍に委ねる。
2. **複数セッション体制での実機検証中、他役の`dotnet build`とexe上書き競合が起きた**
   （運用上の教訓、既に侍と解消手順を合意済み・詳細は本ファイルでなくpeerメッセージ参照）。
   実機検証中は検証対象アプリのビルドを他役が控える運用を、次回以降も踏襲されたし。

## 回帰の有無

なし。既存機能（要素配置・ツール切替・デバイス名編集・機器表反映）は正常動作。
