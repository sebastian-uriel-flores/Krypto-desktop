using FilesEncryptor.dto;
using FilesEncryptor.dto.hamming;
using FilesEncryptor.dto.huffman;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.hamming;
using FilesEncryptor.helpers.huffman;
using FilesEncryptor.helpers.processes;
using Krypto.viewmodels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using static FilesEncryptor.utils.DebugUtils;

// La plantilla de elemento Página en blanco está documentada en https://go.microsoft.com/fwlink/?LinkId=234238

namespace FilesEncryptor.pages
{
    /// <summary>
    /// Una página vacía que se puede usar de forma independiente o a la que se puede navegar dentro de un objeto Frame.
    /// </summary>
    public sealed partial class ProcessPage : Page, IProcessView
    {
        public enum PAGE_MODES
        {
            Huffman_Encode, Huffman_Decode, Hamming_Encode, Hamming_Decode, Hamming_Broke
        }

        public const string VIEW_MODEL_PARAM = "view_model";
        public const string ARGS_PARAM = "args";
        public const string APP_ACTIVATED_ARGS = "app_activated_args";

        private PAGE_MODES _pageMode;
        private IProcessViewModel _viewModel;
        private FileHelper _fileOpener;
        private FileHeader _fileHeader;

        #region HUFFMAN_DECODE

        HuffmanDecoder _huffmanDecoder;

        #endregion

        private ObservableCollection<HammingEncodeType> _encodeTypes = new ObservableCollection<HammingEncodeType>(BaseHammingCodifier.EncodeTypes);

        public ProcessPage()
        {
            this.InitializeComponent();
            
            _fileOpener = new FileHelper();
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

            Dictionary<string, object> paramsDict = null;

            if (e.Parameter is Dictionary<string, object>)
            {
                paramsDict = e.Parameter as Dictionary<string, object>;

                if (paramsDict.ContainsKey(VIEW_MODEL_PARAM) && paramsDict[VIEW_MODEL_PARAM] is IProcessViewModel)
                {
                    _viewModel = paramsDict[VIEW_MODEL_PARAM] as IProcessViewModel;
                    _viewModel.OnNavigatedTo(this, (bool)paramsDict[APP_ACTIVATED_ARGS]);
                }
            }
            else
            {
                
            }
            
            /*switch(_pageMode)
            {
                case PAGE_MODES.Huffman_Encode:
                    pageHeaderContent.Text = "Compactar con Huffman";
                    FindName("fileContentTextHeader");
                    FindName("fileContentTextBlock");
                    fileContentTextHeader.Visibility = Visibility.Collapsed;
                    fileContentTextBlock.Visibility = Visibility.Collapsed;
                    break;
                case PAGE_MODES.Huffman_Decode:
                    pageHeaderContent.Text = "";
                    break;                
            }*/

            if (paramsDict != null && paramsDict.TryGetValue(ARGS_PARAM, out object args) && args is IReadOnlyList<IStorageItem>)
            {
                var storageFiles = args as IReadOnlyList<IStorageItem>;
                _viewModel.TakeFile(storageFiles[0] as StorageFile);                
            }
        }

        private void SelectFileBt_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.PickFile();
        }

        private async void ConfirmBt_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Process();
            return;
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                switch (_pageMode)
                {
                    case PAGE_MODES.Huffman_Encode:
                        HuffmanEncode();
                        break;
                    case PAGE_MODES.Huffman_Decode:
                        HuffmanDecode();
                        break;
                }
            });
        }

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
            _viewModel.CloseProgressPanelButtonClicked();
        }

        private List<string> GetExtensions(PAGE_MODES mode)
        {
            List<string> extensions = new List<string>();
            switch (mode)
            {
                case PAGE_MODES.Huffman_Encode:
                    extensions.Add(".txt");
                    break;
                case PAGE_MODES.Huffman_Decode:
                    extensions.Add(".huf");
                    break;
            }

            return extensions;
        }

        private async Task ShowLoadingPanel()
        {
            if(loadingPanel == null)
            {
                FindName("loadingPanel");
            }
            loadingPanel.Visibility = Visibility.Visible;
            await Task.Delay(200);
        }

        private void HideLoadingPanel() => loadingPanel.Visibility = Visibility.Collapsed;

        private async Task ShowProgressPanel()
        {
            if (progressPanel == null)
            {
                FindName("progressPanel");
            }

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

        private async void FileTaked()
        {
            await ShowLoadingPanel();

            confirmBt.IsEnabled = false;

            //Si voy a codificar un archivo con Huffman, 
            //entonces debo leer la codificacion del archivo original
            bool takeEncoding = _pageMode == PAGE_MODES.Huffman_Encode;

            if (await _fileOpener.OpenFile(FileAccessMode.Read, takeEncoding))
            {
                //Muestro los datos del archivo cargado
                fileNameBlock.Text = _fileOpener.SelectedFileName;
                fileSizeBlock.Text = string.Format("{0} bytes", _fileOpener.FileSize);
                fileDescriptionBlock.Text = string.Format("{0} ({1})", _fileOpener.SelectedFileDisplayType, _fileOpener.SelectedFileExtension);

                confirmBt.IsEnabled = true;

                switch (_pageMode)
                {
                    #region HUFFMAN_ENCODE
                    case PAGE_MODES.Huffman_Encode:
                        
                        break;
                    #endregion
                }

                await _fileOpener.Finish();
            }

            HideLoadingPanel();
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
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                progressPanelEventsList.Items.Add($"{kEvent.Tag} : {kEvent.Message}");

                var selectedIndex = progressPanelEventsList.Items.Count - 1;
                if (selectedIndex < 0)
                    return;

                progressPanelEventsList.SelectedIndex = selectedIndex;
                //progressPanelEventsList.UpdateLayout();

                progressPanelEventsList.ScrollIntoView(progressPanelEventsList.SelectedItem);
            });
        }

        public void SetShowFailureInformationButtonVisible(bool visible)
        {

        }

        #endregion

        #region HUFFMAN_ENCODE

        private async void HuffmanEncode()
        {
            
        }

        #endregion

        #region HUFFMAN_DECODE

        private async void HuffmanDecode()
        {
            
        }

        #endregion

        public void SetTitle(string title)
        {
            pageHeaderContent.Text = title;
        }

        public void SetSelectorVisibility(Visibility vis)
        {
            if (hammingEncodeTypeHeader == null)
            {
                FindName("hammingEncodeTypeHeader");
                FindName("hammingEncodeTypeSelector");
            }
            hammingEncodeTypeHeader.Visibility = vis;
            hammingEncodeTypeSelector.Visibility = vis;
        }

        public void SetTextAreaVisibility(Visibility vis)
        {

        }

        public void SetFilePickerButtonVisibility(Visibility vis)
        {
            if (selectFileBt == null && vis == Visibility.Visible)
            {
                FindName("selectFileBt");
            }
            else if (selectFileBt != null)
            {
                selectFileBt.Visibility = vis;
            }
        }

        public void SetBackButtonVisibility(Visibility vis)
        {
            if (backBt == null && vis == Visibility.Visible)
            {
                FindName("backBt");
            }
            else if (backBt != null)
            {
                backBt.Visibility = vis;
            }
        }

        async Task IProcessView.SetLoadingPanelVisibility(Visibility vis)
        {
            if (loadingPanel == null)
            {
                FindName("loadingPanel");
            }
            loadingPanel.Visibility = vis;
            await Task.Delay(200);
        }

        async Task IProcessView.SetProgressPanelVisibility(Visibility vis)
        {
            if (progressPanel == null)
            {
                FindName("progressPanel");
            }
            progressPanel.Visibility = vis;
            await Task.Delay(200);

            if (vis == Visibility.Collapsed)
            {
                progressPanelStatus.Text = "";
                progressPanelTime.Text = "";
                progressPanelCurrentEvent.Text = "";
                progressPanelProgressBar.Value = 0;
                progressPanelEventsList.Items.Clear();
                progressPanelCloseButton.Visibility = Visibility.Collapsed;
            }
        }

        public void SetConfirmButtonStatus(bool enabled)
        {
            confirmBt.IsEnabled = enabled;
        }

        public void SetFilePath(string path)
        {
            fileNameBlock.Text = path;
        }

        public void SetFileSize(string size)
        {
            fileSizeBlock.Text = size;
        }

        public void SetFileDescription(string description)
        {
            fileDescriptionBlock.Text = description;
        }

        public void SetSelectorSelectedIndex(int index)
        {
            if (hammingEncodeTypeSelector != null)
            {
                hammingEncodeTypeSelector.SelectedIndex = index;
            }
        }

        public int GetSelectorSelectedIndex()
        {
            return hammingEncodeTypeSelector != null ? hammingEncodeTypeSelector.SelectedIndex : -1;
        }

        public void SetProgressPanelCloseButtonVisibility(Visibility vis)
        {
            if (progressPanel != null)
            {
                progressPanelCloseButton.Visibility = vis;
            }
        }

        public async Task SetTextAreaContent(string content)
        {
            if (fileContentTextBlock != null)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    //Seteo el texto en pantalla
                    fileContentTextBlock.Text = content;
                });
            }
        }
    }
}
