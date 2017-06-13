using FilesEncryptor.dto;
using FilesEncryptor.dto.hamming;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.hamming;
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
    public sealed partial class HammingEncodePage : Page
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
                ShowProgressPanel();
                await Task.Delay(200);

                settingsPanel.Visibility = Visibility.Collapsed;
                pageCommandsDivider.Visibility = Visibility.Collapsed;
                pageCommands.Visibility = Visibility.Collapsed;

                bool openResult = await _filesHelper.OpenFile(FileAccessMode.Read);
                
                if (openResult)
                {
                    DebugUtils.WriteLine(string.Format("Selected file: {0} with size of {1} bytes", _filesHelper.SelectedFilePath, _filesHelper.FileSize));

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

                    DebugUtils.WriteLine("File bytes extracted properly");
                    DebugUtils.WriteLine("Closing file");
                    await _filesHelper.Finish();
                }

                HideProgressPanel();
            }
        }

        private void HammingEncodeTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedEncoding = hammingEncodeTypeSelector.SelectedIndex;
        }

        private async void EncodeBt_Click(object sender, RoutedEventArgs e)
        {
            HammingEncodeType selectedEncodingType = _encodeTypes[_selectedEncoding];

            bool pickResult = await _filesHelper.PickToSave(_filesHelper.SelectedFileDisplayName, selectedEncodingType.LongDescription, selectedEncodingType.Extension);

            //Si el usuario no canceló la operación
            if (pickResult)
            {
                bool openFileResult = await _filesHelper.OpenFile(FileAccessMode.ReadWrite);

                if (openFileResult)
                {
                    ShowProgressPanel();
                    await Task.Delay(200);

                    DebugUtils.WriteLine(string.Format("Output file: \"{0}\"", _filesHelper.SelectedFilePath));
                    DebugUtils.WriteLine(string.Format("Starting Hamming Encoding in {0} format working with {1} bits input words", selectedEncodingType.Extension, selectedEncodingType.WordBitsSize));

                    DateTime startDate = DateTime.Now;

                    //Codifico el archivo original
                    HammingEncoder encoder = HammingEncoder.From(new BitCode(_rawFileBytes, _rawFileBytes.Count * 8));                    
                    HammingEncodeResult encodeResult = await encoder.Encode(selectedEncodingType);

                    //Imprimo la cantidad de tiempo que implico la codificacion
                    TimeSpan totalTime = DateTime.Now.Subtract(startDate);
                    DebugUtils.WriteLine(string.Format("Encoding process finished in a time of {0}", totalTime.ToString()));

                    //Si pudo encodearse el archivo
                    if (encodeResult != null)
                    {
                        //Escribo el Header
                        if (_filesHelper.WriteFileHeader(_fileHeader))
                        {
                            DebugUtils.WriteLine(string.Format("Dumping hamming encoded bytes to \"{0}\"", _filesHelper.SelectedFilePath));

                            bool writeResult = HammingEncoder.WriteEncodedToFile(encodeResult, _filesHelper);

                            //Show congrats message
                            if (writeResult)
                            {
                                DebugUtils.WriteLine("Dumping completed properly");
                                DebugUtils.WriteLine("Closing file");
                                await _filesHelper.Finish();
                                MessageDialog dialog = new MessageDialog("El archivo ha sido guardado", "Ha sido todo un Exito");
                                await dialog.ShowAsync();
                            }
                            else
                            {
                                DebugUtils.WriteLine("Dumping uncompleted");
                                DebugUtils.WriteLine("Closing file");
                                await _filesHelper.Finish();
                                MessageDialog dialog = new MessageDialog("El archivo no pudo ser guardado.", "Ha ocurrido un error");
                                await dialog.ShowAsync();
                            }
                        }
                    }

                    HideProgressPanel();
                }
            }
            else
            {
                DebugUtils.WriteLine("File encoded canceled because user cancel output file selection");
            }
        }

        private void ShowProgressPanel() => progressPanel.Visibility = Visibility.Visible;

        private void HideProgressPanel() => progressPanel.Visibility = Visibility.Collapsed;
    }
}
