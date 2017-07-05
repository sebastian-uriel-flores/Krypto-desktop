using FilesEncryptor.dto;
using FilesEncryptor.dto.hamming;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.hamming;
using FilesEncryptor.helpers.processes;
using FilesEncryptor.utils;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Krypto.viewmodels.hamming
{
    public class HammingBrokeViewModel : IProcessViewModel
    {
        private IProcessView _view;
        private FileHelper _fileOpener;
        private FileHeader _fileHeader;
        private bool _appActivated;

        private HammingBroker _broker;

        public void OnNavigatedTo(IProcessView view, bool appActivated)
        {
            _view = view;
            _view.SetTitle("Introducir errores en archivo Hamming");

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
                HammingEncodeType fileEncodeType = BaseHammingCodifier.EncodeTypes.First(encType => encType.Extension == _fileOpener.SelectedFileExtension);
                _broker = new HammingBroker(_fileOpener, fileEncodeType);

                await _fileOpener.Finish();
            }

            await _view.SetLoadingPanelVisibility(Visibility.Collapsed);
        }

        public async void Process()
        {
            BaseKryptoProcess process = new BaseKryptoProcess();
            await _view.SetProgressPanelVisibility(Visibility.Visible);
            process.Start(_view);

            //Introduzco errores en el archivo original
            HammingEncodeResult result = await _broker.Broke(process);
            process.StopWatch();

            //Si el archivo pudo ser decodificado con Hamming
            if (result != null)
            {
                FileHelper fileSaver = new FileHelper();

                //Si el usuario selecciona un archivo
                if (await fileSaver.PickToSave(_fileOpener.SelectedFileName, _fileOpener.SelectedFileDisplayType, _fileOpener.SelectedFileExtension))
                {
                    process.AddEvent(new BaseKryptoProcess.KryptoEvent()
                    {
                        Message = $"Output file selected {fileSaver.SelectedFilePath}",
                        ProgressAdvance = 0,
                        Tag = "[INFO]"
                    });

                    //Si el archivo pudo ser abierto correctamente
                    if (await fileSaver.OpenFile(FileAccessMode.ReadWrite))
                    {
                        process.UpdateStatus($"Dumping broken file to {fileSaver.SelectedFilePath}");

                        if (fileSaver.WriteFileHeader(_fileHeader) && HammingEncoder.WriteEncodedToFile(result, fileSaver))
                        {
                            process.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = "Broken file dumped properly",
                                ProgressAdvance = 100,
                                Tag = "[PROGRESS]"
                            });
                            process.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = "Closing file",
                                ProgressAdvance = 100,
                                Tag = "[INFO]"
                            });

                            await fileSaver.Finish();

                            process.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = "File closed",
                                ProgressAdvance = 100,
                                Tag = "[INFO]"
                            });
                            process.Stop();
                        }
                        else
                        {
                            process.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = "Failed dumping broken file",
                                ProgressAdvance = 100,
                                Tag = "[PROGRESS]"
                            });
                            process.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = "Closing file",
                                ProgressAdvance = 100,
                                Tag = "[INFO]"
                            });

                            await fileSaver.Finish();

                            process.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = "File closed",
                                ProgressAdvance = 100,
                                Tag = "[INFO]"
                            });
                            process.Stop(true);
                        }
                    }
                    //Si el archivo no pudo ser abierto para edicion
                    else
                    {
                        process.AddEvent(new BaseKryptoProcess.KryptoEvent()
                        {
                            Message = "File could not be opened to ReadWrite",
                            ProgressAdvance = 100,
                            Tag = "[FAIL]"
                        });
                        process.Stop(true);
                    }

                    if (!_appActivated)
                    {
                        _view.SetProgressPanelCloseButtonVisibility(Visibility.Visible);
                    }
                }
                //Si el usuario no selecciono ningun archivo
                else
                {
                    DebugUtils.ConsoleWL("User cancel file selection");                    
                    process.Stop(true);
                    CloseProgressPanelButtonClicked();
                }
            }
            //Si el archivo no pudo ser decodificado
            else
            {
                process.AddEvent(new BaseKryptoProcess.KryptoEvent()
                {
                    Message = "File could not be broken",
                    ProgressAdvance = 100,
                    Tag = "[FAIL]"
                });
                process.Stop(true);
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
