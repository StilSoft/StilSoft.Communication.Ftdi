using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using StilSoft.Communication.Ftdi.Exceptions;

namespace StilSoft.Communication.Ftdi
{
    public class FtdiDevice
    {
        private FtdiDeviceInternal _ftdiDevice;
        private DataBits _dataBits = DataBits.Eight;
        private StopBits _stopBits = StopBits.One;
        private Parity _parity = Parity.None;
        private int _baudRate = 9600;
        private int _writeTimeout;
        private int _readTimeout;

        public int BaudRate
        {
            get => _baudRate;
            set
            {
                _baudRate = value;

                SetBaudRate(_baudRate);
            }
        }

        public DataBits DataBits
        {
            get => _dataBits;
            set
            {
                if (!Enum.IsDefined(typeof(StopBits), value))
                    throw new ArgumentOutOfRangeException(nameof(StopBits), value, "");

                _dataBits = value;

                SetDataCaracteristics(_dataBits, _stopBits, _parity);
            }
        }

        public StopBits StopBits
        {
            get => _stopBits;
            set
            {
                if (!Enum.IsDefined(typeof(StopBits), value))
                    throw new ArgumentOutOfRangeException(nameof(StopBits), value, "");

                _stopBits = value;

                SetDataCaracteristics(_dataBits, _stopBits, _parity);
            }
        }

        public Parity Parity
        {
            get => _parity;
            set
            {
                if (!Enum.IsDefined(typeof(Parity), value))
                    throw new ArgumentOutOfRangeException(nameof(Parity), value, "");

                _parity = value;

                SetDataCaracteristics(_dataBits, _stopBits, _parity);
            }
        }

        public int WriteTimeout
        {
            get => _writeTimeout;
            set
            {
                _writeTimeout = value;

                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(DataBits));

                SetTimeouts(_writeTimeout, _readTimeout);
            }
        }

        public int ReadTimeout
        {
            get => _readTimeout;
            set
            {
                _readTimeout = value;

                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(DataBits));

                SetTimeouts(_writeTimeout, _readTimeout);
            }
        }

        public IReadOnlyList<DeviceInfo> GetDeviceList()
        {
            InitializeDevice();

            uint deviceCount = 0;

            var status = _ftdiDevice.GetNumberOfDevices(ref deviceCount);
            if (status != FT_STATUS.FT_OK)
                ThrowFtdiDeviceException("Unable to get number of devices");

            if (deviceCount <= 0)
                return new List<DeviceInfo>().AsReadOnly();

            var deviceList = new FT_DEVICE_INFO_NODE[deviceCount];

            status = _ftdiDevice.GetDeviceList(deviceList);
            if (status != FT_STATUS.FT_OK)
                ThrowFtdiDeviceException("Failed to get devices information");

            return deviceList.Select(device => new DeviceInfo(device.SerialNumber, device.Description)).ToList().AsReadOnly();
        }

        public async Task<IReadOnlyList<DeviceInfo>> GetDeviceListAsync()
        {
            return await Task.Run(GetDeviceList).ConfigureAwait(false);
        }

        public void OpenByIndex(int index)
        {
            if (IsOpen())
                ThrowFtdiDeviceException("Device is already open");

            Debug.WriteLine("Device Opening");

            InitializeDevice();

            var deviceListCount = GetDeviceList().Count;

            if (deviceListCount == 0 || deviceListCount <= index)
                throw new FtdiDeviceNotFoundException("Device not found");

            var status = _ftdiDevice.OpenByIndex((uint)index);
            if (status != FT_STATUS.FT_OK)
                ThrowFtdiDeviceException("Failed to open device");

            ConfigureDevice();

            Debug.WriteLine("Device Opened");
        }

        public async Task OpenByIndexAsync(int index)
        {
            await Task.Run(() => OpenByIndex(index)).ConfigureAwait(false);
        }

        public void OpenBySerialNumber(string serialNumber)
        {
            if (IsOpen())
                ThrowFtdiDeviceException("Device is already open");

            InitializeDevice();

            var deviceIndex = SearchDeviceBySerialNumber(serialNumber);

            OpenByIndex(deviceIndex);
        }

        public async Task OpenBySerialNumberAsync(string serialNumber)
        {
            await Task.Run(() => OpenBySerialNumber(serialNumber)).ConfigureAwait(false);
        }

        public void OpenByDescription(string description)
        {
            if (IsOpen())
                ThrowFtdiDeviceException("Device is already open");

            InitializeDevice();

            var deviceIndex = SearchDeviceByDescription(description);

            OpenByIndex(deviceIndex);
        }

        public async Task OpenByDescriptionAsync(string description)
        {
            await Task.Run(() => OpenByDescription(description)).ConfigureAwait(false);
        }

        public void Close()
        {
            if (!IsOpen())
                ThrowFtdiDeviceException("Device is closed");

            Debug.WriteLine("Device Closing");

            var status = _ftdiDevice.Close();
            if (status != FT_STATUS.FT_OK)
                ThrowFtdiDeviceException("Failed to close device");

            Debug.WriteLine("Device Closed");
        }

        public async Task CloseAsync()
        {
            await Task.Run(Close).ConfigureAwait(false);
        }

        public bool IsOpen()
        {
            return _ftdiDevice != null && _ftdiDevice.IsOpen;
        }

        public void Write(byte[] data)
        {
            if (!IsOpen())
                ThrowFtdiDeviceException("Device is closed");

            uint numBytesWritten = 0;

            var status = _ftdiDevice.Write(data, data.Length, ref numBytesWritten);
            if (status != FT_STATUS.FT_OK)
                ThrowFtdiDeviceCommunicationException("Failed to write to device");

            if (numBytesWritten != data.Length) // TODO rewrite (try to resend remain bytes if written bytes is < txData.Length )
                Debug.WriteLine("WRITE not completed");

            Debug.WriteLine("TxData: " + BitConverter.ToString(data, 0, data.Length).Replace("-", ""));
        }

        public async Task WriteAsync(byte[] dataBuffer)
        {
            await Task.Run(() => Write(dataBuffer)).ConfigureAwait(false);
        }

        public int Read(byte[] receiveBuffer, int numberOfBytesToRead)
        {
            if (!IsOpen())
                ThrowFtdiDeviceException("Device is closed");

            var sw = new Stopwatch();

            if (_readTimeout > 0)
                sw.Start();

            uint cntBytesRead = 0;
            var status = _ftdiDevice.Read(receiveBuffer, (uint)numberOfBytesToRead, ref cntBytesRead);
            if (status != FT_STATUS.FT_OK)
                ThrowFtdiDeviceCommunicationException("Failed to read from device");

            sw.Stop();

            if (cntBytesRead > 0)
            {
                Debug.WriteLine(
                    $"RxData: {BitConverter.ToString(receiveBuffer, 0, (int)cntBytesRead).Replace("-", "")} - elapsed time: {sw.Elapsed.TotalMilliseconds}");
            }

            if (_readTimeout > 0 && sw.ElapsedMilliseconds >= _readTimeout)
                Debug.WriteLine($"Read timeout. Elapsed time: {sw.Elapsed.TotalMilliseconds}");

            return (int)cntBytesRead;
        }

        public async Task<int> ReadAsync(byte[] dataBuffer, int numberOfBytesToRead)
        {
            return await Task.Run(() => Read(dataBuffer, numberOfBytesToRead)).ConfigureAwait(false);
        }

        public void ClearTransmitBuffer()
        {
            if (!IsOpen())
                ThrowFtdiDeviceException("Device is closed");

            var status = _ftdiDevice.Purge(FT_PURGE.FT_PURGE_TX);
            if (status != FT_STATUS.FT_OK)
                throw new FtdiDeviceCommunicationException("Failed to clear transmit buffer");
        }

        public async Task ClearTransmitBufferAsync()
        {
            await Task.Run(ClearTransmitBuffer).ConfigureAwait(false);
        }

        public void ClearReceiveBuffer()
        {
            if (!IsOpen())
                ThrowFtdiDeviceException("Device is closed");

            var status = _ftdiDevice.Purge(FT_PURGE.FT_PURGE_RX);
            if (status != FT_STATUS.FT_OK)
                throw new FtdiDeviceCommunicationException("Failed clear receive buffer");
        }

        public async Task ClearReceiveBufferAsync()
        {
            await Task.Run(ClearReceiveBuffer).ConfigureAwait(false);
        }

        private void InitializeDevice()
        {
            try
            {
                if (_ftdiDevice == null)
                    _ftdiDevice = new FtdiDeviceInternal();
            }
            catch
            {
                ThrowFtdiDeviceException("Unable to find device.\r\n" +
                                         "Check is device drivers are installed properly");
            }
        }

        private void ConfigureDevice()
        {
            InitializeDevice();

            if (!IsOpen())
                return;

            SetBaudRate(_baudRate);
            SetDataCaracteristics(_dataBits, _stopBits, _parity);
            SetTimeouts(_writeTimeout, _readTimeout);

            var status = _ftdiDevice.SetFlowControl(FT_FLOW_CONTROL.FT_FLOW_NONE, 0x00, 0x00);
            if (status != FT_STATUS.FT_OK)
                throw new FtdiDeviceConfigurationException("Failed to set flow control");

            status = _ftdiDevice.SetLatency(1);
            if (status != FT_STATUS.FT_OK)
                throw new FtdiDeviceConfigurationException("Failed to set latency");

            status = _ftdiDevice.SetBitMode(0x00, FT_BIT_MODES.FT_BIT_MODE_RESET);
            if (status != FT_STATUS.FT_OK)
                throw new FtdiDeviceConfigurationException("Failed to set bit mode");
        }

        private void SetTimeouts(int writeTimeout, int readTimeout)
        {
            InitializeDevice();

            if (!IsOpen())
                return;

            var status = _ftdiDevice.SetTimeouts((uint)readTimeout, (uint)writeTimeout);
            if (status != FT_STATUS.FT_OK)
                throw new FtdiDeviceConfigurationException("Failed to set timeouts");
        }

        private void SetBaudRate(int baudRate)
        {
            InitializeDevice();

            if (!IsOpen())
                return;

            var status = _ftdiDevice.SetBaudRate((uint)baudRate);
            if (status != FT_STATUS.FT_OK)
                throw new FtdiDeviceConfigurationException("Failed to set baud rate");
        }

        private void SetDataCaracteristics(DataBits dataBits, StopBits stopBits, Parity parity)
        {
            InitializeDevice();

            if (!IsOpen())
                return;

            byte ftdiStopBits;
            switch (stopBits)
            {
                case StopBits.One:
                    ftdiStopBits = 0;
                    break;
                case StopBits.Two:
                    ftdiStopBits = 2;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var status = _ftdiDevice.SetDataCharacteristics((byte)dataBits, ftdiStopBits, (byte)parity);
            if (status != FT_STATUS.FT_OK)
                throw new FtdiDeviceConfigurationException("Failed to set data characteristics");
        }

        private int SearchDeviceBySerialNumber(string serialNumber)
        {
            var deviceList = GetDeviceList();

            var deviceIndex = -1;
            for (var i = 0; i < deviceList.Count; i++)
            {
                if (string.Equals(deviceList[i].SerialNumber, serialNumber))
                    deviceIndex = i;
            }

            if (deviceIndex == -1)
                throw new FtdiDeviceNotFoundException("Device not found");

            return deviceIndex;
        }

        private int SearchDeviceByDescription(string description)
        {
            var deviceList = GetDeviceList();

            var deviceIndex = -1;
            for (var i = 0; i < deviceList.Count; i++)
            {
                if (string.Equals(deviceList[i].Description, description))
                    deviceIndex = i;
            }

            if (deviceIndex == -1)
                throw new FtdiDeviceNotFoundException("Device not found");

            return deviceIndex;
        }

        private void ThrowFtdiDeviceException(string message)
        {
            try
            {
                if (IsOpen())
                    Close();
            }
            catch
            {
                // ignored
            }
            finally
            {
                _ftdiDevice = null;
            }

            throw new FtdiDeviceException(message);
        }

        private void ThrowFtdiDeviceCommunicationException(string message)
        {
            try
            {
                if (IsOpen())
                    Close();
            }
            catch
            {
                // ignored
            }
            finally
            {
                _ftdiDevice = null;
            }

            throw new FtdiDeviceCommunicationException(message);
        }
    }
}