using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Krypto.viewmodels
{
    public interface IProcessViewModel
    {
        void OnNavigatedTo(IProcessView view, bool appActivated);

        void PickFile();

        void TakeFile(StorageFile file);

        void Process();

        void CloseProgressPanelButtonClicked();
    }
}
