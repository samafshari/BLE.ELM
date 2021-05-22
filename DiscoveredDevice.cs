using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Devices.Enumeration;

namespace BLE
{
    public class DiscoveredDevice : ViewModel
    {
        public string Id { get; set; }
        public DeviceInformationKind Kind { get; set; }
        public string Name { get; set; }
        public string VisibleName { get; set; }
        public DiscoveredDevice() { }
        public DiscoveredDevice(DeviceInformation d)
        {
            Id = d.Id;
            Kind = d.Kind;
            Name = d.Name;
            VisibleName = Name;
            if (string.IsNullOrWhiteSpace(VisibleName))
                VisibleName = "(Unnamed Device)";
        }

        public void Update(DeviceInformationUpdate d)
        {
            Id = d.Id;
            Kind = d.Kind;
            RaisePropertyChanged(nameof(Id));
            RaisePropertyChanged(nameof(Kind));
        }
    }
}
