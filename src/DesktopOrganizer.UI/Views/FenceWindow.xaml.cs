using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DesktopOrganizer.UI.Helpers;
using DesktopOrganizer.UI.ViewModels;

namespace DesktopOrganizer.UI.Views;

public partial class FenceWindow : Window
{
    private readonly FenceViewModel _viewModel;

    public FenceWindow(FenceViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        
        // Initialize hook to force Z-Order
        var source = PresentationSource.FromVisual(this) as HwndSource;
        source?.AddHook(WndProc);
        
        // Initial push to bottom
        DesktopWindowHelper.SendToBottom(this);
    }

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
            // Intercept any attempt to move Z-order
            var wp = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            
            // Force it to stay at bottom
            wp.hwndInsertAfter = new IntPtr(1); // HWND_BOTTOM
            wp.flags |= 0x0010; // SWP_NOACTIVATE (Don't activate)
            
            Marshal.StructureToPtr(wp, lParam, false);
        }
        
        if (msg == WM_ACTIVATE)
        {
            // If activated, push to bottom immediately
            DesktopWindowHelper.SendToBottom(this);
        }

        return IntPtr.Zero;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                _viewModel.AddFiles(files);
            }
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void File_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var border = sender as System.Windows.Controls.Grid;
            if (border?.DataContext is FileItemViewModel file)
            {
                // Initiate drag and drop
                var data = new DataObject(DataFormats.FileDrop, new[] { file.FullPath });
                DragDrop.DoDragDrop(border, data, DragDropEffects.Copy | DragDropEffects.Move);
            }
        }
    }

    private void File_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            var border = sender as System.Windows.Controls.Grid;
            if (border?.DataContext is FileItemViewModel file)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(file.FullPath) { UseShellExecute = true });
                }
                catch { }
            }
        }
    }
}
