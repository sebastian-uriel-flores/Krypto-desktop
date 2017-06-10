using FilesEncryptor.dto;
using FilesEncryptor.dto.hamming;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.hamming;
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
        private FileHelper _filesHelper;
        private HammingDecoder _decoder;
        private FileHeader _fileHeader;

        public HammingDecodePage()
        {
            this.InitializeComponent();
            _filesHelper = new FileHelper();
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
            var extensions = new List<string>();
            foreach (HammingEncodeType type in BaseHammingCodifier.EncodeTypes)
            {
                extensions.Add(type.Extension);
            }

            bool pickResult = await _filesHelper.PickToOpen(extensions);

            if(pickResult)
            {
                ShowProgressPanel();
                await Task.Delay(200);

                settingsPanel.Visibility = Visibility.Collapsed;
                pageCommandsDivider.Visibility = Visibility.Collapsed;
                pageCommands.Visibility = Visibility.Collapsed;

                bool openResult = await _filesHelper.OpenFile(FileAccessMode.Read);

                if(openResult)
                {
                    DebugUtils.WriteLine(string.Format("Selected file: {0} with size of {1} bytes", _filesHelper.SelectedFilePath, _filesHelper.FileSize));

                    fileNameBlock.Text = _filesHelper.SelectedFileName;
                    fileSizeBlock.Text = string.Format("{0} bytes", _filesHelper.FileSize);
                    fileDescriptionBlock.Text = string.Format("{0} ({1})", _filesHelper.SelectedFileDisplayType, _filesHelper.SelectedFileExtension);

                    settingsPanel.Visibility = Visibility.Visible;
                    pageCommandsDivider.Visibility = Visibility.Visible;
                    pageCommands.Visibility = Visibility.Visible;

                    bool extractResult = ExtractFileProperties();
                    await _filesHelper.Finish();
                    DebugUtils.WriteLine("File bytes extracted properly");
                    DebugUtils.WriteLine("Closing file");
                }
            }
            HideProgressPanel();
        }

        private bool ExtractFileProperties()
        {
            bool extractResult = false;
            _fileHeader = _filesHelper.ReadFileHeader();

            if (_fileHeader != null)
            {
                _decoder = HammingDecoder.FromFile(_filesHelper, BaseHammingCodifier.EncodeTypes.First(encType => encType.Extension == _filesHelper.SelectedFileExtension));
                extractResult = true;
            }

            return extractResult;
        }

        private async void DecodeBt_Click(object sender, RoutedEventArgs e)
        {
            bool pickResult = await _filesHelper.PickToSave(_fileHeader.FileName, _fileHeader.FileDisplayType, _fileHeader.FileExtension);

            if(pickResult)
            {
                bool openResult = await _filesHelper.OpenFile(FileAccessMode.ReadWrite);

                if(openResult)
                {
                    ShowProgressPanel();
                    await Task.Delay(200);

                    DebugUtils.WriteLine(string.Format("Output file: \"{0}\"", _filesHelper.SelectedFilePath));
                    DebugUtils.WriteLine("Starting Hamming Decoding");

                    DateTime startDate = DateTime.Now;

                    //Codifico el archivo original
                    BitCode result = await _decoder.Decode();

                    //Imprimo la cantidad de tiempo que implico la decodificacion
                    TimeSpan totalTime = DateTime.Now.Subtract(startDate);
                    DebugUtils.WriteLine(string.Format("Decoding process finished in a time of {0}:{1}:{2}:{3}", totalTime.Hours, totalTime.Milliseconds, totalTime.Seconds, totalTime.Milliseconds));

                    if (result != null)
                    {
                        DebugUtils.WriteLine(string.Format("Dumping hamming decoded bytes to \"{0}\"", _filesHelper.SelectedFilePath));

                        bool writeResult = _filesHelper.WriteBytes(result.Code.ToArray());

                        //Show congrats message
                        if (writeResult)
                        {
                            DebugUtils.WriteLine("Dumping completed properly");
                            DebugUtils.WriteLine("Closing file");
                            await _filesHelper.Finish();
                            MessageDialog dialog = new MessageDialog("El archivo ha sido guardado", "Ha sido todo un Exito");
                            await dialog.ShowAsync();
                        }
                        else
                        {
                            DebugUtils.WriteLine("Dumping uncompleted");
                            DebugUtils.WriteLine("Closing file");
                            await _filesHelper.Finish();
                            MessageDialog dialog = new MessageDialog("El archivo no pudo ser guardado.", "Ha ocurrido un error");
                            await dialog.ShowAsync();
                        }
                    }

                    HideProgressPanel();
                }
            }
        }

        private void ShowProgressPanel() => progressPanel.Visibility = Visibility.Visible;

        private void HideProgressPanel() => progressPanel.Visibility = Visibility.Collapsed;
    }
}
