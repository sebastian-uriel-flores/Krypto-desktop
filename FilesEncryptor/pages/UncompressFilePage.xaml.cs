using FilesEncryptor.dto;
using FilesEncryptor.helpers;
using FilesEncryptor.utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
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
    public sealed partial class UncompressFilePage : Page
    {
        private StorageFile _compTextFile;
        private string _compTextStr;
        private string _compTextType;
        private string _compTextDisplayType;

        public UncompressFilePage()
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
            picker.FileTypeFilter.Add(".huf");

            var file = await picker.PickSingleFileAsync();

            if (file != null)
            {
                try
                {
                    _compTextFile = file;
                    _compTextStr = null;

                    ShowProgressPanel();

                    //Oculto los paneles que muestran informacion del archivo anterior
                    compTextExtraData.Visibility = Visibility.Collapsed;
                    uncompressBt.Visibility = Visibility.Collapsed;

                    //Abro el archivo para lectura y obtengo su tamaño en bytes
                    var stream = await _compTextFile.OpenAsync(FileAccessMode.Read);
                    ulong size = stream.Size;

                    string fileType = "";
                    string fileDisplayType = "";
                    Dictionary<char, EncodedString> probabilitiesTable = new Dictionary<char, EncodedString>();
                    EncodedString encodedText = new EncodedString(new List<byte>(), 0);

                    using (var inputStream = stream.GetInputStreamAt(0))
                    {
                        using (var dataReader = new DataReader(inputStream))
                        {
                            //Cargo en el buffer todos los bytes del archivo
                            uint numBytesLoaded = await dataReader.LoadAsync((uint)size);

                            string temp = "";

                            //Obtengo el largo del tipo de archivo
                            string fileTypeLength = "";

                            temp = dataReader.ReadString(1);

                            while (temp != ":")
                            {
                                fileTypeLength += temp;
                                temp = dataReader.ReadString(1);
                            }

                            //Obtengo el tipo de archivo
                            fileType = dataReader.ReadString(uint.Parse(fileTypeLength));

                            //Obtengo el largo de la descripcion del tipo de archivo
                            string fileDisplayTypeLength = "";

                            temp = dataReader.ReadString(1);

                            while (temp != ":")
                            {
                                fileDisplayTypeLength += temp;
                                temp = dataReader.ReadString(1);
                            }

                            //Obtengo la descripcion del tipo de archivo
                            fileDisplayType = dataReader.ReadString(uint.Parse(fileDisplayTypeLength));
                            
                            //Leo los 2 primeros caracteres del texto correspondiente a la tabla de probabilidades
                            string endOfTableReader = dataReader.ReadString(2);

                            //Cuando el primer caracter sea un . entonces habre leido toda la tabla de probabilidades
                            while (endOfTableReader != "..")
                            {
                                char currentChar = endOfTableReader.First();

                                //Obtengo la longitud en bits del siguiente codigo de la tabla
                                string currentCodeLength = endOfTableReader.Last().ToString();

                                temp = dataReader.ReadString(1);

                                while (temp != ":")
                                {
                                    currentCodeLength += temp;
                                    temp = dataReader.ReadString(1);
                                }

                                uint currentCodeBitsLength = uint.Parse(currentCodeLength);

                                //Creo un buffer y guardo en él la tabla de probabilidades
                                byte[] currentCodeBytes = new byte[CommonUtils.BitsLengthToBytesLength(currentCodeBitsLength)];
                                dataReader.ReadBytes(currentCodeBytes);

                                probabilitiesTable.Add(currentChar, new EncodedString(currentCodeBytes.ToList(), (int)currentCodeBitsLength));

                                //Leo los 2 ultimos caracteres para verificar si llegue o no al final de la tabla de probabilidades
                                endOfTableReader = dataReader.ReadString(2);
                                
                            }

                            //Obtengo la longitud en bits del texto codificado
                            string encodedTextLength = "";

                            temp = dataReader.ReadString(1);

                            while (temp != ":")
                            {
                                encodedTextLength += temp;
                                temp = dataReader.ReadString(1);
                            }

                            uint encodedTextBitsLength = uint.Parse(encodedTextLength);

                            //Creo un buffer y guardo en él el texto codificado
                            byte[] encodedTextBytes = new byte[CommonUtils.BitsLengthToBytesLength(encodedTextBitsLength)];
                            dataReader.ReadBytes(encodedTextBytes);

                            //Creo un EncodedString con el texto codificado
                            encodedText = new EncodedString(new List<byte>(encodedTextBytes), (int)encodedTextBitsLength);
                        }
                    }

                    stream.Dispose();

                    //Guardo el tipo de archivo original y su descripción
                    _compTextType = fileType;
                    _compTextDisplayType = fileDisplayType;

                    //Decodifico la tabla de probabilidades
                    ProbabilitiesScanner scanner = ProbabilitiesScanner.FromDictionary(probabilitiesTable);
                    var dif = scanner.AreAllDifferent();

                    //Decodifico el texto
                    HuffmanEncoder decoder = new HuffmanEncoder();
                    _compTextStr = decoder.Decode(scanner, encodedText);

                    //Muestro el texto decodificado
                    compTextContainer.Visibility = Visibility.Visible;
                    uncompressBt.Visibility = Visibility.Visible;
                    compTextExtraData.Visibility = Visibility.Visible;
                    compText.Text = _compTextStr;
                    compTextLength.Text = _compTextStr.Length.ToString();
                }
                catch (Exception)
                {

                }

                HideProgressPanel();
            }
        }

        private async void UncompressBt_Click(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = _compTextFile.DisplayName
            };
            savePicker.FileTypeChoices.Add(_compTextDisplayType, new List<string>() { _compTextType });

            StorageFile file = await savePicker.PickSaveFileAsync();

            if (file != null)
            {
                ShowProgressPanel();

                // Prevent updates to the remote version of the file until
                // we finish making changes and call CompleteUpdatesAsync.
                CachedFileManager.DeferUpdates(file);

                var stream = await file.OpenAsync(FileAccessMode.ReadWrite);

                using (var outputStream = stream.GetOutputStreamAt(0))
                {
                    using (var dataWriter = new DataWriter(outputStream))
                    {
                        dataWriter.WriteString(_compTextStr);

                        await dataWriter.StoreAsync();
                        await outputStream.FlushAsync();
                    }
                }
                stream.Dispose(); // Or use the stream variable (see previous code snippet) with a using statement as well.

                // Let Windows know that we're finished changing the file so
                // the other app can update the remote version of the file.
                // Completing updates may require Windows to ask for user input.
                Windows.Storage.Provider.FileUpdateStatus status =
                    await CachedFileManager.CompleteUpdatesAsync(file);
                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
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


        private void ShowProgressPanel() => progressPanel.Visibility = Visibility.Visible;

        private void HideProgressPanel() => progressPanel.Visibility = Visibility.Collapsed;
    }
}
