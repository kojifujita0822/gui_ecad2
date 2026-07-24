using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace T068PartEditorPoc;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Canvas.StateChanged += (_, _) => UpdateStatus();
        Loaded += (_, _) => { Canvas.Draw(); UpdateStatus(); };
    }

    private void Tool_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }) return;
        Canvas.Tool = tag switch
        {
            "Select" => EditTool.Select,
            "Line" => EditTool.Line,
            "Polyline" => EditTool.Polyline,
            "Rect" => EditTool.Rect,
            "Circle" => EditTool.Circle,
            "Arc" => EditTool.Arc,
            "Rotate" => EditTool.Rotate,
            _ => EditTool.Select,
        };
        UpdateStatus();
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => Canvas.Undo();
    private void Redo_Click(object sender, RoutedEventArgs e) => Canvas.Redo();

    private void Delete_Click(object sender, RoutedEventArgs e) => Canvas.DeleteSelected();

    private void ZoomReset_Click(object sender, RoutedEventArgs e) => Canvas.Zoom = 1.0;

    private void ArcRyBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (double.TryParse(ArcRyBox.Text, out var ry)) Canvas.SetSelectedArcRy(ry);
        Canvas.Focus();
    }

    private void UpdateStatus()
    {
        var arc = Canvas.SelectedArc;
        if (arc is { } a)
        {
            ArcRyLabel.Visibility = Visibility.Visible;
            ArcRyBox.Visibility = Visibility.Visible;
            ArcRyBox.Text = a.EffRy.ToString("0.###");
        }
        else
        {
            ArcRyLabel.Visibility = Visibility.Collapsed;
            ArcRyBox.Visibility = Visibility.Collapsed;
        }

        StatusText.Text = $"ツール: {Canvas.Tool} / 選択index: {Canvas.SelectedIndex} / " +
            $"プリミティブ数: {Canvas.Primitives.Count} / Undo: {Canvas.UndoCount} / Redo: {Canvas.RedoCount} / " +
            $"Zoom: {Canvas.Zoom:0.00}";
    }
}
