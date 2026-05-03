using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using CommunityToolkit.HighPerformance;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;
using ReVision.Dispatch;
using ReVision.EyeDevice.Platform;
using ReVision.EyeDevice.Platform.Client;
using ReVision.EyeDevice.Platform.Server;

namespace ReVision.EyeDevice;

public class EyeDevice
{
    public string SerialNumber { get; private set; }
    public string DeviceModel { get; private set; }
    
    private CancellationTokenSource _cancellationTokenSource;
    private CancellationToken _cancellationToken;
    
    private IUsbDevice _usbDevice;
    private UsbEndpointReader _reader;
    private UsbEndpointWriter _writer;

    private IReadOnlyDictionary<uint, RealmKey> _realmKeys = new Dictionary<uint, RealmKey>
    {
        {
            1001, new RealmKey
            {
                Key = [0x70, 0xF0, 0x5E, 0xCF, 0x0A, 0x28, 0x66, 0x0C, 0xAD, 0x5A, 0x69, 0x85, 0xAA, 0x9F, 0x85, 0x82],
                Salt = [0x99, 0x30, 0xc7, 0x98, 0x19, 0x93, 0xda, 0x87, 0x3a, 0xde, 0x2b, 0x0a, 0x62, 0x19, 0xeb, 0x31]
            }
        }
    };

    private IReadOnlyDictionary<string, string> _deviceInformation;
    
    private Channel<BasePacket> _packetsToSend = Channel.CreateUnbounded<BasePacket>();
    private ConcurrentDictionary<uint, TaskCompletionSource<BasePacket>> _receiveTasks = new ConcurrentDictionary<uint, TaskCompletionSource<BasePacket>>();
    
    private uint _messageId;
    private uint _protocolVersion;
    private TimestampMethod _timestampMethod;
    
    private ImageDataDispatcher _imageDataDispatcher;

    public EyeDevice(IUsbDevice usbDevice, CancellationToken linkedCancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(linkedCancellationToken);
        _cancellationToken = _cancellationTokenSource.Token;
        
        _usbDevice = usbDevice;
        _imageDataDispatcher = new ImageDataDispatcher();

        _usbDevice.Open();
        _usbDevice.ClaimInterface(0);

        _reader = _usbDevice.OpenEndpointReader(ReadEndpointID.Ep03);
        _writer = _usbDevice.OpenEndpointWriter(WriteEndpointID.Ep05);

        Task.Run(MessageSendQueue, _cancellationToken).ContinueWith(tsk =>
        {
            if (tsk.IsFaulted)
                Console.WriteLine($"Free-floating Task failed with exception: {tsk.Exception}");
        });
        Task.Run(MessageReceiveQueue, _cancellationToken).ContinueWith(tsk =>
        {
            if (tsk.IsFaulted)
                Console.WriteLine($"Free-floating Task failed with exception: {tsk.Exception}");
        });
        Task.Run(InitializeAsync, _cancellationToken).ContinueWith(tsk =>
        {
            if (tsk.IsFaulted)
                Console.WriteLine($"Free-floating Task failed with exception: {tsk.Exception}");
        });
    }

    private async Task InitializeAsync()
    {
        byte[] resetBuffer =
        [
            0x01, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x01, 0x00,
            0x00, 0x00, 0x04, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ];

        await _usbDevice.ControlTransferAsync(new UsbSetupPacket
        {
            RequestType = (byte)(UsbCtrlFlags.RequestType_Vendor | UsbCtrlFlags.Recipient_Interface | UsbCtrlFlags.Direction_Out),
            Request = 48,
            Value = 0,
            Index = 0,
            Length = (short)resetBuffer.Length
        }, resetBuffer, 0, resetBuffer.Length);

        await GetDeviceInformationAsync();
        await GetTimestampMethod();
        
        byte[] buffer = new byte[8];
        await _usbDevice.ControlTransferAsync(new UsbSetupPacket
        {
            RequestType = (byte)(UsbCtrlFlags.RequestType_Vendor | UsbCtrlFlags.Recipient_Interface | UsbCtrlFlags.Direction_Out),
            Request = 65,
            Value = 0,
            Index = 0,
            Length = 0
        }, buffer, 0, 0);

        await UpgradeProtocolAsync();
        await GetUnitInformationAsync();
        await EnumerateDataStreamsAsync();
    }
    
    private async Task GetDeviceInformationAsync()
    {
        byte[] deviceInfoBuffer = new byte[512];
        
        await _usbDevice.ControlTransferAsync(new UsbSetupPacket
        {
            RequestType = (byte)(UsbCtrlFlags.RequestType_Vendor | UsbCtrlFlags.Recipient_Interface | UsbCtrlFlags.Direction_In),
            Request = 48,
            Value = 0,
            Index = 0,
            Length = (short)deviceInfoBuffer.Length
        }, deviceInfoBuffer, 0, deviceInfoBuffer.Length);
        
        int stringLength = BitConverter.ToInt32(deviceInfoBuffer, 0x14);
        string deviceInfo = Encoding.ASCII.GetString(deviceInfoBuffer, 0x18, stringLength);
        
        string[] deviceInfoSplit = deviceInfo.Split("\n", StringSplitOptions.RemoveEmptyEntries);
        
        Dictionary<string, string> deviceInfoDictionary = new Dictionary<string, string>();
        
        // Why are we using java properties format???
        foreach (string deviceInfoLine in deviceInfoSplit)
        {
            string[] deviceInfoLineSplit = deviceInfoLine.Split("=", StringSplitOptions.RemoveEmptyEntries);

            if (deviceInfoLineSplit.Length != 2)
                continue;
            
            deviceInfoDictionary.Add(deviceInfoLineSplit[0], deviceInfoLineSplit[1]);
        }
        
        _deviceInformation = deviceInfoDictionary;
    }

    private async Task GetTimestampMethod()
    {
        try
        {
            byte[] buffer = new byte[8];

            await _usbDevice.ControlTransferAsync(new UsbSetupPacket
            {
                RequestType = (byte)(UsbCtrlFlags.RequestType_Vendor | UsbCtrlFlags.Recipient_Interface |
                                     UsbCtrlFlags.Direction_In),
                Request = 70,
                Value = 0x0001,
                Index = 0,
                Length = 8
            }, buffer, 0, 0);

            _timestampMethod = TimestampMethod.Ctrl1;
        }
        catch (Exception)
        {
            _timestampMethod = TimestampMethod.Legacy;
        }
    }

    private async Task MessageReceiveQueue()
    {
        byte[] receiveBuffer = new byte[1024 * 16];
        byte[] reassembleBuffer = new byte[1024 * 16];

        while (!_cancellationToken.IsCancellationRequested)
        {
            Stream? dataStream = null;
            BasePacket? receivedPacket = null;
            
            var (transferError, transferLength) = await _reader.ReadAsync(receiveBuffer, 0, receiveBuffer.Length, 0);
            transferError.ThrowOnError();

            if (!BasePacket.IsFullPacket(receiveBuffer, out var packetLength))
            {
                if (reassembleBuffer.Length < packetLength)
                    Array.Resize(ref reassembleBuffer, (int)packetLength);
                
                int totalReceived = transferLength;
                Array.Copy(receiveBuffer, 0, reassembleBuffer, 0, transferLength);

                while (totalReceived < packetLength)
                {
                    (transferError, transferLength) = await _reader.ReadAsync(receiveBuffer, 0, receiveBuffer.Length, 0);
                    transferError.ThrowOnError();
                    
                    Array.Copy(receiveBuffer, 0, reassembleBuffer, totalReceived, transferLength);
                    totalReceived += transferLength;
                }

                dataStream = new Memory<byte>(reassembleBuffer, 0, totalReceived).AsStream();
                receivedPacket = new BasePacket(dataStream);
            }
            else
            {
                dataStream = new Memory<byte>(receiveBuffer, 0, transferLength).AsStream();
                receivedPacket = new BasePacket(dataStream);
            }
            
            dataStream.Seek(0, SeekOrigin.Begin);

            if (!_receiveTasks.TryRemove(receivedPacket.MessageId, out var task))
            {
                ProcessData(receivedPacket, dataStream);
                continue;
            }

            switch (receivedPacket.OpCode)
            {
                case OpCode.PROTOCOL_UPGRADE:
                    task.SetResult(new SUpgradeProtocol(dataStream));
                    break;
                case OpCode.EXTENDED_DATA_STREAM_ENUMERATE:
                    task.SetResult(new SExtendedDataStreamEnumerate(dataStream));
                    break;
                case OpCode.UNIT_INFORMATION:
                    task.SetResult(new SUnitInformation(dataStream));
                    break;
                case OpCode.OPEN_REALM_REQUEST:
                    task.SetResult(new SOpenRealmRequest(dataStream));
                    break;
                case OpCode.OPEN_REALM_WITH_SALT:
                    task.SetResult(new SOpenRealmWithSalt(dataStream));
                    break;
                default:
                    task.SetResult(receivedPacket);
                    break;
            }
        }
    }

    private void ProcessData(BasePacket receivedPacket, Stream dataStream)
    {
        switch (receivedPacket.OpCode)
        {
            case (OpCode)1280:
                Console.WriteLine($"Received GAZE from {SerialNumber} ({DeviceModel})");
                break;
            case OpCode.EXTENDED_DATA_STREAM_IMAGE:
                Console.WriteLine($"Received EXTENDED_DATA_STREAM_IMAGE from {SerialNumber} ({DeviceModel})");
                break;
            case OpCode.EXTENDED_DATA_STREAM_WEARABLE:
                var wearableData = new SWearable(dataStream);
                break;
            case OpCode.EXTENDED_DATA_STREAM_WEARABLE_LIMITED_IMAGE:
                var wearableLimitedImage = new SWearableLimitedImage(dataStream);
                _imageDataDispatcher.Dispatch((ImageDataDispatcher.Eye)wearableLimitedImage.GroupConfigs[0].Eye,
                    wearableLimitedImage.ImageData[0]);
                break;
            default:
                break;
        }
    }

    private async Task MessageSendQueue()
    {
        while (!_cancellationToken.IsCancellationRequested)
        {
            await foreach (BasePacket packet in _packetsToSend.Reader.ReadAllAsync(_cancellationToken))
            {
                var packetData = packet.Encode();

                var (transferError, transferLength) = await _writer.WriteAsync(packetData, 0, packetData.Length, 0);
                transferError.ThrowOnError();
            }
        }
    }

    private Task<T> SendAndRetrieveResponse<T>(BasePacket packet, int timeout = 30000) where T: BasePacket
    {
        TaskCompletionSource<BasePacket> tcs = new();
        new CancellationTokenSource(timeout).Token.Register(() => tcs.TrySetCanceled());
        _receiveTasks.TryAdd(packet.MessageId, tcs);
        _packetsToSend.Writer.TryWrite(packet);

        return tcs.Task.ContinueWith(t => (T)t.Result, _cancellationToken);
    }
    
    private void Send(BasePacket packet)
    {
        _packetsToSend.Writer.TryWrite(packet);
    }

    private async Task UpgradeProtocolAsync()
    {
        CUpgradeProtocol upgradeProtocol = new CUpgradeProtocol(++_messageId, [
            65536,
            65537,
            65538,
            65539,
            65540,
            65541,
            65542,
            65543,
            65544
        ]);

        SUpgradeProtocol upgradeProtocolResponse = await SendAndRetrieveResponse<SUpgradeProtocol>(upgradeProtocol);
        _protocolVersion = upgradeProtocolResponse.ProtocolVersion;
    }
    
    private async Task GetUnitInformationAsync()
    {
        CUnitInformation unitInformation = new CUnitInformation(++_messageId);
        SUnitInformation unitInformationResponse = await SendAndRetrieveResponse<SUnitInformation>(unitInformation);

        SerialNumber = unitInformationResponse.SerialNumber;
        DeviceModel = unitInformationResponse.DeviceModel;
        Console.WriteLine(unitInformationResponse);
    }

    private async Task EnumerateDataStreamsAsync()
    {
        CExtendedDataStreamEnumerate extendedDataStreamEnumerate = new CExtendedDataStreamEnumerate(++_messageId);
        SExtendedDataStreamEnumerate extendedDataStreamEnumerateResponse = await SendAndRetrieveResponse<SExtendedDataStreamEnumerate>(extendedDataStreamEnumerate);
        Console.WriteLine(extendedDataStreamEnumerateResponse);

        foreach (var desc in extendedDataStreamEnumerateResponse.DataStreamDescriptions)
        {
            if (desc.Description != "Wearable Limited Image"
                //&& desc.Description != "Wearable"
                && (desc.Description != "Image" || DeviceModel != "VR4_Yoda_P1")
                && (desc.Description != "image" || DeviceModel != "IS5_Large_Eyetracker_5")) // HACK: VR4_YODA_P1 predates VR4_U2_P2
                continue;

            await SubscribeDataStreamAsync(desc);
        }
    }

    private async Task SubscribeDataStreamAsync(SExtendedDataStreamEnumerate.ExtendedDataStreamDescription dataStreamDescription)
    {
        if (dataStreamDescription.FeatureLock != 0)
            await OpenRealmWithSaltAsync(dataStreamDescription.FeatureLock);
        
        Send(new CExtendedDataStreamSubscribe(++_messageId, dataStreamDescription.OpCode));
    }

    private async Task OpenRealmWithSaltAsync(uint realmId)
    {
        if (!_realmKeys.TryGetValue(realmId, out var realmKey))
        {
            KeyWarning.KeyWarning keyWarning = new KeyWarning.KeyWarning();
            await keyWarning.WaitForApplicationExit();
            throw new AuthenticationException();
        }
        
        COpenRealmRequest openRealmRequest = new COpenRealmRequest(++_messageId, realmId);
        SOpenRealmRequest openRealmResponse = await SendAndRetrieveResponse<SOpenRealmRequest>(openRealmRequest);

        using HMAC hmac = new HMACMD5(realmKey.Key);
        byte[] challenge = hmac.ComputeHash(openRealmResponse.ChallengeData);

        COpenRealmWithSalt openRealmWithSaltRequest =
            new COpenRealmWithSalt(++_messageId, realmId, challenge, realmKey.Salt);
        SOpenRealmWithSalt openRealmWithSaltResponse = await SendAndRetrieveResponse<SOpenRealmWithSalt>(openRealmWithSaltRequest);
    }

    public ImageDataDispatcher GetImageDataDispatcher()
    {
        return _imageDataDispatcher;
    }
}