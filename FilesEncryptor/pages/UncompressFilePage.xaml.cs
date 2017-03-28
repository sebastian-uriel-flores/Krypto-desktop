using FilesEncryptor.dto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
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
    public sealed partial class UncompressFilePage : Page
    {
        private StorageFile compTextFile;
        private string compTextStr;

        public UncompressFilePage()
        {
            this.InitializeComponent();            
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame.CanGoBack)
            {
                // Show UI in title bar if opted-in and in-app backstack is not empty.
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Visible;
            }
            else
            {
                // Remove the UI from the title bar if in-app back stack is empty.
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Collapsed;
            }
        }

        private async void SelectFileBt_Click(object sender, RoutedEventArgs e)
        {
            compTextFile = null;
            compTextStr = null;

            var picker = new FileOpenPicker()
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".huf");

            compTextFile = await picker.PickSingleFileAsync();

            ShowProgressPanel();

            origTextContainer.Visibility = Visibility.Collapsed;
            origTextExtraData.Visibility = Visibility.Collapsed;
            uncompressBt.Visibility = Visibility.Collapsed;
            origTextPanel.Visibility = Visibility.Collapsed;

            if (compTextFile != null)
            {
                HuffmanEncodeResult encodedText = await HuffmanCompressor.Uncompress(compTextFile);

                if (encodedText != null)
                {
                    origTextContainer.Visibility = Visibility.Visible;
                    uncompressBt.Visibility = Visibility.Visible;
                    origTextExtraData.Visibility = Visibility.Visible;
                    origText.Text = compTextStr;
                    origTextLength.Text = compTextStr.Length.ToString();
                }

                HideProgressPanel();
            }
        }

        private void UncompressBt_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SaveBt_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ShowProgressPanel() => progressPanel.Visibility = Visibility.Visible;

        private void HideProgressPanel() => progressPanel.Visibility = Visibility.Collapsed;
    }
}
