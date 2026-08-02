using System.Windows;
using Ecad2.App.Views;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-144往復1周目: 履歴を積む経路が <c>StateChanged</c> を発火させることを測る。
/// <para>
/// <b>なぜこのテストが要るか</b>：<c>PartEditorDialog</c> は <c>StateChanged</c> を購読して
/// <c>UndoButton.IsEnabled = ShapeCanvas.CanUndo</c> を更新する。ゆえに履歴を積むだけで通知を欠くと、
/// <b>内部の <c>_undoStack</c> には積まれておるのに「元に戻す」ボタンが有効にならず、
/// UI から Undo へ辿り着けぬ</b>という状態になる。忍者の実機確認（2026-08-02）で実際にこれが起き、
/// 幅・高さ・役割・配置先のいずれを変えてもボタンが一度も有効にならなかった。
/// </para>
/// <para>
/// <b>【STA で包む形を採った理由——これは最後の手段である】</b>
/// <c>samurai.md</c>「『テストしにくい』は設計の匂い——まず設計を変えて純粋関数にできぬかを問う。
/// STA包み込み等は最後の手段」に従い、先に純粋関数化を試みた。判定ロジックは
/// <see cref="PartEditorUndoRules"/> として既に切り出し済みである（T-144本体）。
/// <b>残るのは「キャンバスがイベントを発火させるか」であり、これは WPF の
/// <c>FrameworkElement</c> そのものの振る舞いゆえ純粋関数へは追い出せぬ。</b>
/// <b>T-144 の当初、侍はここを「基盤が無いゆえ張れぬ」と判じて実機へ委ねたが、
/// その判断が甘かった</b>——外部依存を足さずとも下記10行のヘルパーで足りた。
/// <b>結果として、実機で露見した穴を先に捕らえられる網であった。</b>
/// </para>
/// </summary>
public class PartEditorCanvasNotifyTests
{
    /// <summary>STA スレッドでテスト本体を走らせる（実体は <see cref="StaTestRunner"/>）。
    /// T-132増分3で二人目の使い手が現れたため共有クラスへ切り出した。
    /// 呼び名はここに残し、本テスト群の見た目は変えていない。</summary>
    private static void RunSta(Action action) => StaTestRunner.Run(action);

    private static PartEditorExternalState State(int width = 3, int height = 5)
        => new(width, height, PartRole.ContactNO, SheetAffinity.Any);

    /// <summary>
    /// 入力欄の変更を積む経路が <c>StateChanged</c> を発火させること。
    /// <b>これが本往復のNGの直接の原因であった。</b>
    /// </summary>
    [Fact]
    public void 外部状態のスナップショットを積めばStateChangedが発火する()
        => RunSta(() =>
        {
            var canvas = new PartEditorCanvas();
            int fired = 0;
            canvas.StateChanged += (_, _) => fired++;

            canvas.PushExternalStateSnapshot(State());

            Assert.Equal(1, fired);
        });

    /// <summary>
    /// 積んだ結果 <c>CanUndo</c> が真になること。
    /// <b>発火と履歴は別物ゆえ、両方を測る</b>——通知だけ出て履歴が積まれておらねば、
    /// ボタンは有効になるが押しても何も起きぬ。
    /// </summary>
    [Fact]
    public void 外部状態のスナップショットを積めばCanUndoが真になる()
        => RunSta(() =>
        {
            var canvas = new PartEditorCanvas();
            Assert.False(canvas.CanUndo);

            canvas.PushExternalStateSnapshot(State());

            Assert.True(canvas.CanUndo);
        });

    /// <summary>
    /// Undo を実行すれば履歴が減り、<c>StateChanged</c> が再び発火すること。
    /// <b>積む側だけでなく戻す側も通知すること</b>を押さえる——片方だけでは
    /// 「戻したのにボタンが有効なまま」といった食い違いを素通しする。
    /// </summary>
    [Fact]
    public void Undoを実行すればCanUndoが偽へ戻りStateChangedが発火する()
        => RunSta(() =>
        {
            var canvas = new PartEditorCanvas();
            canvas.PushExternalStateSnapshot(State());

            int fired = 0;
            canvas.StateChanged += (_, _) => fired++;

            canvas.Undo();

            Assert.False(canvas.CanUndo);
            Assert.Equal(1, fired);
        });
}

// 【本テスト群が測れておらぬこと・侍が自ら区切る】
// 1. 既存経路（SetSelectedPortKind 等）との対照を取っていない。接続点を選択させる公開経路が無く、
//    テスト専用の口を本番コードへ開けるのは本往復の範囲を超えるため見送った。
//    ゆえに「新設経路だけが作法から外れていないか」は、本テストではなく
//    PartEditorCanvas.cs 内の PushUndo 系呼び出しを数えることで確認した（往復1周目の報告に記載）。
// 2. PartEditorDialog 側の繋ぎ込み（StateChanged を購読して UndoButton.IsEnabled を更新する経路）は
//    測れていない。Window 派生ゆえ。そこは実機確認に委ねる。
