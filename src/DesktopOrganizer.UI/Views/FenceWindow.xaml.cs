using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using DesktopOrganizer.UI.Helpers;
using DesktopOrganizer.UI.ViewModels;

// Aliases for ambiguous types between WPF and WinForms
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DataObject = System.Windows.DataObject;
using DragDrop = System.Windows.DragDrop;
using MessageBox = System.Windows.MessageBox;

namespace DesktopOrganizer.UI.Views;

public partial class FenceWindow : Window
{
    private readonly FenceViewModel _viewModel;
    private bool _isCollapsed = false;
    private double _expandedHeight;
    
    // For resize
    private bool _isResizing = false;
    private string _resizeDirection = "";
    private System.Windows.Point _resizeStartPoint;
    private double _startWidth;
    private double _startHeight;
    private double _startLeft;
    private double _startTop;

    public FenceWindow(FenceViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _expandedHeight = this.Height;
    }

    private void WriteDebug(string msg)
    {
        try 
        {
            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "fences_drag_debug.txt");
            System.IO.File.AppendAllText(path, $"{DateTime.Now.ToString("HH:mm:ss.fff")}: {msg}\n");
        }
        catch {}
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        // WriteDebug($"DragOver. Formats: {String.Join(", ", e.Data.GetFormats())}");
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy | DragDropEffects.Move;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        WriteDebug("Window_Drop event fired!");
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                WriteDebug($"Dropping {files.Length} files: {files[0]}...");
                _viewModel.AddFiles(files);
            }
            else
            {
                WriteDebug("FileDrop present but files array is null/empty");
            }
        }
        else
        {
            WriteDebug("No FileDrop data found");
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        
        var source = PresentationSource.FromVisual(this) as HwndSource;
        source?.AddHook(WndProc);
        
        // Hide from Alt+Tab
        var handle = source!.Handle;
        int exStyle = (int)GetWindowLong(handle, GWL_EXSTYLE);
        SetWindowLong(handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        
        DesktopWindowHelper.SendToBottom(this);
    }
    
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private const int WM_WINDOWPOSCHANGING = 0x0046;
    private const int WM_ACTIVATE = 0x0006;
    
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_WINDOWPOSCHANGING)
        {
            var wp = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            wp.hwndInsertAfter = new IntPtr(1);
            wp.flags |= 0x0010;
            Marshal.StructureToPtr(wp, lParam, false);
        }
        
        if (msg == WM_ACTIVATE)
        {
            DesktopWindowHelper.SendToBottom(this);
        }

        return IntPtr.Zero;
    }

    #region Context Menu Handlers

    private void ContextMenu_Rename(object sender, RoutedEventArgs e)
    {
        // Show edit mode for title
        TitleText.Visibility = Visibility.Collapsed;
        TitleEdit.Visibility = Visibility.Visible;
        TitleEdit.Focus();
        TitleEdit.SelectAll();
    }

    private void ContextMenu_ToggleRollUp(object sender, RoutedEventArgs e)
    {
        ToggleRollUp();
    }

    private void ContextMenu_Delete(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            $"¿Eliminar el fence '{_viewModel.Title}'?\n\nLos archivos no se eliminarán, solo el fence.",
            "Eliminar Fence",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var manager = App.GetService<DesktopOrganizer.UI.Services.FenceManager>();
            if (manager != null)
            {
                manager.DeleteFence(_viewModel);
            }
            else
            {
                this.Close();
            }
        }
    }

    #endregion

    #region Resize Handling
    
    private void ResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Rectangle rect && rect.Tag is string direction)
        {
            _isResizing = true;
            _resizeDirection = direction;
            _resizeStartPoint = PointToScreen(e.GetPosition(this));
            _startWidth = this.ActualWidth;
            _startHeight = this.ActualHeight;
            _startLeft = this.Left;
            _startTop = this.Top;
            
            rect.CaptureMouse();
            rect.MouseMove += ResizeHandle_MouseMove;
            rect.MouseLeftButtonUp += ResizeHandle_MouseUp;
            e.Handled = true;
        }
    }

    private void ResizeHandle_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isResizing) return;
        
        var currentPos = PointToScreen(e.GetPosition(this));
        var deltaX = currentPos.X - _resizeStartPoint.X;
        var deltaY = currentPos.Y - _resizeStartPoint.Y;

        switch (_resizeDirection)
        {
            case "Left":
                var newWidthL = Math.Max(MinWidth, _startWidth - deltaX);
                if (newWidthL >= MinWidth)
                {
                    this.Width = newWidthL;
                    this.Left = _startLeft + deltaX;
                }
                break;
                
            case "Right":
                this.Width = Math.Max(MinWidth, _startWidth + deltaX);
                break;
                
            case "Bottom":
                this.Height = Math.Max(MinHeight, _startHeight + deltaY);
                break;
                
            case "BottomLeft":
                var newWidthBL = Math.Max(MinWidth, _startWidth - deltaX);
                if (newWidthBL >= MinWidth)
                {
                    this.Width = newWidthBL;
                    this.Left = _startLeft + deltaX;
                }
                this.Height = Math.Max(MinHeight, _startHeight + deltaY);
                break;
                
            case "BottomRight":
                this.Width = Math.Max(MinWidth, _startWidth + deltaX);
                this.Height = Math.Max(MinHeight, _startHeight + deltaY);
                break;
        }
        
        if (!_isCollapsed)
        {
            _expandedHeight = this.Height;
        }
    }

    private void ResizeHandle_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Rectangle rect)
        {
            rect.ReleaseMouseCapture();
            rect.MouseMove -= ResizeHandle_MouseMove;
            rect.MouseLeftButtonUp -= ResizeHandle_MouseUp;
        }
        _isResizing = false;
    }
    
    #endregion



    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleRollUp();
            e.Handled = true;
            return;
        }
        
        if (e.ClickCount == 1 && e.ButtonState == MouseButtonState.Pressed)
        {
            var element = e.OriginalSource as FrameworkElement;
            while (element != null && element != this)
            {
                if (element is Button || element is TextBox)
                {
                    return;
                }
                element = element.Parent as FrameworkElement;
            }
            
            DragMove();
        }
    }

    private void TitleText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            TitleText.Visibility = Visibility.Collapsed;
            TitleEdit.Visibility = Visibility.Visible;
            TitleEdit.Focus();
            TitleEdit.SelectAll();
            e.Handled = true;
        }
    }

    private void TitleEdit_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FinishTitleEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            TitleEdit.Text = _viewModel.Title;
            FinishTitleEdit();
            e.Handled = true;
        }
    }

    private void TitleEdit_LostFocus(object sender, RoutedEventArgs e)
    {
        FinishTitleEdit();
    }

    private void FinishTitleEdit()
    {
        if (!string.IsNullOrWhiteSpace(TitleEdit.Text))
        {
            _viewModel.Title = TitleEdit.Text;
        }
        TitleEdit.Visibility = Visibility.Collapsed;
        TitleText.Visibility = Visibility.Visible;
    }

    private void RollUpButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ToggleRollUp();
    }

    private void ToggleRollUp()
    {
        if (_isCollapsed)
            ExpandFence();
        else
            CollapseFence();
    }

    private void CollapseFence()
    {
        if (_isCollapsed) return;
        
        _expandedHeight = this.ActualHeight;
        
        var animation = new DoubleAnimation
        {
            From = this.ActualHeight,
            To = 55,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        
        animation.Completed += (s, e) =>
        {
            ContentListBox.Visibility = Visibility.Collapsed;
            RollUpButton.Content = "▼";
            _isCollapsed = true;
        };
        
        this.BeginAnimation(HeightProperty, animation);
    }

    private void ExpandFence()
    {
        if (!_isCollapsed) return;
        
        ContentListBox.Visibility = Visibility.Visible;
        
        var animation = new DoubleAnimation
        {
            From = this.ActualHeight,
            To = _expandedHeight,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        
        animation.Completed += (s, e) =>
        {
            RollUpButton.Content = "▲";
            _isCollapsed = false;
        };
        
        this.BeginAnimation(HeightProperty, animation);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        // Same logic as ContextMenu_Delete
        ContextMenu_Delete(sender, e);
    }

    private void File_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var grid = sender as Grid;
            if (grid?.DataContext is FileItemViewModel file)
            {
                var data = new DataObject(DataFormats.FileDrop, new[] { file.FullPath });
                DragDrop.DoDragDrop(grid, data, DragDropEffects.Copy | DragDropEffects.Move);
            }
        }
    }
}
