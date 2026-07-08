using System.Windows;

namespace T041DragPoc;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Canvas.StatusChanged += status => StatusText.Text = status;
        Loaded += (_, _) => Canvas.RunRenderBenchmark();
    }
}
