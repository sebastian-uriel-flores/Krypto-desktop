using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
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
    public sealed partial class BifurcatorPage : Page
    {
        public BifurcatorPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;

            //PC customization
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
            {
                var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                if (titleBar != null)
                {
                    titleBar.ButtonForegroundColor = Colors.WhiteSmoke;
                    titleBar.ButtonPressedForegroundColor = Colors.WhiteSmoke;
                    titleBar.ButtonBackgroundColor = Colors.DodgerBlue;
                    titleBar.ButtonPressedBackgroundColor = Colors.DodgerBlue;
                    titleBar.BackgroundColor = Colors.DodgerBlue;
                    titleBar.ForegroundColor = Colors.WhiteSmoke;
                    titleBar.InactiveForegroundColor = Colors.WhiteSmoke;
                    titleBar.InactiveBackgroundColor = Colors.DodgerBlue;
                    titleBar.ButtonInactiveBackgroundColor = Colors.DodgerBlue;
                    titleBar.ButtonInactiveForegroundColor = Colors.WhiteSmoke;
                }
            }
        }

        private void CompressFileBt_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(CompressFilePage));

        private void UncompressFileBt_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(UncompressFilePage));

        private void HammingEncodeBt_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(HammingEncodePage));

        private void HammingDecodeBt_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(HammingDecodePage));
    }
}
