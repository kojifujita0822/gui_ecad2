# T-044 GuiEcad比較調査（隠密）

> 2026-07-07 隠密調査。殿発意「前作GuiEcadではOR配線ロジックが正しく動いていた、ecad2との差分を
> 検証せよ」。侍の修正（T-044往復1周目、`d04c9a3`）と並行実施。Exploreエージェントによる
> GuiEcad実ソース（`C:\Users\kojif\Desktop\生産物\gui_ecad\src`）の網羅的調査。

---

## 総括

**重要な前提の訂正**：GuiEcadには、ecad2の`isOr`フラグに相当する「OR接続（並列回路）を要素配置と
同時に自動生成する」ロジックは**存在しない**。縦コネクタ（`VerticalConnector`）は常にユーザーが専用
ツール（「分岐」ツール）でドラッグ操作して手動配置するものであり、要素配置コマンドは縦コネクタの
生成を一切行わない。「前作では正しく動いていた」という殿の前提は、厳密には「自動生成ロジックそのもの
が前作に存在しなかった」という意味で成立しない可能性が高い（推測：殿が念頭に置いているのは「手動配線
モデルなので冗長分岐・コネクタ見落としという概念自体が起きなかった」という意味と考えられる）。

**ただし**、GuiEcadの**ネットリスト構築ロジック（`NetlistBuilder.cs`）はecad2へほぼ逐語的に移植されて
おり、「既存コネクタを`sheet.Connectors`全体から正として参照する」という設計原則を持つ**。この原則は、
侍が既に着手した修正（`d04c9a3`、T-044往復1周目）と完全に整合することを確認した。

---

## 観点(1): GuiEcadにOR自動生成ロジックが存在するか —— **存在しない（確認）**

- `GuiEcad.App/Commands/ElementCommands.cs:5-19`の`PlaceElementCommand`は`sheet.Elements.Add`のみを
  行い、`sheet.Connectors`には一切触れない。
- `GuiEcad.App/MainPage.Pointer.cs:84-136`（`PlaceElementAt`）にも`isOr`のような引数・分岐、縦コネクタ
  生成コードは一切含まれない。
- 縦コネクタは、ユーザーが「分岐」ツール（`ToolMode.PlaceConnector`）を選び列境界をドラッグすることで
  初めて生成される（`MainPage.Pointer.cs:643-649`の`AddConnectorCommand`）。
- 設計書にも明記：`docs/drawing-spec.md:38`「並列回路は要素の行またぎではなく縦コネクタで表現」。
  ヘルプ文言（`MainPage.Menu.cs:506`）「縦の分岐は『分岐』ツールで列の交点を上から下へドラッグ」。
- `isOr`/`並列回路自動`/`OrConnect`等のキーワード検索（`src`全体）でヒットなし。

## 観点(2): 母線際の冗長縦分岐を回避する設計だったか —— **前提が成立しない（確認）**

自動生成ロジック自体が無いため、「列位置に応じて生成有無を条件分岐する」ような判定は存在しない。母線際
での冗長分岐が起こるか否かは完全にユーザーの手作業（どこにドラッグするか）に委ねられている。「常に
無条件生成」でも「列位置で回避」でもなく、そもそも自動判定という概念がGuiEcadには無い。

（推測）ecad2のT-044のような「トポロジー等価なら左縦分岐を省略する」という最適化自体、GuiEcadの設計
思想には存在しない。GuiEcadでは「配線の見た目＝ユーザーの意図した結線」であり、システムが勝手に配線を
間引く発想がそもそも無い。

## 観点(3): 連鎖OR（3階層以上）の扱いと既存コネクタチェック機構 —— **配置側にはないが、ネットリスト側に核心となる設計原則あり（確認）**

GuiEcadには「新規の縦コネクタ生成前に既存コネクタの有無を事前チェックする」仕組みは無い（自動生成が
無いため必然性がない）。連鎖的なOR配置もユーザーが手動で1本ずつ積み重ねるだけで、システムによる整合性
の自動判定は行われない。

**ただし**、「既存コネクタを考慮する」処理自体は、電気的トポロジーを計算する側（ネットリスト構築・
描画側）に明確に存在し、これがecad2のT-044バグと直接対応する重要な設計原則である：

```csharp
// GuiEcad.Core/Simulation/NetlistBuilder.cs:240-247
private static bool LeftRailReached(Sheet sheet, int row, int leftBoundary)
{
    foreach (var c in sheet.Connectors)
        if ((c.TopRow == row || c.BottomRow == row) && c.Column > 0 && c.Column <= leftBoundary)
            return false;
    return true;
}
```

この関数は、ある行が母線に直結しているかを判定する際、**`sheet.Elements`ではなく`sheet.Connectors`
（既存の縦コネクタ一覧）を全走査**して、その行に絡む縦コネクタが左側に存在するかを確認している。
3階層以上の連鎖ORでも、各階層で新たに引かれた縦コネクタが`sheet.Connectors`に蓄積される限り、ネット
リスト計算は常にそれらを正しく参照するため、電気的トポロジーは壊れない。

GuiEcadでは「配置（要素追加）」と「電気的整合性の判定（ネットリスト構築）」が完全に分離されており、
後者は常に`sheet.Connectors`全体を正として計算する。**ecad2のT-044初期実装バグ（0d3425a時点）は、
本来ネットリスト計算側にしか存在しなかった「既存コネクタ参照」という発想を、配置ロジック側（縦分岐
省略の事前判定）に新規実装した際、当初`sheet.Elements`のみに限定してしまったことが原因**と考えられる
（推測）。

## 観点(4): ecad2とGuiEcadの対応関係

| # | 処理内容 | ecad2 | GuiEcad | 対応関係 |
|---|---|---|---|---|
| 1 | `VerticalConnector`データモデル | `Ecad2.Core/Model/Element.cs:122-128` | `GuiEcad.Core/Model/Element.cs:122-128` | 確認：フィールド名・コメントまで完全一致、逐語移植 |
| 2 | `Sheet.Connectors` | `Sheet.cs` | `GuiEcad.Core/Model/Sheet.cs:12` | 確認：同名・同型 |
| 3 | ネットリスト`LeftRailReached`/`RightRailReached` | `Ecad2.Core/Simulation/NetlistBuilder.cs:241-256` | `GuiEcad.Core/Simulation/NetlistBuilder.cs:240-256` | 確認：ロジックほぼ同一、`sheet.Connectors`全走査で判定する設計を継承 |
| 4 | 描画`LeftTerminator`/`RightTerminator` | `DiagramRenderer.cs:373-392`（**`BottomRow==row`のみ**） | `DiagramRenderer.cs:366-384`（`TopRow\|\|BottomRow`） | **意図的差分**：ecad2はT-026（P-003）で「TopRow/BottomRowを区別せずOR基準行が母線から浮くバグ」を自前で発見・修正済み。GuiEcad由来のロジックを移植後、独自に改良した実績がある |
| 5 | 要素配置コマンド（単純Add、コネクタ非連動） | `MainWindowViewModel.cs:344`（直接呼び出し） | `Commands/ElementCommands.cs:17`（Undo対応コマンド経由） | 確認：発想は同じ（単純Add）、実装様式のみ異なる |
| 6 | OR自動配線（基準行探索→縦コネクタ2本生成） | `MainWindowViewModel.cs:354-393` | **該当なし** | ecad2固有の新規機能。GuiEcadに移植元は無い |
| 7 | T-044省略判定`NothingBetweenRailAndColumn` | `MainWindowViewModel.cs:387-389`（現行、`sheet.Elements`と`sheet.Connectors`両方参照） | **該当なし（GuiEcadに"省略"概念自体が無い）** | ecad2独自実装。`sheet.Connectors`を見る発想は#3のネットリスト側ロジックからの類推移入と推測 |

## 観点(5): 侍の修正方針の妥当性 —— **GuiEcadの実績あるロジックと完全に整合（確認）**

侍の修正（`d04c9a3`、T-044往復1周目）後の現行コードを確認した：

```csharp
// MainWindowViewModel.cs:387-389（現行）
bool NothingBetweenRailAndColumn(int row, int column)
    => !sheet.Elements.Any(el => el.Pos.Row == row && el.Pos.Column < column)
    && !sheet.Connectors.Any(c => (c.TopRow == row || c.BottomRow == row) && c.Column <= column);
```

この`c.TopRow == row || c.BottomRow == row`という条件は、GuiEcadの`NetlistBuilder.LeftRailReached`
（`c.TopRow == row || c.BottomRow == row`）と**完全に同一の判定パターン**であり、GuiEcadのネットリスト
層が持つ設計原則（「行に紐づく縦コネクタは`sheet.Connectors`から漏れなく参照する」）を配置ロジック側に
正しく転写した状態になっている。侍の修正方針は、GuiEcadの実績あるロジックと整合していると判断する。

### 追加所見（設計改善提案、今回のバグとは別軸）

1. **判定ロジックの分散（Single Source of Truth未確立）**：GuiEcad由来の「母線到達判定」は現状
   `NetlistBuilder`（Top||Bottom）・`DiagramRenderer`（Bottomのみ、T-026で意図的に変更）・
   `MainWindowViewModel.NothingBetweenRailAndColumn`（Top||Bottom、侍修正後）の**3箇所**に散在して
   おり、うち2箇所（NetlistBuilderとViewModel側）は一致するが、`DiagramRenderer`だけが意図的に異なる
   条件を持つ非対称性が既に存在する（今回のバグには直接影響しなかったが、隠密の事前調査・T-044レビュー
   でも既に言及済みの既知の差分）。将来また別の箇所に同種の判定を追加する際、同じ「参照漏れ」のバグが
   再発するリスクがある。`Ecad2.Core`側に共有関数として集約することを技術的負債として記録することを
   提案する（今回の往復での対応は不要、次に母線判定ロジックへ触れる機会に検討）。
2. **WireBreak（配線分断）の考慮漏れの可能性**：GuiEcadの`NetlistBuilder`は`severed(...)`という別の
   判定で配線分断（`WireBreak`）も母線到達判定に組み込んでいる。ecad2にも`WireBreak`相当のモデルが
   存在する（`Ecad2.Core/Model/Element.cs`等でGrep確認）が、`NothingBetweenRailAndColumn`が
   `WireBreak`を考慮しているかは今回未確認。もし考慮していない場合、「配線分断がある行でも縦分岐を
   省略してしまう」という別種の懸念が理論上ありうる（未検証、推測。実害の有無は別途調査が必要）。

---

## 忍者・家老への申し送り

- 侍の修正（`d04c9a3`）は、GuiEcadの実績あるネットリスト計算ロジックと同じ判定パターンを採用しており、
  設計方針としては妥当。ただし、この修正自体の正式なコードレビュー（テストの実効性、連鎖ORケースの
  回帰テスト追加等）は本調査の範囲外であり、別途レビュー采配が必要と考える。
- WireBreak考慮の要否（追加所見2）は、今回のT-044スコープでは未検証。必要なら別途調査する。

---

## 出典・参照

- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\Commands\ElementCommands.cs`
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Pointer.cs`
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Tools.cs`
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.Core\Simulation\NetlistBuilder.cs`
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.Core\Rendering\DiagramRenderer.cs`
- `C:\Users\kojif\Desktop\生産物\gui_ecad\docs\drawing-spec.md`
- `src\Ecad2.App\ViewModels\MainWindowViewModel.cs`（現行、`d04c9a3`後）
- `src\Ecad2.Core\Simulation\NetlistBuilder.cs`・`src\Ecad2.Core\Rendering\DiagramRenderer.cs`
- `docs\archive/ecad2-t040-wire-survey-onmitsu.md`（前回のGuiEcad手動配線UI調査）
- `docs\ecad2-t044-review-onmitsu.md`（T-044所見1、CONFIRMED重大バグ）
- Exploreエージェントによるコード調査（事実は「確認」、推測は「推測」と明記して報告）
