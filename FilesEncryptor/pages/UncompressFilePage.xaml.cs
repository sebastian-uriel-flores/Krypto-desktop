﻿using FilesEncryptor.dto;
using FilesEncryptor.helpers;
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

                    origTextContainer.Visibility = Visibility.Collapsed;
                    origTextExtraData.Visibility = Visibility.Collapsed;
                    uncompressBt.Visibility = Visibility.Collapsed;
                    origTextPanel.Visibility = Visibility.Collapsed;

                    var stream = await _compTextFile.OpenAsync(FileAccessMode.Read);
                    ulong size = stream.Size;

                    string probTableString = "";
                    EncodedString encodedText = new EncodedString(new List<byte>(), 0);
                    Encoding u8 = Encoding.UTF8;

                    using (var inputStream = stream.GetInputStreamAt(0))
                    {
                        using (var dataReader = new DataReader(inputStream))
                        {
                            dataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                            uint numBytesLoaded = await dataReader.LoadAsync((uint)size);
                          
                            string probTableLength = "";
                            byte[] oneCharBuffer = new byte[u8.GetByteCount(":")];

                            dataReader.ReadBytes(oneCharBuffer);
                            string temp = u8.GetString(oneCharBuffer);

                            while (temp != ":")
                            {
                                probTableLength += temp;
                                dataReader.ReadBytes(oneCharBuffer);
                                temp = u8.GetString(oneCharBuffer);
                            }


                            byte[] probTableBytes = new byte[uint.Parse(probTableLength)];
                            dataReader.ReadBytes(probTableBytes);

                            probTableString = u8.GetString(probTableBytes);

                            string codeBitsLengthStr = "";
                            dataReader.ReadBytes(oneCharBuffer);
                            temp = u8.GetString(oneCharBuffer);

                            while (temp != ":")
                            {
                                codeBitsLengthStr += temp;
                                dataReader.ReadBytes(oneCharBuffer);
                                temp = u8.GetString(oneCharBuffer);
                            }

                            int codeBitsLength = int.Parse(codeBitsLengthStr);
                            uint codeBytesLength = (uint)Math.Ceiling((float)codeBitsLength / 8.0);

                            var encodStringBytes = new byte[codeBytesLength];
                            dataReader.ReadBytes(encodStringBytes);
                            encodedText = new EncodedString(new List<byte>(encodStringBytes), codeBitsLength);
                        }
                    }

                    stream.Dispose();

                    ProbabilitiesScanner scanner = await ProbabilitiesScanner.FromEncodedTable(probTableString, u8);
                    HuffmanEncoder decoder = new HuffmanEncoder();
                    _compTextStr = decoder.Decode(scanner, encodedText);

                    origTextContainer.Visibility = Visibility.Visible;
                    uncompressBt.Visibility = Visibility.Visible;
                    origTextExtraData.Visibility = Visibility.Visible;
                    origText.Text = _compTextStr;
                    origTextLength.Text = _compTextStr.Length.ToString();
                }
                catch (Exception)
                {

                }

                HideProgressPanel();
            }
        }

        private void UncompressBt_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SaveBt_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ShowProgressPanel() => progressPanel.Visibility = Visibility.Visible;

        private void HideProgressPanel() => progressPanel.Visibility = Visibility.Collapsed;
    }
}
