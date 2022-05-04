using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;
using System.Diagnostics;

namespace ExpoHelpers
{
    /// <summary>
    /// Manages all the active devices.  Polls them to keep status up to date.
    /// </summary>
    public class ActiveDeviceList : NotifyPropertyChangedBase
    {
        private ObservableCollection<ActiveDevice> activeDevices = new ObservableCollection<ActiveDevice>();
        public ReadOnlyObservableCollection<ActiveDevice> ActiveDevices { get; private set; }

        bool heartbeatActive = false;
        bool inHeartbeat = false;

        private List<InstalledApplicationInfo> allInstalledApplications = null;
        public List<InstalledApplicationInfo> AllInstalledApplications
        {
            get { return this.allInstalledApplications; }
            set { this.PropertyChangedHelper(ref this.allInstalledApplications, value); }
        }

        public event LogEventHandler Log;

        public ActiveDeviceList()
        {
            this.ActiveDevices = new ReadOnlyObservableCollection<ActiveDevice>(this.activeDevices);
        }

        public async Task UpdateActiveDevicesAsync(IList<DeviceInformation> allDevices)
        {
            int nextDeviceIndex = 0;
            foreach(var deviceInformation in allDevices)
            {

                if(deviceInformation.IsChecked)
                {
                    int currentDeviceIndex = this.TryGetActiveDeviceIndex(deviceInformation.Address);

                    if(currentDeviceIndex >= 0)
                    {
                        // We already have this active device so just put it in the next position

                        // Only move it if it's in the wrong position
                        if(currentDeviceIndex != nextDeviceIndex)
                        {
                            var activeDevice = this.activeDevices[currentDeviceIndex];
                            this.activeDevices.RemoveAt(currentDeviceIndex);
                            this.activeDevices.Insert(nextDeviceIndex, activeDevice);
                        }
                    }
                    else
                    {
                        // We don't have an active device for this item so create and add
                        var activeDevice = new ActiveDevice(deviceInformation);
                        activeDevice.GetRunningProcessesOnHeartbeat = true; // maybe just do this with the selected device
                        activeDevice.Log += this.LogDeviceMessage;
                        this.activeDevices.Insert(nextDeviceIndex, activeDevice);
                    }

                    ++nextDeviceIndex;
                }
            }

            // remove any Active Devices the user doesn't want
            while(this.activeDevices.Count > nextDeviceIndex)
            {
                var activeDevice = this.activeDevices[nextDeviceIndex];
                await activeDevice.Close();
                this.activeDevices.RemoveAt(nextDeviceIndex);
            }

            // Get the heartbeat going if we need to
            if (this.activeDevices.Count > 0 && !this.heartbeatActive)
            {
                this.LogMessage(false, "Starting heartbeat");
                // we have active devices but no heartbeat so start it
                this.heartbeatActive = true;
                Device.BeginInvokeOnMainThread(() => 
                {
                    Device.StartTimer(TimeSpan.FromSeconds(5.0), () =>
                    {
                        //this.LogMessage(false, "Heartbeat");

                        if (!this.inHeartbeat)
                        {
                            this.inHeartbeat = true;
                            this.HeartbeatTimerTick();
                            this.inHeartbeat = false;
                        }

                        return this.heartbeatActive;
                    });
                });
            }
            else if (this.activeDevices.Count == 0 && this.heartbeatActive)
            {
                this.LogMessage(false, "Stopping heartbeat");

                // We have no active devices but there is a heartbeat. So stop it
                this.heartbeatActive = false;
            }
        }

        private bool HaveActiveDevice(string address)
        {
            foreach(ActiveDevice d in this.activeDevices)
            {
                if (d.Address == address) return true;
            }
            return false;
        }

        private int TryGetActiveDeviceIndex(string address)
        {

            for(int activeDeviceIndex = 0; activeDeviceIndex < this.ActiveDevices.Count; ++activeDeviceIndex)
            {
                var activeDevice = this.ActiveDevices[activeDeviceIndex];
                if(activeDevice.Address == address)
                {
                    return activeDeviceIndex;
                }
            }
            return -1;
        }

        private ActiveDevice[] GetActiveDeviceArray()
        {
            var deviceArray = new ActiveDevice[this.activeDevices.Count];
            for(int i = 0; i < this.activeDevices.Count; i++)
            {
                deviceArray[i] = this.activeDevices[i];
            }
            return deviceArray;
        }

        private async void HeartbeatTimerTick()
        {
            if (this.activeDevices.Count <= 0)
            {
                return;
            }

            // Note that if there are a lot of active devices, we have in the
            // past needed to throttle this.  Basically put a half-second
            // delay between the calls to ActiveDevice.Heartbeat.
            // Different approaches may be needed based on the network stack
            // for Android, iOS, and UWP

            // Make an array copy in case activeDevices gets modified
            // while we're enumerating it
            var activeDeviceArray = this.GetActiveDeviceArray();
            
            var heartbeatTasks = new Task[activeDeviceArray.Length];
            for (int deviceIndex = 0; deviceIndex < heartbeatTasks.Length; deviceIndex++)
            {
                heartbeatTasks[deviceIndex] = activeDeviceArray[deviceIndex].Heartbeat();
            }
            // Wait for at least one of the heartbeats to complete so there will
            // be some data to use below
            await Task.WhenAny(heartbeatTasks);

            this.UpdateInstalledApps(activeDeviceArray);
        }

        private void UpdateInstalledApps(ActiveDevice[] activeDeviceArray)
        {
            // union of all the apps sideloaded on all the devices
            // this is used to generate the list of apps in the settings screen
            Dictionary<string, InstalledApplicationInfo> allInstalledApps = new Dictionary<string, InstalledApplicationInfo>();

            foreach (var device in activeDeviceArray)
            {
                if (device.InstalledApplications != null)
                {
                    foreach (var app in device.InstalledApplications)
                    {
                        if (!allInstalledApps.ContainsKey(app.AppId))
                        {
                            allInstalledApps[app.AppId] = app;
                        }
                    }
                }
            }

            // See if the set of installed apps has changed by
            // comparing the old and new sorted list of all the apps
            bool updateInstalledApplications = false;
            List<InstalledApplicationInfo> newInstalledApps = new List<InstalledApplicationInfo>(allInstalledApps.Values.Count);
            foreach (var app in allInstalledApps.Values)
            {
                newInstalledApps.Add(app);
            }
            newInstalledApps.Sort((a, b) => a.AppId.CompareTo(b.AppId));
            if (this.AllInstalledApplications == null || this.AllInstalledApplications.Count != newInstalledApps.Count)
            {
                updateInstalledApplications = true;
            }
            else
            {
                for (int iApplication = 0; iApplication < newInstalledApps.Count; iApplication++)
                {
                    if (this.AllInstalledApplications[iApplication].AppId != newInstalledApps[iApplication].AppId)
                    {
                        updateInstalledApplications = true;
                        break;
                    }
                }
            }

            // The list has changed so update it and
            // make sure the currently picked apps are something
            // in the new list
            if (updateInstalledApplications)
            {
                this.AllInstalledApplications = newInstalledApps;
            }
        }

        private void LogDeviceMessage(bool isError, string deviceId, string message)
        {
            this.Log?.Invoke(isError, deviceId, message);
        }

        private void LogMessage(bool isError, string message)
        {
            this.Log?.Invoke(isError, null, $"ActiveDeviceList: {message}");
        }
    }
}
