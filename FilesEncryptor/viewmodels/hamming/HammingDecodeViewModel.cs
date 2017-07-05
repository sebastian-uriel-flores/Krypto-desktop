using FilesEncryptor.dto;
using FilesEncryptor.dto.hamming;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.hamming;
using FilesEncryptor.helpers.huffman;
using FilesEncryptor.helpers.processes;
using FilesEncryptor.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using static FilesEncryptor.utils.DebugUtils;

namespace Krypto.viewmodels.hamming
{
    public class HammingDecodeViewModel : IProcessViewModel
    {
        private IProcessView _view;
        private FileHelper _fileOpener;
        private FileHeader _fileHeader;
        private bool _appActivated;

        private HammingDecoder _decoder;

        public void OnNavigatedTo(IProcessView view, bool appActivated)
        {
            _view = view;
            _view.SetTitle("Recuperar archivo");

            _appActivated = appActivated;
            if (_appActivated)
            {
                _view.SetBackButtonVisibility(Visibility.Collapsed);
                _view.SetFilePickerButtonVisibility(Visibility.Collapsed);
            }
            else
            {
                _view.SetBackButtonVisibility(Visibility.Visible);
                _view.SetFilePickerButtonVisibility(Visibility.Visible);
            }
        }

        public async void PickFile()
        {
            _fileOpener = _fileOpener ?? new FileHelper();
            List<string> extensions = new List<string>();
            foreach (HammingEncodeType type in BaseHammingCodifier.EncodeTypes)
            {
                extensions.Add(type.Extension);
            }

            if (await _fileOpener.PickToOpen(extensions))
            {
                FileTaked();
            }
        }

        public async void TakeFile(StorageFile file)
        {
            if (_fileOpener != null)
            {
                await _fileOpener.Finish();
            }
            _fileOpener = new FileHelper(file);
        }

        private async void FileTaked()
        {
            await _view.SetLoadingPanelVisibility(Visibility.Visible);
            _view.SetConfirmButtonStatus(false);

            if (await _fileOpener.OpenFile(FileAccessMode.Read))
            {
                //Muestro los datos del archivo cargado
                _view.SetFilePath(_fileOpener.SelectedFileName);
                _view.SetFileSize($"{_fileOpener.FileSize} bytes");
                _view.SetFileDescription($"{_fileOpener.SelectedFileDisplayType} ({_fileOpener.SelectedFileExtension})");
                _view.SetConfirmButtonStatus(true);

                _fileHeader = _fileOpener.ReadFileHeader();

                HammingEncodeType encodeType = BaseHammingCodifier.EncodeTypes.First(encType => encType.Extension == _fileOpener.SelectedFileExtension);
                _decoder = new HammingDecoder(_fileOpener, encodeType);

                if (_fileHeader.FileExtension == BaseHuffmanCodifier.HUFFMAN_FILE_EXTENSION)
                {
                    DebugUtils.ConsoleWL("The original file is compressed with Huffman", "[WARN]");
                }

                await _fileOpener.Finish();
            }

            await _view.SetLoadingPanelVisibility(Visibility.Collapsed);
        }

        public async void Process()
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
            await _view.SetProgressPanelVisibility(Visibility.Visible);
            decodingProcess.Start(_view);
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

                                decodingProcess.StopWatch();

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

                                        if (!_appActivated)
                                        {
                                            _view.SetProgressPanelCloseButtonVisibility(Visibility.Visible);
                                        }
                                    }
                                    //Si el usuario no selecciono ningun archivo
                                    else
                                    {
                                        ConsoleWL("User cancel file selection");
                                        decodingProcess.Stop(true);
                                        await _view.SetProgressPanelVisibility(Visibility.Collapsed);
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

                                    if (!_appActivated)
                                    {
                                        _view.SetProgressPanelCloseButtonVisibility(Visibility.Visible);
                                    }
                                }
                            }
                        }
                    }
                }

                #endregion

                //Si el archivo debe ser almacenado tal y como fue decodificado
                #region DECODE_ONLY_HAMMING

                else
                {
                    decodingProcess.StopWatch();
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

                                await fileSaver.Finish();

                                decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                {
                                    Message = "File closed",
                                    ProgressAdvance = 100,
                                    Tag = "[INFO]"
                                });
                                decodingProcess.Stop(true);
                            }
                        }

                        //Si el archivo no pudo ser abierto para edicion
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

                        if (!_appActivated)
                        {
                            _view.SetProgressPanelCloseButtonVisibility(Visibility.Visible);
                        }
                    }
                    //Si el usuario no selecciono ningun archivo
                    else
                    {
                        ConsoleWL("User cancel file selection");
                        decodingProcess.Stop(true);
                        CloseProgressPanelButtonClicked();
                    }
                }

                #endregion
            }
            //Si el archivo no pudo ser decodificado con Hamming
            else
            {
                decodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                {
                    Message = "File could not be decoded with Hamming",
                    ProgressAdvance = 100,
                    Tag = "[FAIL]"
                });
                decodingProcess.Stop(true);
                if (!_appActivated)
                {
                    _view.SetProgressPanelCloseButtonVisibility(Visibility.Visible);
                }
            }
        }

        public void CloseProgressPanelButtonClicked()
        {
            if (_appActivated)
            {
                Application.Current.Exit();
            }
            else
            {
                _view.SetProgressPanelVisibility(Visibility.Collapsed);
            }
        }
    }
}