using DemoAssistant.Services;
using ExpoHelpers;
using Microsoft.Tools.WindowsDevicePortal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace DemoAssistant.Views
{
    public partial class ActiveDevicePage : ContentPage
    {
        private ActiveDevice activeDevice = null;

        private Dictionary<string, int> vkeyNames = new Dictionary<string, int>();

        public Command SendText { get; private set; }

        public Command SendVirtualKey { get; private set; }

        public ActiveDevicePage()
        {
            this.vkeyNames["left"] = 0x25;
            this.vkeyNames["up"] = 0x26;
            this.vkeyNames["down"] = 0x28;
            this.vkeyNames["right"] = 0x27;
            this.vkeyNames["tab"] = 0x09;

            this.SendText = new Command(async (param) =>
            {
                var text = param as string;
                if (!string.IsNullOrEmpty(text))
                {
                    await this.activeDevice.SendText(text);
                }
            });

            this.SendVirtualKey = new Command(async (param) =>
            {
                var vkeyName = param as string;
                if (!string.IsNullOrEmpty(vkeyName))
                {
                    await this.SendVkeyByName(vkeyName);
                }
            });

            InitializeComponent();

            VisualStateManager.GoToState(this.VisualStateContainer, "Inactive");
            VisualStateManager.GoToState(this.VisualStateContainer, "AppLaunchHidden");

            this.OnSettingsSaved();
        }

        protected override void OnParentSet()
        {
            if(this.Parent != null)
            {
                MessagingCenter.Subscribe<SettingsPage>(this, SettingsPage.SettingsSavedMessageName, (p) => this.OnSettingsSaved());
            }
            else
            {
                MessagingCenter.Unsubscribe<SettingsPage>(this, SettingsPage.SettingsSavedMessageName);
            }

            base.OnParentSet();
        }

        private void OnSettingsSaved()
        {
            DependencyService.Get<OptionalButtons>().ApplyTo(this.OptionalButtonsLayout);
        }

        private void ActiveDevicePageBindingContextChanged(object sender, EventArgs e)
        {
            var newActiveDevice = this.BindingContext as ActiveDevice;
            if(this.activeDevice != null && this.activeDevice != newActiveDevice)
            {
                this.activeDevice.PropertyChanged -= this.ActiveDevicePropertyChanged;
                this.activeDevice.AppLaunchStatusChanged -= this.AppLaunchStatusChanged;
            }

            this.activeDevice = newActiveDevice;

            if (this.activeDevice != null)
            {
                this.activeDevice.PropertyChanged += this.ActiveDevicePropertyChanged;
                this.activeDevice.AppLaunchStatusChanged += this.AppLaunchStatusChanged;
                this.RunningProcessesChanged();
                this.AppLaunchStatusChanged(AppLaunchStatus.None);
            }
        }

        private void ActiveDevicePropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == "RunningProcesses" || args.PropertyName == "Connected")
            {
                this.RunningProcessesChanged();
            }
        }

        private void RunningProcessesChanged()
        {
            string appId;
            string experienceAppPackageName;
            string trainingAppPackageName;

            DevicePortal.RunningProcesses runningProcesses = this.activeDevice.RunningProcesses;

            AppSettings.GetAppPackageInfo(AppPackageSetting.ExperienceApp, out appId, out experienceAppPackageName);
            AppSettings.GetAppPackageInfo(AppPackageSetting.TrainingApp, out appId, out trainingAppPackageName);
            string kioskModeAppPackageName = this.activeDevice.KioskModePackageName;

            bool experienceRunning = false;
            bool trainingRunning = false;
            bool kioskAppRunning = false;
            if (runningProcesses != null)
            {
                foreach (var process in runningProcesses.Processes)
                {
                    if (process.PackageFullName == experienceAppPackageName)
                    {
                        experienceRunning = true;
                    }
                    else if (process.PackageFullName == trainingAppPackageName || process.Name == AppSettings.TrainingAppExecutableName)
                    {
                        trainingRunning = true;
                    }
                    else if (process.PackageFullName == kioskModeAppPackageName)
                    {
                        kioskAppRunning = true;
                    }
                }
            }

            VisualStateManager.GoToState(this.VisualStateContainer, experienceRunning ? "ExperienceRunningState" : "ExperienceNotRunningState");
            VisualStateManager.GoToState(this.VisualStateContainer, trainingRunning ? "TrainingRunningState" : "TrainingNotRunningState");
            VisualStateManager.GoToState(this.VisualStateContainer, kioskAppRunning ? "KioskAppRunningState" : "KioskAppNotRunningState");
        }

        private async void AppLaunchStatusChanged(AppLaunchStatus status)
        {
            Color fromColor;

            switch (status)
            {
                default:
                case AppLaunchStatus.None:
                    fromColor = Color.White;
                    break;

                case AppLaunchStatus.CommandSent:
                    fromColor = Color.Yellow;
                    break;

                case AppLaunchStatus.Failed:
                    fromColor = Color.Red;
                    break;

                case AppLaunchStatus.Succeeded:
                    fromColor = Color.Green;
                    break;
            }

            ViewExtensions.CancelAnimations(this.AppLaunchFrame);
            await ColorAnimation.ColorTo(this.AppLaunchFrame, fromColor, Color.White, (color) => this.AppLaunchFrame.BackgroundColor = color, 500);
        }

        private async void LaunchKioskAppClick(object sender, EventArgs args)
        {
            if(this.activeDevice == null)
            {
                return;
            }

            await this.activeDevice.LaunchKioskModeApplicationAsync();
        }

        private async void LaunchTrainingAppClick(object sender, EventArgs args)
        {
            await this.LaunchApp(AppPackageSetting.TrainingApp);
        }

        private async void LaunchExperienceAppClick(object sender, EventArgs args)
        {
            await this.LaunchApp(AppPackageSetting.ExperienceApp);
        }

        private async void LaunchMixedRealityCaptureClick(object sender, EventArgs args)
        {
            await Task.Delay(0); // placeholder
        }

        private async void ScreenShotClick(object sender, EventArgs args)
        {
            var image = await this.activeDevice.TakeScreenshotAsync();

            if (image != null)
            {
                await Navigation.PushModalAsync(new NavigationPage(new ImageViewer(image)));
            }
        }

        private async void RestartDeviceClick(object sender, EventArgs args)
        {
            await this.activeDevice.RebootAsync();
        }

        private async void StopAllAppsClick(object sender, EventArgs e)
        {
            await this.StopAllAppsImpl();
        }

        private async void DevicePortalClick(object sender, EventArgs e)
        {
            await this.activeDevice.LaunchDevicePortalAsync();
        }

        private async Task LaunchApp(AppPackageSetting app)
        {
            string appId;
            string packageName;

            if (app == AppPackageSetting.ExperienceApp && AppSettings.StopAllAppsBeforeExperienceLaunch)
            {
                await this.StopAllAppsImpl();
            }

            AppSettings.GetAppPackageInfo(app, out appId, out packageName);
            if (!string.IsNullOrEmpty(appId) && !string.IsNullOrEmpty(packageName) && this.activeDevice != null)
            {
                await this.activeDevice.LaunchApplicationAsync(appId, packageName);
            }
        }

        private async Task StopAllAppsImpl()
        {
            if (this.activeDevice != null)
            {
                string appId;
                string packageName;

                AppSettings.GetAppPackageInfo(AppPackageSetting.ExperienceApp, out appId, out packageName);
                if (!(string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(packageName)))
                {
                    await this.activeDevice.TerminateApplicationAsync(packageName, AppSettings.ForceTerminateExperience);
                }

                // This may not work if the training app is running from the shell.  The actual
                // app that started the training is long gone
                AppSettings.GetAppPackageInfo(AppPackageSetting.TrainingApp, out appId, out packageName);
                if (!(string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(packageName)))
                {
                    await this.activeDevice.TerminateApplicationAsync(packageName, true);
                }

                //// Kill the training app by it's executable name.  There is no way currently for the
                //// user to set that (we may be able to hack something)
                //if (this.activeDevice.RunningProcesses.Processes != null)
                //{
                //    foreach (var process in this.activeDevice.RunningProcesses.Processes)
                //    {
                //        if (process.Name == AppSettings.TrainingAppExecutableName)
                //        {
                //            await this.activeDevice.TerminateProcessAsync(process.ProcessId);
                //        }
                //    }
                //}

                // Kill the kiosk mode app nicely - dont' force it
                if (!string.IsNullOrEmpty(this.activeDevice.KioskModePackageName))
                {
                    await this.activeDevice.TerminateApplicationAsync(this.activeDevice.KioskModePackageName, false);
                }
            }
        }

        private async Task SendVkeyByName(string name)
        {
            int code;
            if (this.vkeyNames.TryGetValue(name, out code))
            {
                await this.activeDevice.SendVkey(true, code);
                await this.activeDevice.SendVkey(false, code);
            }
        }

    }
}