using FilesEncryptor.dto.hamming;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.hamming;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
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

        private void CommandsPanel_ItemClick(object sender, ItemClickEventArgs e)
        {
            switch ((e.ClickedItem as FrameworkElement).Name)
            {               
                case "compressFileItem":
                    Frame.Navigate(typeof(ProcessPage), new Dictionary<string, object>() { { ProcessPage.VIEW_MODEL_PARAM, ProcessPage.PAGE_MODES.Huffman_Encode } } );
                    break;
                case "uncompressFileItem":
                    Frame.Navigate(typeof(ProcessPage), new Dictionary<string, object>() { { ProcessPage.VIEW_MODEL_PARAM, ProcessPage.PAGE_MODES.Huffman_Decode } });
                    break;
                case "encodeFileItem":
                    Frame.Navigate(typeof(ProcessPage), new Dictionary<string, object>() { { ProcessPage.VIEW_MODEL_PARAM, ProcessPage.PAGE_MODES.Hamming_Encode } });
                    break;
                case "decodeFileItem":
                    Frame.Navigate(typeof(ProcessPage), new Dictionary<string, object>() { { ProcessPage.VIEW_MODEL_PARAM, ProcessPage.PAGE_MODES.Hamming_Decode } });
                    break;
                case "introduceErrorItem":
                    Frame.Navigate(typeof(ProcessPage), new Dictionary<string, object>() { { ProcessPage.VIEW_MODEL_PARAM, ProcessPage.PAGE_MODES.Hamming_Broke } });
                    break;
            }            
        }

        private void ShowConsoleBt_Click(object sender, RoutedEventArgs e)
        {
            DebugUtils.ShowConsoleInNewWindow();
        }
    }
}
