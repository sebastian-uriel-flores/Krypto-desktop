using FilesEncryptor.dto;
using FilesEncryptor.helpers;
using FilesEncryptor.utils;
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
    public sealed partial class HammingDecodePage : Page
    {        
        private FilesHelper _filesHelper;
        private HammingEncoder _decoder;

        public HammingDecodePage()
        {
            this.InitializeComponent();
            
            List<string> extensions = new List<string>();
            foreach (HammingEncodeType type in HammingEncoder.EncodeTypes)
            {
                extensions.Add(type.Extension);
            }

            _filesHelper = new FilesHelper(extensions);
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
            bool pickResult = await _filesHelper.Pick();

            if(pickResult)
            {
                ShowProgressPanel();
                await Task.Delay(200);

                settingsPanel.Visibility = Visibility.Collapsed;
                pageCommandsDivider.Visibility = Visibility.Collapsed;
                pageCommands.Visibility = Visibility.Collapsed;

                bool openResult = await _filesHelper.OpenFile();

                if(openResult)
                {
                    fileNameBlock.Text = _filesHelper.SelectedFileName;
                    fileSizeBlock.Text = string.Format("{0} bytes", _filesHelper.FileSize);
                    fileDescriptionBlock.Text = string.Format("{0} ({1})", _filesHelper.SelectedFileDisplayName, _filesHelper.SelectedFileExtension);

                    settingsPanel.Visibility = Visibility.Visible;
                    pageCommandsDivider.Visibility = Visibility.Visible;
                    pageCommands.Visibility = Visibility.Visible;

                    bool extractResult = await ExtractFileProperties();
                    DebugUtils.Write("File extracted properly");
                }
            }
            HideProgressPanel();
        }

        private async Task<bool> ExtractFileProperties()
        {
            bool extractResult = false;
            var header = await _filesHelper.ExtractFileHeader();

            if (header != null)
            {
                _decoder = new HammingEncoder(HammingEncoder.EncodeTypes.First(encType => encType.Extension == _filesHelper.SelectedFileExtension));
                extractResult = await _decoder.ReadFileContent(_filesHelper);
            }

            _filesHelper.Finish();
            return extractResult;
        }

        private async void DecodeBt_Click(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = _filesHelper.SelectedFileHeader.FileName
            };
            savePicker.FileTypeChoices.Add(_filesHelper.SelectedFileHeader.FileDisplayType, new List<string>() { _filesHelper.SelectedFileHeader.FileExtension });
            StorageFile file = await savePicker.PickSaveFileAsync();

            //Si el usuario no canceló la operación
            if (file != null)
            {
                ShowProgressPanel();
                await Task.Delay(200);

                //Codifico el archivo original
                BitCode result = await _decoder.Decode();

                //Vuelco el código obtenido en disco
                bool dumpRes = await DumpDecodedResult(result, file);

                //Show congrats message
                if (dumpRes)
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

        private async Task<bool> DumpDecodedResult(BitCode result, StorageFile file)
        {
            DebugUtils.WriteLine(string.Format("Dumping hamming decoded file to \"s{0}\"", file.Name));

            bool dumpResult = false;

            if (file.IsAvailable)
            {
                // Prevent updates to the remote version of the file until
                // we finish making changes and call CompleteUpdatesAsync.
                CachedFileManager.DeferUpdates(file);

                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    using (var outputStream = stream.GetOutputStreamAt(0))
                    {
                        using (var dataWriter = new DataWriter(outputStream))
                        {
                            dataWriter.WriteBytes(result.Code.ToArray());

                            await dataWriter.StoreAsync();
                            await outputStream.FlushAsync();
                        }
                    }
                }

                // Let Windows know that we're finished changing the file so
                // the other app can update the remote version of the file.
                // Completing updates may require Windows to ask for user input.
                Windows.Storage.Provider.FileUpdateStatus status =
                    await CachedFileManager.CompleteUpdatesAsync(file);

                dumpResult = status == Windows.Storage.Provider.FileUpdateStatus.Complete;
            }

            DebugUtils.WriteLine(string.Format("Dump Completed: {0}", dumpResult));

            return dumpResult;
        }

        private void ShowProgressPanel() => progressPanel.Visibility = Visibility.Visible;

        private void HideProgressPanel() => progressPanel.Visibility = Visibility.Collapsed;
    }
}
