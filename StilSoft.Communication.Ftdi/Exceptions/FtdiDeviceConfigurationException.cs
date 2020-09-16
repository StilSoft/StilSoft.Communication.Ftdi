using System;

namespace StilSoft.Communication.Ftdi.Exceptions
{
    public class FtdiDeviceConfigurationException : FtdiDeviceException
    {
        public FtdiDeviceConfigurationException(string message)
            : base(message)
        {
        }

        public FtdiDeviceConfigurationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}