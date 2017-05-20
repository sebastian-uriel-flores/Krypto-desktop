using FilesEncryptor.dto;
using FilesEncryptor.helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
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
        private HammingCodifier _decoder;

        Random _moduleRandom, _bitPositionRandom;
        public IntroduceErrorsPage()
        {
            this.InitializeComponent();
            _moduleRandom = new Random();
            _bitPositionRandom = new Random();
            _filesHelper = new FileHelper();
        }

        private async void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            List<string> extensions = new List<string>();
            foreach (HammingEncodeType type in HammingCodifier.EncodeTypes)
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

                bool openResult = await _filesHelper.OpenFile(FileAccessMode.ReadWrite);

                if (openResult)
                {
                    fileNameBlock.Text = _filesHelper.SelectedFileName;
                    fileSizeBlock.Text = string.Format("{0} bytes", _filesHelper.FileSize);
                    fileDescriptionBlock.Text = string.Format("{0} ({1})", _filesHelper.SelectedFileDisplayName, _filesHelper.SelectedFileExtension);

                    settingsPanel.Visibility = Visibility.Visible;
                    pageCommandsDivider.Visibility = Visibility.Visible;
                    pageCommands.Visibility = Visibility.Visible;

                    bool extractResult = ExtractFileProperties();
                    await _filesHelper.Finish();
                    DebugUtils.WriteLine("File extracted properly");
                }
            }
            HideProgressPanel();
        }

        
        private bool ExtractFileProperties()
        {
            bool extractResult = false;
            var header = _filesHelper.ExtractFileHeader();

            if (header != null)
            {
                _decoder = new HammingCodifier(HammingCodifier.EncodeTypes.First(encType => encType.Extension == _filesHelper.SelectedFileExtension));
                extractResult = _decoder.ReadFileContent(_filesHelper);
            }

            return extractResult;
        }

        private async void ConfirmBt_Click(object sender, RoutedEventArgs e)
        {
            BitCode decoded = await _decoder.Decode();

            List<BitCode> codeBlocks = decoded.Explode(_decoder.EncodeType.WordBitsSize, false).Item1;
            List<BitCode> blocksWithError = new List<BitCode>();

            foreach(BitCode block in codeBlocks)
            {
                if (InsertErrorInModule())
                {
                    uint replacePos = (uint)SelectBitPositionRandom(0, block.CodeLength - 1);
                    blocksWithError.Add(block.ReplaceAt(replacePos, block.ElementAt(replacePos).Negate()));
                }
                else
                {
                    blocksWithError.Add(block);
                }
            }

            //Ahora, escribo los bloques con error, reemplazando el archivo original

        }

        private bool InsertErrorInModule() => _moduleRandom.Next(-1, 1) >= 0;
        
        private int SelectBitPositionRandom(int min, int max) => _bitPositionRandom.Next(min, max);


        private void ShowProgressPanel() => progressPanel.Visibility = Visibility.Visible;

        private void HideProgressPanel() => progressPanel.Visibility = Visibility.Collapsed;
    }
}
