using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers.processes
{
    public class KryptoProcess<T> : KryptoProcess
    {
        private new Action<T> _onCompletedAction;
        private new Action<int> _onFailedAction;
        private Task<T> _lastTask;

        public KryptoProcess(Task<T> lastTask, List<Tuple<Task, string>> previousTasks=null) : base(previousTasks)
        {
            _lastTask = lastTask;
        }

        public void Start(IKryptoProcessUI uiToShow, Action<T> onCompletedAction = null, Action<int> onFailedAction = null)
        {
            _onCompletedAction = onCompletedAction;
            _onFailedAction = onFailedAction;
            base.Start(uiToShow);            
        }

        protected override void StartNextTask(Task lastTask = null)
        {
            if (_currentTaskIndex >= _tasks.Count)
            {
                lock (_processes)
                {
                    if (_currentTaskIndex >= 0)
                    {
                        _processes.Remove(_tasks[_currentTaskIndex].Item1.Id);
                    }

                    if (lastTask != null && lastTask.IsFaulted)
                    {
                        Stop(true);
                        return;
                    }
                    else
                    {
                        _currentTaskIndex++;

                        if (!string.IsNullOrEmpty(_tasks[_currentTaskIndex].Item2))
                        {
                            UpdateStatus($"Initializing {_tasks[_currentTaskIndex].Item2}...", true);
                        }
                        _processes.Add(_tasks[_currentTaskIndex].Item1.Id, this);

                        if (_currentTaskIndex < _tasks.Count - 1)
                        {
                            _tasks[_currentTaskIndex].Item1.ContinueWith((t) => StartNextTask(t));
                        }
                        else
                        {
                            _tasks[_currentTaskIndex].Item1.ContinueWith((t) =>
                            {
                                _processes.Remove(_tasks[_currentTaskIndex].Item1.Id);
                                _currentTaskIndex++;
                                StartNextTask(t);
                            });
                        }
                    }
                }
            }
            else
            {
                StartLastTask(lastTask);
            }
        }

        private void StartLastTask(Task previousTask = null)
        {
            lock (_processes)
            {
                _processes.Add(_lastTask.Id, this);
            }
            _lastTask.Start();
            _lastTask.ContinueWith((t) =>
            {
                Stop(t.IsFaulted, t.Result);
            });
        }

        public void Stop(bool failed=false, T result=default(T))
        {
            base.Stop();

            if (failed)
                _onFailedAction?.Invoke(_currentTaskIndex);
            else
                _onCompletedAction?.Invoke(result);
        }
    }
}
