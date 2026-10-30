using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Threading.Channels;
using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;
using ReVision.Dispatch;
using ReVision.EyeDevice.Platform;
using ReVision.EyeDevice.Platform.Client;
using ReVision.EyeDevice.Platform.Server;

namespace ReVision.EyeDevice;

public sealed class EyeDevice : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop;
    private readonly CancellationToken _cancellationToken;
    private readonly IUsbDevice _usbDevice;
    private readonly UsbEndpointReader _reader;
    private readonly UsbEndpointWriter _writer;
    private readonly ImageDataDispatcher _imageDataDispatcher = new();
    private readonly Channel<BasePacket> _packetsToSend = Channel.CreateBounded<BasePacket>(32);
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<BasePacket>> _receiveTasks = new();
    private readonly Task[] _workers;
    private readonly TaskCompletionSource _transportReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _messageId;
    private int _failed;
    private int _disposed;
    private volatile bool _acceptImages;
    private string _stage = "Opening";
    private string? _error;

    // Realm material inherited unchanged from Blue-Doggo/ReVision-v2.
    private static readonly IReadOnlyDictionary<uint, RealmKey> RealmKeys = new Dictionary<uint, RealmKey>
    {
        [1001] = new(
            [0x70, 0xF0, 0x5E, 0xCF, 0x0A, 0x28, 0x66, 0x0C, 0xAD, 0x5A, 0x69, 0x85, 0xAA, 0x9F, 0x85, 0x82],
            [0x99, 0x30, 0xc7, 0x98, 0x19, 0x93, 0xda, 0x87, 0x3a, 0xde, 0x2b, 0x0a, 0x62, 0x19, 0xeb, 0x31])
    };

    public string SerialNumber { get; private set; } = "";
    public string DeviceModel { get; private set; } = "";
    public uint ProtocolVersion { get; private set; }
    public string InitializationStage => Volatile.Read(ref _stage);
    public string? InitializationError => Volatile.Read(ref _error);
    public bool HasFailed => Volatile.Read(ref _failed) != 0;

    public EyeDevice(IUsbDevice usbDevice, CancellationToken cancellationToken)
    {
        _usbDevice = usbDevice;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationToken = _stop.Token;
        try
        {
            _usbDevice.Open();
            _usbDevice.ClaimInterface(0);
            _reader = _usbDevice.OpenEndpointReader(ReadEndpointID.Ep03);
            _writer = _usbDevice.OpenEndpointWriter(WriteEndpointID.Ep05);
        }
        catch
        {
            _imageDataDispatcher.Dispose();
            _usbDevice.Dispose();
            _stop.Dispose();
            throw;
        }
        // Native reads use a dedicated thread and a bounded timeout, without a detached libusb event loop.
        var receive = Task.Factory.StartNew(() => Guard(() =>
        {
            _transportReady.Task.Wait(_cancellationToken);
            ReceiveLoop();
        }), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var send = Task.Run(async () =>
        {
            try
            {
                await foreach (var packet in _packetsToSend.Reader.ReadAllAsync(_cancellationToken))
                {
                    byte[] bytes = packet.Encode();
                    _writer.Write(bytes, 2000, out int written).ThrowOnError();
                    if (written != bytes.Length) throw new IOException("USB command was only partially written.");
                }
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested) { }
            catch (Exception error) { Fail(error); }
        });
        var initialize = Task.Run(async () =>
        {
            try { await InitializeAsync(); }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested) { }
            catch (Exception error) { Fail(error); }
        });
        _workers = [receive, send, initialize];
    }

    private void SetStage(string stage)
    {
        Volatile.Write(ref _stage, stage);
        Console.WriteLine($"EyeChip {SerialNumber}: {stage}.");
    }

    private void Fail(Exception error)
    {
        if (_cancellationToken.IsCancellationRequested || Interlocked.Exchange(ref _failed, 1) != 0) return;
        Volatile.Write(ref _error, error.GetBaseException().Message);
        SetStage("Camera transport failed");
        Console.Error.WriteLine($"EyeChip {SerialNumber}: {error}");
        foreach (var pending in _receiveTasks.Values) pending.TrySetException(error);
        _stop.Cancel();
    }

    private void Guard(Action action)
    {
        try { action(); }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested) { }
        catch (Exception error) { Fail(error); }
    }

    private uint NextMessageId() => unchecked((uint)Interlocked.Increment(ref _messageId));

    private Task<int> ControlTransferAsync(UsbSetupPacket setup, byte[] buffer, int offset, int length)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_usbDevice.ControlTransfer(setup, buffer, offset, length));
    }

    private async Task InitializeAsync()
    {
        SetStage("Resetting protocol channel");
        byte[] resetBuffer =
        [
            0x01, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x01, 0x00,
            0x00, 0x00, 0x04, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ];

        await ControlTransferAsync(new UsbSetupPacket
        {
            RequestType = (byte)(UsbCtrlFlags.RequestType_Vendor | UsbCtrlFlags.Recipient_Interface | UsbCtrlFlags.Direction_Out),
            Request = 48,
            Value = 0,
            Index = 0,
            Length = (short)resetBuffer.Length
        }, resetBuffer, 0, resetBuffer.Length);

        SetStage("Reading device information");
        await GetDeviceInformationAsync();
        SetStage("Reading timestamp method");
        await GetTimestampMethod();

        byte[] buffer = new byte[8];
        await ControlTransferAsync(new UsbSetupPacket
        {
            RequestType = (byte)(UsbCtrlFlags.RequestType_Vendor | UsbCtrlFlags.Recipient_Interface | UsbCtrlFlags.Direction_Out),
            Request = 65,
            Value = 0,
            Index = 0,
            Length = 0
        }, buffer, 0, 0);

        _transportReady.TrySetResult();
        SetStage("Negotiating protocol");
        await UpgradeProtocolAsync();
        SetStage("Reading tracker identity");
        await GetUnitInformationAsync();
        SetStage("Enumerating image streams");
        await EnumerateDataStreamsAsync();
        _acceptImages = true;
        _cancellationToken.ThrowIfCancellationRequested();
        SetStage("Image subscription requested; waiting for frames");
    }

    private async Task GetDeviceInformationAsync()
    {
        byte[] deviceInfoBuffer = new byte[512];

        await ControlTransferAsync(new UsbSetupPacket
        {
            RequestType = (byte)(UsbCtrlFlags.RequestType_Vendor | UsbCtrlFlags.Recipient_Interface | UsbCtrlFlags.Direction_In),
            Request = 48,
            Value = 0,
            Index = 0,
            Length = (short)deviceInfoBuffer.Length
        }, deviceInfoBuffer, 0, deviceInfoBuffer.Length);

        int stringLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(deviceInfoBuffer.AsSpan(0x14));
        if (stringLength < 0 || stringLength > deviceInfoBuffer.Length - 0x18)
            throw new InvalidDataException("Invalid device information string length.");
    }

    private async Task GetTimestampMethod()
    {
        try
        {
            byte[] buffer = new byte[8];

            await ControlTransferAsync(new UsbSetupPacket
            {
                RequestType = (byte)(UsbCtrlFlags.RequestType_Vendor | UsbCtrlFlags.Recipient_Interface |
                                     UsbCtrlFlags.Direction_In),
                Request = 70,
                Value = 0x0001,
                Index = 0,
                Length = 8
            }, buffer, 0, buffer.Length);

        }
        catch (UsbException)
        {
            // Older firmware does not implement this optional control request.
        }
    }

    private void ReceiveLoop()
    {
        byte[] buffer = new byte[16 * 1024];
        var framer = new PacketFramer(synchronizeOnUpgrade: true);
        while (!_cancellationToken.IsCancellationRequested)
        {
            Error error = _reader.Read(buffer, 500, out int length);
            _cancellationToken.ThrowIfCancellationRequested();
            if (length > 0) framer.Append(buffer.AsSpan(0, length), ProcessPacket);
            if (error != Error.Timeout) error.ThrowOnError();
        }
    }

    private void ProcessPacket(ReadOnlyMemory<byte> bytes)
    {
        if (!MemoryMarshal.TryGetArray(bytes, out var segment)) throw new InvalidDataException("Packet has no backing buffer.");
        using var stream = new MemoryStream(segment.Array!, segment.Offset, segment.Count, false);
        var packet = new BasePacket(stream);
        stream.Position = 0;
        if (packet.MessageType == 82 && _receiveTasks.TryRemove(packet.MessageId, out var response))
        {
            try
            {
                if (packet.StatusCode != 0) throw new IOException($"Command {packet.OpCode} failed with status {packet.StatusCode}.");
                BasePacket decoded = packet.OpCode switch
                {
                    OpCode.PROTOCOL_UPGRADE => new SUpgradeProtocol(stream),
                    OpCode.UNIT_INFORMATION => new SUnitInformation(stream),
                    OpCode.EXTENDED_DATA_STREAM_ENUMERATE => new SExtendedDataStreamEnumerate(stream),
                    OpCode.OPEN_REALM_REQUEST => new SOpenRealmRequest(stream),
                    OpCode.OPEN_REALM_WITH_SALT => new SOpenRealmWithSalt(stream),
                    _ => packet
                };
                response.TrySetResult(decoded);
            }
            catch (Exception error) { response.TrySetException(error); }
            return;
        }
        if (!_acceptImages || packet.OpCode != OpCode.EXTENDED_DATA_STREAM_WEARABLE_LIMITED_IMAGE) return;
        var image = new SWearableLimitedImage(stream);
        if (image.GroupConfigs.Length != image.ImageData.Count || image.ImageData.Count == 0)
            throw new InvalidDataException("Image region and pixel counts do not match.");
        for (int i = 0; i < image.ImageData.Count; i++)
        {
            var region = image.GroupConfigs[i];
            _imageDataDispatcher.Dispatch(new EyeFrame((ImageDataDispatcher.Eye)region.Eye,
                checked((int)region.Width), checked((int)region.Height), image.ImageData[i], image.TimestampTrackerUs));
        }
    }

    private async Task<T> SendAndRetrieveResponse<T>(BasePacket packet) where T : BasePacket
    {
        var response = new TaskCompletionSource<BasePacket>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_receiveTasks.TryAdd(packet.MessageId, response)) throw new InvalidOperationException("Duplicate command identifier.");
        try
        {
            await _packetsToSend.Writer.WriteAsync(packet, _cancellationToken);
            return (T)await response.Task.WaitAsync(TimeSpan.FromSeconds(10), _cancellationToken);
        }
        finally { _receiveTasks.TryRemove(packet.MessageId, out _); }
    }

    private async Task UpgradeProtocolAsync()
    {
        CUpgradeProtocol upgradeProtocol = new CUpgradeProtocol(NextMessageId(), [
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
        ProtocolVersion = upgradeProtocolResponse.ProtocolVersion;
    }

    private async Task GetUnitInformationAsync()
    {
        CUnitInformation unitInformation = new CUnitInformation(NextMessageId());
        SUnitInformation unitInformationResponse = await SendAndRetrieveResponse<SUnitInformation>(unitInformation);

        SerialNumber = unitInformationResponse.SerialNumber;
        DeviceModel = unitInformationResponse.DeviceModel;
        Console.WriteLine($"EyeChip {SerialNumber}: {DeviceModel}, firmware {unitInformationResponse.FirmwareVersion}.");
    }

    private async Task EnumerateDataStreamsAsync()
    {
        CExtendedDataStreamEnumerate extendedDataStreamEnumerate = new CExtendedDataStreamEnumerate(NextMessageId());
        SExtendedDataStreamEnumerate extendedDataStreamEnumerateResponse = await SendAndRetrieveResponse<SExtendedDataStreamEnumerate>(extendedDataStreamEnumerate);
        Console.WriteLine(extendedDataStreamEnumerateResponse);

        bool subscribed = false;
        foreach (var desc in extendedDataStreamEnumerateResponse.DataStreamDescriptions)
        {
            if (desc.Description != "Wearable Limited Image")
                continue;

            await SubscribeDataStreamAsync(desc);
            subscribed = true;
        }
        if (!subscribed) throw new NotSupportedException("This tracker does not advertise a supported camera stream.");
    }

    private async Task SubscribeDataStreamAsync(SExtendedDataStreamEnumerate.ExtendedDataStreamDescription dataStreamDescription)
    {
        Console.WriteLine($"Subscribing to {dataStreamDescription.Description} (opcode {dataStreamDescription.OpCode}, realm {dataStreamDescription.FeatureLock}).");
        if (dataStreamDescription.FeatureLock != 0)
            await OpenRealmWithSaltAsync(dataStreamDescription.FeatureLock);

        await _packetsToSend.Writer.WriteAsync(new CExtendedDataStreamSubscribe(NextMessageId(), dataStreamDescription.OpCode), _cancellationToken);
    }

    private async Task OpenRealmWithSaltAsync(uint realmId)
    {
        if (!RealmKeys.TryGetValue(realmId, out var realmKey))
        {
            throw new AuthenticationException($"No authorization key for realm {realmId} is available in this checkout. This stream cannot be opened.");
        }

        COpenRealmRequest openRealmRequest = new COpenRealmRequest(NextMessageId(), realmId);
        SOpenRealmRequest openRealmResponse = await SendAndRetrieveResponse<SOpenRealmRequest>(openRealmRequest);

        using HMAC hmac = new HMACMD5(realmKey.Key);
        byte[] challenge = hmac.ComputeHash(openRealmResponse.ChallengeData);

        COpenRealmWithSalt openRealmWithSaltRequest =
            new COpenRealmWithSalt(NextMessageId(), realmId, challenge, realmKey.Salt);
        SOpenRealmWithSalt openRealmWithSaltResponse = await SendAndRetrieveResponse<SOpenRealmWithSalt>(openRealmWithSaltRequest);
    }

    public ImageDataDispatcher GetImageDataDispatcher() => _imageDataDispatcher;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Cancel();
        _packetsToSend.Writer.TryComplete();
        _imageDataDispatcher.Dispose();
        await Task.WhenAll(_workers);
        // Endpoint wrappers share the device handle; disposing the device releases them.
        _usbDevice.Dispose();
        _stop.Dispose();
    }
}
