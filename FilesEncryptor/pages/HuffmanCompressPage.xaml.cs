using FilesEncryptor.dto;
using FilesEncryptor.dto.huffman;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.huffman;
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
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// La plantilla de elemento Página en blanco está documentada en https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0xc0a

namespace FilesEncryptor.pages
{
    /// <summary>
    /// Página vacía que se puede usar de forma independiente o a la que se puede navegar dentro de un objeto Frame.
    /// </summary>
    public sealed partial class HuffmanCompressPage : Page
    {
        private string _originalFileContent;
        private FileHelper _fileOpener;

        public HuffmanCompressPage()
        {
            this.InitializeComponent();
            _fileOpener = new FileHelper();
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
            if(await _fileOpener.PickToOpen(new List<string>() { ".txt" }))
            {
                if (await _fileOpener.OpenFile(FileAccessMode.Read, true))
                {
                    await ShowProgressPanel();
                    HidePanels();

                    //Leo todos los bytes del texto
                    byte[] fileBytes = _fileOpener.ReadBytes(_fileOpener.FileSize);                    

                    //Obtengo el texto que sera mostrado en pantalla
                    _originalFileContent = _fileOpener.FileEncoding.GetString(fileBytes);

                    //Si el texto posee un BOM al principio, lo elimino,
                    //dado que sino, el texto pierde el formato en el TextBox.
                    if (_fileOpener.FileBOM != null)
                    {
                        string bom = _fileOpener.FileEncoding.GetString(_fileOpener.FileBOM);
                        _originalFileContent = _originalFileContent.Remove(0, bom.Length);
                    }

                    //Cierro el archivo
                    await _fileOpener.Finish();

                    //Muestro la informacion del archivo
                    await ShowFileInformation();

                    ShowPanels();
                    HideProgressPanel();

                    allOK = true;
                }
            }   

            if(!allOK)
            {
                await new MessageDialog("Ha ocurrido un error").ShowAsync();
            }
        }

        private async void CompressBt_Click(object sender, RoutedEventArgs e)
        {
            bool compressResult = false;
            FileHelper fileSaver = new FileHelper();

            if (await fileSaver.PickToSave(_fileOpener.SelectedFileDisplayName, BaseHuffmanCodifier.HUFFMAN_FILE_DISPLAY_TYPE, BaseHuffmanCodifier.HUFFMAN_FILE_EXTENSION))
            {
                if(await fileSaver.OpenFile(FileAccessMode.ReadWrite))
                {
                    await ShowProgressPanel();

                    //Creo el Huffman Encoder
                    DebugUtils.WriteLine("Creating Huffman Encoder");
                    HuffmanEncoder encoder = HuffmanEncoder.From(_originalFileContent, _fileOpener.FileBOM);

                    //Creo la tabla de probabilidades                                        
                    encoder.Scan();
                    
                    //Comprimo el archivo
                    DebugUtils.WriteLine("Compressing file");
                    HuffmanEncodeResult encodeResult = encoder.Encode();

                    if (encodeResult != null)
                    {
                        DebugUtils.WriteLine("File compressed successfully");

                        //Creo y escribo el header del archivo
                        FileHeader header = new FileHeader()
                        {
                            FileName = _fileOpener.SelectedFileDisplayName,
                            FileDisplayType = _fileOpener.SelectedFileDisplayType,
                            FileExtension = _fileOpener.SelectedFileExtension
                        };

                        DebugUtils.WriteLine(string.Format("File header: {0}", header.ToString()));
                        
                        compressResult = fileSaver.WriteFileHeader(header);

                        if (compressResult)
                        {
                            //Escribo la tabla de probabilidades
                            DebugUtils.WriteLine(string.Format("Start dumping to: {0}", fileSaver.SelectedFilePath));
                            fileSaver.SetFileEncoding(_fileOpener.FileEncoding);
                            compressResult = HuffmanEncoder.WriteToFile(fileSaver, encodeResult);
                        }
                    }

                    //Cierro el archivo comprimido
                    DebugUtils.WriteLine("Closing file");
                    await fileSaver.Finish();
                    DebugUtils.WriteLine("File closed");
                    HideProgressPanel();
                }
            }

            if (compressResult)
            {
                await new MessageDialog("El archivo ha sido comprimido con exito").ShowAsync();
                DebugUtils.WriteLine("Compressing finished successfully");
            }
            else
            {
                await new MessageDialog("Ha ocurrido un error").ShowAsync();
                DebugUtils.WriteLine("Compressing process failed");
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

        private async Task ShowFileInformation()
        {
            fileNameBlock.Text = _fileOpener.SelectedFileDisplayName;
            fileSizeBlock.Text = string.Format("{0} bytes", _fileOpener.FileSize);
            fileDescriptionBlock.Text = _fileOpener.SelectedFileDisplayType;

            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                fileContentBlock.Text = _originalFileContent;
            });            
        }
    }
}
