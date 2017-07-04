using FilesEncryptor.dto;
using FilesEncryptor.dto.huffman;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.huffman;
using FilesEncryptor.helpers.processes;
using System;
using System.Collections.Generic;
using Windows.Storage;
using Windows.UI.Xaml;
using static FilesEncryptor.utils.DebugUtils;

namespace Krypto.viewmodels.huffman
{
    public class HuffmanEncodeViewModel : IProcessViewModel
    {
        private IProcessView _view;
        private FileHelper _fileOpener;
        private FileHeader _fileHeader;
        private bool _appActivated;

        private string _originalText;

        public void OnNavigatedTo(IProcessView view, bool appActivated)
        {
            _view = view;
            _view.SetTitle("Compactar con Huffman");
            _view.SetTextAreaVisibility(Visibility.Collapsed);

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

            if (await _fileOpener.PickToOpen(new List<string>() { ".txt" }))
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

                //TODO Si no se puede obtener el tipo de codificacion del archivo, 
                //se solicita al usuario que elija una de las posibles codificaciones
                if (_fileOpener.FileBOM == null)
                {
                    _fileOpener.SetFileEncoding(System.Text.Encoding.UTF8);
                }

                //Leo todos los bytes del texto
                byte[] fileBytes = _fileOpener.ReadBytes(_fileOpener.FileContentSize);

                //Obtengo el texto que sera mostrado en pantalla
                _originalText = _fileOpener.FileEncoding.GetString(fileBytes);

                //Cierro el archivo
                await _fileOpener.Finish();

                _view.SetTextAreaVisibility(Visibility.Visible);
                await _view.SetTextAreaContent(_originalText);
                
                //Muestro la informacion del archivo
                await _view.SetLoadingPanelVisibility(Visibility.Collapsed);


                //Creo el header que tendra el archivo al guardarlo
                _fileHeader = new FileHeader()
                {
                    FileName = _fileOpener.SelectedFileDisplayName,
                    FileDisplayType = _fileOpener.SelectedFileDisplayType,
                    FileExtension = _fileOpener.SelectedFileExtension
                };

                await _fileOpener.Finish();
            }

            await _view.SetLoadingPanelVisibility(Visibility.Collapsed);
        }

        public async void Process()
        {
            bool compressResult = false;

            BaseKryptoProcess encodingProcess = new BaseKryptoProcess();
            await _view.SetProgressPanelVisibility(Visibility.Visible);
            encodingProcess.Start(_view);

            HuffmanEncoder encoder = HuffmanEncoder.From(_originalText);

            encodingProcess.UpdateStatus("Creating huffman probabilities tree");
            await encoder.Scan();

            HuffmanEncodeResult encodeResult = await encoder.Encode(encodingProcess);
            encodingProcess.StopWatch();

            //Si el archivo pudo ser codificado con Huffman
            if (encodeResult != null)
            {
                FileHelper fileSaver = new FileHelper();

                //Si el usuario selecciono correctamente un archivo
                if (await fileSaver.PickToSave(_fileHeader.FileName, BaseHuffmanCodifier.HUFFMAN_FILE_DISPLAY_TYPE, BaseHuffmanCodifier.HUFFMAN_FILE_EXTENSION))
                {
                    encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                    {
                        Message = $"Output file selected: {fileSaver.SelectedFilePath}",
                        ProgressAdvance = 0,
                        Tag = "[INFO]"
                    });

                    //Si el archivo pudo ser abierto
                    if (await fileSaver.OpenFile(FileAccessMode.ReadWrite))
                    {
                        encodingProcess.UpdateStatus($"Dumping decoded file to {fileSaver.SelectedFilePath}");

                        compressResult = fileSaver.WriteFileHeader(_fileHeader);

                        //Si el header se escribio correctamente
                        if (compressResult)
                        {
                            //Escribo la tabla de probabilidades
                            ConsoleWL(string.Format("Start dumping to: {0}", fileSaver.SelectedFilePath));
                            fileSaver.SetFileEncoding(_fileOpener.FileEncoding);
                            compressResult = HuffmanEncoder.WriteToFile(fileSaver, encodeResult, _fileOpener.FileEncoding, _fileOpener.FileBOM);

                            if (compressResult)
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

                    if (!_appActivated)
                    {
                        _view.SetProgressPanelCloseButtonVisibility(Visibility.Visible);
                    }
                }
                //Si el usuario cancelo la seleccion de archivo
                else
                {
                    ConsoleWL("User cancel file selection");
                    encodingProcess.Stop(true);
                    CloseProgressPanelButtonClicked();
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

                if (!_appActivated)
                {
                    _view.SetProgressPanelCloseButtonVisibility(Visibility.Visible);
                }
            }
        }

        public void CloseProgressPanelButtonClicked()
        {
            throw new NotImplementedException();
        }
    }
}
