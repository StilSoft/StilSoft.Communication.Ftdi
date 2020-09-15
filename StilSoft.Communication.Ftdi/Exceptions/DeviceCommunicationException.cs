using System;

namespace StilSoft.Communication.Ftdi.Exceptions
{
    public class DeviceCommunicationException : DeviceException
    {
        public DeviceCommunicationException(string message)
            : base(message)
        {
        }

        public DeviceCommunicationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}