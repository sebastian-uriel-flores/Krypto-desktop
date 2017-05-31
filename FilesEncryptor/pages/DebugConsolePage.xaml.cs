using FilesEncryptor.helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
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
        public DebugConsolePage()
        {
            InitializeComponent();
            DebugUtils.ConsoleWrited += DebugUtils_ConsoleWrited;
        }

        private async void DebugUtils_ConsoleWrited(string text, string label)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                console.Text = console.Text + string.Format("{0}:{1}", label, text) + Environment.NewLine;

                try
                {
                    console.Select(console.Text.Length - 1, 0);
                }
                catch (Exception) { }
                });
        }
    }
}
