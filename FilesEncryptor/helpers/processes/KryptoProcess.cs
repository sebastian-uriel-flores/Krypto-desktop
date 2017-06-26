using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers.processes
{
    public class KryptoProcess : BaseKryptoProcess
    {
        protected static Dictionary<int, KryptoProcess> _processes = new Dictionary<int, KryptoProcess>();

        public static KryptoProcess GetCurrent()
        {
            KryptoProcess current = null;
                        
            if (Task.CurrentId != null)
            {
                _processes.TryGetValue((int)Task.CurrentId, out current);
            }

            return current;
        }

        protected List<Tuple<Task, string>> _tasks;
        protected int _currentTaskIndex;        
        protected Action<int> _onCompletedAction;
        protected Action<int> _onFailedAction;

        public KryptoProcess(List<Tuple<Task, string>> tasks) : base()
        {
            _tasks = tasks?? new List<Tuple<Task, string>>();
        }

        public void Start(IKryptoProcessUI uiToShow, Action<int> onCompletedAction = null, Action<int> onFailedAction = null)
        {
            base.Start(uiToShow);

            //Inicio la primera de las tareas
            _currentTaskIndex = -1;
            _onCompletedAction = onCompletedAction;
            _onFailedAction = onFailedAction;
            StartNextTask();
        }

        protected virtual void StartNextTask(Task lastTask = null)
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

                    if (_currentTaskIndex < _tasks.Count)
                    {
                        _tasks[_currentTaskIndex].Item1.ContinueWith((t) => StartNextTask(t));
                    }
                    else
                    {
                        Stop(lastTask.IsFaulted);
                    }
                }
            }
        }
     
        public override void Stop(bool failed=false)
        {
            base.Stop(failed);

            if (failed)
                _onFailedAction?.Invoke(_currentTaskIndex);
            else
                _onCompletedAction?.Invoke(_currentTaskIndex);
        }
    }
}
