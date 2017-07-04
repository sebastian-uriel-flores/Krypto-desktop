using FilesEncryptor.dto;
using FilesEncryptor.dto.hamming;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.hamming;
using FilesEncryptor.helpers.processes;
using FilesEncryptor.utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Krypto.viewmodels.hamming
{
    public class HammingEncodeViewModel : IProcessViewModel
    {
        private IProcessView _view;
        private FileHelper _fileOpener;
        private FileHeader _fileHeader;
        private bool _appActivated;

        private List<byte> _rawFileBytes;

        public void OnNavigatedTo(IProcessView view, bool appActivated)
        {
            _view = view;
            _view.SetTitle("Codificar con Hamming");
            _view.SetSelectorVisibility(Visibility.Collapsed);

            _appActivated = appActivated;
            if(_appActivated)
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
            _fileOpener = _fileOpener?? new FileHelper();

            if (await _fileOpener.PickToOpen(new List<string>() { "*" }))
            {
                FileTaked();
            }
        }

        public void TakeFile(StorageFile file)
        {
            _fileOpener = new FileHelper(file);
            FileTaked();
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

                _rawFileBytes = _fileOpener.ReadBytes(_fileOpener.FileSize).ToList();
                _fileHeader = new FileHeader()
                {
                    FileName = _fileOpener.SelectedFileDisplayName,
                    FileDisplayType = _fileOpener.SelectedFileDisplayType,
                    FileExtension = _fileOpener.SelectedFileExtension
                };

                _view.SetSelectorVisibility(Visibility.Visible);
                _view.SetSelectorSelectedIndex(0);

                await _fileOpener.Finish();
            }

            await _view.SetLoadingPanelVisibility(Visibility.Collapsed);
        }

        public async void Process()
        {
            BaseKryptoProcess encodingProcess = new BaseKryptoProcess();
            await _view.SetProgressPanelVisibility(Visibility.Visible);
            encodingProcess.Start(_view);

            HammingEncodeType selectedEncodingType = BaseHammingCodifier.EncodeTypes[_view.GetSelectorSelectedIndex()];

            //Codifico el archivo original
            HammingEncoder encoder = new HammingEncoder(new BitCode(_rawFileBytes, _rawFileBytes.Count * 8));
            HammingEncodeResult encodeResult = await encoder.Encode(selectedEncodingType, encodingProcess);
            encodingProcess.StopWatch();

            //Si el archivo pudo ser codificado con Hamming
            if (encodeResult != null)
            {
                //Si el proceso fue un exito
                FileHelper fileSaver = new FileHelper();

                //Si el usuario no canceló la operación
                if (await fileSaver.PickToSave(_fileOpener.SelectedFileDisplayName, selectedEncodingType.LongDescription, selectedEncodingType.Extension))
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

                        if (fileSaver.WriteFileHeader(_fileHeader) && HammingEncoder.WriteEncodedToFile(encodeResult, fileSaver))
                        {
                            //Show congrats message
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

                            await fileSaver.Finish();

                            encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = "File closed",
                                ProgressAdvance = 100,
                                Tag = "[INFO]"
                            });
                            encodingProcess.Stop();
                        }
                        else
                        {
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

                            encodingProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = "File closed",
                                ProgressAdvance = 100,
                                Tag = "[INFO]"
                            });

                            encodingProcess.Stop(true);
                        }
                    }
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
                else
                {
                    DebugUtils.ConsoleWL("File encoded canceled because user cancel output file selection");
                    CloseProgressPanelButtonClicked();
                }
            }
            //Si el archivo no pudo ser codificado con Hamming
            else
            {
                encodingProcess.Stop(true);
                if (!_appActivated)
                {
                    _view.SetProgressPanelCloseButtonVisibility(Visibility.Visible);
                }
            }
        }

        public void CloseProgressPanelButtonClicked()
        {
            if(_appActivated)
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
