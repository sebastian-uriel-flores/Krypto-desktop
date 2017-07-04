using FilesEncryptor.helpers.processes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace Krypto.viewmodels
{
    public interface IProcessView : IKryptoProcessUI
    {
        void SetTitle(string title);

        void SetBackButtonVisibility(Visibility vis);

        void SetFilePickerButtonVisibility(Visibility vis);

        void SetFilePath(string path);

        void SetFileSize(string size);

        void SetFileDescription(string description);

        void SetSelectorVisibility(Visibility vis);

        int GetSelectorSelectedIndex();

        void SetSelectorSelectedIndex(int index);

        void SetTextAreaVisibility(Visibility vis);

        void SetConfirmButtonStatus(bool enabled);

        Task SetLoadingPanelVisibility(Visibility vis);

        Task SetProgressPanelVisibility(Visibility vis);

        void SetProgressPanelCloseButtonVisibility(Visibility vis);
    }
}