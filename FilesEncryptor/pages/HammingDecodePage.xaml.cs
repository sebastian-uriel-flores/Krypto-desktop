﻿using FilesEncryptor.dto;
using FilesEncryptor.dto.hamming;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.hamming;
using FilesEncryptor.helpers.huffman;
using FilesEncryptor.utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
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
        private FileHelper _fileOpener;
        private FileHeader _fileHeader;
        HammingDecoder _decoder;

        public HammingDecodePage()
        {
            InitializeComponent();
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
            var extensions = new List<string>();
            foreach (HammingEncodeType type in BaseHammingCodifier.EncodeTypes)
            {
                extensions.Add(type.Extension);
            }

            bool pickResult = await _fileOpener.PickToOpen(extensions);

            if(pickResult)
            {
                await ShowProgressPanel();
                await Task.Delay(200);

                HidePanels();
                
                //Si el archivo pudo ser abierto
                if(await _fileOpener.OpenFile(FileAccessMode.Read))
                {
                    DebugUtils.WriteLine($"Selected file: {_fileOpener.SelectedFilePath} with size of { _fileOpener.FileSize} bytes");

                    ShowFileInformation();
                    ShowPanels();
                    _fileHeader = _fileOpener.ReadFileHeader();

                    if (_fileHeader != null)
                    {
                        DebugUtils.WriteLine("File bytes extracted properly");
                        _decoder = HammingDecoder.FromFile(_fileOpener, BaseHammingCodifier.EncodeTypes.First(encType => encType.Extension == _fileOpener.SelectedFileExtension));

                        if (_fileHeader.FileExtension == BaseHuffmanCodifier.HUFFMAN_FILE_EXTENSION)
                        {
                            DebugUtils.WriteLine("The original file is compressed with Huffman", "[WARN]");
                        }
                    }
                    else
                    {
                        DebugUtils.WriteLine("File header is corrupt", "[FAIL]");

                        MessageDialog dialog = new MessageDialog("El archivo está dañado", "Ha ocurrido un error");
                        await dialog.ShowAsync();
                    }

                    DebugUtils.WriteLine("Closing file");
                    await _fileOpener.Finish();
                    DebugUtils.WriteLine("File closed");
                }
                //Si el archivo no pudo ser abierto
                else
                {
                    DebugUtils.WriteLine("File could not be opened to Read", "[FAIL]");

                    MessageDialog dialog = new MessageDialog("El archivo no pudo ser cargado", "Ha ocurrido un error");
                    await dialog.ShowAsync();
                }
            }
            HideProgressPanel();
        }

        private async void DecodeBt_Click(object sender, RoutedEventArgs e)
        {
            //Si el archivo original es un .huf, 
            //entonces pregunto al usuario si desea descomprimirlo luego de decodificarlo
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
                    DebugUtils.WriteLine("User decided to uncompress Huffman format next to Hamming decoding", "[WARN]");
                    uncompressHuff = true;
                }
                else
                {
                    DebugUtils.WriteLine("User decided to maintain Huffman format next to Hamming decoding", "[WARN]");
                }
            }

            //Inicio la decodificacion en Hamming
            await ShowProgressPanel();
            DebugUtils.WriteLine("Starting Hamming Decoding");

            DateTime startDate = DateTime.Now;

            //Codifico el archivo original
            
            BitCode result = await _decoder.Decode();

            //Imprimo la cantidad de tiempo que implico la decodificacion
            TimeSpan hammingDecodingTime = DateTime.Now.Subtract(startDate);
            DebugUtils.WriteLine($"Hamming decoding process finished in a time of {hammingDecodingTime}");

            //Si el archivo pudo ser decodificado con Hamming
            if (result != null)
            {   
                //Si el archivo debe ser decodificado de Huffman
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

                            if(await tempHufFileHelper.OpenFile(FileAccessMode.Read))
                            {
                                DebugUtils.WriteLine($"Dumping hamming decoded bytes to temp file: \"{tempHufFileHelper.SelectedFilePath}\"");

                                _fileHeader = tempHufFileHelper.ReadFileHeader();

                                DateTime huffmanDecodingStart = DateTime.Now;

                                //Creo el decodificador de Huffman
                                HuffmanDecoder huffDecoder = HuffmanDecoder.FromFile(tempHufFileHelper);
                                string huffDecoded = await huffDecoder.DecodeWithTreeMultithreaded();

                                //Imprimo la cantidad de tiempo que implico la decodificacion
                                TimeSpan huffmanDecodingTime = DateTime.Now.Subtract(huffmanDecodingStart);
                                DebugUtils.WriteLine($"Huffman decoding process finished in a time of {huffmanDecodingTime}");

                                //Si la decodificacion finalizo correctamente
                                if (huffDecoded != null)
                                {
                                    FileHelper fileSaver = new FileHelper();

                                    //Solicito al usuario que seleccione la carpeta en la que se almacenara el archivo decodificado
                                    if (await fileSaver.PickToSave(_fileHeader.FileName, _fileHeader.FileDisplayType, _fileHeader.FileExtension))
                                    {
                                        DebugUtils.WriteLine($"Output file: \"{fileSaver.SelectedFilePath}\"");

                                        //Si el archivo pudo abrirse
                                        if (await fileSaver.OpenFile(FileAccessMode.ReadWrite))
                                        {
                                            //Seteo la codificacion en la que se escribira el texto
                                            fileSaver.SetFileEncoding(tempHufFileHelper.FileEncoding);

                                            //Escribo el texto decodificado en el archivo de salida
                                            fileSaver.WriteString(huffDecoded);

                                            //Cierro el archivo descomprimido
                                            DebugUtils.WriteLine("Closing file");
                                            await fileSaver.Finish();
                                            DebugUtils.WriteLine("File closed");
                                        }
                                        //Si el archivo no pudo ser abierto
                                        else
                                        {
                                            DebugUtils.WriteLine("File could not be opened to ReadWrite", "[FAIL]");                                            

                                            MessageDialog dialog = new MessageDialog("El archivo no pudo ser creado correctamente", "Ha ocurrido un error");
                                            await dialog.ShowAsync();
                                        }
                                    }
                                    //Si el usuario no selecciono ningun archivo
                                    else
                                    {
                                        DebugUtils.WriteLine("User cancel file selection");
                                    }
                                }
                                //Si no se pudo decodificar
                                else
                                {
                                    DebugUtils.WriteLine("File could not be decoded with Huffman", "[FAIL]");

                                    MessageDialog dialog = new MessageDialog("El archivo no pudo ser decodificado", "Ha ocurrido un error");
                                    await dialog.ShowAsync();
                                }
                            }                            
                        }
                    }
                }

                //Si el archivo debe ser almacenado tal y como fue decodificado
                else
                {
                    FileHelper fileSaver = new FileHelper();

                    //Si el usuario selecciona un archivo
                    if (await fileSaver.PickToSave(_fileHeader.FileName, _fileHeader.FileDisplayType, _fileHeader.FileExtension))
                    {
                        DebugUtils.WriteLine($"Output file: \"{fileSaver.SelectedFilePath}\"");

                        //Si el archivo pudo ser abierto correctamente
                        if (await fileSaver.OpenFile(FileAccessMode.ReadWrite))
                        {                            
                            DebugUtils.WriteLine($"Dumping hamming decoded bytes to \"{fileSaver.SelectedFilePath}\"");

                            if(fileSaver.WriteBytes(result.Code.ToArray()))
                            {
                                DebugUtils.WriteLine("Dumping completed properly");
                                DebugUtils.WriteLine("Closing file");
                                await fileSaver.Finish();
                                DebugUtils.WriteLine("File closed");

                                MessageDialog dialog = new MessageDialog("El archivo ha sido guardado", "Ha sido todo un Exito");
                                await dialog.ShowAsync();
                            }
                            else
                            {
                                DebugUtils.WriteLine("Dumping uncompleted");
                                DebugUtils.WriteLine("Closing file");
                                await fileSaver.Finish();
                                DebugUtils.WriteLine("File closed");

                                MessageDialog dialog = new MessageDialog("El archivo no pudo ser guardado.", "Ha ocurrido un error");
                                await dialog.ShowAsync();
                            }
                        }
                        //Si el archivo no pudo ser abierto para edicion
                        else
                        {
                            DebugUtils.WriteLine("File could not be opened to ReadWrite", "[FAIL]");

                            MessageDialog dialog = new MessageDialog("El archivo no pudo ser creado correctamente", "Ha ocurrido un error");
                            await dialog.ShowAsync();
                        }
                    }
                    //Si el usuario no selecciono ningun archivo
                    else
                    {
                        DebugUtils.WriteLine("User cancel file selection");
                    }
                }
            }
            //Si el archivo no pudo ser decodificado
            else
            {
                DebugUtils.WriteLine("File could not be decoded with Hamming", "[FAIL]");
                
                MessageDialog dialog = new MessageDialog("El archivo no pudo ser decodificado", "Ha ocurrido un error");
                await dialog.ShowAsync();
            }

            HideProgressPanel();
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
            fileNameBlock.Text = _fileOpener.SelectedFileName;
            fileSizeBlock.Text = string.Format("{0} bytes", _fileOpener.FileSize);
            fileDescriptionBlock.Text = string.Format("{0} ({1})", _fileOpener.SelectedFileDisplayType, _fileOpener.SelectedFileExtension);
        }
    }
}
