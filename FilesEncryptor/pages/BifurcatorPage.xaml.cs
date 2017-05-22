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

            commandsPanel.Loaded += new RoutedEventHandler((sender,args) =>
            {
                foreach (object item in commandsPanel.Items)
                {
                    (item as FrameworkElement).PointerEntered += BifurcatorPage_PointerEntered;
                    (item as FrameworkElement).PointerExited += BifurcatorPage_PointerExited;
                }
            });

            Loaded += BifurcatorPage_Loaded;
        }

        private void BifurcatorPage_Loaded(object sender, RoutedEventArgs e)
        {
            //PC customization
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
            {
                ApplicationView.GetForCurrentView().Title = "Teoría de la información";
                var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                if (titleBar != null)
                {
                    Color orange = new Color() { R = 251, G = 131, B = 0 };

                    titleBar.ButtonForegroundColor = Colors.WhiteSmoke;
                    titleBar.ButtonPressedForegroundColor = Colors.WhiteSmoke;
                    titleBar.ButtonBackgroundColor = orange;
                    titleBar.ButtonPressedBackgroundColor = orange;                    
                    titleBar.InactiveForegroundColor = Colors.WhiteSmoke;
                    titleBar.InactiveBackgroundColor = orange;
                    titleBar.ButtonInactiveBackgroundColor = orange;
                    titleBar.ButtonInactiveForegroundColor = Colors.WhiteSmoke;
                    titleBar.BackgroundColor = orange;
                    titleBar.ForegroundColor = Colors.WhiteSmoke;
                }
            }
        }

        private void BifurcatorPage_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                FrameworkElement panel = sender as FrameworkElement;
                panel.Projection = new PlaneProjection();
                ((PlaneProjection)panel.Projection).GlobalOffsetZ = 70;
            }
        }
        private void BifurcatorPage_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                FrameworkElement panel = sender as FrameworkElement;
                panel.Projection = new PlaneProjection();
                ((PlaneProjection)panel.Projection).GlobalOffsetZ = 0;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
        }

        private void CompressFileBt_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(CompressFilePage));

        private void UncompressFileBt_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(UncompressFilePage));

        private void HammingEncodeBt_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(HammingEncodePage));

        private void HammingDecodeBt_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(HammingDecodePage));

        private void commandsPanel_ItemClick(object sender, ItemClickEventArgs e)
        {
            switch ((e.ClickedItem as FrameworkElement).Name)
            {
                case "compressFileItem":
                    Frame.Navigate(typeof(CompressFilePage));
                    break;
                case "uncompressFileItem":
                    Frame.Navigate(typeof(UncompressFilePage));
                    break;
                case "encodeFileItem":
                    Frame.Navigate(typeof(HammingEncodePage));
                    break;
                case "decodeFileItem":
                    Frame.Navigate(typeof(HammingDecodePage));
                    break;
                case "introduceErrorItem":
                    Frame.Navigate(typeof(IntroduceErrorsPage));
                    break;
            }            
        }
    }
}
