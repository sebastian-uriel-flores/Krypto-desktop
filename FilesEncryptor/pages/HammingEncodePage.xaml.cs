using FilesEncryptor.dto;
using FilesEncryptor.dto.hamming;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.hamming;
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

// La plantilla de elemento Página en blanco está documentada en https://go.microsoft.com/fwlink/?LinkId=234238

namespace FilesEncryptor.pages
{
    /// <summary>
    /// Una página vacía que se puede usar de forma independiente o a la que se puede navegar dentro de un objeto Frame.
    /// </summary>
    public sealed partial class HammingEncodePage : Page, IKryptoProcessUI
    {


        private int _selectedEncoding;
        private List<byte> _rawFileBytes;
        private ObservableCollection<HammingEncodeType> _encodeTypes = new ObservableCollection<HammingEncodeType>(BaseHammingCodifier.EncodeTypes);

        private FileHelper _filesHelper;
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
            _filesHelper = new FileHelper();
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


        }

        private async void SelectFileBt_Click(object sender, RoutedEventArgs e)
        {
            bool pickResult = await _filesHelper.PickToOpen(new List<string>()
            {
                ".txt",
                ".huf",
                ".pdf",
                ".docx",
                ".doc",
                ".jpg"
            });

            if (pickResult)
            {
                _rawFileBytes = null;
                ShowLoadingPanel();
                await Task.Delay(200);

                settingsPanel.Visibility = Visibility.Collapsed;
                pageCommandsDivider.Visibility = Visibility.Collapsed;
                pageCommands.Visibility = Visibility.Collapsed;

                bool openResult = await _filesHelper.OpenFile(FileAccessMode.Read);

                if (openResult)
                {
                    DebugUtils.ConsoleWL(string.Format("Selected file: {0} with size of {1} bytes", _filesHelper.SelectedFilePath, _filesHelper.FileSize));

                    //Muestro los datos del archivo cargado
                    fileNameBlock.Text = _filesHelper.SelectedFileName;
                    fileSizeBlock.Text = string.Format("{0} bytes", _filesHelper.FileSize);
                    fileDescriptionBlock.Text = string.Format("{0} ({1})", _filesHelper.SelectedFileDisplayType, _filesHelper.SelectedFileExtension);

                    settingsPanel.Visibility = Visibility.Visible;
                    pageCommandsDivider.Visibility = Visibility.Visible;
                    pageCommands.Visibility = Visibility.Visible;

                    _rawFileBytes = _filesHelper.ReadBytes(_filesHelper.FileSize).ToList();
                    _fileHeader = new FileHeader()
                    {
                        FileName = _filesHelper.SelectedFileDisplayName,
                        FileDisplayType = _filesHelper.SelectedFileDisplayType,
                        FileExtension = _filesHelper.SelectedFileExtension
                    };

                    DebugUtils.ConsoleWL("File bytes extracted properly");
                    DebugUtils.ConsoleWL("Closing file");
                    await _filesHelper.Finish();
                }

                HideLoadingPanel();
            }
        }

        private void HammingEncodeTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedEncoding = hammingEncodeTypeSelector.SelectedIndex;
        }

        private async void EncodeBt2_Click(object sender, RoutedEventArgs e)
        {
            HammingEncodeType selectedEncodingType = _encodeTypes[_selectedEncoding];

            bool pickResult = await _filesHelper.PickToSave(_filesHelper.SelectedFileDisplayName, selectedEncodingType.LongDescription, selectedEncodingType.Extension);

            //Si el usuario no canceló la operación
            if (pickResult)
            {
                bool openFileResult = await _filesHelper.OpenFile(FileAccessMode.ReadWrite);

                if (openFileResult)
                {
                    await ShowLoadingPanel();

                    DebugUtils.ConsoleWL(string.Format("Output file: \"{0}\"", _filesHelper.SelectedFilePath));
                    DebugUtils.ConsoleWL(string.Format("Starting Hamming Encoding in {0} format working with {1} bits input words", selectedEncodingType.Extension, selectedEncodingType.WordBitsSize));

                    DateTime startDate = DateTime.Now;

                    //Codifico el archivo original
                    HammingEncoder encoder = HammingEncoder.From(new BitCode(_rawFileBytes, _rawFileBytes.Count * 8));
                    HammingEncodeResult encodeResult = await Task.Run(() => encoder.Encode(selectedEncodingType));

                    //Imprimo la cantidad de tiempo que implico la codificacion
                    TimeSpan totalTime = DateTime.Now.Subtract(startDate);
                    DebugUtils.ConsoleWL(string.Format("Encoding process finished in a time of {0}", totalTime.ToString()));

                    //Si pudo encodearse el archivo
                    if (encodeResult != null)
                    {
                        //Escribo el Header
                        if (_filesHelper.WriteFileHeader(_fileHeader))
                        {
                            DebugUtils.ConsoleWL(string.Format("Dumping hamming encoded bytes to \"{0}\"", _filesHelper.SelectedFilePath));

                            bool writeResult = HammingEncoder.WriteEncodedToFile(encodeResult, _filesHelper);

                            //Show congrats message
                            if (writeResult)
                            {
                                DebugUtils.ConsoleWL("Dumping completed properly");
                                DebugUtils.ConsoleWL("Closing file");
                                await _filesHelper.Finish();
                                MessageDialog dialog = new MessageDialog("El archivo ha sido guardado", "Ha sido todo un Exito");
                                await dialog.ShowAsync();
                            }
                            else
                            {
                                DebugUtils.ConsoleWL("Dumping uncompleted");
                                DebugUtils.ConsoleWL("Closing file");
                                await _filesHelper.Finish();
                                MessageDialog dialog = new MessageDialog("El archivo no pudo ser guardado.", "Ha ocurrido un error");
                                await dialog.ShowAsync();
                            }
                        }
                    }

                    HideLoadingPanel();
                }
            }
            else
            {
                DebugUtils.ConsoleWL("File encoded canceled because user cancel output file selection");
            }
        }

        private async void EncodeBt_Click(object sender, RoutedEventArgs e)
        {
            HammingEncodeType selectedEncodingType = _encodeTypes[_selectedEncoding];

            //Codifico el archivo original
            HammingEncoder encoder = HammingEncoder.From(new BitCode(_rawFileBytes, _rawFileBytes.Count * 8));

            KryptoProcess<HammingEncodeResult> encodingProcess = new KryptoProcess<HammingEncodeResult>(
                new Task<HammingEncodeResult>(() => encoder.Encode(selectedEncodingType)));

            await ShowProgressPanel();

            encodingProcess.Start(this,
                async (result) =>
                {
                    //Si el proceso fue un exito
                    FileHelper fileSaver = new FileHelper();
                    bool pickResult = false;

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        pickResult = await fileSaver.PickToSave(_filesHelper.SelectedFileDisplayName, selectedEncodingType.LongDescription, selectedEncodingType.Extension);

                        //Si el usuario no canceló la operación
                        if (pickResult)
                        {
                            bool openFileResult = await fileSaver.OpenFile(FileAccessMode.ReadWrite);

                            if (openFileResult)
                            {
                                encodingProcess.UpdateStatus($"Dumping encoded file to {fileSaver.SelectedFilePath}", true);

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
    }
}
