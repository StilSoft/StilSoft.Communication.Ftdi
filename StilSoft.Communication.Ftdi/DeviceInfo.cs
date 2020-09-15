namespace StilSoft.Communication.Ftdi
{
    public class DeviceInfo
    {
        public string SerialNumber { get; }
        public string Description { get; }

        public DeviceInfo(string serialNumber, string description)
        {
            SerialNumber = serialNumber;
            Description = description;
        }
    }
}