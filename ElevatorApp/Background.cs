using System;
using System.ComponentModel;

namespace ElevatorApp
{
    public static class Background
    {
        public static void Run<T>(Func<T> work, Action<T> done, Action<Exception> fail)
        {
            var w = new BackgroundWorker();
            w.DoWork += (s, e) => { e.Result = work(); };
            w.RunWorkerCompleted += (s, e) =>
            {
                if (e.Error != null) { if (fail != null) fail(e.Error); }
                else if (done != null) done((T)e.Result);
                w.Dispose();
            };
            w.RunWorkerAsync();
        }
    }

    public class LogObserver : IElevatorObserver
    {
        private readonly Action<Exception> onError;
        public LogObserver(Action<Exception> onError) { this.onError = onError; }

        public void ElevatorChanged(Elevator elevator, string logMessage)
        {
            if (logMessage == null) return;
            int id = elevator.Id, floor = elevator.CurrentFloor;
            Background.Run<int>(() => { LogRepository.Add(id, floor, logMessage); return 0; }, null, onError);
        }
    }
}