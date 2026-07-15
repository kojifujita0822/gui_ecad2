# T-058増分4（パネルレイアウト永続化）静的レビュー（隠密）

レビュー日: 2026-07-15
対象コミット: 8a07472
対象diff: `src/Ecad2.App/MainWindow.xaml`（5行）・`src/Ecad2.App/MainWindow.xaml.cs`（148行）・`tests/Ecad2.App.Tests/T058Increment4LayoutFileNameTests.cs`（新規）
レビュー深度: 軽量既定（1周目、karo.md方針）
併用: code-reviewスキル（low effort）

## 総合判定

**指摘1件（CONFIRMED相当、severity中〜高）。それ以外は設計叩き台どおりの実装で問題なし。**

## (a) 台帳DoDとの整合

- 保存先パス`%AppData%\Ecad2\docking-layout\`（`DockingLayoutDirectory`、`Environment.SpecialFolder.ApplicationData`）：確認OK。
- DockingManager単位の個別ファイル3つ（`left-palette.xml`/`output-panel.xml`/`right-panel.xml`、`GetDockingLayoutFileName`）：確認OK。
- 終了時自動保存（`Window_Closing`）＋明示保存コマンド（`SaveDockingLayoutMenuItem_Click`・Ctrl+Alt+S）の両方から同一の`SaveDockingLayoutAsDefault()`を呼ぶ設計：確認OK。
- Ctrl+Alt+Rはファイル優先に変更（`TryReadSavedDockingLayoutXml(manager) ?? _defaultDockingLayoutXmlByManager.GetValueOrDefault(manager)`）：ロジック自体は確認OK（後述の指摘1を除く）。

## (b) code-reviewスキル併用（low effort）

diffのhunkから正しさのバグを1件検出（下記「指摘1」）。それ以外に重複・デッドコードは見当たらない。`RebindDockingContent`への共通化（増分3隠密指摘2と同型判断）も適切。

## (c) 狙い撃ち観点

### 起動順序
コンストラクタ内`RegisterDockingContents(); SerializeDefaultDockingLayouts(); LoadDockingLayoutFromFileIfExists();`の順を確認。設計叩き台どおり、出荷時ハードコード既定のキャプチャがファイル読込より必ず先に行われる。**問題なし。**

### 指摘1（CONFIRMED相当、severity中〜高）：Ctrl+Alt+Rリセット時、破損XMLファイルの`Deserialize`失敗が未捕捉

`ResetDockingLayoutToDefault()`（`MainWindow.xaml.cs:306-326`）：

```csharp
string? xml = TryReadSavedDockingLayoutXml(manager)
    ?? _defaultDockingLayoutXmlByManager.GetValueOrDefault(manager);
if (xml is null) continue;
var serializer = new XmlLayoutSerializer(manager);
serializer.LayoutSerializationCallback += RebindDockingContent;
using var reader = new StringReader(xml);
serializer.Deserialize(reader);   // ← try-catch無し
```

`TryReadSavedDockingLayoutXml()`（328-341行）は`File.ReadAllText(path)`の失敗（`IOException`/`UnauthorizedAccessException`）のみを捕捉してnullを返す設計。しかし**ファイル自体は正常に読めても、中身のXML構文が壊れているケース**（ユーザーの手動編集ミス、アプリクラッシュ時の中途半端な書き込み等）では、`TryReadSavedDockingLayoutXml`は成功してxml文字列を返し、その後の`serializer.Deserialize(reader)`（316行目）で`System.Xml.XmlException`や`InvalidOperationException`が投げられうる。この呼び出しはtry-catchで保護されておらず、呼び出し元（`OnGlobalDockingLayoutShortcut`イベントハンドラ）にも保護が無いため、**Ctrl+Alt+R押下時にアプリがクラッシュする経路が存在する**。

**対照比較**：`LoadDockingLayoutFromFileIfExists()`（起動時読込、266-298行）は同種のリスクに対し、ファイル読込とDeserializeの両方を単一のtry-catch（`IOException or InvalidOperationException or System.Xml.XmlException`）で保護しており、正しくフォールバックする設計になっている。**この2経路の非対称性が問題の核心。**

設計叩き台5節の表にも「Ctrl+Alt+R | ファイル破損/読込失敗 | ハードコード既定へフォールバック」と明記されており、殿裁定(5)「破損ファイル等はハードコード既定のまま起動を継続する（クラッシュ厳禁）」の原則は、**起動時経路では正しく実装されているが、Ctrl+Alt+Rリセット経路では実装が設計意図を満たしていない**と判断する。

**対処案**：`ResetDockingLayoutToDefault()`のforeachループ内、`serializer.Deserialize(reader)`をtry-catch（`LoadDockingLayoutFromFileIfExists`と同じ例外セット`IOException or InvalidOperationException or System.Xml.XmlException`）で囲み、失敗時は`_defaultDockingLayoutXmlByManager`側へフォールバックする（あるいは最低限、そのDockingManagerの処理をスキップしてクラッシュを防ぐ）よう修正するのが妥当と考える。

### Ctrl+Alt+Rの優先順位変更ロジック（指摘1を除く部分）
null合体演算子によるファイル優先→ハードコード既定フォールバックの構造自体は正しい。

### フォールバック処理の網羅性（例外種別の妥当性）
- `SaveDockingLayoutAsDefault()`：`IOException or UnauthorizedAccessException`、書き込み系の例外として妥当。
- `LoadDockingLayoutFromFileIfExists()`：`IOException or InvalidOperationException or System.Xml.XmlException`、読込+デシリアライズ失敗を正しく網羅。
- `TryReadSavedDockingLayoutXml()`：`IOException or UnauthorizedAccessException`、ファイル読込のみを想定した妥当な範囲だが、**呼び出し元の`ResetDockingLayoutToDefault()`側でDeserialize失敗が別途保護されていないため、結果として全体としての防御に穴がある**（指摘1）。

### 読込失敗時のステータスメッセージ折衷案の実装箇所
`LoadDockingLayoutFromFileIfExists()`内の`anyLoadFailed`フラグ、ループ後に一括で「保存済みレイアウトの読込に失敗したため既定で起動しました」を表示。個別DockingManagerごとの詳細通知ではなく一括通知とする設計判断は、UXとして妥当（過度な詳細化を避ける）。

## スコープ確認（指摘2、軽微）

家老依頼文言では「スコープ境界：`MainWindow.xaml.cs`のみ（侍自己申告）」とあったが、実際には`MainWindow.xaml`にも5行の変更（新設メニュー項目、表示メニューへの追加）が含まれている。ただしこれは設計叩き台4節に明記された意図した変更であり、範囲外の混入ではない。侍申告の伝達（家老要約時の齟齬の可能性）を軽微な申告精度の問題として記録するに留め、実害はない。

## 総括・家老への申し送り

- **指摘1（Deserialize未保護）は修正を推奨する。** 忍者実機確認で「破損ファイルフォールバック」を検証済みとのことだが、その検証が起動時経路（`LoadDockingLayoutFromFileIfExists`）のみだった場合、Ctrl+Alt+R経路の同種シナリオは未検証の可能性がある。忍者への追加確認観点として「破損ファイル状態でCtrl+Alt+Rを押す」ケースを申し送ることを推奨する。
- 指摘2はスコープ申告の軽微な精度問題、実害なし。
