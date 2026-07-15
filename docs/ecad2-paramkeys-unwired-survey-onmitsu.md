# Core層パラメータ単位の未結線 網羅調査（隠密）

調査日: 2026-07-15
調査者: 隠密
委任元: 家老（殿指示「core層の未結線拾い出しができていないな。再調査してみて」）

## 背景

タイマー接点調査（`docs/ecad2-timer-setpoint-investigation-onmitsu.md`）で`ParamKeys.Setpoint`のApp層未結線を発見、PR-16として正式パターン確定。既存の棚卸し調査（`docs/ecad2-guiecad-unwired-features-survey-onmitsu2.md`、隠密2）は**機能単位**の粗い粒度（画像挿入・ドキュメント情報・BOM編集・自作パーツ管理等、B区分11件）だったため見落とされていた可能性を踏まえ、本調査は`Element.Params`（`Dictionary<string,string>`）が持つ**パラメータ単位**の細かい粒度で再点検した。

## 調査範囲・方法

`src/Ecad2.Core/Model/Element.cs`の`ParamKeys`静的クラス（全6定数、一元管理のtypo防止用）を起点に、各キーについてCore層の使用箇所とApp層の編集UI有無をExploreエージェント委譲で照合した。加えて、`ParamKeys`を経由しない直接文字列リテラルでの`Params`アクセス（未文書化の隠れたキー）が無いかも確認した。

## 結果一覧

| キー | 用途 | Core使用箇所 | App編集UI | 判定 |
|---|---|---|---|---|
| `Position` | SelectSWノッチ位置(int) | `NetlistBuilder`等 | `MainWindowViewModel.cs:1854-1874,2275`（`SelectedElementNotchPosition`） | **結線済み**（T-086対応済み） |
| `Setpoint` | タイマ設定時間(秒・double) | `Evaluator.cs:161-169`、`TestSession.cs`、`NetlistBuilder.cs:315-324` | 0件 | **未結線**（既報告、PR-16正式パターン確定） |
| `LampColor` | ランプ色 | `DiagramRenderer`等 | 0件 | **未結線**（P-057既知、T-085として起票済み・未実装） |
| `Type` | Breaker3P種別(NFB/MCCB/ELB) | `DiagramRenderer.cs:1001,1008,1055`（未設定時は既定値`"NFB"`にフォールバック） | 0件 | **未結線**（新規発見） |
| `LabelDy` | ラベル高さオフセット(mm・double) | `DiagramRenderer.cs:1074-1077`（コメント上「密集時の重なり回避」用の個別調整と明記） | 0件 | **未結線**（新規発見） |
| `Orient` | 主回路記号の向き(V/H) | `DiagramRenderer.cs:996,1001,1009,1055` | 0件 | **未結線**（新規発見） |

**追加確認**：`ParamKeys`を経由しない直接文字列リテラルアクセスは`src/Ecad2.Core/`全体で0件（コメント内言及を除く）。未文書化の隠れたパラメータキーは存在しない。

## 6項目中5項目が未結線という実態

`Position`のみが結線済みで、残る5項目（`Setpoint`・`LampColor`・`Type`・`LabelDy`・`Orient`）は全てApp層編集UIが皆無。うち`LampColor`は既にP-057→T-085として起票済み、`Setpoint`は本調査の端緒として既報告済み。**新規発見は`Type`・`LabelDy`・`Orient`の3件。**

### 各新規発見の実害・所感

- **`Type`（Breaker3P種別）**：主回路パーツ（非シミュレート対象）。既定値`"NFB"`に固定されるため、MCCB/ELBを使いたい場合に表現できず、ラベル・記号の脇表示が常にNFB相当になる。実害は表示上の制約のみ（クラッシュ等ではない）。
- **`LabelDy`**：コメント上「密集時のラベル重なり回避」用の個別微調整パラメータ。無くても種別既定値（`ElementCatalog.DefaultLabelDy`）で運用は成立するため、致命度は他2件より低いと考えられる（快適性向上機能に近い）。
- **`Orient`（主回路記号の向き）**：未設定時は`null`のまま扱われ、実質常に既定（縦向き相当）の描画になると推測される（推測）。主回路記号の向きを使い分けたい場合に表現できない。

## 既存の機能単位棚卸しとの関係

`docs/ecad2-guiecad-unwired-features-survey-onmitsu2.md`のB区分（Core完備・App未結線）11件は「機能」（画像挿入・ドキュメント情報ダイアログ・BOM編集・自作パーツ管理等）の粒度であり、`Element.Params`のようなモデル内部のキー単位までは踏み込んでいなかった。同調査15番（シート設定ダイアログの列数・電源ラベル`BusConfig.PowerLabel`欠落、P-070既知）も、実は本調査と同型の「パラメータ単位の未結線」であり、本調査の観点と合流する既知事例と言える。

**不明点**：`Device`クラス（`Model`/`Maker`/`Quantity`等）・`Sheet`/`BusConfig`クラスの他プロパティについて、`Element.Params`と同型の網羅調査は本調査の範囲外（家老依頼が`ParamKeys`起点だったため）。必要であれば追加調査可能。

## 派生提案

- パターン台帳へ「1パラメータのみの書き込み経路欠落」を正式パターンとして提示する材料が今回で4例（`Position`T-086・`Setpoint`本件・`LampColor`P-057・今回の`Type`/`LabelDy`/`Orient`3件）に達した。家老判断で台帳記帳を検討されたい（自ら着手はしない）。
- `Type`・`LabelDy`・`Orient`の3件を個別タスク化するか一括タスク化するかは家老・殿判断に委ねる（本調査は一覧化のみ、依頼どおり個別着手はしない）。
