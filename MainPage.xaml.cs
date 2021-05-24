using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices;
using Windows.Devices.Enumeration;
using System.Collections.ObjectModel;
using Windows.UI.Core;
using Windows.Devices.Bluetooth;
using Windows.Security.Cryptography;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using System.Text;
using Windows.Storage.Streams;
using System.Diagnostics;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BLE
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        DeviceWatcher deviceWatcher;
        BluetoothLEDevice currentDevice;
        GattCharacteristic selectedCharacteristic;

        public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; set; } = new ObservableCollection<DiscoveredDevice>();
        public MainPage()
        {
            this.InitializeComponent();
            ProgressRing.IsActive = false;
            DataContext = this;
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            DiscoveredDevices.Clear();
            StartWatching();
        }

        void StartWatching()
        {
            if (deviceWatcher == null)
            {
                string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };
                string aqsAllBluetoothLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

                deviceWatcher =
                        DeviceInformation.CreateWatcher(
                            aqsAllBluetoothLEDevices,
                            requestedProperties,
                            DeviceInformationKind.AssociationEndpoint);

                deviceWatcher.Added += DeviceWatcher_Added;
                deviceWatcher.Updated += DeviceWatcher_Updated;
                deviceWatcher.Removed += DeviceWatcher_Removed;
                deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
                deviceWatcher.Stopped += DeviceWatcher_Stopped;
            }

            deviceWatcher.Start();
            ProgressRing.IsActive = true;
        }

        private void DeviceWatcher_Stopped(DeviceWatcher sender, object args)
        {
            Dispatch(() =>
            {
                ProgressRing.IsActive = false;
            });
        }

        private void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            Dispatch(() =>
            {
                ProgressRing.IsActive = false;
            });
        }

        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            Dispatch(() =>
            {
                DiscoveredDevices.Add(new DiscoveredDevice(args));
            });
        }

        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            Dispatch(() =>
            {
                var device = DiscoveredDevices.FirstOrDefault(x => x.Id == args.Id);
                if (device != null) device.Update(args);
            });
        }

        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            Dispatch(() =>
            {
                var device = DiscoveredDevices.FirstOrDefault(x => x.Id == args.Id);
                if (device != null) DiscoveredDevices.Remove(device);
            });
        }

        async void Dispatch(Action a)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    lock (this)
                    {
                        a();
                    }
                });
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                deviceWatcher.Stop();
            }
            catch { }

            TxtStatus.Text = "Connecting...";
            if (DevicesList.SelectedItem is DiscoveredDevice d)
            {
                currentDevice = await BluetoothLEDevice.FromIdAsync(d.Id);
                if (currentDevice == null)
                {
                    TxtStatus.Text = "Could not connect: device is null.";
                    return;
                }

                TxtStatus.Text = "Services:\n";
                var services = await currentDevice.GetGattServicesAsync();
                foreach (var service in services.Services)
                {
                    TxtStatus.Text += service.Uuid + "\n";
                }

                foreach (var service in services.Services)
                {
                    var items = await service.GetCharacteristicsAsync();
                    foreach (var characteristic in items.Characteristics)
                    {
                        Debug.WriteLine($"Service: {service.Uuid} - Characteristic: {characteristic.Uuid} - Properties: {characteristic.CharacteristicProperties}");
                        //if (readCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read))
                        {
                            //Debug.WriteLine("Read");
                            characteristic.ValueChanged += SelectedCharacteristic_ValueChanged;
                        }
                        if (characteristic.Uuid.ToString() == "bef8d6c9-9c21-4c9e-b632-bd58c1009f9f") ;// "00002af1-0000-1000-8000-00805f9b34fb")
                        selectedCharacteristic = characteristic;
                    }
                }
            }
        }

        async Task WriteAsync(GattCharacteristic characteristic, string command)
        {
            var writeBuffer = CryptographicBuffer.ConvertStringToBinary(command, BinaryStringEncoding.Utf8);

            try
            {
                // BT_Code: Writes the value from the buffer to the characteristic.
                var result = await characteristic.WriteValueAsync(writeBuffer);
                if (result == GattCommunicationStatus.Success)
                {
                    var value = await characteristic.ReadValueAsync();
                    TxtStatus.Text = $"Write to characteristic successful for: {command}\n" + TxtStatus.Text;
                    //TxtStatus.Text += $"Read value: {value?.Value?.ToString()}";
                    ReadValue(value.Value);
                    return;
                }
                else
                {
                    TxtStatus.Text = $"Write to characteristic fail for: {command} -- {result}\n" + TxtStatus.Text;
                    return;
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error writing characteristic: {ex}\n{TxtStatus.Text}";
            }
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            Send(TxtCommand.Text);
        }

        async void Send(string command)
        {
            await WriteAsync(selectedCharacteristic, command + "\r");
        }

        private void SelectedCharacteristic_ValueChanged(GattCharacteristic characteristic, GattValueChangedEventArgs args)
        {
            ReadValue(args.CharacteristicValue);
        }

        void ReadValue(IBuffer buffer)
        {
            if (buffer == null) return;
            byte[] data = new byte[buffer.Length];
            DataReader.FromBuffer(buffer).ReadBytes(data);
            var bytes = buffer.ToArray();
            var utf8 = Encoding.UTF8.GetString(bytes);
            Dispatch(() =>
            {
                TxtStatus.Text += $"Read: {utf8}";
            });
        }
    }
}
