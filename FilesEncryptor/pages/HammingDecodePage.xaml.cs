using FilesEncryptor.dto;
using FilesEncryptor.helpers;
using FilesEncryptor.utils;
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
        private int _selectedEncoding;
        private List<byte> _rawFileBytes;
        private StorageFile originalFile;
        private HammingDecodeResult decodedFileResult;
        private HammingEncodeResult encodedFileResult;

        public HammingDecodePage()
        {
            this.InitializeComponent();
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

            foreach (HammingEncodeType type in HammingEncoder.EncodeTypes)
            {
                picker.FileTypeFilter.Add(type.Extension);
            }

            var file = await picker.PickSingleFileAsync();

            if (file != null)
            {                
                HammingEncodeType encodeType = HammingEncoder.EncodeTypes.FirstOrDefault(encType => encType.Extension == file.FileType);

                if (encodeType == default(HammingEncodeType))
                {
                    //Extension invalida
                }
                else
                {
                    decodedFileResult = new HammingDecodeResult();
                    encodedFileResult = null;

                    try
                    {
                        _rawFileBytes = null;

                        ShowProgressPanel();
                        await Task.Delay(200);

                        decodeBt.Visibility = Visibility.Collapsed;

                        //Abro el archivo para lectura
                        using (var stream = await file.OpenAsync(FileAccessMode.Read))
                        {
                            using (var inputStream = stream.GetInputStreamAt(0))
                            {
                                using (var dataReader = new DataReader(inputStream))
                                {
                                    var size = stream.Size;
                                    //Cargo en el buffer todos los bytes del archivo
                                    uint numBytesLoaded = await dataReader.LoadAsync((uint)size);

                                    string temp = "";

                                    //Obtengo el largo del tipo de archivo
                                    string fileExtLength = "";

                                    temp = dataReader.ReadString(1);

                                    while (temp != ":")
                                    {
                                        fileExtLength += temp;
                                        temp = dataReader.ReadString(1);
                                    }

                                    //Obtengo el tipo de archivo
                                    decodedFileResult.FileExtension = dataReader.ReadString(uint.Parse(fileExtLength));

                                    //Obtengo el largo de la descripcion del tipo de archivo
                                    string fileDisplayTypeLength = "";

                                    temp = dataReader.ReadString(1);

                                    while (temp != ":")
                                    {
                                        fileDisplayTypeLength += temp;
                                        temp = dataReader.ReadString(1);
                                    }

                                    //Obtengo la descripcion del tipo de archivo
                                    decodedFileResult.FileDescription = dataReader.ReadString(uint.Parse(fileDisplayTypeLength));

                                    //Obtengo el largo del código
                                    string rawCodeLength = "";

                                    temp = dataReader.ReadString(1);

                                    while (temp != ":")
                                    {
                                        rawCodeLength += temp;
                                        temp = dataReader.ReadString(1);
                                    }

                                    //Obtengo los bytes del código
                                    byte[] rawCodeBytes = new byte[CommonUtils.BitsLengthToBytesLength(uint.Parse(rawCodeLength))];
                                    dataReader.ReadBytes(rawCodeBytes);
                                    encodedFileResult = new HammingEncodeResult(new BitCode(rawCodeBytes.ToList(), int.Parse(rawCodeLength)), encodeType);
                                }
                            }
                        }

                        originalFile = file;
                    }
                    catch (Exception ex)
                    {
                        MessageDialog errorDialog = new MessageDialog("No se pudo abrir el archivo. Intente con otro formato.", "Ha ocurrido un error");
                        await errorDialog.ShowAsync();

                        Debug.Fail("Excepcion al cargar archivo para codificacion con hamming", ex.Message);
                    }
                }

                if (decodedFileResult != null && encodedFileResult != null)
                {
                    decodeBt.Visibility = Visibility.Visible;
                }

                HideProgressPanel();
            }
        }

        private async void DecodeBt_Click(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = originalFile.DisplayName
            };
            savePicker.FileTypeChoices.Add(decodedFileResult.FileDescription , new List<string>() { decodedFileResult.FileExtension });
            StorageFile file = await savePicker.PickSaveFileAsync();

            //Si el usuario no canceló la operación
            if (file != null)
            {
                ShowProgressPanel();
                await Task.Delay(200);

                //Codifico el archivo original
                BitCode result = await HammingEncoder.Decode(encodedFileResult);

                //Vuelco el código obtenido en disco
                bool dumpRes = await DumpDecodedResult(result, file);

                //Show congrats message
                if (dumpRes)
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
        }

        private async Task<bool> DumpDecodedResult(BitCode result, StorageFile file)
        {
            Debug.WriteLine(string.Format("Dumping hamming decoded file to \"{0}\"", file.Name), "[INFO]");

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
                            dataWriter.WriteBytes(result.Code.ToArray());

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
            }

            Debug.WriteLine(string.Format("Dump Completed: {0}", dumpResult), "[INFO]");

            return dumpResult;
        }

        private void ShowProgressPanel() => progressPanel.Visibility = Visibility.Visible;

        private void HideProgressPanel() => progressPanel.Visibility = Visibility.Collapsed;
    }
}
