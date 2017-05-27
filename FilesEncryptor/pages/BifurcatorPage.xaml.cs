﻿using FilesEncryptor.dto.Hamming;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.file_management;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
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
    public sealed partial class BifurcatorPage : Page
    {
        public BifurcatorPage()
        {
            this.InitializeComponent();

            commandsPanel.Loaded += new RoutedEventHandler((sender,args) =>
            {
                foreach (object item in commandsPanel.Items)
                {
                    (item as FrameworkElement).PointerEntered += BifurcatorPage_PointerEntered;
                    (item as FrameworkElement).PointerExited += BifurcatorPage_PointerExited;
                }
            });

            Loaded += BifurcatorPage_Loaded;
        }

        private void BifurcatorPage_Loaded(object sender, RoutedEventArgs e)
        {
            //PC customization
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
            {
                ApplicationView.GetForCurrentView().Title = "Teoría de la información";
                var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                if (titleBar != null)
                {
                    Color orange = new Color() { R = 251, G = 131, B = 0 };

                    titleBar.ButtonForegroundColor = Colors.WhiteSmoke;
                    titleBar.ButtonPressedForegroundColor = Colors.WhiteSmoke;
                    titleBar.ButtonBackgroundColor = orange;
                    titleBar.ButtonPressedBackgroundColor = orange;                    
                    titleBar.InactiveForegroundColor = Colors.WhiteSmoke;
                    titleBar.InactiveBackgroundColor = orange;
                    titleBar.ButtonInactiveBackgroundColor = orange;
                    titleBar.ButtonInactiveForegroundColor = Colors.WhiteSmoke;
                    titleBar.BackgroundColor = orange;
                    titleBar.ForegroundColor = Colors.WhiteSmoke;
                }
            }

            DebugUtils.ShowConsoleInNewWindow();
        }

        private void BifurcatorPage_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                FrameworkElement panel = sender as FrameworkElement;
                panel.Projection = new PlaneProjection();
                ((PlaneProjection)panel.Projection).GlobalOffsetZ = 70;
            }
        }
        private void BifurcatorPage_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                FrameworkElement panel = sender as FrameworkElement;
                panel.Projection = new PlaneProjection();
                ((PlaneProjection)panel.Projection).GlobalOffsetZ = 0;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
        }

        private void CompressFileBt_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(CompressFilePage));

        private void UncompressFileBt_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(UncompressFilePage));

        private void HammingEncodeBt_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(HammingEncodePage));

        private void HammingDecodeBt_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(HammingDecodePage));

        private async void commandsPanel_ItemClick(object sender, ItemClickEventArgs e)
        {
            switch ((e.ClickedItem as FrameworkElement).Name)
            {
                case "compareFilesItem":
                    FilesComparer filesComp = new FilesComparer();

                    /*var extensions = new List<string>();
                    foreach (HammingEncodeType type in BaseHammingCodifier.EncodeTypes)
                    {
                        extensions.Add(type.Extension);
                    }*/

                    bool pickRes = await filesComp.PickFiles(new List<string>() { ".txt", ".pdf", ".doc", ".docx", ".jpg" });
                    //bool pickRes = await filesComp.PickFiles(new List<string>() { "." });

                    if (pickRes)
                    {
                        bool openRes = await filesComp.OpenFiles();

                        if (openRes)
                        {
                            bool compRes = filesComp.CompareFiles();
                            await filesComp.Finish();

                            MessageDialog diag = new MessageDialog(string.Format("Resultado de la comparación: {0}", compRes));
                            await diag.ShowAsync();
                        }
                    }
                    break;
                case "checkHammingAlgorithmItem":
                    FileHelper openFileHelper = new FileHelper();

                    if(await openFileHelper.PickToOpen(new List<string>() { ".txt", ".pdf", ".doc", ".docx", ".jpg" }))
                    {
                        if(await openFileHelper.OpenFile(Windows.Storage.FileAccessMode.Read))
                        {
                            progressPanel.Visibility = Visibility.Visible;

                            string message = "Loading file bytes";
                            DebugUtils.WriteLine(message);
                            progressText.Text = message;

                            DateTime startTime = DateTime.Now;
                            
                            var rawFileBytes = openFileHelper.ReadBytes(openFileHelper.FileSize).ToList();

                            DebugUtils.WriteLine(string.Format("Loaded {0} bytes", rawFileBytes.Count));

                            //Creo el codificador
                            HammingEncoder encoder = HammingEncoder.From(new dto.BitCode(rawFileBytes, rawFileBytes.Count * 8));
                            List<HammingEncodeResult> encodedResults = new List<HammingEncodeResult>();

                            //Por cada tipo de codificacion Hamming, codifico el archivo
                            DebugUtils.WriteLine("Starting encoding process");

                            foreach (HammingEncodeType encodeType in BaseHammingCodifier.EncodeTypes)
                            {
                                message = string.Format("Encoding in {0} encode type", encodeType.ShortDescription);
                                DebugUtils.WriteLine(message);
                                progressText.Text = message;

                                var encodeRes = await encoder.Encode(encodeType);

                                if (encodeRes != null)
                                {
                                    DebugUtils.WriteLine(string.Format("Encoding was successfull in {0} encode type", encodeType.ShortDescription));
                                    encodedResults.Add(encodeRes);
                                }
                                else
                                {
                                    DebugUtils.WriteLine(string.Format("Encoding with error in {0} encode type", encodeType.ShortDescription));
                                    /*progressPanel.Visibility = Visibility.Collapsed;

                                    await new MessageDialog(string.Format("Hubo un error al codificar en: {0}", encodeType.ShortDescription)).ShowAsync();                                    
                                    return;*/
                                }
                            }

                            DebugUtils.WriteLine("All encodings were finished");

                            //Ahora decodifico a todos los archivos codificados
                            DebugUtils.WriteLine("Starting decoding process");

                            List<List<byte>> decodedBytes = new List<List<byte>>();
                            
                            foreach (HammingEncodeResult encodeRes in encodedResults)
                            {
                                message = string.Format("Decoding in {0} encode type", encodeRes.EncodeType.ShortDescription);
                                DebugUtils.WriteLine(message);
                                progressText.Text = message;

                                HammingDecoder decoder = HammingDecoder.FromEncoded(encodeRes);                                
                                var decodeRes = await decoder.Decode();

                                if (decodeRes != null)
                                {
                                    DebugUtils.WriteLine(string.Format("Decoding was successfull in {0} encode type", encodeRes.EncodeType.ShortDescription));
                                    decodedBytes.Add(decodeRes.Code);
                                }
                                else
                                {
                                    DebugUtils.WriteLine(string.Format("Decoding with error in {0} encode type", encodeRes.EncodeType.ShortDescription));
                                    /*progressPanel.Visibility = Visibility.Collapsed;

                                    await new MessageDialog(string.Format("Hubo un error al decodificar en: {0}", encodeRes.EncodeType.ShortDescription)).ShowAsync();
                                    return;*/
                                }                                
                            }

                            DebugUtils.WriteLine("All encodings were finished");

                            //Por ultimo, comparo los archivos decodificados
                            DebugUtils.WriteLine("Starting comparing process");

                            bool compareResult = false;
                            for(int i = 1; i< decodedBytes.Count; i++)
                            {
                                message = 
                                    string.Format("Comparing {0} with {1} encode types", 
                                    encodedResults[i-1].EncodeType.ShortDescription, 
                                    encodedResults[i].EncodeType.ShortDescription);

                                DebugUtils.WriteLine(message);
                                progressText.Text = message;

                                compareResult = decodedBytes[i - 1].SequenceEqual(decodedBytes[i]);
                                DebugUtils.WriteLine(string.Format("Compare result: {0}", compareResult));

                                if(!compareResult)
                                {
                                    break;
                                }
                            }

                            DebugUtils.WriteLine("Comparing with original file bytes");

                            compareResult = decodedBytes.Last().SequenceEqual(rawFileBytes);

                            DebugUtils.WriteLine(string.Format("Compare result: {0}", compareResult));

                            DateTime endTime = DateTime.Now;
                            var timeDif = endTime.Subtract(startTime);

                            progressPanel.Visibility = Visibility.Collapsed;
                            message = string.Format("El resultado de la comparacion es: {0}, realizado en {1} horas, {2} minutos, {3} segundos, {4} milisegundos", compareResult, timeDif.Hours, timeDif.Minutes, timeDif.Seconds, timeDif.Milliseconds);
                            DebugUtils.WriteLine(message, "[RESULT]");
                            await new MessageDialog(message).ShowAsync();
                        }
                    }
                    
                    break;
                case "compressFileItem":
                    Frame.Navigate(typeof(CompressFilePage));
                    break;
                case "uncompressFileItem":
                    Frame.Navigate(typeof(UncompressFilePage));
                    break;
                case "encodeFileItem":
                    Frame.Navigate(typeof(HammingEncodePage));
                    break;
                case "decodeFileItem":
                    Frame.Navigate(typeof(HammingDecodePage));
                    break;
                case "introduceErrorItem":
                    Frame.Navigate(typeof(IntroduceErrorsPage));
                    break;
            }            
        }
    }
}
