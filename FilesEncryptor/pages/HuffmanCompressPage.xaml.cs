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
using Windows.UI.Xaml.Media.Animation;
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

                    if(_fileOpener.FileBOM == null)
                    {
                        await ShowEncodingPickPrompt();
                    }
                    else
                    {
                        //Leo todos los bytes del texto
                        byte[] fileBytes = _fileOpener.ReadBytes(_fileOpener.FileContentSize);

                        //Obtengo el texto que sera mostrado en pantalla
                        _originalFileContent = _fileOpener.FileEncoding.GetString(fileBytes);

                        //Cierro el archivo
                        await _fileOpener.Finish();

                        //Muestro la informacion del archivo
                        await ShowFileInformation();

                        ShowPanels();
                        HideProgressPanel();
                    }                    

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
                    DateTime startDate = DateTime.Now;

                    HuffmanEncoder encoder = HuffmanEncoder.From(_originalFileContent);

                    //Creo la tabla de probabilidades                                        
                    await encoder.Scan();
                    
                    //Comprimo el archivo
                    DebugUtils.WriteLine("Compressing file");
                    HuffmanEncodeResult encodeResult = await encoder.Encode();

                    //Imprimo la cantidad de tiempo que implico la codificacion
                    TimeSpan totalTime = DateTime.Now.Subtract(startDate);
                    DebugUtils.WriteLine(string.Format("Encoding process finished in a time of {0}", totalTime.ToString()));

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
                            compressResult = HuffmanEncoder.WriteToFile(fileSaver, encodeResult, _fileOpener.FileEncoding, _fileOpener.FileBOM);
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
            hidePagePanel.Visibility = Visibility.Visible;
            progressRing.Visibility = Visibility.Visible;
            await Task.Delay(200);
        }

        private void HideProgressPanel()
        {
            progressRing.Visibility = Visibility.Collapsed;
            hidePagePanel.Visibility = Visibility.Collapsed;
        }

        private async Task ShowEncodingPickPrompt()
        {
            hidePagePanel.Visibility = Visibility.Visible;
            progressRing.Visibility = Visibility.Collapsed;
            encodingPrompt.Visibility = Visibility.Visible;
            encodingPicker.SelectedIndex = 2;

            await Task.Delay(200);
        }

        private void HideEncodingPickPrompt()
        {
            encodingPrompt.Visibility = Visibility.Collapsed;
            hidePagePanel.Visibility = Visibility.Collapsed;
        }

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

        private void BackBt_Click(object sender, RoutedEventArgs e)
        {
            if(Frame.CanGoBack)
                Frame.GoBack(new SlideNavigationTransitionInfo());
        }

        private async void ConfirmEncodingPick_Click(object sender, RoutedEventArgs e)
        {
            HideEncodingPickPrompt();
            await ShowProgressPanel();

            switch (encodingPicker.SelectedIndex)
            {
                //ASCII
                case 0:
                    _fileOpener.SetFileEncoding(Encoding.ASCII);
                    break;
                //UTF-7
                case 1:
                    _fileOpener.SetFileEncoding(Encoding.UTF7);
                    break;
                //UTF-8
                case 2:
                    _fileOpener.SetFileEncoding(Encoding.UTF8);
                    break;
                //UTF-16LE
                case 3:
                    _fileOpener.SetFileEncoding(Encoding.Unicode);
                    break;
                //UTF-16BE
                case 4:
                    _fileOpener.SetFileEncoding(Encoding.BigEndianUnicode);
                    break;
                //UTF-32
                case 5:
                    _fileOpener.SetFileEncoding(Encoding.UTF32);
                    break;
            }

            //Leo todos los bytes del texto
            byte[] fileBytes = _fileOpener.ReadBytes(_fileOpener.FileContentSize);

            //Obtengo el texto que sera mostrado en pantalla
            _originalFileContent = _fileOpener.FileEncoding.GetString(fileBytes);

            //Cierro el archivo
            await _fileOpener.Finish();

            //Muestro la informacion del archivo
            await ShowFileInformation();

            ShowPanels();
            HideProgressPanel();
        }

        private async void CloseEncodingPickPrompt_Click(object sender, RoutedEventArgs e)
        {
            HideEncodingPickPrompt();
            await _fileOpener.Finish();
        }        
    }
}
