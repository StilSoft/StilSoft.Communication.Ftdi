using System;

namespace StilSoft.Communication.Ftdi.Exceptions
{
    public class FtdiDeviceCommunicationException : FtdiDeviceException
    {
        public FtdiDeviceCommunicationException(string message)
            : base(message)
        {
        }

        public FtdiDeviceCommunicationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}