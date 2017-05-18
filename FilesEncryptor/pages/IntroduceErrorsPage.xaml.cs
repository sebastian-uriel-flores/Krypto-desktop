using FilesEncryptor.dto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
        Random _moduleRandom, _bitPositionRandom;
        public IntroduceErrorsPage()
        {
            this.InitializeComponent();
            _moduleRandom = new Random();
            _bitPositionRandom = new Random();
        }

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ConfirmBt_Click(object sender, RoutedEventArgs e)
        {
            List<BitCode> codeBlocks = new List<BitCode>();
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
    }
}
