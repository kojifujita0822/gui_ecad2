using Ecad2.App.ViewModels;
using Ecad2.App.Views;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-153（殿ご下命2026-08-16）＝配置バーの入力欄をデバイス名とコメントで共用する。
/// 隠密調査 <c>docs/ecad2-t153-comment-button-investigation-onmitsu.md</c> に対応。
/// <para>
/// 【隠密が名指しした最重要の穴】設計書2節＝<b>確定時に「表示中の値」を先に退避してから両方を渡さねば、
/// コメントを打った直後に OK を押すとコメントが落ちる。</b>本ファイルの1節がそこを固定する。
/// </para>
/// <para>
/// 【測れておらぬ範囲——形まで区切っておく】
/// <list type="number">
/// <item><b>View 層の繋ぎ込み。</b>本ファイルが測るのは <see cref="PlacementInputRules"/> という写像と、
/// <c>PlaceElementAtSelectedCell</c> という受け口の二つにて、<b>その間を繋ぐ
/// <c>PlacementOkButton_Click</c>／<c>PlacementCommentToggle_Click</c> は測れておらぬ</b>。
/// <b>素通しされる形＝「写像は正しいが、確定処理がそれを呼ばず <c>PlacementDeviceNameBox.Text</c> を
/// 直に渡す」状態</b>——この時コメントは常に空で渡り、本ファイルは全件 GREEN のまま通る
/// （<c>samurai.md</c>「述語を切り出したら呼び出し側にもテストを置く」が当たる箇所にて、
/// 呼び出し側が <c>MainWindow</c> ゆえ置けておらぬ）。<b>実機で「コメントを打って OK、パネルで見える」
/// を一度通せば塞がる</b></item>
/// <item><b>バーを開き直した折の初期化</b>（<c>ShowPlacementBar</c> 相当）。前回のコメントが
/// 持ち越されぬことは実機でしか測れぬ</item>
/// <item><b>トグルの押下表示（グレーアウト）の見え方。</b>隠密の断り＝既定の表示は薄い恐れがあり、
/// 弱ければ T-101 のハイライトの流儀へ揃える要があるやも</item>
/// </list>
/// いずれも忍者の検分へ委ねる。
/// </para>
/// </summary>
public class T153PlacementCommentTests : ViewModelTestBase
{
    private const string CustomId = "t153-part";

    private static PartDefinition CustomPart() => new()
    {
        Id = CustomId,
        Name = "T153自作",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.ContactNO,
        Ports = new() { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
        Primitives = new() { new PartLine(0, 0, 1, 0) },
    };

    // ==================================================================
    // 1. 入力欄の共用規則（純粋関数）——設計書2節の穴を直接突く
    // ==================================================================

    /// <summary>
    /// <b>本タスクの要</b>＝コメントを表示しておる最中に確定しても、打った値がコメントとして解かれること。
    /// <para>
    /// 実装が「表示中の値を退避せず、変数に入っておる値だけを渡す」形であれば、
    /// コメント欄へ打った文字はどこにも渡らず落ちる。<b>本テストはその形を検出する。</b>
    /// </para></summary>
    [Fact]
    public void コメント表示中に確定すると打った値がコメントとして解かれる()
    {
        // 入力欄には今コメントが出ており、裏にデバイス名が退避してある状態。
        var (deviceName, comment) = PlacementInputRules.Resolve(
            isCommentMode: true, visibleText: "非常停止の押釦", savedText: "X001");

        Assert.Equal("X001", deviceName);
        Assert.Equal("非常停止の押釦", comment);
    }

    /// <summary>対称の側＝デバイス名を表示しておる最中に確定した場合。
    /// <b>両モードを対にして測る</b>——片方だけでは「常に表示中の値をデバイス名として返す」実装を素通しする。</summary>
    [Fact]
    public void デバイス名表示中に確定すると打った値がデバイス名として解かれる()
    {
        var (deviceName, comment) = PlacementInputRules.Resolve(
            isCommentMode: false, visibleText: "X001", savedText: "非常停止の押釦");

        Assert.Equal("X001", deviceName);
        Assert.Equal("非常停止の押釦", comment);
    }

    /// <summary>トグルを往復させても両方の値が保たれること（設計書2節「切替時の値の保持」）。
    /// <para>
    /// 実際の切替と同じ順で <c>Resolve</c> → <c>ForMode</c> を通し、二度押して元へ戻す。
    /// <b>入力値は非対称に選んである</b>——両方を同じ文字列にすれば、取り違える実装でも通ってしまう
    /// （<c>samurai.md</c> テスト入力の対称性・退化性チェック）。
    /// </para></summary>
    [Fact]
    public void トグルを往復させても両方の値が保たれる()
    {
        string visible = "X001";        // 入力欄（デバイス名を表示中）
        string saved = "";              // 退避（コメントはまだ空）
        bool isCommentMode = false;

        // 一度目＝コメントへ切替。
        (string dn1, string cm1) = PlacementInputRules.Resolve(isCommentMode, visible, saved);
        isCommentMode = true;
        (visible, saved) = PlacementInputRules.ForMode(isCommentMode, dn1, cm1);
        Assert.Equal("", visible);      // コメントは空から始まる
        Assert.Equal("X001", saved);    // デバイス名が退避された

        // コメントを打つ。
        visible = "非常停止の押釦";

        // 二度目＝デバイス名へ戻す。
        (string dn2, string cm2) = PlacementInputRules.Resolve(isCommentMode, visible, saved);
        isCommentMode = false;
        (visible, saved) = PlacementInputRules.ForMode(isCommentMode, dn2, cm2);

        Assert.Equal("X001", visible);          // デバイス名が戻ってきた
        Assert.Equal("非常停止の押釦", saved);   // 打ったコメントは失われておらぬ
    }

    /// <summary>ラベルはモードに従うこと（語彙は殿ご裁可＝「デバイス名」に揃える）。</summary>
    [Theory]
    [InlineData(false, "デバイス名:")]
    [InlineData(true, "コメント:")]
    public void ラベルはモードに従う(bool isCommentMode, string expected)
    {
        Assert.Equal(expected, PlacementInputRules.LabelFor(isCommentMode));
    }

    /// <summary>UIA名もモードに従うこと（隠密の指摘2026-08-16）。
    /// <para>
    /// 視覚のラベルだけを切り替えて UIA 名を据え置けば、<b>コメントを入れておる最中も支援技術には
    /// 「デバイス名」と伝わる</b>。実機で UIA から引く者の取り違えの因にもなる。
    /// <b>コロンが付かぬのは既存の作法</b>——XAML の <c>AutomationProperties.Name</c> は
    /// 元より「デバイス名」にて、コロンは画面上の見出しの体裁にすぎ申さぬ。
    /// </para></summary>
    [Theory]
    [InlineData(false, "デバイス名")]
    [InlineData(true, "コメント")]
    public void UIA名もモードに従う(bool isCommentMode, string expected)
    {
        Assert.Equal(expected, PlacementInputRules.AutomationNameFor(isCommentMode));
    }

    /// <summary>ラベルと UIA 名が同じ綴りを共有し、コロンの有無だけで違うこと。
    /// <para>
    /// 分けて持てば、片方だけ直した折に「画面には『コメント:』と出るが UIA には『デバイス名』」
    /// という食い違いが生まれる。本テストはその食い違いを捕らえる。
    /// </para>
    /// <para>
    /// <b>【射程を実測で区切った・侍の自己訂正2026-08-16】</b>初稿は「実装は <c>const</c> の連結で
    /// 実体を一つにしてあり、本テストはその形が保たれておるかを見る」と書いておったが、<b>過大であった</b>。
    /// <c>const</c> の連結をやめて直書きへ戻す改変を当てたところ、<b>本テストは鳴らなんだ</b>
    /// ——値が同じである限り通るゆえ。<b>すなわち本テストが測るのは「値の一致」までにて、
    /// 「綴りが一箇所に保たれておるか」は測れておらぬ。</b>
    /// 二重管理そのものを禁ずる網ではなく、二重管理が食い違った時にだけ鳴る網にござる。
    /// </para></summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ラベルはUIA名にコロンを足したものである(bool isCommentMode)
    {
        Assert.Equal(PlacementInputRules.AutomationNameFor(isCommentMode) + ":",
                     PlacementInputRules.LabelFor(isCommentMode));
    }

    // ==================================================================
    // 2. 保存経路（ViewModel）——配置と同時にコメントが機器表へ入る
    // ==================================================================

    private MainWindowViewModel CreateViewModelWithPart()
    {
        var vm = CreateViewModel();
        vm.PartPalette.SaveNewPart(CustomPart());
        vm.NewDocument();
        return vm;
    }

    /// <summary>新規デバイス名なら、生成される <c>Device</c> にコメントが入ること。</summary>
    [Fact]
    public void 新規デバイス名の配置でコメントが機器表へ入る()
    {
        var vm = CreateViewModelWithPart();
        vm.SelectedCell = new GridPos(0, 0);

        vm.PlaceElementAtSelectedCell(CustomId, "X001", isOr: false, comment: "非常停止の押釦");

        Assert.Equal("非常停止の押釦", vm.Document.Devices.ByName["X001"].Comment);
    }

    /// <summary>既存デバイス名なら上書きすること（殿ご裁可2026-08-16＝案(ii)）。
    /// <para>
    /// <b>T-036の作法（既存エントリを上書きせぬ）を、コメントについてのみ崩す形</b>にござる。
    /// 打った値が黙って消えるのを避けるための裁可にて、<b>同じデバイス名を持つ他の箇所の
    /// コメントも一斉に変わる</b>ことは殿が承知のうえ。
    /// </para></summary>
    [Fact]
    public void 既存デバイス名の配置ではコメントが上書きされる()
    {
        var vm = CreateViewModelWithPart();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(CustomId, "X001", isOr: false, comment: "最初の説明");
        vm.SelectedCell = new GridPos(1, 0);

        vm.PlaceElementAtSelectedCell(CustomId, "X001", isOr: false, comment: "改めた説明");

        Assert.Equal("改めた説明", vm.Document.Devices.ByName["X001"].Comment);
        // 機器表のエントリ自体は増えておらぬ（同一デバイス名ゆえ）。
        Assert.Single(vm.Document.Devices.ByName);
    }

    /// <summary>コメントが空なら、既存のコメントを消さぬこと。
    /// <para>
    /// <b>「打っておらぬ」と「消したい」は別物</b>にござる。欄に触れずに配置しただけで既存の
    /// コメントが消えれば、上書きを採った理由（打った値が黙って消えるのを避ける）を逆向きに犯す。
    /// 消す経路はプロパティパネル（<c>SelectedElementComment</c>）に別途在り、そちらは空を明示的に受ける。
    /// </para></summary>
    [Fact]
    public void コメントが空なら既存のコメントを消さぬ()
    {
        var vm = CreateViewModelWithPart();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(CustomId, "X001", isOr: false, comment: "残るべき説明");
        vm.SelectedCell = new GridPos(1, 0);

        vm.PlaceElementAtSelectedCell(CustomId, "X001", isOr: false, comment: "");

        Assert.Equal("残るべき説明", vm.Document.Devices.ByName["X001"].Comment);
    }

    /// <summary>コメントを渡さぬ既存の呼び出し（既定値）は従来どおり振る舞うこと。
    /// <para>
    /// 引数へ既定値 <c>""</c> を置いた判断を固定する網にござる——129箇所ある既存の呼び出しが
    /// 無改修で従来と同じ結果になることを、代表1件で押さえる。
    /// </para></summary>
    [Fact]
    public void コメントを渡さぬ配置は従来どおり空のまま()
    {
        var vm = CreateViewModelWithPart();
        vm.SelectedCell = new GridPos(0, 0);

        vm.PlaceElementAtSelectedCell(CustomId, "X001", isOr: false);

        Assert.Equal("", vm.Document.Devices.ByName["X001"].Comment);
    }

    /// <summary>デバイス名が空なら機器表を一切触らぬこと（T-036の既存の作法。コメントを足しても崩れておらぬ）。</summary>
    [Fact]
    public void デバイス名が空なら機器表を触らぬ()
    {
        var vm = CreateViewModelWithPart();
        vm.SelectedCell = new GridPos(0, 0);

        vm.PlaceElementAtSelectedCell(CustomId, "", isOr: false, comment: "行き場のないコメント");

        Assert.Empty(vm.Document.Devices.ByName);
    }

    /// <summary>コメントは <c>Device.Comment</c> という既存の器へ入るゆえ、
    /// プロパティパネルの経路（<c>SelectedElementComment</c>）からも同じ値が見えること。
    /// <para>
    /// <b>器が同じであることの確認</b>にござる——別の場所へ書いておれば、配置時に入れた値が
    /// パネルに現れず、使い手には「消えた」と映る。
    /// </para></summary>
    [Fact]
    public void 配置時のコメントはプロパティパネルからも見える()
    {
        var vm = CreateViewModelWithPart();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(CustomId, "X001", isOr: false, comment: "非常停止の押釦");

        vm.SelectedCell = new GridPos(0, 0);

        Assert.Equal("非常停止の押釦", vm.SelectedElementComment);
    }
}
