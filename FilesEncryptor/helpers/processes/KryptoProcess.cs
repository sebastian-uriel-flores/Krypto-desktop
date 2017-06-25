using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers.processes
{
    public class KryptoProcess
    {
        private string _status;
        private double _progressLevel;
        private DateTime _startTime;
        private TimeSpan _currentTime;
        private List<KryptoEvent> _events;

        private IKryptoProcessUI _currentUI;
        private Timer _timer;

        public void Start(IKryptoProcessUI uiToShow)
        {
            if(uiToShow != null)
            {
                _events = new List<KryptoEvent>();
                _startTime = DateTime.Now;
                _progressLevel = 0;

                UpdateStatus("Initializing");
                AddEvent(new KryptoEvent()
                {
                    Moment = _startTime,
                    Message = "Setting up system...",
                    ProgressAdvance = 0,
                    Tag = "[INFO]"
                });                

                _timer = new Timer(
                (empty) =>
                {
                    _currentTime = DateTime.Now.Subtract(_startTime);
                    _currentUI.SetTime(_currentTime);
                },
                null, 0, 1000);
            }
        }

        public void ChangeUI(IKryptoProcessUI uiToSet)
        {
            lock (_currentUI)
            {
                _currentUI = uiToSet;
            }
        }

        public void UpdateStatus(string status)
        {
            _status = status;
            _currentUI.SetStatus(status);
        }

        public void AddEvent(KryptoEvent kEvent)
        {
            _events.Add(kEvent);
            _progressLevel = Math.Max(Math.Min(0, _progressLevel + kEvent.ProgressAdvance), 100);
            _currentUI.AddEvent(kEvent);
            _currentUI.SetProgressMessage(kEvent.Message);
            _currentUI.SetProgressLevel(_progressLevel);
        }

        public void Stop(bool failed = false)
        {
            _timer.Dispose();
            _currentUI.SetTime(DateTime.Now.Subtract(_startTime));
            _currentUI.SetProgressLevel(100.0);
            _currentUI.SetShowFailureInformationButtonVisible(failed);

            if (failed)
            {
                _currentUI.SetStatus("Failed");                
            }
            else
            {
                _currentUI.SetStatus("Completed");
            }
        }
    

        public sealed class KryptoEvent
        {
            public DateTime Moment { get; set; }
            public string Tag { get; set; }
            public string Message { get; set; }
            public double ProgressAdvance { get; set; }
        }
    }
}
