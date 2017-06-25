using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers.processes
{
    public interface IKryptoProcessUI
    {
        void SetStatus(string currentStatus);
        void SetTime(TimeSpan totalTime);
        void SetProgressMessage(string progressMessage);
        void SetProgressLevel(double progressLevel);        
        void AddEvent(KryptoProcess.KryptoEvent kEvent);
        void SetShowFailureInformationButtonVisible(bool visible);
    }
}
