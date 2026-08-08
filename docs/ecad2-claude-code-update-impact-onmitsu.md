# Claude Codeアップデートが四セッション運用へ及ぼす影響調査（S-001）

> 2026-08-08 隠密調査。殿の直々のご下命（家老経由、task_id＝S-001）。
> 現行バージョン＝2.1.226（家老が`claude --version`で実測、2026-08-08）。
>
> 手法＝`claude-code-guide`エージェントへ一次調査を委譲した上で、既存memory
> （`project_subagent_delegation_limits`）と食い違う重要な指摘（サブエージェント上限の変遷）が
> 出たため、公式`CHANGELOG.md`（`raw.githubusercontent.com/anthropics/claude-code/main/CHANGELOG.md`）
> をcurlでscratchpadへ直接取得し、隠密自身が一次ソースで裏取りした。
>
> **確認範囲**＝CHANGELOG.mdの`v2.1.210`〜`v2.1.226`区間（直近17バージョン）を全文通読。
> それより古い版・GitHub Issues・公式ドキュメント本体は未確認。

---

## 結論（要点のみ）

1. **claude-peers（MCP）の役割自動決定フロー**——影響なし。確認範囲内で`list_peers`/`set_summary`/
   `send_message`/`check_messages`相当のパターン仕様に変更する記述は無かった。
2. **サブエージェント委譲の上限**——**既存memoryが陳腐化していた。訂正済み**（下記2節）。
   累計200件の上限はv2.1.224で撤廃されており、現行は同時実行20件・ネスト深さ3のみが効く。
3. **スキル・hooks・settings.json**——`dir/**`型ルールの挙動修正（v2.1.214）が唯一の要確認点。
   `ecad2-ui-automation`等の独自スキル・現行hooksの記述には該当パターンが無ければ影響なし。
4. **Grep contentモード・出力破損**——確認範囲内で`<invoke>`生表示等の破損症状に直結する修正の
   記述は見当たらなかった。現行の回避運用（`files_with_matches`/`count`＋`Read`直読）を継続する
   のが妥当。

我らが直ちに手を打つ要のあるものは無い。ただし2節のmemory訂正は`memory`側で既に反映済み。

---

## 1. claude-peers（社内MCPサーバー）の動作

確認範囲（v2.1.210〜v2.1.226）で以下のMCP関連変更が見つかった。

| バージョン | 内容 |
|---|---|
| v2.1.224 | cross-session `SendMessage`を追加（同一マシン上の複数Claude Codeインスタンス間でメッセージ送受信）。**これは`claude-peers`と類似の目的を持つ公式機能だが、別系統**——`claude-peers`の`send_message`/`list_peers`ツール自体を置き換える記述は無い |
| v2.1.221 | Remote Control関連の改善（MCP接続の切断検知等）。`claude-peers`とは無関係 |

**判定**＝`list_peers`/`set_summary`/`send_message`/`check_messages`の呼び出しパターン・
`startup-auto.md`の役割自動決定フロー（step0〜6）に影響する変更は確認範囲内で見当たらなかった。
**測ったこと**＝CHANGELOG本文の直読。**測っておらぬこと**＝`claude-peers`自体（社内実装）の側の
互換性——これはMCPサーバー実装側の問題であり、Claude Code本体の変更とは別に確認が要る（射程外）。

---

## 2. サブエージェント委譲の上限——既存memoryが陳腐化していた

`memory: project_subagent_delegation_limits`（家老が2026-08-07共有）は
「v2.1.212以降、セッション累計200・同時実行20の上限あり」と記していたが、
**この記述は現行v2.1.226には当てはまらぬ**。一次ソース直読で確認した正しい変遷は以下。

| バージョン | 変更内容 | CHANGELOG該当行 |
|---|---|---|
| v2.1.212 | セッション**累計200件**の上限を導入（`CLAUDE_CODE_MAX_SUBAGENTS_PER_SESSION`で上書き可、`/clear`でリセット） | 「Added a per-session cap on subagent spawns (default 200...)」 |
| v2.1.217 | **同時実行20件**の上限を導入（`CLAUDE_CODE_MAX_CONCURRENT_SUBAGENTS`）。併せてネスト委譲を既定で禁止（深さ1） | 「Added a cap on concurrently-running subagents (default 20...)」「Changed subagents to no longer spawn nested subagents by default」 |
| v2.1.219 | ネスト委譲の既定深さを1→**3**へ変更（`CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH`で上書き可） | 「Subagents can now spawn nested subagents up to depth 3 by default (was 1)」 |
| v2.1.224 | **累計200件の上限を撤廃**。「長時間セッションでも新規エージェントを拒否しない」（同時実行20・深さ3の上限は継続） | 「Removed the 200-subagent-per-session spawn cap; long-running sessions no longer refuse new agents (concurrency and depth limits still apply)」 |

**現行（v2.1.226）で効いている上限は、同時実行20件とネスト深さ3のみ。累計200件の上限は存在しない。**

### なぜ食い違いが生じたか

`claude-code-guide`エージェントの一次報告は「v2.1.217で累計200件の上限が撤廃された」としていたが、
これは誤り。撤廃はv2.1.224であり、v2.1.217は**同時実行20件の上限を新設した**バージョンにて、
むしろ制約が一つ増えた回にござる。隠密が自らCHANGELOG.mdを直読し、この誤りを訂正した。

**対応**＝`memory: project_subagent_delegation_limits`を本日付で訂正済み（隠密が直接編集）。
`MEMORY.md`索引行も併せて更新した。

### 実務上の意味

ecad2の4セッション体制は`Explore`等への並列委譲を多用する（`onmitsu.md`調査ワークフロー等）。
従来「累計200件に達したのでは」という切り分けを想定していたが、**現行バージョンではこの切り分けは
成立しない**。並列調査が原因不明に詰まった場合、疑うべきは**同時実行20件**の方である。

---

## 3. スキル・hooks・settings.json

確認範囲で見つかった関連変更。

| バージョン | 内容 |
|---|---|
| v2.1.214 | 単一segmentの`dir/**`型許可ルール（例＝`Edit(src/**)`）が、ツリー内の任意の場所にある`dir/`配下にまで誤って自動承認してしまう不具合を修正。正しくは`<cwd>/dir`配下のみに限定される |
| v2.1.218 | `/code-review`がバックグラウンドsubagentとして走るよう変更。会話を専有しなくなった |
| v2.1.215 | `/verify`・`/code-review`のモデル自身による自動起動を廃止し、明示起動専用へ（`onmitsu.md`該当節に既に反映済み、隠密役儀書に記載の恒久事象と一致） |

**判定**＝ecad2の`.claude/settings.json`・独自スキル（`ecad2-ui-automation`）で`dir/**`型の許可
ルールを使っている場合、v2.1.214の修正で「以前は誤って広く自動承認されていたものが、正しく
`<cwd>/dir`限定になった」可能性がある——**動作が狭まる方向の修正ゆえ、意図せぬ許可漏れとして
気づかれることがあれば、この修正が原因の可能性を疑うとよい**。**測っておらぬこと**＝ecad2の
現行`settings.json`の実際のルール記述内容と、この修正が実際に挙動を変えたか否かの実地確認
（射程外、必要であれば別途`settings.json`の内容確認から着手する）。

---

## 4. Grep contentモード・出力破損

`docs-notes/output-corruption-log.md`が記録する「`<invoke>`生表示等の出力破損」症状に対応する
CHANGELOG記載を、確認範囲（v2.1.210〜v2.1.226）で検索したが**見当たらなかった**。

Grep関連で見つかったのは以下のみ（出力破損とは別種の不具合）。

| バージョン | 内容 |
|---|---|
| v2.1.210台以前（本調査の確認範囲外、`claude-code-guide`の一次報告による言及） | 無効な正規表現・nullバイト関連の修正 |

**判定**＝出力破損（`&lt;invoke&gt;`生表示等）に直結する修正が入った形跡は確認範囲内では無い。
**現行の回避運用（`Grep`の`content`モード回避、`files_with_matches`/`count`＋`Read`直読）は
引き続き継続するのが妥当**。ただし本調査は**CHANGELOG本文の記述を根拠とする消極的確認**であり、
「修正されていないこと」の積極的な証明ではない——記述が無いことは、直っていないことの確証には
ならぬ。実際に直ったか否かを確かめたい場合は、対照実験（`content`モードを実機で使い症状再現を
試す等）が要るが、これは`feedback_no_live_injection_on_shared_main`等の既存の禁則（共有main上での
一時注入検証禁止）とも関わるため、試すなら家老の裁可を要する。

---

## 5. 我らが手を打つ要のあるもの・その所在と規模

| 項目 | 規模 | 所在 |
|---|---|---|
| `memory: project_subagent_delegation_limits`の訂正 | 軽微・完了済み | 本セッションで隠密が既に編集 |
| `settings.json`の`dir/**`型ルール確認（v2.1.214影響の実地確認） | 軽微・任意 | 気になる挙動が出た時に着手すればよい、今すぐの要はない |
| その他 | なし | 確認範囲内で運用変更を要する変更は見当たらなかった |

---

## 落とし先

- サブエージェント上限の訂正＝`memory: project_subagent_delegation_limits`（完了済み）
- 本調査書自体の存在＝家老が次回のClaude Codeバージョン確認時の参照先として使えるよう、
  ここに一次ソースの直読結果を残した
