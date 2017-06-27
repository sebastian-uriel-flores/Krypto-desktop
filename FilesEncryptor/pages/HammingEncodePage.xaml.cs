using FilesEncryptor.dto;
using FilesEncryptor.dto.hamming;
using FilesEncryptor.dto.huffman;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.hamming;
using FilesEncryptor.helpers.huffman;
using FilesEncryptor.helpers.processes;
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
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
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
using static FilesEncryptor.helpers.DebugUtils;

// La plantilla de elemento Página en blanco está documentada en https://go.microsoft.com/fwlink/?LinkId=234238

namespace FilesEncryptor.pages
{
    /// <summary>
    /// Una página vacía que se puede usar de forma independiente o a la que se puede navegar dentro de un objeto Frame.
    /// </summary>
    public sealed partial class HammingEncodePage : Page, IKryptoProcessUI
    {
        public enum PAGE_MODES
        {
            Huffman_Encode, Huffman_Decode, Hamming_Encode, Hamming_Decode, Hamming_Broke
        }

        private PAGE_MODES _pageMode;

        #region HAMMING_DECODE

        private HammingDecoder _decoder;

        #endregion

        #region HUFFMAN_DECODE

        HuffmanDecoder _huffmanDecoder;

        #endregion  

        private int _selectedEncoding;
        private List<byte> _rawFileBytes;
        private ObservableCollection<HammingEncodeType> _encodeTypes = new ObservableCollection<HammingEncodeType>(BaseHammingCodifier.EncodeTypes);

        private FileHelper _fileOpener;
        private FileHeader _fileHeader;

        public HammingEncodePage()
        {
            this.InitializeComponent();
            hammingEncodeTypeSelector.Loaded += new RoutedEventHandler((obj, routEvArgs) =>
            {
                if (hammingEncodeTypeSelector.Items.Count > 0)
                {
                    hammingEncodeTypeSelector.SelectedIndex = 0;
                }
            });
            _fileOpener = new FileHelper();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame.CanGoBack)
            {
                // Show UI in title bar if opted-in and in-app backstack is not empty.
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Collapsed;
            }
            else
            {
                // Remove the UI from the title bar if in-app back stack is empty.
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Collapsed;
            }

            //Analizo el modo de uso de la pagina
            _pageMode = e.Parameter != null && e.Parameter is PAGE_MODES
                ? (PAGE_MODES)e.Parameter
                : PAGE_MODES.Hamming_Encode;

            switch(_pageMode)
            {
                case PAGE_MODES.Huffman_Encode:
                    pageHeaderContent.Text = "Comprimir con Huffman";
                    hammingEncodeTypeHeader.Visibility = Visibility.Visible;
                    hammingEncodeTypeSelector.Visibility = Visibility.Visible;
                    break;
                case PAGE_MODES.Huffman_Decode:
                    pageHeaderContent.Text = "Descomprimir con Huffman";
                    hammingEncodeTypeHeader.Visibility = Visibility.Collapsed;
                    hammingEncodeTypeSelector.Visibility = Visibility.Collapsed;
                    break;
                case PAGE_MODES.Hamming_Encode:
                    pageHeaderContent.Text = "Codificar con Hamming";
                    hammingEncodeTypeHeader.Visibility = Visibility.Collapsed;
                    hammingEncodeTypeSelector.Visibility = Visibility.Collapsed;
                    break;
                case PAGE_MODES.Hamming_Decode:
                    pageHeaderContent.Text = "Decodificar con Hamming";
                    hammingEncodeTypeHeader.Visibility = Visibility.Collapsed;
                    hammingEncodeTypeSelector.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private List<string>GetExtensions(PAGE_MODES mode)
        {
            List<string> extensions = new List<string>();
            switch (mode)
            {
                case PAGE_MODES.Huffman_Encode:
                    extensions.Add(".txt");
                    break;
                case PAGE_MODES.Huffman_Decode:
                    extensions.Add(".huf");
                    break;
                case PAGE_MODES.Hamming_Encode:
                    extensions = new List<string>(){
                        ".txt",
                        ".huf",
                        ".pdf",
                        ".docx",
                        ".doc",
                        ".jpg"
                        };
                    break;
                case PAGE_MODES.Hamming_Decode:
                case PAGE_MODES.Hamming_Broke:
                    foreach (HammingEncodeType type in BaseHammingCodifier.EncodeTypes)
                    {
                        extensions.Add(type.Extension);
                    }

                    break;
            }

            return extensions;
        }

        private async void SelectFileBt_Click(object sender, RoutedEventArgs e)
        {
            if (await _fileOpener.PickToOpen(GetExtensions(_pageMode)))
            {
                _rawFileBytes = null;
                await ShowLoadingPanel();

                //settingsPanel.Visibility = Visibility.Collapsed;
                //pageCommandsDivider.Visibility = Visibility.Collapsed;
                //pageCommands.Visibility = Visibility.Collapsed;
                confirmBt.IsEnabled = false;

                bool takeEncoding = _pageMode == PAGE_MODES.Huffman_Encode;

                if (await _fileOpener.OpenFile(FileAccessMode.Read, takeEncoding))
                {
                    //Muestro los datos del archivo cargado
                    fileNameBlock.Text = _fileOpener.SelectedFileName;
                    fileSizeBlock.Text = string.Format("{0} bytes", _fileOpener.FileSize);
                    fileDescriptionBlock.Text = string.Format("{0} ({1})", _fileOpener.SelectedFileDisplayType, _fileOpener.SelectedFileExtension);

                    //settingsPanel.Visibility = Visibility.Visible;
                    //pageCommandsDivider.Visibility = Visibility.Visible;
                    //pageCommands.Visibility = Visibility.Visible;
                    confirmBt.IsEnabled = true;

                    switch (_pageMode)
                    {
                        case PAGE_MODES.Huffman_Encode:
                            //TODO Si no se puede obtener el tipo de codificacion del archivo, 
                            //se solicita al usuario que elija una de las posibles codificaciones
                            if (_fileOpener.FileBOM == null)
                            {
                                _fileOpener.SetFileEncoding(System.Text.Encoding.UTF8);
                            }

                            //Leo todos los bytes del texto
                            byte[] fileBytes = _fileOpener.ReadBytes(_fileOpener.FileContentSize);

                            //Obtengo el texto que sera mostrado en pantalla
                            string originalFileContent = _fileOpener.FileEncoding.GetString(fileBytes);

                            //Cierro el archivo
                            await _fileOpener.Finish();

                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                             {
                                 //Seteo el texto en pantalla
                             });

                            //Muestro la informacion del archivo
                            HideLoadingPanel();


                            //Creo el header que tendra el archivo al guardarlo
                            _fileHeader = new FileHeader()
                            {
                                FileName = _fileOpener.SelectedFileDisplayName,
                                FileDisplayType = _fileOpener.SelectedFileDisplayType,
                                FileExtension = _fileOpener.SelectedFileExtension
                            };
                            break;
                        case PAGE_MODES.Huffman_Decode:
                            _fileHeader = _fileOpener.ReadFileHeader();
                            _huffmanDecoder = await HuffmanDecoder.FromFile(_fileOpener);
                            break;
                        case PAGE_MODES.Hamming_Encode:
                            _rawFileBytes = _fileOpener.ReadBytes(_fileOpener.FileSize).ToList();
                            _fileHeader = new FileHeader()
                            {
                                FileName = _fileOpener.SelectedFileDisplayName,
                                FileDisplayType = _fileOpener.SelectedFileDisplayType,
                                FileExtension = _fileOpener.SelectedFileExtension
                            };

                            break;
                        case PAGE_MODES.Hamming_Decode:
                            _fileHeader = _fileOpener.ReadFileHeader();

                            HammingEncodeType encodeType = BaseHammingCodifier.EncodeTypes.First(encType => encType.Extension == _fileOpener.SelectedFileExtension);

                            _decoder = new HammingDecoder(_fileOpener, encodeType);

                            if (_fileHeader.FileExtension == BaseHuffmanCodifier.HUFFMAN_FILE_EXTENSION)
                            {
                                ConsoleWL("The original file is compressed with Huffman", "[WARN]");
                            }
                            break;
                    }

                    await _fileOpener.Finish();
                }

                HideLoadingPanel();
            }
        }

        private void HammingEncodeTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedEncoding = hammingEncodeTypeSelector.SelectedIndex;
        }

        private void ConfirmBt_Click(object sender, RoutedEventArgs e)
        {
            switch(_pageMode)
            {
                case PAGE_MODES.Huffman_Encode:
                    HuffmanEncode();
                    break;
                case PAGE_MODES.Huffman_Decode:
                    HuffmanDecode();
                    break;
                case PAGE_MODES.Hamming_Encode:
                    HammingEncode();
                    break;
                case PAGE_MODES.Hamming_Decode:
                    HammingDecode();
                    break;
            }
        }

        private void BackBt_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        private void ProgressPanelEventsToggleBt_Click(object sender, RoutedEventArgs e)
        {
            progressPanelEventsList.Visibility = (bool)progressPanelEventsToggleBt.IsChecked
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ProgressPanelCloseButton_Click(object sender, RoutedEventArgs e)
        {
            HideProgressPanel();
            ResetProgressPanel();
        }


        private async Task ShowLoadingPanel()
        {
            loadingPanel.Visibility = Visibility.Visible;
            await Task.Delay(200);
        }

        private void HideLoadingPanel() => loadingPanel.Visibility = Visibility.Collapsed;

        private async Task ShowProgressPanel()
        {
            progressPanelCloseButton.Visibility = Visibility.Collapsed;
            progressPanel.Visibility = Visibility.Visible;
            await Task.Delay(200);
        }

        private void HideProgressPanel() => progressPanel.Visibility = Visibility.Collapsed;

        private void ResetProgressPanel()
        {
            progressPanelStatus.Text = "";
            progressPanelTime.Text = "";
            progressPanelCurrentEvent.Text = "";
            progressPanelProgressBar.Value = 0;
            progressPanelEventsList.Items.Clear();
        }

       

        #region KRYPTO_PROCESS_UI_INTERFACE

        public async void SetStatus(string currentStatus)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                progressPanelStatus.Text = currentStatus ?? "";
            });
        }

        public async void SetTime(TimeSpan totalTime)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                progressPanelTime.Text = totalTime != null ? totalTime.ToString() : "";
            });
        }

        public async void SetProgressMessage(string progressMessage)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                progressPanelCurrentEvent.Text = progressMessage ?? "";
            });
        }

        public async void SetProgressLevel(double progressLevel)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                progressPanelProgressBar.Value = progressLevel;
            });
        }

        public async void AddEvent(BaseKryptoProcess.KryptoEvent kEvent)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                progressPanelEventsList.Items.Add($"{kEvent.Tag} : {kEvent.Message}");

                var selectedIndex = progressPanelEventsList.Items.Count - 1;
                if (selectedIndex < 0)
                    return;

                progressPanelEventsList.SelectedIndex = selectedIndex;
                progressPanelEventsList.UpdateLayout();

                progressPanelEventsList.ScrollIntoView(progressPanelEventsList.SelectedItem);
            });
        }

        public void SetShowFailureInformationButtonVisible(bool visible)
        {

        }

        #endregion

        #region HUFFMAN_ENCODE

        private async void HuffmanEncode()
        {
            bool compressResult = false;

            BaseKryptoProcess encodingProcess = new BaseKryptoProcess();
            await ShowProgressPanel();
            encodingProcess.Start(this);

            HuffmanEncoder encoder = HuffmanEncoder.From("");
            await encoder.Scan();

            HuffmanEncodeResult encodeResult = await encoder.Encode();

            //Si el archivo pudo ser codificado con Huffman
            if (encodeResult != null)
            {
                FileHelper fileSaver = new FileHelper();

                //Si el usuario selecciono correctamente un archivo
                if (await fileSaver.PickToSave(_fileHeader.FileName, BaseHuffmanCodifier.HUFFMAN_FILE_DISPLAY_TYPE, BaseHuffmanCodifier.HUFFMAN_FILE_EXTENSION))
                {
                    //Si el archivo pudo ser abierto
                    if (await fileSaver.OpenFile(FileAccessMode.ReadWrite))
                    {
                        compressResult = fileSaver.WriteFileHeader(_fileHeader);

                        //Si el header se escribio correctamente
                        if (compressResult)
                        {
                            //Escribo la tabla de probabilidades
                            DebugUtils.ConsoleWL(string.Format("Start dumping to: {0}", fileSaver.SelectedFilePath));
                            fileSaver.SetFileEncoding(_fileOpener.FileEncoding);
                            compressResult = HuffmanEncoder.WriteToFile(fileSaver, encodeResult, _fileOpener.FileEncoding, _fileOpener.FileBOM);

                            if(compressResult)
                            {
                                //Cierro el archivo comprimido
                                encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                {
                                    Message = "Encoded file dumped properly",
                                    ProgressAdvance = 100,
                                    Tag = "[PROGRESS]"
                                });
                                encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                {
                                    Message = "Closing file",
                                    ProgressAdvance = 100,
                                    Tag = "[INFO]"
                                });

                                //DebugUtils.ConsoleWL("Dumping completed properly");
                                //DebugUtils.ConsoleWL("Closing file");
                                await fileSaver.Finish();


                                encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                {
                                    Message = "File closed",
                                    ProgressAdvance = 100,
                                    Tag = "[INFO]"
                                });
                                encodingProcess.Stop();
                            }
                            //Si ocurrio un error al escribir el archivo
                            else
                            {
                                encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                {
                                    Message = "Decoded file dumping uncompleted",
                                    ProgressAdvance = 100,
                                    Tag = "[PROGRESS]"
                                });
                                encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                {
                                    Message = "Closing file",
                                    ProgressAdvance = 100,
                                    Tag = "[INFO]"
                                });

                                await fileSaver.Finish();

                                encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                {
                                    Message = "File closed",
                                    ProgressAdvance = 100,
                                    Tag = "[INFO]"
                                });
                                encodingProcess.Stop(true);
                            }
                        }
                        //Si ocurrio un error al escribir el Header
                        else
                        {
                            //Cierro el archivo comprimido
                            encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = "Decoded file dumping uncompleted",
                                ProgressAdvance = 100,
                                Tag = "[PROGRESS]"
                            });
                            encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = "Closing file",
                                ProgressAdvance = 100,
                                Tag = "[INFO]"
                            });

                            await fileSaver.Finish();

                            encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = "File closed",
                                ProgressAdvance = 100,
                                Tag = "[INFO]"
                            });
                            encodingProcess.Stop(true);
                        }
                    }
                    //Si el archivo seleccionado no pudo ser abierto
                    else
                    {
                        encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                        {
                            Message = "File could not be opened to ReadWrite",
                            ProgressAdvance = 100,
                            Tag = "[FAIL]"
                        });
                        encodingProcess.Stop(true);
                    }
                }
                //Si el usuario cancelo la seleccion de archivo
                else
                {
                    HideProgressPanel();
                    ResetProgressPanel();
                    encodingProcess.Stop(true);
                }
            }
            //Si el archivo no pudo ser codificado con Huffman
            else
            {
                encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                {
                    Message = "File could not be encoded with Huffman",
                    ProgressAdvance = 100,
                    Tag = "[FAIL]"
                });
                encodingProcess.Stop(true);

                progressPanelCloseButton.Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region HUFFMAN_DECODE

        private async void HuffmanDecode()
        {
            BaseKryptoProcess decodingProcess = new BaseKryptoProcess();

            await ShowProgressPanel();
            decodingProcess.Start(this);

            //Creo el decodificador de Huffman            
            string huffDecoded = await _huffmanDecoder.DecodeWithTreeMultithreaded(decodingProcess);

            //Imprimo la cantidad de tiempo que implico la decodificacion
            decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
            {
                Message = $"Huffman decoding process finished",
                ProgressAdvance = 100,
                Tag = "[RESULT]"
            });

            //Si la decodificacion finalizo correctamente
            if (huffDecoded != null)
            {
                FileHelper fileSaver = new FileHelper();

                //Solicito al usuario que seleccione la carpeta en la que se almacenara el archivo decodificado
                if (await fileSaver.PickToSave(_fileHeader.FileName, _fileHeader.FileDisplayType, _fileHeader.FileExtension))
                {
                    decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                    {
                        Message = $"Output file selected: {fileSaver.SelectedFilePath}",
                        ProgressAdvance = 0,
                        Tag = "[INFO]"
                    });

                    //Si el archivo pudo abrirse
                    if (await fileSaver.OpenFile(FileAccessMode.ReadWrite))
                    {
                        decodingProcess.UpdateStatus($"Dumping decoded file to {fileSaver.SelectedFilePath}");

                        //Seteo la codificacion en la que se escribira el texto
                        fileSaver.SetFileEncoding(_fileOpener.FileEncoding);

                        //Escribo el texto decodificado en el archivo de salida
                        fileSaver.WriteString(huffDecoded);

                        //Cierro el archivo descomprimido
                        decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                        {
                            Message = "Decoded file dumped properly",
                            ProgressAdvance = 100,
                            Tag = "[PROGRESS]"
                        });
                        decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                        {
                            Message = "Closing file",
                            ProgressAdvance = 100,
                            Tag = "[INFO]"
                        });

                        //DebugUtils.ConsoleWL("Dumping completed properly");
                        //DebugUtils.ConsoleWL("Closing file");
                        await fileSaver.Finish();

                        decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                        {
                            Message = "File closed",
                            ProgressAdvance = 100,
                            Tag = "[INFO]"
                        });
                        decodingProcess.Stop();
                    }
                    //Si el archivo no pudo ser abierto
                    else
                    {
                        decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                        {
                            Message = "File could not be opened to ReadWrite",
                            ProgressAdvance = 100,
                            Tag = "[FAIL]"
                        });
                        decodingProcess.Stop(true);
                    }

                    progressPanelCloseButton.Visibility = Visibility.Visible;
                }
                //Si el usuario no selecciono ningun archivo
                else
                {
                    ConsoleWL("User cancel file selection");
                    decodingProcess.Stop(true);
                    HideProgressPanel();
                    ResetProgressPanel();
                }
            }

            //Si no se pudo decodificar
            else
            {
                decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                {
                    Message = "File could not be decoded with Huffman",
                    ProgressAdvance = 100,
                    Tag = "[FAIL]"
                });
                decodingProcess.Stop(true);

                progressPanelCloseButton.Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region HAMMING_ENCODE

        private async void HammingEncode()
        {
            HammingEncodeType selectedEncodingType = _encodeTypes[_selectedEncoding];

            //Codifico el archivo original
            HammingEncoder encoder = HammingEncoder.From(new BitCode(_rawFileBytes, _rawFileBytes.Count * 8));

            KryptoProcess<HammingEncodeResult> encodingProcess = new KryptoProcess<HammingEncodeResult>(
                new Task<HammingEncodeResult>(() => encoder.Encode(selectedEncodingType)));

            await ShowProgressPanel();

            encodingProcess.Start(this, true,
                async (result) =>
                {
                    //Si el proceso fue un exito
                    FileHelper fileSaver = new FileHelper();
                    bool pickResult = false;

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        pickResult = await fileSaver.PickToSave(_fileOpener.SelectedFileDisplayName, selectedEncodingType.LongDescription, selectedEncodingType.Extension);

                        //Si el usuario no canceló la operación
                        if (pickResult)
                        {
                            encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = $"Output file selected {fileSaver.SelectedFilePath}",
                                ProgressAdvance = 100,
                                Tag = "[INFO]"
                            });

                            if (await fileSaver.OpenFile(FileAccessMode.ReadWrite))
                            {
                                encodingProcess.UpdateStatus($"Dumping encoded file to {fileSaver.SelectedFilePath}");

                                if (fileSaver.WriteFileHeader(_fileHeader))
                                {
                                    encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                    {
                                        Message = "File header dumped properly",
                                        ProgressAdvance = 50,
                                        Tag = "[PROGRESS]"
                                    });
                                    //DebugUtils.ConsoleWL(string.Format("Dumping hamming encoded bytes to \"{0}\"", fileSaver.SelectedFilePath));

                                    bool writeResult = HammingEncoder.WriteEncodedToFile(result, fileSaver);

                                    //Show congrats message
                                    if (writeResult)
                                    {
                                        encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                        {
                                            Message = "Encoded file dumped properly",
                                            ProgressAdvance = 100,
                                            Tag = "[PROGRESS]"
                                        });
                                        encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                        {
                                            Message = "Closing file",
                                            ProgressAdvance = 100,
                                            Tag = "[INFO]"
                                        });

                                        //DebugUtils.ConsoleWL("Dumping completed properly");
                                        //DebugUtils.ConsoleWL("Closing file");
                                        await fileSaver.Finish();

                                        encodingProcess.UpdateStatus("Completed");
                                        encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                        {
                                            Message = "File closed",
                                            ProgressAdvance = 100,
                                            Tag = "[INFO]"
                                        });
                                    }
                                    else
                                    {
                                        //DebugUtils.ConsoleWL("Dumping uncompleted");
                                        //DebugUtils.ConsoleWL("Closing file");
                                        encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                        {
                                            Message = "Encoded file dumping uncompleted",
                                            ProgressAdvance = 100,
                                            Tag = "[PROGRESS]"
                                        });
                                        encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                        {
                                            Message = "Closing file",
                                            ProgressAdvance = 100,
                                            Tag = "[INFO]"
                                        });

                                        await fileSaver.Finish();

                                        encodingProcess.UpdateStatus("Failed");
                                        encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                        {
                                            Message = "File closed",
                                            ProgressAdvance = 100,
                                            Tag = "[INFO]"
                                        });
                                    }

                                    progressPanelCloseButton.Visibility = Visibility.Visible;
                                }
                            }
                        }
                        else
                        {
                            DebugUtils.ConsoleWL("File encoded canceled because user cancel output file selection");
                            HideProgressPanel();
                            ResetProgressPanel();
                        }
                    });
                },
                async (failedTaskIndex) =>
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        progressPanelCloseButton.Visibility = Visibility.Visible;
                    });
                });
        }
       
        #endregion

        #region HAMMING_DECODE

        private async void HammingDecode()
        {
            //Si el archivo original es un .huf, 
            //entonces pregunto al usuario si desea descomprimirlo luego de decodificarlo

            #region ASK_UNCOMPRESS_HUF

            bool uncompressHuff = false;

            if (_fileHeader.FileExtension == BaseHuffmanCodifier.HUFFMAN_FILE_EXTENSION)
            {
                MessageDialog askPrompt = new MessageDialog("El archivo comprimido está comprimido con Huffman. ¿Desea descomprimirlo?")
                {
                    DefaultCommandIndex = 0,
                    CancelCommandIndex = 1
                };
                askPrompt.Commands.Add(new UICommand("Descomprimir") { Id = 0 });
                askPrompt.Commands.Add(new UICommand("No") { Id = 1 });
                var promptRes = await askPrompt.ShowAsync();

                if ((int)promptRes.Id == 0)
                {
                    ConsoleWL("User decided to uncompress Huffman format next to Hamming decoding", "[WARN]");
                    uncompressHuff = true;
                }
                else
                {
                    ConsoleWL("User decided to maintain Huffman format next to Hamming decoding", "[WARN]");
                }
            }

            #endregion

            //Inicio la decodificacion en Hamming
            BaseKryptoProcess decodingProcess = new BaseKryptoProcess();
            await ShowProgressPanel();
            decodingProcess.Start(this);
            BitCode result = await _decoder.Decode(decodingProcess);

            if (result != null)
            {
                //Si el archivo debe ser decodificado de Huffman
                #region DECODE_HUFFMAN

                if (uncompressHuff)
                {
                    //Almaceno el codigo decodificado con Hamming en un archivo temporal
                    StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
                    StorageFile tempHufFile =
                        await storageFolder.CreateFileAsync($"temp-hamming{BaseHuffmanCodifier.HUFFMAN_FILE_EXTENSION}",
                        CreationCollisionOption.GenerateUniqueName);

                    FileHelper tempHufFileHelper = new FileHelper(tempHufFile);

                    if (await tempHufFileHelper.OpenFile(FileAccessMode.ReadWrite))
                    {
                        if (tempHufFileHelper.WriteBytes(result.Code.ToArray()))
                        {
                            //Cierro el archivo temporal y vuelvo a abrirlo para lectura
                            await tempHufFileHelper.Finish();

                            if (await tempHufFileHelper.OpenFile(FileAccessMode.Read))
                            {
                                decodingProcess.UpdateStatus($"Dumping hamming decoded bytes to temp file: \"{tempHufFileHelper.SelectedFilePath}\"", true);

                                FileHeader internalFileHeader = tempHufFileHelper.ReadFileHeader();

                                //Creo el decodificador de Huffman
                                HuffmanDecoder huffDecoder = await HuffmanDecoder.FromFile(tempHufFileHelper);
                                string huffDecoded = await huffDecoder.DecodeWithTreeMultithreaded(decodingProcess);

                                //Imprimo la cantidad de tiempo que implico la decodificacion
                                decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                {
                                    Message = $"Huffman decoding process finished",
                                    ProgressAdvance = 100,
                                    Tag = "[RESULT]"
                                });

                                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                                {
                                        //Si la decodificacion finalizo correctamente
                                        if (huffDecoded != null)
                                    {
                                        FileHelper fileSaver = new FileHelper();

                                            //Solicito al usuario que seleccione la carpeta en la que se almacenara el archivo decodificado
                                            if (await fileSaver.PickToSave(internalFileHeader.FileName, internalFileHeader.FileDisplayType, internalFileHeader.FileExtension))
                                        {
                                            decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                            {
                                                Message = $"Output file selected: {fileSaver.SelectedFilePath}",
                                                ProgressAdvance = 0,
                                                Tag = "[INFO]"
                                            });

                                                //Si el archivo pudo abrirse
                                                if (await fileSaver.OpenFile(FileAccessMode.ReadWrite))
                                            {
                                                decodingProcess.UpdateStatus($"Dumping decoded file to {fileSaver.SelectedFilePath}");

                                                    //Seteo la codificacion en la que se escribira el texto
                                                    fileSaver.SetFileEncoding(tempHufFileHelper.FileEncoding);

                                                    //Escribo el texto decodificado en el archivo de salida
                                                    fileSaver.WriteString(huffDecoded);

                                                    //Cierro el archivo descomprimido
                                                    decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                                {
                                                    Message = "Decoded file dumped properly",
                                                    ProgressAdvance = 100,
                                                    Tag = "[PROGRESS]"
                                                });
                                                decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                                {
                                                    Message = "Closing file",
                                                    ProgressAdvance = 100,
                                                    Tag = "[INFO]"
                                                });

                                                    //DebugUtils.ConsoleWL("Dumping completed properly");
                                                    //DebugUtils.ConsoleWL("Closing file");
                                                    await fileSaver.Finish();

                                                decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                                {
                                                    Message = "File closed",
                                                    ProgressAdvance = 100,
                                                    Tag = "[INFO]"
                                                });
                                                decodingProcess.Stop();
                                            }
                                            //Si el archivo no pudo ser abierto
                                            else
                                            {
                                                decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                                {
                                                    Message = "File could not be opened to ReadWrite",
                                                    ProgressAdvance = 100,
                                                    Tag = "[FAIL]"
                                                });
                                                decodingProcess.Stop(true);
                                            }

                                            progressPanelCloseButton.Visibility = Visibility.Visible;
                                        }
                                            //Si el usuario no selecciono ningun archivo
                                            else
                                        {
                                            ConsoleWL("User cancel file selection");
                                            decodingProcess.Stop(true);
                                            HideProgressPanel();
                                            ResetProgressPanel();
                                        }
                                    }

                                        //Si no se pudo decodificar
                                        else
                                    {
                                        decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                        {
                                            Message = "File could not be decoded with Huffman",
                                            ProgressAdvance = 100,
                                            Tag = "[FAIL]"
                                        });
                                        decodingProcess.Stop(true);

                                        progressPanelCloseButton.Visibility = Visibility.Visible;
                                    }
                                });
                            }
                        }
                    }
                }

                #endregion

                //Si el archivo debe ser almacenado tal y como fue decodificado
                #region DECODE_ONLY_HAMMING

                else
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        FileHelper fileSaver = new FileHelper();

                            //Si el usuario selecciona un archivo
                        if (await fileSaver.PickToSave(_fileHeader.FileName, _fileHeader.FileDisplayType, _fileHeader.FileExtension))
                        {
                            decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = $"Output file selected {fileSaver.SelectedFilePath}",
                                ProgressAdvance = 0,
                                Tag = "[INFO]"
                            });

                            //Si el archivo pudo ser abierto correctamente
                            if (await fileSaver.OpenFile(FileAccessMode.ReadWrite))
                            {
                                decodingProcess.UpdateStatus($"Dumping decoded file to {fileSaver.SelectedFilePath}");

                                if (fileSaver.WriteBytes(result.Code.ToArray()))
                                {
                                    decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                    {
                                        Message = "Decoded file dumped properly",
                                        ProgressAdvance = 100,
                                        Tag = "[PROGRESS]"
                                    });
                                    decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                    {
                                        Message = "Closing file",
                                        ProgressAdvance = 100,
                                        Tag = "[INFO]"
                                    });

                                        //DebugUtils.ConsoleWL("Dumping completed properly");
                                        //DebugUtils.ConsoleWL("Closing file");
                                    await fileSaver.Finish();


                                    decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                    {
                                        Message = "File closed",
                                        ProgressAdvance = 100,
                                        Tag = "[INFO]"
                                    });
                                    decodingProcess.Stop();
                                }
                                //Si ocurrio un error al escribir el archivo
                                else
                                {
                                    decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                    {
                                        Message = "Decoded file dumping uncompleted",
                                        ProgressAdvance = 100,
                                        Tag = "[PROGRESS]"
                                    });
                                    decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                    {
                                        Message = "Closing file",
                                        ProgressAdvance = 100,
                                        Tag = "[INFO]"
                                    });

                                        //DebugUtils.ConsoleWL("Dumping completed properly");
                                        //DebugUtils.ConsoleWL("Closing file");
                                        await fileSaver.Finish();

                                    decodingProcess.UpdateStatus("Failed");
                                    decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                    {
                                        Message = "File closed",
                                        ProgressAdvance = 100,
                                        Tag = "[INFO]"
                                    });
                                }
                            }

                                //Si el archivo no pudo ser abierto para edicion
                                else
                            {
                                decodingProcess.UpdateStatus("Failed");
                                decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                {
                                    Message = "File could not be opened to ReadWrite",
                                    ProgressAdvance = 100,
                                    Tag = "[FAIL]"
                                });
                            }

                            progressPanelCloseButton.Visibility = Visibility.Visible;
                        }
                            //Si el usuario no selecciono ningun archivo
                            else
                        {
                            ConsoleWL("User cancel file selection");
                            HideProgressPanel();
                            ResetProgressPanel();
                        }
                    });
                }

                #endregion
            }
            else
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    progressPanelCloseButton.Visibility = Visibility.Visible;
                });
            }
        }

        #endregion
    }
}
