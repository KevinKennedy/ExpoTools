using DemoAssistant.Services;
using ExpoHelpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace DemoAssistant.Views
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : TabbedPage
    {
        public MainPage()
        {
            InitializeComponent();

            this.ItemsSource = DependencyService.Get<ActiveDeviceList>().ActiveDevices;
        }

        public async void SettingsClicked(object sender, EventArgs args)
        {
            await Navigation.PushModalAsync(new NavigationPage(new SettingsPage()));
        }

        public async void LogClicked(object sender, EventArgs args)
        {
            var navigationPage = new NavigationPage(new LogView());
            NavigationPage.SetHasBackButton(navigationPage, true);
            await Navigation.PushModalAsync(navigationPage);
        }
    }
}