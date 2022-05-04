using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DemoAssistant.Services;
using Xamarin.Forms;

namespace DemoAssistant.UWP
{
    /// <summary>
    /// Class to get the list of devices.  Will get re-done since
    /// the list can't be part of the package for regular use.
    /// </summary>
    class DeviceListStorage : IDeviceListStorage
    {
        public async Task<System.IO.Stream> GetDeviceListStreamAsync()
        {
            await Task.CompletedTask; // quiet the compiler warning.  Maybe this doesn't need to be async?

            Stream stream = GetResourceStreamAsync("Test Devices.xml");
            return stream;
        }

        public async Task<System.IO.Stream> GetTestScreenshotStreamAsync()
        {
            await Task.CompletedTask; // quiet the compiler warning.  Maybe this doesn't need to be async?

            Stream stream = null;
            
            return stream;
        }

        private Stream GetResourceStreamAsync(string assetName)
        {
            var assembly = typeof(DeviceListStorage).Assembly;
            var resourceNames = assembly.GetManifestResourceNames();
            Console.WriteLine(resourceNames);

            string name = $"{assembly.GetName().Name}.Assets.{assetName}";
            var stream = assembly.GetManifestResourceStream(name);

            return stream;
        }
    }
}