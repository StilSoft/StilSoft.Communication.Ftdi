using System;

namespace StilSoft.Communication.Ftdi.Exceptions
{
    public class FtdiDeviceTimeOutException : FtdiDeviceException
    {
        public FtdiDeviceTimeOutException(string message)
            : base(message)
        {
        }

        public FtdiDeviceTimeOutException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}