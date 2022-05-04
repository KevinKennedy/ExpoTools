using DemoAssistant.Services;
using ExpoHelpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using ZXing;

namespace DemoAssistant.Views
{
    public partial class ManageDevicesPage : ContentPage
    {
        public Command MoveUpCommand { get; }

        public Command MoveDownCommand { get; }

        public ObservableCollection<DeviceInformation> Devices { get; private set; }

        public ManageDevicesPage(ObservableCollection<DeviceInformation> devices)
        {
            this.Devices = devices;

            this.MoveUpCommand = new Command((param) =>
            {
                var info = (DeviceInformation)param;
                int index = this.Devices.IndexOf(info);
                if (index > 0)
                {
                    this.Devices.Move(index, index - 1);
                }
            });

            this.MoveDownCommand = new Command((param) =>
            {
                var info = (DeviceInformation)param;
                int index = this.Devices.IndexOf(info);
                if (index >= 0 && index < this.Devices.Count - 2)
                {
                    this.Devices.Move(index, index + 1);
                }
            });


            this.BindingContext = this;
            InitializeComponent();
        }

        private async void AddClicked(object sender, EventArgs e)
        {
            // Code we run if the user clicks save in the DeviceInformationEditor
            Action<DeviceInformation> onSave = (info) =>
            {
                info.IsChecked = true; // assume they want the new device visible
                this.Devices.Add(info);
                this.DeviceListView.ScrollTo(info, ScrollToPosition.MakeVisible, true);
            };

            await Navigation.PushModalAsync(new NavigationPage(new DeviceInformationEditor(new DeviceInformation(), onSave)));
        }

        private async void EditClicked(object sender, EventArgs e)
        {
            var selectedItem = this.DeviceListView.SelectedItem as DeviceInformation;
            if (selectedItem != null)
            {
                // Code we run if the user clicks save in the DeviceInformationEditor
                Action<DeviceInformation> onSave = (info) =>
                {
                    selectedItem.UpdateFrom(info);
                };

                // Edit a copy so if the user cancels the editor
                // we haven't changed anything in the selected item
                var scratchDeviceInfo = new DeviceInformation();
                scratchDeviceInfo.UpdateFrom(selectedItem);

                await Navigation.PushModalAsync(new NavigationPage(new DeviceInformationEditor(scratchDeviceInfo, onSave)));
            }
        }

        private void DeleteClicked(object sender, EventArgs e)
        {
            var selectedItem = this.DeviceListView.SelectedItem as DeviceInformation;
            if(selectedItem != null)
            {
                this.Devices.Remove(selectedItem);
            }
        }

        private async void ScanQrClicked(object sender, EventArgs e)
        {

            ILoggingService log = DependencyService.Get<ILoggingService>();

            var scanner = new ZXing.Mobile.MobileBarcodeScanner();

            var result = await scanner.Scan();

            if (result != null)
            {
                log.LogMessage(false, $"Read QR code.  Text length = {result.Text.Length}");
                var list = new DeviceList();
                list.LoadDeviceListFromString(result.Text);
                log.LogMessage(false, $"{list.DeviceInfos.Count} connections read");
                if (list.DeviceInfos.Count > 0)
                {
                    this.Devices.Clear();
                    foreach (var info in list.DeviceInfos)
                    {
                        this.Devices.Add(info);
                    }
                }
            }
            else
            {
                log.LogMessage(false, "No QR code read");
            }
        }

        public async void DoneClicked(object sender, EventArgs args)
        {
            await Navigation.PopModalAsync();
        }

    }
}