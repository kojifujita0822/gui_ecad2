# T-049 静的レビュー（隠密）

対象コミット: `bfa37cc`（デバイス名編集中の未確定編集を保存/破棄判定前に確定）
対象ファイル: `src/Ecad2.App/MainWindow.xaml.cs`（22行追加12行削除、1ファイルのみ）

## 結論

**要修正なし。** 家老指定の3観点・code-reviewスキル（medium effort、8角度）とも重大な指摘なし。

---

## 観点1: RED先行証明不可の妥当性（侍申告の検分）

**妥当と判断。**

- 新設`CommitDeviceNameEdit()`は`DeviceNameBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource()`と`RedrawCanvas()`の2呼び出しのみで、条件分岐（判定ロジック）を一切含まない。「常時呼んでよい」という設計方針自体が、複雑な判定を`SelectedElementDeviceName`セッターの同値早期return（`MainWindowViewModel.cs:1199`）へ委譲する形で単純化している。
- T-047型パターン（判定ロジックを純粋関数へ切り出しユニットテスト化）は、切り出すべき判定ロジックがそもそも存在しない本件には適用対象がない。`CommitDeviceNameEdit`自体はWPF `TextBox`/`BindingExpression`という具体的FrameworkElementに直接依存するView層の処理であり、これはユニットテスト化不可能な性質（STA/Window未対応、`docs-notes/handover-next-session.md`既定方針）。
- セッター本体（ViewModel層、純粋ロジック）は既存テスト`SelectedElementDeviceName_Set_MarksDirty`・`SelectedElementDeviceName_Set_WithNewDeviceName_UsesResolvedDeviceClass`（`MainWindowViewModelTests.cs:65,373`）でカバー済み。T-049コミット自体はテストファイルへの変更なし（範囲外、妥当）。

## 観点2: CommitDeviceNameEditの呼び出し網羅

**網羅性に問題なし。**

呼び出し元は4箇所を確認：
1. `DeviceNameBox_LostKeyboardFocus`（139行目）
2. `DeviceNameBox_PreviewKeyDown` Enter（145行目）
3. `SaveDocument()`冒頭（197行目）— Ctrl+S/ツールバー/メニューを一元カバーする既存の単一ゲートウェイ（T-024由来）
4. `ConfirmDiscardIfDirty()`冒頭（269行目）— 新規/開く/ウィンドウクローズを一元カバーする既存の単一ゲートウェイ（T-024由来）

家老懸念の追加経路を個別確認：
- **Ctrl+W等の他ショートカット**：grep該当なし（存在しない）
- **タブ切替（シート切替）**：`SheetNavList`は`SelectedItem`双方向バインドだが、操作にはマウスクリックか当該コントロール自体へのフォーカス移動を要する。DeviceNameBox編集中にフォーカスを保持したままシートを切り替えるグローバルショートカットは存在しない（マウスクリック等は必然的にLostKeyboardFocusを先に発火させ`CommitDeviceNameEdit`でカバーされる）
- **Undo機構**：本アプリにCtrl+Z/UndoCommand等の実装が見当たらず、該当なし（P-032で「シート追加削除がUndo対象外」との気づきはあるが、グローバルUndo機構自体の実在は本調査では確認できず）
- `ReplaceDocument`（Document丸ごと差し替え、新規/開く経由）は`ConfirmDiscardIfDirty()`を通過後にのみ呼ばれるため、別途カバー不要と確認

軽微な冗長性（バグではない）：`ConfirmDiscardIfDirty`のYes分岐で`SaveDocument()`を呼ぶ際、`CommitDeviceNameEdit`が2回発火するが、2回目はセッターの同値早期returnで無害。

## 観点3: SetProperty早期returnの再発トラップ確認

**該当なし。**

`SelectedElementDeviceName`セッター（`MainWindowViewModel.cs:1199`）の早期return条件`if (oldName == newName) return;`は、値（デバイス名文字列）そのものの直接比較。過去の罠（`CurrentSheetIndex`等の代理キーが数値上偶然一致しクリア処理がスキップされる型、`ecad2_setproperty_early_return_trap.md`）とは構造が異なり、該当しない。`CommitDeviceNameEdit`自体も判定ロジックを持たないため、この罠の対象コードが存在しない。

---

## code-reviewスキル（medium、8角度・candidate 2件→Verify後いずれもREFUTED）

- **Angle A（line-by-line）候補**：`DeviceNameBox_PreviewKeyDown`のEnter処理で`e.Handled=true`未設定、伝播による誤動作懸念 → **REFUTED**。当該コードはT-049の差分スコープ外（過去から一貫して未設定）。かつ懸念先の`IsDefault`ボタン（配置バー内`PlacementOkButton`）は`IsPlacementBarVisible`により`DeviceNameBox`（プロパティパネル）と排他制御されており、同時フォーカス不能な構造。
- **Altitude候補**（2件統合）：`CommitDeviceNameEdit`呼び出しがテストで強制されず、将来の第3入口追加時に呼び忘れる構造的リスク → **REFUTED**。View層code-behindは既存方針として非テスト対象（STA/Window未対応、T-047で確立済みの切り分け）であり、T-049固有の新規欠陥ではなく既存の構造的特性の一事例。P-040のDispatcherアーキテクチャテスト（否定リスト型）とは検出原理が逆（未来の欠落は文字列grepで検出不可能）で、同種の機械的担保は移植不可能と判断。
- Angle B（removed-behavior）・Reuse・Simplification・Efficiency・Conventions：候補なし。

## 不明点

- Undo機構自体の実在（grep該当なしだが、UI Automation等の別実装形態の可能性は排除しきれず）

## 派生提案の有無

なし。
