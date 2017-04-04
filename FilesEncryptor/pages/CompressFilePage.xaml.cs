using FilesEncryptor.dto;
using FilesEncryptor.helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// La plantilla de elemento Página en blanco está documentada en https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0xc0a

namespace FilesEncryptor.pages
{
    /// <summary>
    /// Página vacía que se puede usar de forma independiente o a la que se puede navegar dentro de un objeto Frame.
    /// </summary>
    public sealed partial class CompressFilePage : Page
    {
        private StorageFile origTextFile;
        private string origTextStr;
        private HuffmanEncodeResult _encodeResult;

        //{lProbTab}:{{key}{length}:{code}{key} ... {length}:{code}}{lText}:{text}
        private const string COMPRESSED_FILE_FORMAT = "{0}:{1}{2}:{3}";

        public CompressFilePage()
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
            var picker = new FileOpenPicker()
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add(".doc");
            picker.FileTypeFilter.Add(".docx");

            var file = await picker.PickSingleFileAsync();

            if (file != null)
            {
                try
                {
                    origTextFile = file;
                    origTextStr = null;

                    ShowProgressPanel();

                    origTextContainer.Visibility = Visibility.Collapsed;
                    origTextExtraData.Visibility = Visibility.Collapsed;
                    compressBt.Visibility = Visibility.Collapsed;
                    compTextPanel.Visibility = Visibility.Collapsed;

                    var stream = await origTextFile.OpenAsync(FileAccessMode.Read);
                    ulong size = stream.Size;

                    using (var inputStream = stream.GetInputStreamAt(0))
                    {
                        using (var dataReader = new DataReader(inputStream))
                        {
                            uint numBytesLoaded = await dataReader.LoadAsync((uint)size);
                            origTextStr = dataReader.ReadString(numBytesLoaded);
                        }
                    }

                    stream.Dispose();
                }
                catch (Exception)
                {

                }

                if (origTextStr != null)
                {
                    origTextContainer.Visibility = Visibility.Visible;
                    compressBt.Visibility = Visibility.Visible;
                    origTextExtraData.Visibility = Visibility.Visible;
                    origText.Text = origTextStr;
                    origTextLength.Text = origTextStr.Length.ToString();
                }

                HideProgressPanel();
            }            
        }

        private async void CompressBt_Click(object sender, RoutedEventArgs e)
        {
            ShowProgressPanel();

            HuffmanEncoder encoder = new HuffmanEncoder();
            _encodeResult = await encoder.Encode(origTextStr);

            string encodedStr = _encodeResult.Encoded.GetEncodedString();

            if (encodedStr != null)
            {
                compText.Text = encodedStr;
                compTextLength.Text = encodedStr.Length.ToString();
            }
            else
            {
                compText.Text = "";
                compTextLength.Text = "0";
            }

            compTextPanel.Visibility = Visibility.Visible;

            HideProgressPanel();
        }

        private async void SaveBt_Click(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = origTextFile.DisplayName
            };
            savePicker.FileTypeChoices.Add("Huffman encrypted file", new List<string>() { ".huf" });

            StorageFile file = await savePicker.PickSaveFileAsync();

            if(file != null)
            {
                ShowProgressPanel();

                // Prevent updates to the remote version of the file until
                // we finish making changes and call CompleteUpdatesAsync.
                CachedFileManager.DeferUpdates(file);

                var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                
                using (var outputStream = stream.GetOutputStreamAt(0))
                {
                    using (var dataWriter = new DataWriter(outputStream))
                    {
                        uint probabilitiesTableLength = dataWriter.MeasureString(_encodeResult.EncodedProbabilitiesTable);
                        dataWriter.WriteString(string.Format(COMPRESSED_FILE_FORMAT,
                            probabilitiesTableLength,
                            _encodeResult.EncodedProbabilitiesTable,
                            _encodeResult.Encoded.CodeLength,
                            _encodeResult.Encoded.GetEncodedString()));

                        await dataWriter.StoreAsync();
                        await outputStream.FlushAsync();
                    }
                }
                stream.Dispose(); // Or use the stream variable (see previous code snippet) with a using statement as well.
                
                // Let Windows know that we're finished changing the file so
                // the other app can update the remote version of the file.
                // Completing updates may require Windows to ask for user input.
                Windows.Storage.Provider.FileUpdateStatus status =
                    await CachedFileManager.CompleteUpdatesAsync(file);
                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {
                    MessageDialog dialog = new MessageDialog("El archivo ha sido guardado", "Ha sido todo un Exito");
                    await dialog.ShowAsync();
                }
                else
                {
                    MessageDialog dialog = new MessageDialog("El archivo no pudo ser guardado.", "Ha ocurrido un error");
                    await dialog.ShowAsync();
                }

                HideProgressPanel();
            }
        }

        private void ShowProgressPanel() => progressPanel.Visibility = Visibility.Visible;

        private void HideProgressPanel() => progressPanel.Visibility = Visibility.Collapsed;
    }
}
