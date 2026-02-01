using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Win32.Foundation;
using WinRT;

namespace XamlApp
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
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
                XamlRoot = (sender as Button)?.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
