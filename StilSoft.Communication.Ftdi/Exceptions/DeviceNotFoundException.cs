using System;

namespace StilSoft.Communication.Ftdi.Exceptions
{
    public class DeviceNotFoundException : DeviceException
    {
        public DeviceNotFoundException(string message)
            : base(message)
        {
        }

        public DeviceNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}