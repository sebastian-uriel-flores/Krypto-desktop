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

        public static void ConsoleW(object message, string category = "[INFO]")
        {
            string messageWithDate = string.Format("({0}) - {1}", DateTime.Now, message);
            Debug.Write(messageWithDate, category);

            ConsoleWrited?.Invoke(messageWithDate, category);
        }
        public static void ConsoleWL(object message, string category = "[INFO]")
        {
            string messageWithDate = string.Format("({0}) - {1}", DateTime.Now, message);
            Debug.WriteLine(messageWithDate, category);

            ConsoleWrited?.Invoke(messageWithDate, category);
        }
        public static void ConsoleF(object shortMessage, string detailedMessage = "")
        {
            string messageWithDate = string.Format("({0}) - {1}", DateTime.Now, shortMessage);
            Debug.Fail(messageWithDate, detailedMessage);

            ConsoleWrited?.Invoke(string.Format("{0}\r\n{1}",messageWithDate, detailedMessage), "[EXCEPTION]");
        }
    }
}
