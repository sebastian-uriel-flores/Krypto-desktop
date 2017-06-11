using FilesEncryptor.helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// La plantilla de elemento Página en blanco está documentada en https://go.microsoft.com/fwlink/?LinkId=234238

namespace FilesEncryptor.pages
{
    /// <summary>
    /// Una página vacía que se puede usar de forma independiente o a la que se puede navegar dentro de un objeto Frame.
    /// </summary>
    public sealed partial class DebugConsolePage : Page
    {
        private bool _lockToBottom;

        public DebugConsolePage()
        {
            InitializeComponent();
            Loaded += DebugConsolePage_Loaded;
            //listConsole.CollectionChanged += items.CollectionChanged += (s, args) => ScrollToBottom();
            _lockToBottom = true;
            listConsole.Loaded += ListConsole_Loaded;
            DebugUtils.ConsoleWrited += DebugUtils_ConsoleWrited;
        }

        private void DebugConsolePage_Loaded(object sender, RoutedEventArgs e)
        {
            //PC customization
            var view = SystemNavigationManager.GetForCurrentView();
            view.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
            //view.BackRequested += View_BackRequested;
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
            {
                var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                if (titleBar != null)
                {
                    var mainOrange = GetSolidColorBrush("#FFFB8300").Color;
                    var secondOrange = GetSolidColorBrush("#FFCD3927").Color;

                    titleBar.ButtonBackgroundColor = mainOrange;
                    titleBar.ButtonForegroundColor = Colors.White;
                    titleBar.ButtonHoverBackgroundColor = secondOrange;
                    titleBar.ButtonInactiveBackgroundColor = mainOrange;
                    titleBar.ButtonInactiveForegroundColor = Colors.White;

                    titleBar.BackgroundColor = mainOrange;
                    titleBar.ForegroundColor = Colors.White;
                    titleBar.InactiveBackgroundColor = mainOrange;
                    titleBar.InactiveForegroundColor = Colors.White;
                }
            }
        }

        private void ListConsole_Loaded(object sender, RoutedEventArgs e)
        {
            /*var scrollViewer = listConsole.GetFirstDescendantOfType<ScrollViewer>();
            var scrollbars = scrollViewer.GetDescendantsOfType<ScrollBar>().ToList();
            var verticalBar = scrollbars.FirstOrDefault(x => x.Orientation == Orientation.Vertical);

            if (verticalBar != null)
                verticalBar.Scroll += BarScroll;*/
        }

        private void LockScroll_Click(object sender, RoutedEventArgs e)
        {
            _lockToBottom = !_lockToBottom;
        }

        private void CleanConsole_Click(object sender, RoutedEventArgs e)
        {
            listConsole.Items.Clear();
        }

        private async void DebugUtils_ConsoleWrited(string text, string label)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                listConsole.Items.Add(string.Format("{0}:{1}", label, text));
                ScrollToBottom();
            });


            //listConsole.ScrollIntoView(listConsole.Items.Last());
            /*console.Text = console.Text + string.Format("{0}:{1}", label, text) + Environment.NewLine;

            try
            {
                console.Select(console.Text.Length - 1, 0);
            }
            catch (Exception) { }*/
        }

        void BarScroll(object sender, ScrollEventArgs e)
        {
            if (e.ScrollEventType != ScrollEventType.EndScroll) return;

            var bar = sender as ScrollBar;
            if (bar == null)
                return;

            System.Diagnostics.Debug.WriteLine("Scrolling ended");

            if (e.NewValue >= bar.Maximum)
            {
                System.Diagnostics.Debug.WriteLine("We are at the bottom");
                _lockToBottom = true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("We are away from the bottom");
                _lockToBottom = false;
            }
        }

        private void ScrollToBottom()
        {
            if (!_lockToBottom)
                return;

            var selectedIndex = listConsole.Items.Count - 1;
            if (selectedIndex < 0)
                return;

            listConsole.SelectedIndex = selectedIndex;
            listConsole.UpdateLayout();

            listConsole.ScrollIntoView(listConsole.SelectedItem);
        }

        /// <summary>
        /// Function to convert Hex to Color
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public SolidColorBrush GetSolidColorBrush(string hex)
        {
            hex = hex.Replace("#", string.Empty);
            byte a = (byte)(Convert.ToUInt32(hex.Substring(0, 2), 16));
            byte r = (byte)(Convert.ToUInt32(hex.Substring(2, 2), 16));
            byte g = (byte)(Convert.ToUInt32(hex.Substring(4, 2), 16));
            byte b = (byte)(Convert.ToUInt32(hex.Substring(6, 2), 16));
            SolidColorBrush myBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
            return myBrush;
        }
    }
}
