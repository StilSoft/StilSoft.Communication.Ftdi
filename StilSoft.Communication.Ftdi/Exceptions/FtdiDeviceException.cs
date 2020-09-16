using System;

namespace StilSoft.Communication.Ftdi.Exceptions
{
    public class FtdiDeviceException : Exception
    {
        public FtdiDeviceException(string message)
            : base(message)
        {
        }

        public FtdiDeviceException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}