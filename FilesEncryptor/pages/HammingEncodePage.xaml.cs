using FilesEncryptor.dto;
using FilesEncryptor.helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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

// La plantilla de elemento Página en blanco está documentada en https://go.microsoft.com/fwlink/?LinkId=234238

namespace FilesEncryptor.pages
{
    /// <summary>
    /// Una página vacía que se puede usar de forma independiente o a la que se puede navegar dentro de un objeto Frame.
    /// </summary>
    public sealed partial class HammingEncodePage : Page
    {
        private int _selectedEncoding;
        private List<byte> _rawFileBytes;
        private ObservableCollection<HammingEncodeType> _encodeTypes = new ObservableCollection<HammingEncodeType>(HammingEncoder.EncodeTypes);

        public HammingEncodePage()
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
            picker.FileTypeFilter.Add(".huf");
            
            var file = await picker.PickSingleFileAsync();

            if (file != null)
            {
                try
                {
                    _rawFileBytes = null;

                    ShowProgressPanel();
                    await Task.Delay(200);

                    settingsPanel.Visibility = Visibility.Collapsed;
                    encodeBt.Visibility = Visibility.Collapsed;

                    var stream = await file.OpenAsync(FileAccessMode.Read);
                    ulong size = stream.Size;

                    using (var inputStream = stream.GetInputStreamAt(0))
                    {
                        using (var dataReader = new DataReader(inputStream))
                        {
                            uint numBytesLoaded = await dataReader.LoadAsync((uint)size);
                            byte[] buffer = new byte[numBytesLoaded];
                            dataReader.ReadBytes(buffer);

                            _rawFileBytes = new List<byte>(buffer);
                        }
                    }

                    stream.Dispose();
                }
                catch (Exception ex)
                {
                    MessageDialog errorDialog = new MessageDialog("No se pudo abrir el archivo. Intente con otro formato.", "Ha ocurrido un error");
                    await errorDialog.ShowAsync();

                    Debug.Fail("Excepcion al cargar archivo para codificacion con hamming", ex.Message);
                }

                if (_rawFileBytes != null)
                {
                    settingsPanel.Visibility = Visibility.Visible;
                    encodeBt.Visibility = Visibility.Visible;                    
                }

                HideProgressPanel();
            }
        }

        private void HammingEncodeTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedEncoding = hammingEncodeTypeSelector.SelectedIndex;
        }

        private async void EncodeBt_Click(object sender, RoutedEventArgs e)
        {
            await HammingEncoder.Encode(_rawFileBytes, _encodeTypes[_selectedEncoding]);
        }

        private void ShowProgressPanel() => progressPanel.Visibility = Visibility.Visible;

        private void HideProgressPanel() => progressPanel.Visibility = Visibility.Collapsed;
    }
}
