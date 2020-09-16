using System;

namespace StilSoft.Communication.Ftdi.Exceptions
{
    public class FtdiDeviceNotFoundException : FtdiDeviceException
    {
        public FtdiDeviceNotFoundException(string message)
            : base(message)
        {
        }

        public FtdiDeviceNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}