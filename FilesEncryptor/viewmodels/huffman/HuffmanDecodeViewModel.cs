using FilesEncryptor.dto;
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
    public class HuffmanDecodeViewModel : IProcessViewModel
    {
        private IProcessView _view;
        private FileHelper _fileOpener;
        private FileHeader _fileHeader;
        private bool _appActivated;

        private HuffmanDecoder _huffmanDecoder;

        public void OnNavigatedTo(IProcessView view, bool appActivated)
        {
            _view = view;
            _view.SetTitle("Des-compactar con Huffman");

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

            if (await _fileOpener.PickToOpen(new List<string>() { ".huf" }))
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
                _huffmanDecoder = await HuffmanDecoder.FromFile(_fileOpener);

                await _fileOpener.Finish();
            }

            await _view.SetLoadingPanelVisibility(Visibility.Collapsed);
        }


        public async void Process()
        {
            BaseKryptoProcess decodingProcess = new BaseKryptoProcess();

            await _view.SetProgressPanelVisibility(Visibility.Visible);
            decodingProcess.Start(_view);

            //Creo el decodificador de Huffman            
            string huffDecoded = await _huffmanDecoder.DecodeWithTreeMultithreaded(decodingProcess);

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
                    CloseProgressPanelButtonClicked();
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
