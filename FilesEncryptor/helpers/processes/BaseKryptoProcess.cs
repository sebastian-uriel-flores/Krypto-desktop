using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers.processes
{
    public class BaseKryptoProcess
    {
        private string _status;
        private double _progressLevel;
        private DateTime _startTime;
        private TimeSpan _currentTime;
        private List<KryptoEvent> _events;

        private IKryptoProcessUI _currentUI;
        private Timer _timer;
        private bool _stopWatchWhenFinish;

        public virtual void Start(IKryptoProcessUI uiToShow, bool stopWatchWhenFinish = true)
        {
            if(uiToShow != null)
            {
                _currentUI = uiToShow;
                _events = new List<KryptoEvent>();
                _startTime = DateTime.Now;
                _progressLevel = 0;
                _stopWatchWhenFinish = stopWatchWhenFinish;

                UpdateStatus("Initializing");
                AddEvent(new KryptoEvent()
                {
                    Moment = TimeSpan.Zero,
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

        public void UpdateStatus(string status, bool restartProgressLevel = false)
        {
            _status = status;
            _currentUI.SetStatus(status);

            if (restartProgressLevel)
            {
                _progressLevel = 0;
            }
        }

        public void AddEvent(KryptoEvent kEvent)
        {
            _events.Add(kEvent);
            //_progressLevel = Math.Max(Math.Min(0, _progressLevel + kEvent.ProgressAdvance), 100);
            _progressLevel = Math.Min(Math.Max(0, kEvent.ProgressAdvance), 100);
            kEvent.Moment = DateTime.Now.Subtract(_startTime);
            _currentUI.AddEvent(kEvent);
            _currentUI.SetProgressMessage(kEvent.Message);
            _currentUI.SetProgressLevel(_progressLevel);
        }

        public virtual void Stop(bool failed = false)
        {
            if (_stopWatchWhenFinish)
            {
                _timer.Dispose();
                _currentUI.SetTime(DateTime.Now.Subtract(_startTime));
            }
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

        public void StopWatch()
        {
            _timer?.Dispose();
            _currentUI.SetTime(DateTime.Now.Subtract(_startTime));
        }
    

        public sealed class KryptoEvent
        {
            public TimeSpan Moment { get; set; }
            public string Tag { get; set; }
            public string Message { get; set; }
            public double ProgressAdvance { get; set; }
        }
    }
}
