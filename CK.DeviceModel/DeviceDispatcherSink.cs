using CK.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    internal class DeviceDispatcherSink<T> where T: Device
    {
        readonly BlockingCollection<Event> _queue;
        readonly Action _externalOnTimer;
        readonly IActivityMonitor _initialRegister;
        readonly Task _task;
        readonly object _externalLogLock;
        readonly object _confTrigger;

         T[] _devices;
        IDeviceConfiguration[] _newConf;
        volatile int _stopFlag;
        TimeSpan _timerDuration;
        long _nextTicks;
        long _deltaTicks;





        public TimeSpan TimerDuration
        {
            get => _timerDuration;
            set
            {
                if (_timerDuration != value)
                {
                    _timerDuration = value;
                    _deltaTicks = value.Ticks;
                }
            }
        }

        public DeviceDispatcherSink(
             IActivityMonitor initialRegister,
            TimeSpan timerDuration,
            TimeSpan externalTimerDuration,
            Action externalTimer,
            Action<LogFilter?, LogLevelFilter?> filterChange )
        {
            _devices = new T[0];
            _queue = new BlockingCollection<Event>();
            _task = new Task(Process, TaskCreationOptions.LongRunning);
            _confTrigger = new object();
            _initialRegister = initialRegister;
            _stopFlag = 0;
            _timerDuration = timerDuration;
            _deltaTicks = timerDuration.Ticks;

            _task.Start();
        }



        private void DoConfigure(IActivityMonitor monitor, IDeviceConfiguration[] newConf)
        {
            Util.InterlockedSet(ref _newConf, t => t.Skip(newConf.Length).ToArray());
            var c = newConf[newConf.Length - 1];

            for (int i = 0; i < _devices.Count(); i++)
            {
                try
                {
                    if (c.Name == _devices[i].Name)
                        _devices[i].ApplyConfiguration(monitor, c);
                }
                catch (Exception ex)
                {
                    var msg = $"Handler {_devices[i].Name} crashed.";
                    ActivityMonitor.CriticalErrorCollector.Add(ex, msg);
                }
            }
            
            lock (_confTrigger)
                Monitor.PulseAll(_confTrigger);
        }


        internal bool IsRunning => _stopFlag == 0;

        internal void ApplyConfiguration(IDeviceConfiguration configuration, bool waitForApplication)
        {
            Util.InterlockedAdd(ref _newConf, configuration);
            if (waitForApplication)
            {
                lock (_confTrigger)
                {
                    IDeviceConfiguration[] newConf;
                    while (IsRunning && (newConf = _newConf) != null && newConf.Contains(configuration))
                        Monitor.Wait(_confTrigger);
                }
            }
        }


        private void Process()
        {
            var monitor = new ActivityMonitor(applyAutoConfigurations: false);
            
            // Simple pooling for initial configuration.
            IDeviceConfiguration[] newConf = _newConf;
            while (newConf == null)
            {
                Thread.Sleep(0);
                newConf = _newConf;
            }

            monitor.SetTopic(GetType().FullName);
            DoConfigure(monitor, newConf);

            #region Process OnTimer
            long now = DateTime.UtcNow.Ticks;
            if (now >= _nextTicks)
            {
                foreach (var device in _devices)
                {
                    try
                    {
                        device.OnTimer(monitor, _timerDuration);
                    }
                    catch (Exception ex)
                    {
                        var msg = $"{device.GetType().FullName}.OnTimer() crashed.";
                        ActivityMonitor.CriticalErrorCollector.Add(ex, msg);
                    }
                }
                _nextTicks = now + _deltaTicks;
            }
            #endregion
            monitor.MonitorEnd();
        }
    }
}
