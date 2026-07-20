# 「保存済みレイアウトの読込に失敗」メッセージ調査（緊急、家老委譲）

調査日: 2026-07-20　調査者: 隠密
発端: 忍者T-089派生検証の気づきB（`docs/ecad2-t089-followup-mouseover-selection-verification-ninja.md`）

---

## 結論（先出し）

**T-099(c)案Y実装（コミット61ecdfd）自体に、Deserializeが常に失敗するという回帰は無い。**
今回のメッセージ表示は、**現在の作業ツリーの未コミット差分（T-104増分1、基本機能ダミータブ
追加）が、既存の`%AppData%`永続化ファイル（1タブ構成の旧レイアウト）との間に新規の非互換を
生んだこと**が直接原因と、コードロジックから静的に断定できる。忍者の過去「6項目OK」判定
（T-099(c)本体）は、本件とは独立した別経路の検証であり、再精査の必要は無いと判断する。

---

## (a) メッセージの出所

`MainWindow.xaml.cs:485` `LoadDockingLayoutFromFileIfExists()`内、`anyLoadFailed`が`true`の
場合に発火。**これはT-099(c)案Yのドッキング復帰ロジック（`ResetPlacementToolBarLayoutToDefault()`、
`ContentDockingハンドラ`由来）とは別のメソッドである**——アプリ**起動時**にのみ呼ばれる
（コンストラクタ、`RegisterDockingContents()`→`SerializeDefaultDockingLayouts()`→...→
`LoadDockingLayoutFromFileIfExists()`の順、191-195行目）。

両者は`TryDeserializeDockingLayout()`（536行目）という共通ヘルパーを経由するが、
`ResetPlacementToolBarLayoutToDefault()`は`_defaultDockingLayoutXmlByManager`（起動時の
自己Serialize、ハードコード既定）のみを使い、`%AppData%`の保存済みファイルは**意図的に
参照しない**（コード231-234行目コメント「保存済みファイルは意図的に参照しない——ユーザー
保存レイアウトがフロート状態だった場合、『ドッキングせよ』という操作意図と矛盾する状態を
復元してしまうため」）。

## (b) 失敗理由

`git show 61ecdfd`の全差分を精読したところ、**61ecdfd自体に`HasExpectedContent`という
Content実体欠落検出機構が新設されている**（コミットメッセージには明記されていないが、
diffには含まれる。家老采配2026-07-19「読込側防御・本丸」由来、T-099(c)復旧作業中に発覚した
`%AppData%`汚染XML対策として同一コミットに同梱された模様）。

- 旧実装（61ecdfd以前）: `Deserialize()`が例外を投げなければ無条件で`true`
- 新実装（61ecdfd以降）: `Deserialize()`成功後、さらに`HasExpectedContent(manager)`を呼び、
  その結果を返す（546行目）
- `HasExpectedContent`: 起動時にXAML構造から構築した「期待されるContentIdの集合」
  （`_expectedContentIdsByManager`、`RegisterDockingContents()`が現在のXAML状態から構築）と、
  Deserialize後の実際のContentId集合を突き合わせ、全て揃っているかを判定する

**実測**: `%AppData%\Ecad2\docking-layout\placement-toolbar.xml`（更新日時2026-07-20 08:24、
61ecdfdコミット時刻07:26より後）の中身：

```xml
<LayoutAnchorablePane>
  <LayoutAnchorable ... Title="配置ツール" IsSelected="True" ContentId="PlacementToolBar" />
</LayoutAnchorablePane>
```

**`ContentId="PlacementToolBar"`のみの1タブ構成**。

一方、現在の（未コミットの）`MainWindow.xaml`（1075-1078行目、T-104増分1）は
`ContentId="MainToolBar"`（基本機能ダミータブ）を追加した**2タブ構成**。

## (c) 未コミット差分が原因か、61ecdfd単体から失敗していたか

**現在の未コミット差分（T-104増分1のダミータブ追加）が原因と、コードロジックから静的に
断定できる**（git stash等での実機再現実験は行っていないが、以下の理由により確度は高いと
判断する。実機での最終裏付けは6節参照）。

論理の連鎖：
1. `RegisterDockingContents()`は起動時、**現在のXAML構造**から期待ContentId集合を構築する
   （373行目付近、`manager.Layout.Descendents().OfType<LayoutAnchorable>()`を走査）
2. 未コミット差分ありの状態で起動すれば、`_expectedContentIdsByManager[PlacementToolBarDockingManager]
   = {"MainToolBar", "PlacementToolBar"}`（2要素）になる
3. `LoadDockingLayoutFromFileIfExists()`は`%AppData%`の1タブ構成XML（PlacementToolBarのみ）を
   Deserialize（XML構文自体は正しいため`Deserialize()`は成功）
4. `HasExpectedContent`が`expectedIds.All(id => presentIds.Contains(id))`を評価 →
   `"MainToolBar"`が`presentIds`に無いため**必然的に`false`**
5. `TryDeserializeDockingLayout`が`false`を返す → `anyLoadFailed=true`
   → ステータスメッセージ表示（これは分岐の必然的帰結であり、偶発的挙動ではない）

**61ecdfd単体（T-104ダミータブ追加前）では、この非互換は起こり得ない**——当時のXAML構造は
`PlacementToolBar`のみの1タブ構成であり、期待ContentId集合も`{"PlacementToolBar"}`のみで
保存済みファイルと一致するため、`HasExpectedContent`は`true`を返すはずである。

`%AppData%`ファイルの更新日時（08:24）が61ecdfdコミット時刻（07:26）より後である点も、
「61ecdfd後・T-104ダミータブ追加前の間に、誰かが（旧1タブ構成のまま）アプリを起動・終了させ、
その保存がこのタイミングで走った」という推測と整合する（**この経緯自体は推測**、断定材料はない）。

MinHeight93→103の変更（同じくMainWindow.xaml未コミット差分）はContentIdに無関係であり、
本件には影響しない。

## (d) 過去「6項目OK」判定の再精査要否

**再精査は不要と判断する。**

理由: 忍者が検証した「フロート/ドッキング復帰・反復3周・DocumentPane残骸なし」は
`ResetPlacementToolBarLayoutToDefault()`（ContentDockingハンドラ経由）を通る経路であり、
(a)節の通り`%AppData%`ファイルを一切参照しない設計（ハードコード既定のみ使用）。したがって
`%AppData%`ファイルとの整合性問題（今回発覚した件）とは完全に独立した経路であり、6項目OK
判定の正当性には影響しない。

「再起動後正常」の項目のみ起動シーケンス全体（`LoadDockingLayoutFromFileIfExists`含む）を
通るが、(c)節の通り当時（61ecdfd検証時点）はT-104ダミータブが未追加でありContentIdミスマッチ
自体が存在しなかったため、当時この経路でメッセージが出ていなかったこと自体は正常な結果と
判断できる（デフォルトフォールバック経由の動作を「正常」と誤認していた、という懸念には
当たらない）。

---

## 5. 気づき（範囲外、T-104固有の設計論点）

T-104でContentIdを追加する変更は、**既存ユーザー環境に保存済みの`%AppData%`レイアウトファイル
との間で、今回のような非互換を必然的に発生させる**（新設`ContentId`を含むレイアウト変更は
すべて同型の影響を持つ）。これは実装のバグではなく`HasExpectedContent`防御機構が意図通り
動作している証拠だが、**「既定へフォールバックしました」という一言だけでは、ユーザーは
『何のレイアウトが変わったのか』を理解できない**可能性がある。T-104本実装（増分2）が
確定した際、このメッセージ文言のままでよいか、あるいは「新機能追加に伴いレイアウトが更新
されました」等の案内に変えるべきかは、UI/UX分岐として検討の余地がある（家老・殿判断事項、
隠密は着手せず気づきとして報告のみ）。

## 6. 実機での最終裏付け（推奨、忍者委譲）

上記(c)の結論は静的解析（コードロジックの必然的帰結）によるもので確度は高いが、
`git stash`（作業ツリー未コミット差分の一時退避）→アプリ起動→ステータスメッセージ不出現を
確認する実機実験で完全に裏付けられる。これは実機起動を伴うため、忍者への委譲を推奨する
（隠密は調査のみ、実装・実機起動は行わない原則）。
