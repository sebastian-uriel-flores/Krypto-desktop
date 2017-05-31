using FilesEncryptor.dto;
using FilesEncryptor.dto.Huffman;
using FilesEncryptor.helpers;
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

        private FileHelper _fileOpener, _fileSaver;
        private FileHeader _fileHeader;

        public HuffmanCompressPage()
        {
            this.InitializeComponent();
            _fileOpener = new FileHelper();
            _fileSaver = new FileHelper();
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
                if (await _fileOpener.OpenFile(FileAccessMode.Read))
                {
                    await ShowProgressPanel();
                    HidePanels();

                    //Leo todos los bytes del archivo y los convierto a string UTF8
                    byte[] fileBytes = _fileOpener.ReadBytes(_fileOpener.FileSize);
                    _originalFileContent = Encoding.UTF8.GetString(fileBytes);

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
            bool writeResult = false;

            if (await _fileSaver.PickToSave(_fileOpener.SelectedFileDisplayName, "Huffman encrypted file", ".huf"))
            {
                if(await _fileSaver.OpenFile(FileAccessMode.ReadWrite))
                {
                    await ShowProgressPanel();

                    //Creo la tabla de probabilidades
                    DebugUtils.WriteLine("Creating probabilities table");
                    ProbabilitiesScanner textScanner = await ProbabilitiesScanner.FromText(_originalFileContent);

                    //Comprimo el archivo
                    DebugUtils.WriteLine("Compressing file");
                    HuffmanEncodeResult encodeResult = await HuffmanEncoder.Encode(textScanner, _originalFileContent);

                    if (encodeResult != null)
                    {
                        DebugUtils.WriteLine("File compressed successfully");

                        //Creo y escribo el header del archivo                        
                        FileHeader header = new FileHeader()
                        {
                            FileName = _fileSaver.SelectedFileDisplayName,
                            FileDisplayType = _fileSaver.SelectedFileDisplayType,
                            FileExtension = _fileSaver.SelectedFileExtension
                        };

                        DebugUtils.WriteLine(string.Format("File header: {0}", header.ToString()));
                        writeResult = _fileSaver.WriteFileHeader(header);

                        //Escribo la tabla de probabilidades
                        DebugUtils.WriteLine("Dumping probabilities table to file");
                        foreach (var element in encodeResult.ProbabilitiesTable)
                        {
                            writeResult = _fileSaver.WriteString(string.Format("{0}{1}:", element.Key, element.Value.CodeLength));
                            writeResult = _fileSaver.WriteBytes(element.Value.Code.ToArray());
                        }

                        //Escribo el texto comprimido
                        DebugUtils.WriteLine("Dumping compressed bytes to file");
                        writeResult = _fileSaver.WriteString(string.Format("..{0}:", encodeResult.Encoded.CodeLength));
                        writeResult = _fileSaver.WriteBytes(encodeResult.Encoded.Code.ToArray());
                    }

                    //Cierro el archivo comprimido
                    DebugUtils.WriteLine("Closing file");
                    await _fileOpener.Finish();
                    DebugUtils.WriteLine("File closed");
                    HideProgressPanel();
                }
            }

            if (writeResult)
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
