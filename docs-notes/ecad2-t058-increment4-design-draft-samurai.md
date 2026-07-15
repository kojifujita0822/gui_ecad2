# T-058増分4（レイアウト永続化）設計叩き台（侍）

作成日: 2026-07-15
対象: 3つのDockingManager（LeftPalette/OutputPanel/RightPanel）のレイアウト保存/復元を本実装へ移植。

## 0. 前提（殿裁定の確認）

- 保存タイミング＝**両方**（アプリ終了時の自動保存＋「現在のレイアウトを既定として保存」の明示コマンド）
- 保存先＝**アプリ共通設定**（%AppData%配下等、全プロジェクト共通）
- Ctrl+Alt+Rのリセット先＝**明示保存済みの既定レイアウト**（未保存なら出荷時ハードコード既定）に統一

## 1. 保存タイミングの読み解き

「両方」＝終了時自動保存と明示コマンドは**同一の保存先・同一の保存メソッドを共有する2つの契機**と解釈する。
つまり「既定レイアウト」という単一の実体に対し、(a) 終了時に自動で今の状態を書き込む、(b) いつでも明示的に
今の状態を書き込める、の2経路がある設計。保存メソッド自体を分岐させる必要はなく
`SaveDockingLayoutAsDefault()`という単一メソッドを両方の契機から呼ぶ。

## 2. 保存先パス

`%AppData%\Ecad2\docking-layout\`（`Environment.SpecialFolder.ApplicationData`、Roaming）配下に、
DockingManager単位で個別ファイルを持たせる（1ファイルに3レイアウトを統合する自前フォーマットは
複雑化を招くため見送り、`XmlLayoutSerializer`は単一Writer/Reader前提のAPIでもある）。

```
%AppData%\Ecad2\docking-layout\left-palette.xml
%AppData%\Ecad2\docking-layout\output-panel.xml
%AppData%\Ecad2\docking-layout\right-panel.xml
```

既存の`AllDockingManagers`（`LeftPaletteDockingManager`/`OutputPanelDockingManager`/
`RightPanelDockingManager`）と1対1対応させ、`DockingManager → ファイル名`の対応表を1箇所で持つ
（既存の`_defaultDockingLayoutXmlByManager`と同型のDictionary）。

既存のパーツライブラリ関連（`PartFolderStore`/`PinnedPartStore`）は`MyDocuments`配下だが、これは
プロジェクトデータ（ユーザーの作図資産）としての性質。レイアウト設定は「アプリの見た目の好み」で
性質が異なり、殿裁定どおり`ApplicationData`（アプリ共通設定）を新規に使う。

## 3. 実装方式（3つの層）

既存の`_defaultDockingLayoutXmlByManager`（起動直後にXAMLの初期状態をメモリ上でキャプチャした
「出荷時ハードコード既定」）はそのまま維持し、変更しない。その上に「保存済み既定レイアウト
（ファイル）」という層を追加する。

### 3-1. 起動時（`MainWindow()`コンストラクタ内）

現行の呼び出し順序:
```csharp
RegisterDockingContents();
SerializeDefaultDockingLayouts();   // ← ここで出荷時ハードコード既定をキャプチャ(不変)
```
の直後に新規追加:
```csharp
LoadDockingLayoutFromFileIfExists();   // ← 保存済みファイルがあればここで適用
```
**順序が重要**：`SerializeDefaultDockingLayouts()`は必ずファイル読込より前に呼ぶ。これにより
「出荷時ハードコード既定」のキャプチャは常にXAML初期状態を指し、ファイルからの復元有無に
左右されない（Ctrl+Alt+Rのフォールバック先として不変であり続ける）。

### 3-2. 保存（`SaveDockingLayoutAsDefault()`、新規メソッド）

```csharp
private void SaveDockingLayoutAsDefault()
{
    try
    {
        Directory.CreateDirectory(DockingLayoutDirectory);
        foreach (var manager in AllDockingManagers)
        {
            var serializer = new XmlLayoutSerializer(manager);
            using var writer = new StreamWriter(GetDockingLayoutFilePath(manager));
            serializer.Serialize(writer);
        }
        _viewModel.StatusMessage = "現在のパネルレイアウトを既定として保存しました";
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        _viewModel.StatusMessage = "パネルレイアウトの保存に失敗しました";
    }
}
```
呼び出し元:
- `Window.Closing`イベントハンドラ（自動保存）
- 新設メニュー項目/ショートカットのClickハンドラ（明示保存）

### 3-3. 読込（`LoadDockingLayoutFromFileIfExists()`、新規メソッド）

```csharp
private void LoadDockingLayoutFromFileIfExists()
{
    foreach (var manager in AllDockingManagers)
    {
        var path = GetDockingLayoutFilePath(manager);
        if (!File.Exists(path)) continue;
        try
        {
            var serializer = new XmlLayoutSerializer(manager);
            serializer.LayoutSerializationCallback += RebindDockingContent;
            using var reader = new StreamReader(path);
            serializer.Deserialize(reader);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Xml.XmlException)
        {
            // 破損ファイル等はハードコード既定のまま起動を継続する(クラッシュ厳禁、殿裁定(5))。
        }
    }
}
```
`RebindDockingContent`は既存`ResetDockingLayoutToDefault()`内のラムダ（ContentIdキーで
`_dockingContentRegistry`から再バインド）と同一ロジックのため、`LayoutSerializationCallback`
デリゲートとして共通化する（rule of three未達でも2箇所の完全一致重複は避ける、増分3隠密指摘2と
同型の判断）。

### 3-4. Ctrl+Alt+Rの動作変更（`ResetDockingLayoutToDefault()`改修）

現行はDockingManagerごとに無条件で`_defaultDockingLayoutXmlByManager`へDeserializeしているが、
**ファイルが存在すればファイルを優先**するよう変更する:

```csharp
private void ResetDockingLayoutToDefault()
{
    foreach (var manager in AllDockingManagers)
    {
        var path = GetDockingLayoutFilePath(manager);
        string? xml = File.Exists(path) ? TryReadAllText(path) : null;
        xml ??= _defaultDockingLayoutXmlByManager.GetValueOrDefault(manager);
        if (xml is null) continue;
        // 既存のDeserialize+RebindDockingContent呼び出し(変更なし)
    }
    ...
}
```
`TryReadAllText`は読込失敗時null（破損ファイル→ハードコード既定へフォールバック、殿裁定(5)と
同型の防御）。

## 4. 新設UI要素（家老へ叩き台として提示）

**コマンド名**：「現在のレイアウトを既定として保存」

**キーバインド案**：`Ctrl+Alt+S`
- 理由: 既存の`Ctrl+Alt+R`（リセット）と同じ修飾キー体系で対をなし、記憶しやすい。`Ctrl+S`
  （上書き保存）とは別立て（保存対象が図面データではなくパネルレイアウトのため、両者混同を避ける
  意図で単独のCtrl+Sには乗せない）。

**メニュー配置案**：「表示(_V)」メニュー末尾へ追加。現状同メニューは「グリッド表示(_G)」1項目のみ。
```xml
<MenuItem Header="表示(_V)">
    <MenuItem Header="グリッド表示(_G)" .../>
    <Separator/>
    <MenuItem Header="現在のレイアウトを既定として保存(_L)" InputGestureText="Ctrl+Alt+S" Click="SaveDockingLayoutMenuItem_Click"/>
</MenuItem>
```
既存のCtrl+Alt+R自体はメニュー未搭載（ショートカットのみ運用）だが、今回新設するCtrl+Alt+Sを
対称的にメニュー化する狙いは「明示コマンドという性質上、存在に気づきにくいキーボードショートカット
専用は不親切」という判断（叩き台、家老裁量あり）。

## 5. 破損/存在しないファイルへのフォールバック一覧（殿裁定(5)対応）

| 契機 | 状態 | 挙動 |
|---|---|---|
| 起動時読込 | ファイル無し | 何もしない(XAML初期状態のまま、通常の初回起動) |
| 起動時読込 | ファイル破損/Deserialize失敗 | try-catchで捕捉、そのDockingManagerのみXAML初期状態のまま起動継続 |
| Ctrl+Alt+R | ファイル無し | 現行どおりハードコード既定(`_defaultDockingLayoutXmlByManager`)へ |
| Ctrl+Alt+R | ファイル破損/読込失敗 | ハードコード既定へフォールバック |
| 保存(自動/明示) | 書込失敗(権限/容量等) | try-catchで捕捉、ステータスメッセージのみ、例外伝播させない |

## 6. スコープ境界

- 対象は`MainWindow.xaml.cs`のみ（既存の`AllDockingManagers`等の枠組みをそのまま拡張）。
- `_defaultDockingLayoutXmlByManager`（出荷時ハードコード既定）自体の仕組みは変更しない。
- ファイルI/Oは同期処理でよい(既存の`SaveDocument`等と同水準、非同期化は過大)。

## 7. 未確定・家老検分事項

- キーバインド`Ctrl+Alt+S`・メニュー配置・「表示」メニューへのSeparator追加は叩き台であり、
  殿確認が必要な場合は家老経由で仰ぐ。
- ファイル形式はDockingManagerごとの個別XML(3ファイル)とした。統合1ファイル案との比較要否は
  家老判断に委ねる（個別ファイル案はPoC・既存実装との差分が最小、KISSの観点で優位と判断）。
