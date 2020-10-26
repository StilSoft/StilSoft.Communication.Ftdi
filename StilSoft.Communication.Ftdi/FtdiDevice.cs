using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StilSoft.Communication.Ftdi.Exceptions;

namespace StilSoft.Communication.Ftdi
{
    public class FtdiDevice
    {
        private const int DefaultInfiniteTimeout = -1;
        private readonly object _lock = new object();
        private FtdiDeviceInternal _ftdiDevice;
        private DataBits _dataBits = DataBits.Eight;
        private StopBits _stopBits = StopBits.One;
        private Parity _parity = Parity.None;
        private int _baudRate = 9600;
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
                    throw new ArgumentOutOfRangeException(nameof(DataBits), value, "");

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

        public int ReadTimeout
        {
            get => _readTimeout;
            set
            {
                _readTimeout = value;

                if (value < DefaultInfiniteTimeout)
                    throw new ArgumentOutOfRangeException(nameof(ReadTimeout));
            }
        }

        public IReadOnlyList<DeviceInfo> GetDeviceList()
        {
            lock (_lock)
            {
                InitializeDevice();

                uint deviceCount = 0;

                var status = _ftdiDevice.GetNumberOfDevices(ref deviceCount);
                if (status != FT_STATUS.FT_OK)
                    throw new FtdiDeviceException("Unable to get number of devices");

                if (deviceCount <= 0)
                    return new List<DeviceInfo>().AsReadOnly();

                var deviceList = new FT_DEVICE_INFO_NODE[deviceCount];

                status = _ftdiDevice.GetDeviceList(deviceList);
                if (status != FT_STATUS.FT_OK)
                    throw new FtdiDeviceException("Failed to get devices information");

                return deviceList.Select(device => new DeviceInfo(device.SerialNumber, device.Description)).ToList().AsReadOnly();
            }
        }

        public async Task<IReadOnlyList<DeviceInfo>> GetDeviceListAsync()
        {
            return await Task.Run(GetDeviceList).ConfigureAwait(false);
        }

        public void OpenByIndex(int index)
        {
            lock (_lock)
            {
                if (IsOpen())
                    throw new FtdiDeviceException("Device is already open");

                Debug.WriteLine("Device Opening");

                InitializeDevice();

                var deviceListCount = GetDeviceList().Count;

                if (deviceListCount == 0 || deviceListCount <= index)
                    throw new FtdiDeviceNotFoundException("Failed to open device");

                var status = _ftdiDevice.OpenByIndex((uint)index);
                if (status != FT_STATUS.FT_OK)
                    throw new FtdiDeviceException("Failed to open device");

                ConfigureDevice();

                Debug.WriteLine("Device Opened");
            }
        }

        public async Task OpenByIndexAsync(int index)
        {
            await Task.Run(() => OpenByIndex(index)).ConfigureAwait(false);
        }

        public void OpenBySerialNumber(string serialNumber)
        {
            lock (_lock)
            {
                if (IsOpen())
                    throw new FtdiDeviceException("Device is already open");

                var deviceIndex = SearchDeviceBySerialNumber(serialNumber);

                OpenByIndex(deviceIndex);
            }
        }

        public async Task OpenBySerialNumberAsync(string serialNumber)
        {
            await Task.Run(() => OpenBySerialNumber(serialNumber)).ConfigureAwait(false);
        }

        public void OpenByDescription(string description)
        {
            lock (_lock)
            {
                if (IsOpen())
                    throw new FtdiDeviceException("Device is already open");

                var deviceIndex = SearchDeviceByDescription(description);

                OpenByIndex(deviceIndex);
            }
        }

        public async Task OpenByDescriptionAsync(string description)
        {
            await Task.Run(() => OpenByDescription(description)).ConfigureAwait(false);
        }

        public void Close()
        {
            lock (_lock)
            {
                if (!IsOpen())
                    throw new FtdiDeviceException("Device is closed");

                Debug.WriteLine("Device Closing");

                var status = _ftdiDevice.Close();
                if (status != FT_STATUS.FT_OK)
                    throw new FtdiDeviceException("Failed to close device");

                CleanUp();
            }
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
            lock (_lock)
            {
                if (!IsOpen())
                    throw new FtdiDeviceException("Device is closed");

                uint numBytesWritten = 0;

                var status = _ftdiDevice.Write(data, data.Length, ref numBytesWritten);
                if (status != FT_STATUS.FT_OK)
                    throw new FtdiDeviceCommunicationException("Failed to write to device");

                //if (numBytesWritten != data.Length) // TODO (try to resend remain bytes if written bytes is < txData.Length )
                //    Debug.WriteLine("WRITE not completed");
            }
        }

        public async Task WriteAsync(byte[] dataBuffer)
        {
            await Task.Run(() => Write(dataBuffer)).ConfigureAwait(false);
        }

        public void Read(byte[] receiveBuffer, int numberOfBytesToRead, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (!IsOpen())
                    throw new FtdiDeviceException("Device is closed");

                var sw = new Stopwatch();
                var rxBuffTmp = new byte[numberOfBytesToRead];
                uint numberOfBytesReceived = 0;
                uint totalNumberOfBytesReceived = 0;

                if (_readTimeout > 0)
                    sw.Start();

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var status = _ftdiDevice.Read(rxBuffTmp, (uint)(numberOfBytesToRead - totalNumberOfBytesReceived), ref numberOfBytesReceived);
                    if (status != FT_STATUS.FT_OK)
                        throw new FtdiDeviceCommunicationException("Failed to read from device");

                    Array.Copy(rxBuffTmp, 0, receiveBuffer, totalNumberOfBytesReceived, numberOfBytesReceived);

                    totalNumberOfBytesReceived += numberOfBytesReceived;

                    if (totalNumberOfBytesReceived >= numberOfBytesToRead)
                        break;

                    if (_readTimeout == 0)
                        throw new FtdiDeviceTimeOutException("Read timeout");

                    if (sw.IsRunning && sw.Elapsed.TotalMilliseconds >= _readTimeout)
                    {
                        sw.Stop();

                        throw new FtdiDeviceTimeOutException("Read timeout");
                    }
                }

                sw.Stop();

                var receivedDataHexString = BitConverter.ToString(receiveBuffer, 0, (int)totalNumberOfBytesReceived).Replace("-", "");
                Debug.WriteLine($"RxData: {receivedDataHexString} - elapsed time: {sw.Elapsed.TotalMilliseconds}");
            }
        }

        public async Task ReadAsync(byte[] dataBuffer, int numberOfBytesToRead, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => Read(dataBuffer, numberOfBytesToRead, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        public void ClearTransmitBuffer()
        {
            lock (_lock)
            {
                if (!IsOpen())
                    throw new FtdiDeviceException("Device is closed");

                var status = _ftdiDevice.Purge(FT_PURGE.FT_PURGE_TX);
                if (status != FT_STATUS.FT_OK)
                    throw new FtdiDeviceException("Clear transmit buffer failed");
            }
        }

        public async Task ClearTransmitBufferAsync()
        {
            await Task.Run(ClearTransmitBuffer).ConfigureAwait(false);
        }

        public void ClearReceiveBuffer()
        {
            lock (_lock)
            {
                if (!IsOpen())
                    throw new FtdiDeviceException("Device is closed");

                var status = _ftdiDevice.Purge(FT_PURGE.FT_PURGE_RX);
                if (status != FT_STATUS.FT_OK)
                    throw new FtdiDeviceException("Clear receive buffer failed");
            }
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
                _ftdiDevice = null;

                throw new FtdiDeviceException("Unable to find FTDI device.\r\n" +
                                              "Check is FTDI device drivers are installed properly");
            }
        }

        private void ConfigureDevice()
        {
            lock (_lock)
            {
                SetBaudRate(_baudRate);
                SetDataCaracteristics(_dataBits, _stopBits, _parity);

                var status = _ftdiDevice.SetTimeouts(1, 0);
                if (status != FT_STATUS.FT_OK)
                    throw new FtdiDeviceConfigurationException("Failed to set timeouts");

                status = _ftdiDevice.SetFlowControl(FT_FLOW_CONTROL.FT_FLOW_NONE, 0x00, 0x00);
                if (status != FT_STATUS.FT_OK)
                    throw new FtdiDeviceConfigurationException("Failed to set flow control");

                status = _ftdiDevice.SetLatency(1);
                if (status != FT_STATUS.FT_OK)
                    throw new FtdiDeviceConfigurationException("Failed to set latency");

                status = _ftdiDevice.SetBitMode(0x00, FT_BIT_MODES.FT_BIT_MODE_RESET);
                if (status != FT_STATUS.FT_OK)
                    throw new FtdiDeviceConfigurationException("Failed to set bit mode");
            }
        }

        private void SetBaudRate(int baudRate)
        {
            lock (_lock)
            {
                if (!IsOpen())
                    return;

                var status = _ftdiDevice.SetBaudRate((uint)baudRate);
                if (status != FT_STATUS.FT_OK)
                    throw new FtdiDeviceConfigurationException("Failed to set baud rate");
            }
        }

        private void SetDataCaracteristics(DataBits dataBits, StopBits stopBits, Parity parity)
        {
            lock (_lock)
            {
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

        private void CleanUp()
        {
            Debug.WriteLine("Device CleanUp");

            try
            {
                if (_ftdiDevice != null)
                {
                    if (IsOpen())
                        Close();
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                _ftdiDevice?.Dispose();
            }
            catch
            {
                // ignored
            }
            finally
            {
                _ftdiDevice = null;
            }
        }
    }
}