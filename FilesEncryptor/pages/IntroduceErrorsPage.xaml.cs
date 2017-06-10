using FilesEncryptor.dto;
using FilesEncryptor.dto.hamming;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.hamming;
using FilesEncryptor.utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
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
    public sealed partial class IntroduceErrorsPage : Page
    {
        private FileHelper _filesHelper;
        private BitCode _fullCode;
        private HammingEncodeType _encodeType;
        private FileHeader _fileHeader;
        private HammingCodeLength _fullCodeLenth;

        Random _moduleRandom, _bitPositionRandom;
        public IntroduceErrorsPage()
        {
            this.InitializeComponent();
            _moduleRandom = new Random();
            _bitPositionRandom = new Random();
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
            var extensions = new List<string>();
            foreach (HammingEncodeType type in BaseHammingCodifier.EncodeTypes)
            {
                extensions.Add(type.Extension);
            }

            bool pickResult = await _filesHelper.PickToOpen(extensions);

            if (pickResult)
            {
                ShowProgressPanel();
                await Task.Delay(200);

                settingsPanel.Visibility = Visibility.Collapsed;
                pageCommandsDivider.Visibility = Visibility.Collapsed;
                pageCommands.Visibility = Visibility.Collapsed;

                bool openResult = await _filesHelper.OpenFile(FileAccessMode.Read);

                if (openResult)
                {
                    DebugUtils.WriteLine(string.Format("Selected file: {0} with size of {1} bytes", _filesHelper.SelectedFilePath, _filesHelper.FileSize));

                    fileNameBlock.Text = _filesHelper.SelectedFileName;
                    fileSizeBlock.Text = string.Format("{0} bytes", _filesHelper.FileSize);
                    fileDescriptionBlock.Text = string.Format("{0} ({1})", _filesHelper.SelectedFileDisplayType, _filesHelper.SelectedFileExtension);

                    settingsPanel.Visibility = Visibility.Visible;
                    pageCommandsDivider.Visibility = Visibility.Visible;
                    pageCommands.Visibility = Visibility.Visible;

                    bool extractResult = ExtractFileProperties();
                    if (extractResult)
                    {
                        DebugUtils.WriteLine("File bytes extracted properly");
                    }
                    else
                    {
                        DebugUtils.WriteLine("Failed extracting File bytes", "[FAIL]");
                    }

                    DebugUtils.WriteLine("Closing file");
                    await _filesHelper.Finish();
                }
            }
            HideProgressPanel();
        }

        private bool ExtractFileProperties()
        {
            bool extractResult = false;
            _fileHeader = _filesHelper.ReadFileHeader();

            if (_fileHeader != null)
            {
                _encodeType = BaseHammingCodifier.EncodeTypes.First(encType => encType.Extension == _filesHelper.SelectedFileExtension);
                HammingDecoder decoder = HammingDecoder.FromFile(_filesHelper, _encodeType);
                _fullCode = decoder.RawCode;
                _fullCodeLenth = decoder.RawCodeLength;
                extractResult = true;
            }

            return extractResult;
        }

        private async void ConfirmBt_Click(object sender, RoutedEventArgs e)
        {
            bool pickResult = await _filesHelper.PickToSave(_filesHelper.SelectedFileName, _filesHelper.SelectedFileDisplayType, _filesHelper.SelectedFileExtension);

            if (pickResult)
            {
                bool openResult = await _filesHelper.OpenFile(FileAccessMode.ReadWrite);

                if (openResult)
                {
                    ShowProgressPanel();
                    await Task.Delay(200);

                    DebugUtils.WriteLine(string.Format("Output file: \"{0}\"", _filesHelper.SelectedFilePath));
                    DebugUtils.WriteLine("Extracting input words");

                    List<BitCode> inputWords = _fullCode.Explode(_encodeType.WordBitsSize, false).Item1;

                    DebugUtils.WriteLine(string.Format("Extracted {0} input words of {1} bits size", inputWords.Count, _encodeType.WordBitsSize));
                    DebugUtils.WriteLine("Start inserting errors");

                    //Inserto errores en el archivo codificado en Hamming
                    List<BitCode> outputWords = new List<BitCode>();

                    int wordIndex = 0;
                    int wordsWithError = 0;

                    foreach (BitCode inputWord in inputWords)
                    {
                        if (InsertErrorInModule())
                        {
                            uint replacePos = (uint)SelectBitPositionRandom(0, inputWord.CodeLength - 1);
                            DebugUtils.WriteLine(string.Format("Insert error in word {0} bit {1}", wordIndex, replacePos), "[PROGRESS]");
                            outputWords.Add(inputWord.ReplaceAt(replacePos, inputWord.ElementAt(replacePos).Negate()));
                            wordsWithError++;
                        }
                        else
                        {
                            outputWords.Add(inputWord);
                        }
                        wordIndex++;
                    }

                    DebugUtils.WriteLine(string.Format("Inserting errors finished with {0} words with error", wordsWithError));
                    BitCode outputCode = BitOps.Join(outputWords);

                    DebugUtils.WriteLine(string.Format("Dumping file with errors to \"{0}\"", _filesHelper.SelectedFilePath));

                    bool writeResult = _filesHelper.WriteFileHeader(_fileHeader);
                    writeResult = HammingEncoder.WriteEncodedToFile(new HammingEncodeResult(outputCode, _encodeType, _fullCodeLenth), _filesHelper);

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

                    HideProgressPanel();
                }
            }
        }

        private bool InsertErrorInModule() => _moduleRandom.Next(-1, 1) >= 0;
        
        private int SelectBitPositionRandom(int min, int max) => _bitPositionRandom.Next(min, max);


        private void ShowProgressPanel() => progressPanel.Visibility = Visibility.Visible;

        private void HideProgressPanel() => progressPanel.Visibility = Visibility.Collapsed;
    }
}
