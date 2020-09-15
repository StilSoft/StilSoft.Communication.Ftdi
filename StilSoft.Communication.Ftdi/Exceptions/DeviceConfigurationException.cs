using System;

namespace StilSoft.Communication.Ftdi.Exceptions
{
    public class DeviceConfigurationException : DeviceException
    {
        public DeviceConfigurationException(string message)
            : base(message)
        {
        }

        public DeviceConfigurationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}