using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.WinRT;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT;
using static Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX;
using static Windows.Win32.PInvoke;
using static Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS;
using static Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE;
using static XamlApp.NativeMethods;

namespace XamlApp;

static partial class NativeMethods
{
    public enum CoreWindowType : int
    {
        IMMERSIVE_BODY = 0,
        IMMERSIVE_DOCK,
        IMMERSIVE_HOSTED,
        IMMERSIVE_TEST,
        IMMERSIVE_BODY_ACTIVE,
        IMMERSIVE_DOCK_ACTIVE,
        NOT_IMMERSIVE
    }

    [LibraryImport("windows.ui.dll", EntryPoint = "#1500", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int PrivateCreateCoreWindow(CoreWindowType windowType, string windowTitle,
        int x, int y, uint width, uint height,
        uint dwAttributes, nint hOwnerWindow, in Guid riid, out nint pWindow);
}

public class App : Application, IXamlMetadataProvider
{
    public IXamlType? GetXamlType(Type type)
    {
        foreach (var provider in _providers)
        {
            var xamlType = provider.GetXamlType(type);
            if (xamlType != null) return xamlType;
        }
        return null;
    }

    public IXamlType? GetXamlType(string fullName)
    {
        foreach (var provider in _providers)
        {
            var xamlType = provider.GetXamlType(fullName);
            if (xamlType != null) return xamlType;
        }
        return null;
    }

    public XmlnsDefinition[] GetXmlnsDefinitions()
    {
        return _providers.SelectMany(p => p.GetXmlnsDefinitions()).ToArray();
    }

    private List<IXamlMetadataProvider> _providers = new();
}
public class XamlApplicationView : ICoreApplicationView
{
    public CoreWindow CoreWindow => CoreWindow.GetForCurrentThread();

    public bool IsHosted => false;

    public bool IsMain => true;

    public event TypedEventHandler<CoreApplicationView, IActivatedEventArgs>? Activated;
}

static class Program
{
    private static CoreWindow? coreWindow;
    private static FrameworkView? frameworkView;
    public static LRESULT OnCreate(HWND hwnd, CREATESTRUCTW cs)
    {
        var hr = PrivateCreateCoreWindow(CoreWindowType.IMMERSIVE_HOSTED, "", 0, 0, 0, 0, 0, hwnd.Value, typeof(ICoreWindow).GUID, out var _);
        ExceptionHelpers.ThrowExceptionForHR(hr);

        coreWindow = CoreWindow.GetForCurrentThread();
        var view = new XamlApplicationView();
        frameworkView = new FrameworkView();
        frameworkView.As<IFrameworkViewModified>().Initialize(view);
        frameworkView.SetWindow(coreWindow);

        var hwndCoreWindow = new HWND(coreWindow.As<ICoreWindowInterop>().WindowHandle);
        SetParent(hwndCoreWindow, hwnd);
        SetWindowLongPtr(hwndCoreWindow, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (nint)(WS_CHILD | WS_VISIBLE));

        return new LRESULT(0);
    }
    public static LRESULT OnResize(HWND hwnd, nuint sizeType, int clientWidth, int clientHeight)
    {
        if (coreWindow != null)
        {
            var hwndCoreWindow = new HWND(coreWindow.As<ICoreWindowInterop>().WindowHandle);
            SetWindowPos(hwndCoreWindow, HWND.Null, 0, 0, clientHeight, clientWidth, SWP_NOMOVE | SWP_NOZORDER);
        }
        return new LRESULT(0);
    }
    public static LRESULT OnDpiChanged(HWND hwnd, nuint dpi, RECT windowRect)
    {
        SetWindowPos(hwnd, HWND.Null, 
            windowRect.X, windowRect.Y, windowRect.Width, windowRect.Height, 
            SWP_NOZORDER | SWP_NOACTIVATE);
        return new LRESULT(0);
    }
    public static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lPARAM)
    {
        
        switch (msg)
        {
            case WM_CREATE:
                return OnCreate(hwnd, Marshal.PtrToStructure<CREATESTRUCTW>(lPARAM));
            case WM_SIZE:
                return OnResize(hwnd, wParam.Value, (int)(lPARAM.Value & 0xffff), (int)((lPARAM.Value >> 16) & 0xffff));
            case WM_DPICHANGED:
                return OnDpiChanged(hwnd, wParam.Value & 0xffff, Marshal.PtrToStructure<RECT>(lPARAM));
            case WM_DESTROY:
                PostQuitMessage(0);
                return new LRESULT(0);
            default:
                return DefWindowProc(hwnd, msg, wParam, lPARAM);
        }
    }
    
    public unsafe static void Main(string[] args)
    {
        _ = new App();

        var hInstance = GetModuleHandle((PCWSTR)null);
        char* className = stackalloc char[] { 'X', 'a', 'm', 'l', 'W', 'n', 'd', (char)0 };
        var wndClass = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            style = 0,
            lpfnWndProc = WndProc,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = hInstance,
            hIcon = HICON.Null,
            hCursor = HCURSOR.Null,
            hbrBackground = new HBRUSH((nint)(COLOR_WINDOW + 1)),
            lpszMenuName = null,
            lpszClassName = className
        };
        RegisterClassEx(wndClass);

        var hwnd = CreateWindowEx(0, className, className, WS_OVERLAPPEDWINDOW, 
            CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, 
            HWND.Null, HMENU.Null, hInstance);
        if (hwnd == HWND.Null)
        {
            throw new Exception("CreateWindowEx failed");
        }

        ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_NORMAL);


        var textBlock = new TextBlock
        {
            Text = "Hello world"
        };
        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical
        };
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100
        };
        stackPanel.Children.Add(textBlock);
        stackPanel.Children.Add(slider);
        stackPanel.Children.Add(new TextBox());
        stackPanel.Children.Add(new Button { Content = "Button" });
        stackPanel.Children.Add(new CalendarDatePicker());
        Window.Current.Content = stackPanel;

        frameworkView?.Run();
    }
}