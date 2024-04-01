using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Markup;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.WinRT.Xaml;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT;
using static Windows.Win32.PInvoke;
using static Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS;
using static Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE;

namespace XamlApp;

[ComImport]
[Guid("06636C29-5A17-458D-8EA2-2422D997A922")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IWindowPrivate
{
    void GetIids(out int iidCount, out IntPtr iids);
    void GetRuntimeClassName(out IntPtr className);
    void GetTrustLevel(out TrustLevel trustLevel);
    bool TransparentBackground { get; set; }
    void Show();
    void Hide();
    void MoveWindow(int x, int y, int width, int height);
    void SetAtlasSizeHint(uint width, uint height);
    void ReleaseGraphicsDeviceOnSuspend(bool enable);
    void SetAtlasRequestCallback(nint callback);
    Rect GetWindowContentBoundsForElement(nint element);
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

static class Program
{
    private static DesktopWindowXamlSource? xamlSource;
    public static LRESULT OnCreate(HWND hwnd, CREATESTRUCTW cs)
    {
        xamlSource = new DesktopWindowXamlSource();
        var xamlSourceNative = xamlSource?.As<IDesktopWindowXamlSourceNative>();
        xamlSourceNative?.AttachToWindow(hwnd);

        if (xamlSource != null)
        {
            xamlSource.TakeFocusRequested += (s, e) =>
            {
                s.NavigateFocus(e.Request);
            };
        }

        Window.Current.As<IWindowPrivate>().TransparentBackground = true;

        var hwndXamlSource = xamlSourceNative?.WindowHandle ?? HWND.Null;
        SetParent(hwndXamlSource, hwnd);
        SetWindowLong(hwndXamlSource, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)(WS_CHILD | WS_VISIBLE));
        SetWindowPos(hwnd, HWND.Null, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_FRAMECHANGED);
        SetFocus(hwndXamlSource);

        return new LRESULT(0);
    }
    public static LRESULT OnResize(HWND hwnd, nuint sizeType, int clientWidth, int clientHeight)
    {
        if (xamlSource != null)
        {
            var hwndXamlSource = xamlSource.As<IDesktopWindowXamlSourceNative>().WindowHandle;
            SetWindowPos(hwndXamlSource, HWND.Null, 0, 0, clientWidth, clientHeight, SWP_NOZORDER);
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
    public unsafe static LRESULT OnActivate(HWND hwnd, WPARAM wParam, LPARAM lParam)
    {
        var margins = new MARGINS
        {
            cxLeftWidth = -1,
            cxRightWidth = -1,
            cyTopHeight = -1,
            cyBottomHeight = -1
        };
        DwmExtendFrameIntoClientArea(hwnd, margins);

        DWM_SYSTEMBACKDROP_TYPE* type = stackalloc DWM_SYSTEMBACKDROP_TYPE[] { DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW };
        DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, type, (uint)Marshal.SizeOf<int>());

        WTA_OPTIONS options = new()
        {
            dwMask = WTNCA_NODRAWCAPTION | WTNCA_NODRAWICON,
            dwFlags = WTNCA_NODRAWCAPTION | WTNCA_NODRAWICON
        };
        SetWindowThemeAttribute(hwnd, WINDOWTHEMEATTRIBUTETYPE.WTA_NONCLIENT, &options, (uint)Marshal.SizeOf<WTA_OPTIONS>());

        return new LRESULT(0);
    }
    public static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        switch (msg)
        {
            case WM_CREATE:
                return OnCreate(hwnd, Marshal.PtrToStructure<CREATESTRUCTW>(lParam));
            case WM_SIZE:
                return OnResize(hwnd, wParam.Value, (int)(lParam.Value & 0xffff), (int)((lParam.Value >> 16) & 0xffff));
            case WM_DPICHANGED:
                return OnDpiChanged(hwnd, wParam.Value & 0xffff, Marshal.PtrToStructure<RECT>(lParam));
            case WM_DESTROY:
                PostQuitMessage(0);
                return new LRESULT(0);
            case WM_ACTIVATE:
                return OnActivate(hwnd, wParam, lParam);
            default:
                return DefWindowProc(hwnd, msg, wParam, lParam);               
        }
    }
    public static T With<T>(this T el, Action<T> action) where T : UIElement
    {
        action(el);
        return el;
    }
    public unsafe static void Main(string[] args)
    {
        var app = new App();

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
            hbrBackground = new HBRUSH(GetStockObject(GET_STOCK_OBJECT_FLAGS.BLACK_BRUSH)),
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

        var content = new NavigationView
        {
            Content = new ScrollViewer
            {
                Content =  new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Padding = new Thickness(24, 24, 24, 24),
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Hello world" },
                        new Slider { Minimum = 0, Maximum = 100 },
                        new TextBox(),
                        new Button() { Content = "Click Me" }.With(btn =>
                        {
                            btn.Click += (s, e) => {
                                var dialog = new ContentDialog
                                {
                                    Content = new StackPanel
                                    {
                                        Children =
                                        {
                                            new TextBlock { Text = "ContentDialog test" },
                                            new TextBox(),
                                            new Slider { Minimum = 0, Maximum = 100 }
                                        }
                                    },
                                    PrimaryButtonText = "OK",
                                    XamlRoot = btn.XamlRoot
                                };
                                _ = dialog.ShowAsync();
                            };
                        }),
                        new ColorPicker(),
                        new CalendarView(),
                    }
                }
            }
        };
        if (xamlSource != null)
        {
            xamlSource.Content = content;
        }

        BOOL ret;
        while ((ret = GetMessage(out var msg, HWND.Null, 0, 0)) != 0)
        {
            if (ret != -1)
            {
                TranslateMessage(msg);
                DispatchMessage(msg);
            }
        }
    }
}