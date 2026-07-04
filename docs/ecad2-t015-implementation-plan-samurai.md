# T-015 実装プラン（侍起草）

対象: 右パネル「部品選択」リスト(`PartSelectionList`)の各項目に図形サムネイルを追加
(案1採用、固定6種＋自作パーツ両方)。技術方針は隠密調査どおり`DiagramRenderer.DrawPreview`を
`DrawingVisual`→`RenderTargetBitmap`化する薄いラッパー。

## 現状・技術的な穴

- `DrawPreview(IRenderer r, ElementInstance e, Color color)`は`_lib`(PartLibrary、privateフィールド)
  に依存するが、`_lib`は`Render(r, sheet, library)`経由でしかセットされない。単独でDrawPreviewだけ
  呼ぶと`_lib`がnullのままとなり、自作パーツ(`PartId`経由で`PartDefinition`を参照)が正しく解決
  できず、組込み種別(`Kind`)のフォールバック描画になってしまう(自作パーツの実際の見た目が出ない)。
  **DrawPreviewに`PartLibrary?`引数を追加する必要がある**(既存呼び出し元は皆無のため後方互換の
  心配なし、確認済み)。
- `PartFolderEntry`はCore層のrecord(永続化にも使われる)のため、WPFの`ImageSource`を直接持たせる
  設計はCore/App層分離の原則に反する。App層側にサムネイル付きのラッパー型を新設する。

## 段階分割

1. **DrawPreviewの拡張**: `PartLibrary? library = null`引数を追加し、呼び出し中だけ`_lib`を
   一時差し替え(try/finallyで元に戻す)。既存呼び出し元が無いため挙動変化なし。
2. **サムネイル生成ラッパー新規実装**: `Ecad2.Rendering.Wpf`層に、`DrawingVisual`→
   `RenderTargetBitmap`で単一`ElementInstance`(Pos=(0,0)固定)をオフスクリーン描画し
   `ImageSource`を返すヘルパーを追加。`WpfRenderer`(既存IRenderer実装)をそのまま流用する。
3. **App層でのサムネイル紐付け**: `PartPaletteViewModel`に、`PartFolderEntry`＋生成済み
   `Thumbnail`(ImageSource)を持つラッパー型(例: `PartSelectionEntryViewModel`)を新設。
   `Entries`の型をこのラッパーへ変更(または並行プロパティとして追加)し、起動時一括生成する。
4. **XAML変更**: `PartSelectionList`の`ItemTemplate`にサムネイル`<Image>`をテキストの左へ追加。
5. 各段階完了時にビルド・テスト(既知3件)。全段階完了後、既定順(隠密静的→忍者実機)で回す。

## 設計判断点(プラン明記)

- **生成タイミング=起動時一括生成**: 固定6種＋自作パーツは現状少数(数十件未満)。T-002 PoCの実績
  (5万個113ms)から見てオフスクリーン描画は軽量と推定され、遅延生成の複雑さを持ち込む理由がない
  (計測してから判断、CLAUDE.md品質哲学のKISSに合致)。将来パーツ数が増え起動が遅くなった場合に
  遅延生成へ切替を検討すればよい。
- **サムネイルサイズ**: 1セル分(CellMm)相当をDIP換算し、初期案24×24px程度で実装(侍裁量)。
  見た目に関わる細部(サイズ・余白)は殿帰宅後の実機確認で微調整するサイクルとする。
- **色**: `DrawPreview`の第3引数`color`は`StrokeRole.SymbolOutline`の既定色(黒系)を使う想定
  (配置ゴースト表示のような半透明色は使わない、通常表示相当)。
