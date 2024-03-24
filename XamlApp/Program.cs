using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.System.WinRT;

using static Windows.Win32.PInvoke;
using static Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE;
using static Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX;
using static Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS;

using static XamlApp.NativeMethods;
using Windows.UI.Core;
using Windows.UI.Xaml;
using WinRT;
using XamlApp.Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.ApplicationModel.Activation;

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
        uint dwAttributes, nint hOwnerWindow, ref Guid riid, out nint pWindow);
}


public partial class XamlApplicationView : ICoreApplicationView
{
    public CoreWindow CoreWindow => CoreWindow.GetForCurrentThread();

    public bool IsHosted => false;

    public bool IsMain => true;

    public event TypedEventHandler<ICoreApplicationView, IActivatedEventArgs>? Activated;
}
static class Program
{
    private static CoreWindow? coreWindow;
    private static FrameworkView? frameworkView;
    private static XamlApplicationView? view;
    public static LRESULT OnCreate(HWND hwnd, CREATESTRUCTW cs)
    {
        var guidCoreWindow = typeof(ICoreWindow).GUID;
        var hwndValue = hwnd.Value;
        var hr = PrivateCreateCoreWindow(CoreWindowType.IMMERSIVE_HOSTED, "", 0, 0, 0, 0, 0, hwnd.Value, ref guidCoreWindow, out var _);
        ExceptionHelpers.ThrowExceptionForHR(hr);

        coreWindow = CoreWindow.GetForCurrentThread();
        view = new XamlApplicationView();
        frameworkView = new FrameworkView();
        //var mf = frameworkView.As<IFrameworkView>();
        view.Activated += (s, e) =>
        {
            Console.WriteLine("aaa");
        };
        frameworkView.As<IFrameworkView>().Initialize(view);
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
        var hInstance = GetModuleHandle((PCWSTR)null);
        char* className = stackalloc char[] { 'X', 'a', 'm', 'l', 'W', 'n', 'd' };
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

        BOOL ret;
        while ((ret = GetMessage(out var msg, HWND.Null, 0, 0)) != 0)
        {
            if (ret != -1)
            {
                TranslateMessage(msg);
                DispatchMessage(msg);
            }
        }

        frameworkView?.Run();
        
    }
}