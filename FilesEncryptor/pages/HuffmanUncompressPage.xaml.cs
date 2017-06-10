using FilesEncryptor.dto;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.huffman;
using FilesEncryptor.utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
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
    public sealed partial class UncompressFilePage : Page
    {
        private FileHelper _fileOpener;
        private FileHeader _fileHeader;
        private HuffmanDecoder _decoder;
        
        public UncompressFilePage()
        {
            this.InitializeComponent();
            _fileOpener = new FileHelper();
            _fileHeader = new FileHeader();
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
            bool allOK = false;
            if (await _fileOpener.PickToOpen(new List<string>() { BaseHuffmanCodifier.HUFFMAN_FILE_EXTENSION }))
            {
                if (await _fileOpener.OpenFile(FileAccessMode.Read))
                {
                    await ShowProgressPanel();
                    HidePanels();

                    //Leo el header del archivo
                    _fileHeader = _fileOpener.ReadFileHeader();

                    //Leo el archivo
                    _decoder = HuffmanDecoder.FromFile(_fileOpener); ;

                    //Cierro el archivo
                    await _fileOpener.Finish();

                    //Muestro la informacion del archivo
                    ShowFileInformation();

                    ShowPanels();
                    HideProgressPanel();

                    allOK = _decoder != null;
                }
            }

            if (!allOK)
            {
                await new MessageDialog("Ha ocurrido un error").ShowAsync();
            }
        }

        private async void UncompressBt_Click(object sender, RoutedEventArgs e)
        {
            bool decodeResult = false;
            FileHelper fileSaver = new FileHelper();
                        
            if (await fileSaver.PickToSave(_fileHeader.FileName, _fileHeader.FileDisplayType, _fileHeader.FileExtension))
            {
                if (await fileSaver.OpenFile(FileAccessMode.ReadWrite))
                {
                    await ShowProgressPanel();
                    
                    //Decodifico el archivo
                    DebugUtils.WriteLine("Starting decoding process");
                    DateTime startDate = DateTime.Now;

                    //[IMPORTANTE]: Inicio un Thread para decodificar el archivo, dado que sino, se bloquea la UI
                    //y, al decodificar archivos muy largos, 
                    //implica que la interfaz permanezca bloqueada por mucho tiempo y el sistema finalice la app
                    await Task.Factory.StartNew(async () =>
                    {
                        string decoded = _decoder.Decode();
                        fileSaver.SetFileEncoding(_fileOpener.FileEncoding);

                        //Si la decodificacion se realizo con exito,                         
                        //Escribo el texto decodificado en el archivo de salida
                        decodeResult = decoded != null && fileSaver.WriteString(decoded);

                        //Imprimo la cantidad de tiempo que implico la decodificacion
                        TimeSpan totalTime = DateTime.Now.Subtract(startDate);
                        DebugUtils.WriteLine(string.Format("Decoding process finished in a time of {0}:{1}:{2}:{3}", totalTime.Hours, totalTime.Milliseconds, totalTime.Seconds, totalTime.Milliseconds));

                        //Cierro el archivo comprimido
                        DebugUtils.WriteLine("Closing file");
                        await fileSaver.Finish();
                        DebugUtils.WriteLine("File closed");
                    });
                    HideProgressPanel();
                }
            }

            if (decodeResult)
            {
                await new MessageDialog("El archivo ha sido descomprimido con exito").ShowAsync();
                DebugUtils.WriteLine("Huffman decoding finished successfully");
            }
            else
            {
                await new MessageDialog("Ha ocurrido un error").ShowAsync();
                DebugUtils.WriteLine("Huffman decoding process failed");
            }
        }
        
        private async Task ShowProgressPanel()
        {
            progressPanel.Visibility = Visibility.Visible;
            await Task.Delay(200);
        }

        private void HideProgressPanel() => progressPanel.Visibility = Visibility.Collapsed;

        private void ShowPanels()
        {
            settingsPanel.Visibility = Visibility.Visible;
            pageCommandsDivider.Visibility = Visibility.Visible;
            pageCommands.Visibility = Visibility.Visible;
        }

        private void HidePanels()
        {
            settingsPanel.Visibility = Visibility.Collapsed;
            pageCommandsDivider.Visibility = Visibility.Collapsed;
            pageCommands.Visibility = Visibility.Collapsed;
        }

        private void ShowFileInformation()
        {
            fileNameBlock.Text = _fileOpener.SelectedFileDisplayName;
            fileSizeBlock.Text = string.Format("{0} bytes", _fileOpener.FileSize);
            fileDescriptionBlock.Text = _fileOpener.SelectedFileDisplayType;
            fileContentBlock.Text = "";            
        }
    }
}
