using FilesEncryptor.pages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace FilesEncryptor.helpers
{
    public static class DebugUtils
    {
        private static int _consoleWindowId = -1;
        private static CoreApplicationView _newView;

        public static event ConsoleHandler ConsoleWrited;
        public delegate void ConsoleHandler(string text, string label);

        public static async void ShowConsoleInNewWindow()
        {
            if (_consoleWindowId == -1)
            {
                _newView = CoreApplication.CreateNewView();
                
                int newViewId = 0;
                await _newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Frame frame = new Frame();
                    frame.Navigate(typeof(DebugConsolePage), null);                                        
                    Window.Current.Content = frame;
                // You have to activate the window in order to show it later.
                    Window.Current.Activate();
                    Window.Current.Closed += Current_Closed;
                    newViewId = ApplicationView.GetForCurrentView().Id;
                });
                bool viewShown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);

                if (viewShown)
                {
                    _consoleWindowId = newViewId;
                }
            }
        }

        private static void Current_Closed(object sender, CoreWindowEventArgs e)
        {
            _consoleWindowId = -1;
        }

        public static void Write(object message, string category = "[INFO]")
        {
            Debug.Write(string.Format("({0}) - {1}", DateTime.Now, message), category);
        }
        public static void WriteLine(object message, string category = "[INFO]")
        {
            string messageWithDate = string.Format("({0}) - {1}", DateTime.Now, message);
            Debug.WriteLine(messageWithDate, category);

            ConsoleWrited?.Invoke(messageWithDate, category);
        }
        public static void Fail(object shortMessage, string detailedMessage = "")
        {
            Debug.Fail(string.Format("({0}) - {1}", DateTime.Now, shortMessage), detailedMessage);
        }
    }
}
