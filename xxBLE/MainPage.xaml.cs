﻿using System;

using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Foundation;

using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace xxBLE
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private DeviceInformation Device;
        private GattCharacteristic character_now;
        private GattCharacteristic registeredCharacteristic;
        ASCIIEncoding ascii = new ASCIIEncoding();
        SerialDevice device_writer = null;
        SerialDevice device_reader = null;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private void FindDevice_Button_Click(object sender, RoutedEventArgs e)
        {
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };
            DeviceWatcher deviceWatcher =
                        DeviceInformation.CreateWatcher(
                                BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),
                                requestedProperties,
                                DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            // Added, Updated and Removed are required to get all nearby devices
            deviceWatcher.Added += new TypedEventHandler<DeviceWatcher, DeviceInformation>(async (watcher, devInfo) =>
            {

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (FindDevice_Name.Text == "" || devInfo.Name == FindDevice_Name.Text)
                    {
                        FindDevice_List.Text += String.Format("{0} : {1}\r\n", devInfo.Name, devInfo.Id);
                        ConnDevice_Id.Text = devInfo.Id;
                        Device = devInfo;
                    }
                });

            }); ;
            deviceWatcher.Updated += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>(async (watcher, devInfo) =>
            {

            }); ;
            deviceWatcher.Removed += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>(async (watcher, devInfo) =>
            {

            }); ;


            // EnumerationCompleted and Stopped are optional to implement.
            deviceWatcher.EnumerationCompleted += new TypedEventHandler<DeviceWatcher, Object>(async (watcher, obj) =>
            {
                Console.WriteLine(String.Format("EnumerationCompleted"));
            });
            deviceWatcher.Stopped += new TypedEventHandler<DeviceWatcher, Object>(async (watcher, obj) =>
            {
                Console.WriteLine(String.Format("deviceWatcher.Stopped"));
            }); ;

            // Start the watcher.
            deviceWatcher.Start();
        }

        private async void ConnDevice_Button_Click(object sender, RoutedEventArgs e)
        {
            BluetoothLEDevice bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(ConnDevice_Id.Text);

            if (bluetoothLeDevice == null)
            {
                FindDevice_List.Text += String.Format("{0} : Failed to connect to device.\r\n", ConnDevice_Id.Text);
                return;
            }

            GattDeviceServicesResult result = await bluetoothLeDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);
            int control_a = 0;
            if (result.Status == GattCommunicationStatus.Success)
            {
                var services = result.Services;
                foreach (var service in services)
                {   
                    FindDevice_List.Text += String.Format("{0} : {1} \r\n", bluetoothLeDevice.Name, service.Uuid);
                    var characteristics = await service.GetCharacteristicsAsync();
                    var characterCount = 0;
                    foreach (var characteristic in characteristics.Characteristics)
                    {
                        if (control_a == 5) character_now = characteristic;
                        control_a++;
                        if (characteristic.CharacteristicProperties.Equals(GattCharacteristicProperties.Notify))
                        {
                            ConnService_Id.Text = service.Uuid.ToString();
                        }
                        FindDevice_List.Text += String.Format("\t {0} : {1} : {2}\r\n", characterCount++, characteristic.UserDescription, characteristic.CharacteristicProperties);
                    }
                    
                }
            }
            else
            {
                FindDevice_List.Text += String.Format("{0} : {1}\r\n", bluetoothLeDevice.Name, "NOT FOUND ANY SERVICE");
            }

        }

        private async void ReadButton_Click(object sender, RoutedEventArgs e)
        {
            // BT_Code: Read the actual value from the device by using Uncached.
            GattReadResult result = await character_now.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (result.Status == GattCommunicationStatus.Success)
            {
                string format = FormatValue(result.Value);
                FindDevice_List.Text += String.Format("{0} : {1} : {2} : \r\n", character_now.CharacteristicProperties, "It is" , format);


            }
            else
            {
                FindDevice_List.Text += String.Format("{0} : {1}\r\n", character_now.CharacteristicProperties, "It isn't");
            }
        }


        private string FormatValue(IBuffer buffer)
        {
            CryptographicBuffer.CopyToByteArray(buffer, out byte[] data);
            try
            {
                return BitConverter.ToString(data);
            }
            catch (ArgumentException)
            {

                return "(error: Invalid ASCII string)";
            }
        }
        private bool subscribedForNotifications = false;
        private async void ValueChangedSubscribeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!subscribedForNotifications)
            {
                // initialize status
                GattCommunicationStatus status = GattCommunicationStatus.Unreachable;
                var cccdValue = GattClientCharacteristicConfigurationDescriptorValue.None;
                if (character_now.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
                {
                    cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Indicate;
                }

                else if (character_now.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
                {
                    cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;
                }

                try
                {
                    // BT_Code: Must write the CCCD in order for server to send indications.
                    // We receive them in the ValueChanged event handler.
                    status = await character_now.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);

                    if (status == GattCommunicationStatus.Success)
                    {
                        AddValueChangedHandler();
                        FindDevice_List.Text += String.Format("{0} \r\n", "Successfully subscribed for value changes");
                    }
                    else
                    {
                        FindDevice_List.Text += String.Format("{0} \r\n", "Error registering for value changes");
                    }
                }
                catch (ArgumentException)
                {
                    // This usually happens when a device reports that it support indicate, but it actually doesn't.
                    FindDevice_List.Text += String.Format("{0} \r\n", "Error Failed  1");
                }
            }
            else
            {
                try
                {
                    // BT_Code: Must write the CCCD in order for server to send notifications.
                    // We receive them in the ValueChanged event handler.
                    // Note that this sample configures either Indicate or Notify, but not both.
                    var result = await
                            character_now.WriteClientCharacteristicConfigurationDescriptorAsync(
                                GattClientCharacteristicConfigurationDescriptorValue.None);
                    if (result == GattCommunicationStatus.Success)
                    {
                        subscribedForNotifications = false;
                        RemoveValueChangedHandler();
                        FindDevice_List.Text += String.Format("{0} \r\n", "Successfully un-subscribed for value changes");
                    }
                    else
                    {
                        FindDevice_List.Text += String.Format("{0} \r\n", "Error un-registering for notifications");
                    }
                }
                catch (ArgumentException)
                {
                    // This usually happens when a device reports that it support notify, but it actually doesn't.
                    FindDevice_List.Text += String.Format("{0} \r\n", "Error Failed  2");
                }
            }
        }


        private void AddValueChangedHandler()
        {
            ValueChangedSubscribeToggle.Content = "Unsubscribe from value changes";
            if (!subscribedForNotifications)
            {
                registeredCharacteristic = character_now;
                registeredCharacteristic.ValueChanged += Characteristic_ValueChanged;
                subscribedForNotifications = true;
            }
        }

        private void RemoveValueChangedHandler()
        {
            ValueChangedSubscribeToggle.Content = "Subscribe to value changes";
            if (subscribedForNotifications)
            {
                registeredCharacteristic.ValueChanged -= Characteristic_ValueChanged;
                registeredCharacteristic = null;
                subscribedForNotifications = false;
            }
        }

        private async void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // BT_Code: An Indicate or Notify reported that the value has changed.
            // Display the new value with a timestamp.
            Windows.Storage.Streams.Buffer buf = new Windows.Storage.Streams.Buffer(args.CharacteristicValue.Length);
            await device_writer.OutputStream.WriteAsync(args.CharacteristicValue);
 /*         IBuffer readBuf = await device_reader.InputStream.ReadAsync(buf, buf.Capacity, new InputStreamOptions());
            string newValue = FormatValue(readBuf);
            var message = $"Value at {DateTime.Now:hh:mm:ss.FFF}: {newValue}";
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () => FindDevice_List.Text = message);
 */
        }

        private async void FindCom_Button_Click(object _, RoutedEventArgs e)
        {
            FindDevice_List.Text += "COM Device List: \r\n";
            int count = 0;
            foreach (var device in await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector()))
            {
                FindDevice_List.Text += $"  {count++} : {device.Name} \r\n";
            }   
        }

        private async void ConnCom_Button_Click(object _, RoutedEventArgs e)
        {
            var devices = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());

            device_writer = await SerialDevice.FromIdAsync(devices[0].Id);
            device_writer.BaudRate=57600;
            if (device_writer != null)
            {
                FindDevice_List.Text += $"Connect to  {device_writer.PortName}: {device_writer.BaudRate} \r\n";
            } 
            else
	        {
                FindDevice_List.Text += $"Connect to  {device_writer.PortName} Failed \r\n";
            }
 /*
            device_reader = await SerialDevice.FromIdAsync(devices[1].Id);
            if (device_reader != null)
            {
                FindDevice_List.Text += $"Connect to  {device_reader.PortName}: {device_reader.BaudRate} \r\n";
            }
            else
            {
                FindDevice_List.Text += $"Connect to  {device_reader.PortName} Failed \r\n";
            }
 */
        }


    }
}