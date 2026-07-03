# ecad2 技術スタック 決定ブリーフ（熟考用）

> 2026-07-03 作成（家老が隠密・忍者・侍の三者調査を統合）。**本書は殿の裁定を仰ぐための決定支援資料であり、スタックは未確定。**
> 詳細な各family調査は同ディレクトリの下記4文書を参照:
> - キーボード要件: [ecad2-keyboard-requirements.md](ecad2-keyboard-requirements.md)（忍者）
> - .NET系: [ecad2-framework-survey-onmitsu.md](ecad2-framework-survey-onmitsu.md)（隠密）
> - Web＆クロスPF系: [ecad2-stack-web-crossplatform.md](ecad2-stack-web-crossplatform.md)（忍者）
> - ネイティブ/軽量系: [ecad2-stack-native-lightweight.md](ecad2-stack-native-lightweight.md)（侍）

## 1. 前提・目的

GuiEcad（WinUI3）の知見を活かし、制御盤・ラダー/シーケンス図CADを **ecad2** として作り直す。最大の眼目は **キーボードファースト操作**。GuiEcad ではキーボードのみでの作図完結を志すも、WinUI3 の暗黙フォーカス移動バグ（[Issue #6179](https://github.com/microsoft/microsoft-ui-xaml/issues/6179)）に阻まれ果たせなかった。

## 2. 評価軸（優先順）

1. **フォーカス管理APIの決定性** ―― 【宣言的かつ確実】か、【暗黙・非決定的な委譲を持つか】。**最重要**。忍者の教訓 R1「フォーカスの所在をアプリが完全に制御できるか」に集約。
2. 高性能な自前2Dベクター描画（グリッド・記号・ヒットテスト）
3. ベクターPDF出力経路
4. デスクトップ優先（クロスプラットフォームは加点）
5. C#/.NET 知見・資産の再利用（加点）
- 補助: 各候補の **フォーカス/キー入力の既知バグ・未解決Issue**（R10）

## 3. 横断して判明した二大要点

**(A) いずれの候補も「#6179型フォーカスバグを確実に回避できる」一次情報レベルの保証はない。** ゆえに三者・全16候補が **採用前の小規模PoC**（フォーカス制御・大量記号描画・PDF出力）を一致して推奨。特に「PointerPressed→PointerReleased 後のフォーカス保持」シナリオの実機検証を最終選定前に行うべき。

**(B) C#資産（IRenderer 抽象・Device 一元管理・ネットリスト分離・記号/種別分離・.GCAD 永続化）を無傷で活かせるのは .NET系のみ。** 非.NET系は View 層フルスクラッチ前提（ただし UI 非依存の Model/Simulation は設計知識としてどの言語へも移植可）。

## 4. 有力候補の絞り込み

| 候補 | (1)フォーカス決定性【最重要】 | (2)(3)描画/PDF | (5)C#資産 | 総評 |
|---|---|---|---|---|
| **WPF** ◎本命 | **最良**。`FocusManager` スコープAPI＋`PreviewLostKeyboardFocus` の `e.Handled` キャンセル機構（WinUI3 に無い明示的防御線）。#6179型の報告は調査範囲で未発見 | 成熟（DrawingContext/DrawingVisual）＋ **PDFsharp 直結** | **維持** | フォーカス・描画・PDF・資産再利用を最もバランス良く満たす。Windows専用（デスクトップ優先ゆえ可）。難点は保守フェーズ（新機能停滞）・Fluent 見劣り |
| **Qt**（C++/PySide）○対抗 | FocusReason で追跡可・宣言的API豊富。ただし QTBUG-11554 が **GuiEcad症状に酷似**（3-5年未修正） | **最成熟**。QPainter→QPdfWriter に同一コード転用可・公式に "CAD/diagram editor 向け" 明記。IRenderer 設計と自然合致 | ほぼ不可（Qyoto/QtSharp 死亡、新 Bridges は Beta・Quick 限定） | CAD/PDF は随一。だが C# を捨て、フォーカスも完全無傷ではない |
| **Avalonia** △クロス保険 | 明快だが #6179同系 #14100 が現行版に存在・area-focus Open Issue 多数 | Skia ベースで良好 | **維持**＋クロスPF | .NET 維持でクロス対応も望むなら。フォーカス risk は WPF より高め |
| **egui**（Rust）△ | **API透明性は最高**（Memory/Context で完全管理）だが 1フレーム遅延・ID一意性はアプリ責務 | epaint は良好だが **PDF標準なし**（外部クレート・未成熟） | ゼロ | フォーカス純度は光るが CAD/PDF 基盤が弱く Rust 学習 |

**非推奨**:
- **WinUI3**: #6179 は Microsoft `not_planned` 確定・自動クローズ。同系バグ（#10366・#3825）が 2025-26 も新規発生＝フォーカス管理サブシステムの構造的弱点。
- **Uno・MAUI**: WinUI3 基盤ゆえ同種リスク継承。MAUI はデスクトップのキーボード操作支援が Microsoft 内部で Backlog。
- **Flutter・Compose MP**: #6179型の致命的フォーカスバグ（#151457・#4803）。
- **Dear ImGui**: メンテナ自身が Focus/Active 概念混同を認める（#7473 でキー入力喪失）。イミディエイトモードでベクター情報非保持＝CAD/PDF 不向き。
- **Slint**: focus 保持バグ（#3578・3年 Open）・PDF 標準なし・C# は Linux 限定実験段階。
- **GTK4**: 宣言的 focus-chain を GTK4 で廃止・継続バグ（#7952 他）・GtkSharp は GTK4 非対応。
- **Tauri**: 致命傷なきも OS 別 WebView 依存で「小地雷多数」・検証コスト高。

**中位**: JavaFX（GuiEcad の Canvas+IRenderer 設計に最も近く移植容易だが C# 不可・コミュニティ小）／Electron（致命的フォーカスバグなきも Web ゆえ C# 不可）。Neutralino はサプライチェーンのガバナンスリスク。

## 5. 決断の分岐

- **路線A（C#維持）**: **WPF** 本命（フォーカス最良・資産無傷・移行コスト最小）。クロス対応も欲すれば **Avalonia**。
- **路線B（C#離脱で基盤最良を取る）**: **Qt**（CAD/PDF 最成熟）。View 層フルスクラッチ前提。

## 6. 家老の推奨

本アプリは **キーボードファースト＋自前2D描画＋ベクターPDF＋既存C#資産** の四条件を同時に要する。この四つを最もバランス良く満たすのは **WPF**（フォーカス唯一の明示的防御線・PDFsharp 直結・資産無傷）。Qt は CAD/PDF 随一なれど C# を捨て、しかもフォーカスは完全無傷でない。

**結論（推奨）**: **WPF 本命・Qt 対抗・Avalonia クロス保険**とし、**確定前に #6179型シナリオのPoCで裏取り**する。全研究者一致の知見にも適う堅実な道。

## 7. 次の一手（殿の裁定待ち）

- 殿が本書を熟考し、路線A/B と本命候補を裁定する。
- 裁定後、有力候補で **フォーカス保持PoC**（PointerReleased 後のフォーカス保持・大量記号描画・PDF出力の最小検証）を実施し、実測で最終確定。
- 確定後、`C:\ECAD2` へ 4セッションを立て直し、選定スタックで雛形・アーキ設計へ着手。

---

**現況**: スタック未確定・殿の熟考中。実装着手なし。
