using FilesEncryptor.dto;
using FilesEncryptor.helpers;
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
        private ObservableCollection<HammingEncodeType> _encodeTypes = new ObservableCollection<HammingEncodeType>(HammingEncoder.EncodeTypes);
        private StorageFile originalFile;

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
            var picker = new FileOpenPicker()
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary                
            };
            picker.FileTypeFilter.Add(".txt");
            
            var file = await picker.PickSingleFileAsync();

            if (file != null)
            {
                try
                {
                    _rawFileBytes = null;

                    ShowProgressPanel();
                    await Task.Delay(200);

                    settingsPanel.Visibility = Visibility.Collapsed;
                    pageCommandsDivider.Visibility = Visibility.Collapsed;
                    pageCommands.Visibility = Visibility.Collapsed;

                    uint numBytesLoaded = 0;

                    //Abro el archivo para lectura
                    using (var stream = await file.OpenAsync(FileAccessMode.Read))
                    {
                        ulong size = stream.Size;

                        using (var inputStream = stream.GetInputStreamAt(0))
                        {
                            using (var dataReader = new DataReader(inputStream))
                            {
                                //Cargo el archivo en memoria
                                numBytesLoaded = await dataReader.LoadAsync((uint)size);
                                byte[] buffer = new byte[numBytesLoaded];
                                dataReader.ReadBytes(buffer);

                                _rawFileBytes = new List<byte>(buffer);
                            }
                        }
                    }

                    originalFile = file;

                    //Muestro los datos del archivo cargado
                    fileNameBlock.Text = originalFile.Name;
                    fileSizeBlock.Text = string.Format("{0} bytes", numBytesLoaded);
                    fileDescriptionBlock.Text = originalFile.DisplayName;

                    settingsPanel.Visibility = Visibility.Visible;
                    pageCommandsDivider.Visibility = Visibility.Visible;
                    pageCommands.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    MessageDialog errorDialog = new MessageDialog("No se pudo abrir el archivo. Intente con otro formato.", "Ha ocurrido un error");
                    await errorDialog.ShowAsync();

                    DebugUtils.Fail("Excepcion al cargar archivo para codificacion con hamming", ex.Message);
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
            var savePicker = new FileSavePicker()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = originalFile.DisplayName
            };
            savePicker.FileTypeChoices.Add(_encodeTypes[_selectedEncoding].LongDescription, new List<string>()
            {
                _encodeTypes[_selectedEncoding].Extension }
            );

            StorageFile file = await savePicker.PickSaveFileAsync();

            //Si el usuario no canceló la operación
            if (file != null)
            {
                ShowProgressPanel();
                await Task.Delay(200);

                HammingEncodeType selectedEncodingType = _encodeTypes[_selectedEncoding];

                DebugUtils.WriteLine(string.Format("Output file: \"{0}\"", file.Path));
                DebugUtils.WriteLine(string.Format("Starting Hamming Encoding in {0} format working with {1} bits input words", selectedEncodingType.Extension, selectedEncodingType.WordBitsSize));

                //Codifico el archivo original
                HammingEncodeResult result = await HammingEncoder.Encode(_rawFileBytes, _encodeTypes[_selectedEncoding]);

                //Vuelco el código obtenido en disco
                bool dumpRes = await DumpEncodedResult(result, file, originalFile.FileType, originalFile.DisplayType, originalFile.DisplayName);

                //Show congrats message
                if(dumpRes)
                {
                    MessageDialog dialog = new MessageDialog("El archivo ha sido guardado", "Ha sido todo un Exito");
                    await dialog.ShowAsync();
                }
                else
                {
                    MessageDialog dialog = new MessageDialog("El archivo no pudo ser guardado.", "Ha ocurrido un error");
                    await dialog.ShowAsync();
                }

                HideProgressPanel();
            }
            else
            {
                DebugUtils.WriteLine("File encoded canceled because user cancel output file selection");
            }
        }

        private async Task<bool> DumpEncodedResult(HammingEncodeResult result, StorageFile file, string originalFileType, string originalFileDisplayType, string originalFileName)
        {
            DebugUtils.WriteLine(string.Format("Dumping hamming encoded result to file \"{0}\"", file.Path));

            bool dumpResult = false;

            if (file.IsAvailable)
            {
                // Prevent updates to the remote version of the file until
                // we finish making changes and call CompleteUpdatesAsync.
                CachedFileManager.DeferUpdates(file);

                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    using (var outputStream = stream.GetOutputStreamAt(0))
                    {
                        using (var dataWriter = new DataWriter(outputStream))
                        {                            
                            string fileHeader = string.Format("{0}:{1}{2}:{3}{4}:{5}",
                                originalFileType.Length,
                                originalFileType,
                                originalFileDisplayType.Length,
                                originalFileDisplayType,
                                originalFileName.Length,
                                originalFileName);

                            string codeLength = string.Format("{0},{1}:", result.Encoded.CodeLength, result.RedundanceBitsCount);

                            DebugUtils.WriteLine(string.Format("File Header: \"{0}\"", fileHeader));
                            DebugUtils.WriteLine(string.Format("Code Length: \"{0}\"", result.Encoded.CodeLength));
                            DebugUtils.WriteLine(string.Format("Redundance bits count: \"{0}\"", result.RedundanceBitsCount));

                            //Escribo el tipo de archivo original y su descripción
                            dataWriter.WriteString(fileHeader);
                            
                            //Escribo la longitud del archivo codificado
                            dataWriter.WriteString(codeLength);

                            //Escribo el archivo codificado                       
                            dataWriter.WriteBytes(result.Encoded.Code.ToArray());

                            await dataWriter.StoreAsync();
                            await outputStream.FlushAsync();
                        }
                    }
                }

                // Let Windows know that we're finished changing the file so
                // the other app can update the remote version of the file.
                // Completing updates may require Windows to ask for user input.
                Windows.Storage.Provider.FileUpdateStatus status =
                    await CachedFileManager.CompleteUpdatesAsync(file);

                dumpResult = status == Windows.Storage.Provider.FileUpdateStatus.Complete;

                if (dumpResult)
                {
                    DebugUtils.WriteLine(string.Format("Dump Completed properly"));
                }
                else
                {
                    DebugUtils.Fail("Dump incompleted", "CachedFileManager could not complete updates async");
                }
            }
            else
            {
                DebugUtils.Fail("Dump incompleted", "File is not available");
            }

            return dumpResult;
        }

        private void ShowProgressPanel() => progressPanel.Visibility = Visibility.Visible;

        private void HideProgressPanel() => progressPanel.Visibility = Visibility.Collapsed;
    }
}
