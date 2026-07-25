# T-068増分3-a 実機確認記録（忍者）

最終更新: 2026-07-25

## 対象

- コミット: `de00c03`（PartEditorDialog タブ廃止・単一画面構成化、440x420固定→800x600・リサイズ可能化）
- 家老委譲: 2026-07-24 23:20（隠密静的レビュー完了後の号令）
- 検証観点は家老予告どおり5点

## 環境・準備

- `ecad2-ui-automation`スキルに従いセカンダリモニタで`Start-Ecad2App`起動、build事前確認（警告0・エラー0）。
- 実装先読み: `PartEditorDialog.xaml`でタブ廃止・単一Grid構成（Row0プロパティ/Row1ツールバー器/Row2左キャンバス器+右端子リスト/Row3エラー/Row4ボタン）を確認、コードビハインドのバリデーション3種のロジックも確認済み。テーマ切替は「表示(V)」→「ダークモード(作図色)(_D)」トグルが`ApplyUiChromeTheme`経由でApp.xamlのMergedDictionariesをTheme.Light/Dark.xamlへ実差し替えする実装と確認。

## 判定結果

| 観点 | 判定 | 詳細 |
|---|---|---|
| 1. プロパティ編集（新規作成・編集両経路） | **OK** | タブ廃止・単一画面構成を確認（`FindAll`でTabItem 0件）。新規作成経路: 名前(`NameBox`)・幅高さ(`WidthBox`/`HeightBox`)・役割(`RoleCombo`)を設定→保存→編集で再度開き反映を確認。編集経路: 既存パーツを開き役割変更(a接点(NO)→コイル)→保存→再度開き反映を確認。増分1/2からのx:Name不変によりUIAクエリはそのまま流用可能。 |
| 2. 端子編集（暫定残置、右列） | **OK** | Row2右列`PortsPanel`(Width=290)に配置されたことを確認。追加ボタンで自動命名(P1→P2)・`GridPattern.GetItem`経由の値編集(RowOffset/BoundaryOffset)とも正常動作。保存後の再読込でBoundaryOffset昇順への並べ替えも確認(P1=3→保存前、P2=1→P1=3の順に並べ替え済み)。 |
| 3. バリデーション3種 | **OK** | (a)名前必須: 空のままOK→`ErrorText`="名前は必須です。" (b)幅高さ1〜12: 幅=13→"幅は1〜12の整数で指定してください。"、幅=0→同エラー、両端とも拒否確認 (c)ポート2点未満: ポート0件+役割=a接点(NO、NonSimulated以外)→"接続点(ポート)を2つ以上配置してください。"で拒否確認(増分2からの回帰なし)。 |
| 4. リサイズ時のレイアウト破綻 | **OK（座標ベース判定）** | `MinWidth=640`/`MinHeight=420`まで`MoveWindow`で縮小し、`NameBox`/`WidthBox`/`HeightBox`/`RoleCombo`/`PortsGrid`/OK/キャンセル/エラーテキスト各要素の`BoundingRectangle`を採取。800x600時と640x420時を比較し、(1)要素同士の重なりなし (2)ウィンドウのクライアント領域からのはみ出しなし、を確認。エラーメッセージ表示状態(640x420)でもOKボタン等と重ならず。**所感**: `PortsGrid`右端がウィンドウ右端まで約20pxしか余裕がなく、最小サイズでは幾分窮屈。Row0プロパティ行(WrapPanel)は640幅でも折り返しが発生せず1行のまま(800x600時と同一座標)。 |
| 5. ダーク/ライト両テーマ配色 | **OK（画素採取）** | 期待値をTheme.Light.xaml/Theme.Dark.xamlから事前取得(`ToolBarBackgroundBrush`: Light=#F0F0F0/Dark=#2D2D30、`WorkAreaBackgroundBrush`: Light=White/Dark=#202224、`DialogBackgroundBrush`: Light=White/Dark=#2D2D30、参考`InputBackgroundBrush`: Light=White/Dark=#3C3C3C)。両テーマでPartEditorDialogを開き対象領域を画素採取した結果、**全項目とも実測値が期待値と完全一致**。 |

## 観点4・5の検証中に発生したGPU描画不全とその対処（重要）

観点4のリサイズ検証中、ダイアログを`MoveWindow`で640x420へ縮小したところ、PrintWindow・CopyFromScreen(フォアグラウンド化あり)両方の撮影が白紙になる現象に遭遇した。UIA探索は正常（座標はウィンドウサイズに正しく追従、OKボタンY座標が800x600時=700→640x420時=520に変化、元へ戻すと700に復帰）。メインウィンドウでも同様の白紙化を対照確認、アプリ再起動・3秒待機・最小化復元いずれでも回復せず。

`memory: ecad2_gpu_hw_render_blank_screenshot`（T-110増分0、2026-07-21）と症状完全一致（UIA正常+両撮影手法とも白紙）。**T-110増分0に続く2度目の遭遇**。

家老へ相談のうえ、以下の手順で対処（家老裁定の4条件を遵守）:
1. 適用前に他役のdotnet/WPFプロセス稼働状況を確認（侍より「Ecad2.App起動していない、遠慮なく適用されよ」の回答を受領）
2. 家老から侍・隠密へ事前通知済み
3. **変更前の値を記録**: `HKCU:\Software\Microsoft\Avalon.Graphics`キーは存在するが`DisableHWAcceleration`値は未設定（既定＝HWアクセラレーション有効）
4. `DisableHWAcceleration=1`(DWord)を設定しアプリ再起動 → **正常描画に回復**（GPU HW描画不全と確定、実装バグではない）
5. 観点5の画素採取完了後、`Remove-ItemProperty`で値を削除し**元の未設定状態へ復元**、`Get-ItemProperty`で復元確認済み

観点4自体は、この描画不全が発生する前の段階でUIA座標ベースの判定に切り替え、撮影なしで判定を完了できた（家老助言どおり）。観点5はDisableHWAcceleration適用後に撮影ベースで実施。

## 範囲外の気づき

特になし。増分2からの継続で確認したバリデーション・並べ替えロジックは変わらず正常動作。

## 総括

**T-068増分3-a、検証観点1〜5すべてOK。回帰なし。** タブ廃止・単一画面構成への再設計は正しく機能しており、800x600↔640x420のリサイズでもレイアウト破綻なし、ダーク/ライト両テーマの新規配色(`ToolBarBackgroundBrush`等)も期待値どおり。検証中に発生したGPU描画不全は環境問題であり実装の不具合ではないと確定（メインウィンドウでも対照確認済み）。同事象の再発をmemoryへ追記する。
