﻿using DemoAssistant.Services;
using ExpoHelpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace DemoAssistant.Views
{
    public partial class SettingsPage : ContentPage
    {
        public const string SettingsSavedMessageName = "SettingsSaved";

        public static readonly BindableProperty UserNameProperty =
            BindableProperty.Create(nameof(UserName), typeof(string), typeof(SettingsPage));

        public string UserName
        {
            get { return (string)this.GetValue(UserNameProperty); }
            set { this.SetValue(UserNameProperty, value); }
        }


        public static readonly BindableProperty PasswordProperty =
            BindableProperty.Create(nameof(Password), typeof(string), typeof(SettingsPage));

        public string Password
        {
            get { return (string)this.GetValue(PasswordProperty); }
            set { this.SetValue(PasswordProperty, value); }
        }


        public static readonly BindableProperty InstalledApplicationsProperty =
            BindableProperty.Create(nameof(InstalledApplications), typeof(IList<InstalledApplicationInfo>), typeof(SettingsPage),
                propertyChanged: (o, oldValue, newValue) =>
                    ((SettingsPage)o).InstalledApplicationsChanged((IList<InstalledApplicationInfo>)oldValue, (IList<InstalledApplicationInfo>)newValue));

        private void InstalledApplicationsChanged(IList<InstalledApplicationInfo> oldInstalledApplications, IList<InstalledApplicationInfo> newInstalledApplications)
        {
            this.UpdatePickedApp(AppPackageSetting.ExperienceApp, ExperienceApplicationProperty, newInstalledApplications);
            this.UpdatePickedApp(AppPackageSetting.TrainingApp, TrainingApplicationProperty, newInstalledApplications);
        }

        public IList<InstalledApplicationInfo> InstalledApplications
        {
            get { return (IList<InstalledApplicationInfo>)this.GetValue(InstalledApplicationsProperty); }
            set { this.SetValue(InstalledApplicationsProperty, value); }
        }


        public static readonly BindableProperty TrainingApplicationProperty =
            BindableProperty.Create(nameof(TrainingApplication), typeof(InstalledApplicationInfo), typeof(SettingsPage));

        public InstalledApplicationInfo TrainingApplication
        {
            get { return (InstalledApplicationInfo)this.GetValue(TrainingApplicationProperty); }
            set { this.SetValue(TrainingApplicationProperty, value); }
        }


        public static readonly BindableProperty ExperienceApplicationProperty =
            BindableProperty.Create(nameof(ExperienceApplication), typeof(InstalledApplicationInfo), typeof(SettingsPage));

        public InstalledApplicationInfo ExperienceApplication
        {
            get { return (InstalledApplicationInfo)this.GetValue(ExperienceApplicationProperty); }
            set { this.SetValue(ExperienceApplicationProperty, value); }
        }


        public static readonly BindableProperty ActiveDevicesTextProperty =
            BindableProperty.Create(nameof(ActiveDevicesText), typeof(string), typeof(SettingsPage));

        public string ActiveDevicesText
        {
            get { return (string)this.GetValue(ActiveDevicesTextProperty); }
            set { this.SetValue(ActiveDevicesTextProperty, value); }
        }


        public static readonly BindableProperty OptionalButtonsTextProperty =
            BindableProperty.Create(nameof(OptionalButtonsText), typeof(string), typeof(SettingsPage));

        public string OptionalButtonsText
        {
            get { return (string)this.GetValue(OptionalButtonsTextProperty); }
            set { this.SetValue(OptionalButtonsTextProperty, value); }
        }


        private DeviceCheckList deviceCheckList;
        private ObservableCollection<OptionalButtonInfo> optionalButtonsList;

        public SettingsPage()
        {
            InitializeComponent();
            this.BindingContext = this;

            // Make user explicitly press Cancel or Save
            NavigationPage.SetHasBackButton(this, false);

            this.UserName = AppSettings.DefaultUserName; 
            this.Password = AppSettings.DefaultPassword; 


            var deviceList = DependencyService.Get<DeviceList>();
            var activeDeviceList = DependencyService.Get<ActiveDeviceList>();

            this.deviceCheckList = new DeviceCheckList();
            this.deviceCheckList.CheckChanged += OnCheckChanged;
            this.deviceCheckList.Reset(deviceList.Devices);
            this.deviceCheckList.UpdateFromString(AppSettings.SelectedDevices);
            this.UpdateActiveDevicesText();

            this.optionalButtonsList = new ObservableCollection<OptionalButtonInfo>(DependencyService.Get<OptionalButtons>().CloneButtonsList());
            this.UpdateObtionalButtonsText();

            // Sometimes ActiveDeviceList.AllInstalledApplications takea a while to update.
            // Do a binding here to hopefully reduce confusion.
            // This binding also updates the UI for the currently picked apps
            this.SetBinding(InstalledApplicationsProperty, new Binding("AllInstalledApplications", BindingMode.OneWay, null, null, null, activeDeviceList));
        }

        private void OnCheckChanged(object sender, EventArgs e)
        {
            this.UpdateActiveDevicesText();
        }

        protected override bool OnBackButtonPressed()
        {
            return true; // stop back navigation
        }

        private async void SelectActiveDevicesClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SelectActiveDevicesPage(this.deviceCheckList.Items));
        }

        private async void SelectOptionalButtonsClicked(object sender, EventArgs e)
        {
            var page = new SelectOptionalButtonsPage(this.optionalButtonsList);
            page.Disappearing += this.SelectOptionalButtonsPageDisappearing;
            await Navigation.PushAsync(page);
        }

        private void SelectOptionalButtonsPageDisappearing(object sender, EventArgs args)
        {
            this.UpdateObtionalButtonsText();
        }

        public async void CancelClicked(object sender, EventArgs args)
        {
            await Navigation.PopModalAsync();
        }

        public async void SaveClicked(object sender, EventArgs args)
        {
            AppSettings.DefaultUserName = this.UserName;
            AppSettings.DefaultPassword = this.Password;

            AppSettings.SelectedDevices = this.deviceCheckList.GetSettingsString();
            var activeDeviceList = DependencyService.Get<ActiveDeviceList>();
            await activeDeviceList.UpdateActiveDevicesAsync(false, this.deviceCheckList.GetCheckedDevices());

            if(this.TrainingApplication != null)
            {
                AppSettings.SetAppPackageInfo(AppPackageSetting.TrainingApp, this.TrainingApplication.AppId, this.TrainingApplication.PackageName);
            }

            if(this.ExperienceApplication != null)
            {
                AppSettings.SetAppPackageInfo(AppPackageSetting.ExperienceApp, this.ExperienceApplication.AppId, this.ExperienceApplication.PackageName);
            }

            var optionalButtonService = DependencyService.Get<OptionalButtons>();
            optionalButtonService.Update(this.optionalButtonsList);
            AppSettings.EnabledButtons = optionalButtonService.GetSettingsString();

            MessagingCenter.Send<SettingsPage>(this, SettingsPage.SettingsSavedMessageName);
            await Navigation.PopModalAsync();
        }

        private void UpdateActiveDevicesText()
        {
            var sb = new StringBuilder();

            foreach(var item in this.deviceCheckList.Items)
            {
                if(item.IsChecked)
                {
                    sb.Append(item.DisplayName);
                    sb.Append(',');
                }
            }
            if(sb.Length > 0)
            {
                sb.Remove(sb.Length - 1, 1);
            }

            this.ActiveDevicesText = sb.ToString();
        }

        private void UpdateObtionalButtonsText()
        {
            var sb = new StringBuilder();

            foreach(var optionalButtonInfo in this.optionalButtonsList)
            {
                if(optionalButtonInfo.IsChecked)
                {
                    sb.Append(optionalButtonInfo.Description);
                    sb.Append(',');
                }
            }

            // remove trailing comma
            if(sb.Length > 0)
            {
                sb.Remove(sb.Length - 1, 1);
            }

            this.OptionalButtonsText = sb.ToString();
        }

        private void UpdatePickedApp(AppPackageSetting app, BindableProperty pickedAppProperty, IList<InstalledApplicationInfo> installedApplications)
        {
            InstalledApplicationInfo pickedApp = this.GetValue(pickedAppProperty) as InstalledApplicationInfo;

            // If we already have something set, don't do anything
            if (pickedApp != null)
            {
                return;
            }

            // See if we can find the app in AppSettings for this property
            string appId;
            string packageName;
            AppSettings.GetAppPackageInfo(app, out appId, out packageName);
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(packageName))
            {
                // no app set in settings
                return;
            }

            // Try to find the app in the list.
            InstalledApplicationInfo installedApplicationInfo = installedApplications.FirstOrDefault((info) => (appId == info.AppId) && (packageName == info.PackageName));
            if (installedApplicationInfo != null)
            {
                this.SetValue(pickedAppProperty, installedApplicationInfo);
                return;
            }

            // Couldn't find a matching app.  Just leave us as null - we won't overwrite the previous setting
            // even if the presses "save"
        }
    }
}