namespace ReVision.EyeDevice.Platform.Server;

public class SWearableLimitedImage : BasePacket
{
    public long TimestampTrackerUs { get; private set; }
    public RegionGroupConfig[] GroupConfigs { get; private set; }
    public LedConfig[] LedConfigs { get; private set; }
    public List<byte[]> ImageData { get; } = new();

    public SWearableLimitedImage(Stream stream) : base(stream)
    {
        var currentRow = 0;
        var rowCount = this.DecodeTreeRow(PrologueType.TREE_XDS_ROW);

        while (rowCount > currentRow)
        {
            switch (this.DecodeTreeColumn(PrologueType.TREE_XDS_COLUMN))
            {
                case (uint)WearableColumnType.TIMESTAMP_TRACKER_US:
                    TimestampTrackerUs = this.DecodeI64();
                    break;
                case (uint)WearableColumnType.UNKNOWN0:
                    this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.UNKNOWN1:
                    this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.UNKNOWN2:
                    this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.UNKNOWN3:
                    this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.UNKNOWN4:
                    this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.UNKNOWN5:
                    this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.REGION_GROUP_CONFIG:
                    GroupConfigs = DecodeRegionGroupConfig();
                    break;
                case (uint)WearableColumnType.UNKNOWN6:
                    this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.UNKNOWN7:
                    this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.UNKNOWN8:
                    this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.UNKNOWN9:
                    this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.UNKNOWN10:
                    this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.LED_CONFIG:
                    LedConfigs = DecodeLedConfig();
                    break;
                case (uint)WearableColumnType.IMAGE_DATA:
                    ImageData.Add(this.DecodeBlob());
                    break;
            }

            currentRow++;
        }
    }
    
    private RegionGroupConfig[] DecodeRegionGroupConfig()
    {
        var (entryCount, _) = this.DecodeTreeList();
        RegionGroupConfig[] results = new RegionGroupConfig[entryCount];

        for (int i = 0; i < entryCount; i++)
        {
            var prologue = this.DecodePrologue();
            
            if (prologue != (uint)PrologueType.EXTENDED_DATA_STREAM_GROUP_CONFIG)
                throw new InvalidDataException($"Expected type {(uint)PrologueType.EXTENDED_DATA_STREAM_GROUP_CONFIG} but got {prologue}");

            results[i] = new RegionGroupConfig
            {
                Eye = (Eye)this.DecodeUInt32(),
                Unknown1 = this.DecodeF32(),
                Unknown2 = this.DecodeF32(),
                Unknown3 = this.DecodeUInt32(),
                Unknown4 = this.DecodeUInt32(),
                Width = this.DecodeUInt32(),
                Height = this.DecodeUInt32(),
                Unknown5 = this.DecodeUInt32(),
                Unknown6 = this.DecodeUInt32(),
            };
        }

        return results;
    }
    
    private LedConfig[] DecodeLedConfig()
    {
        var (entryCount, _) = this.DecodeTreeList();
        LedConfig[] results = new LedConfig[entryCount];

        for (int i = 0; i < entryCount; i++)
        {
            var prologue = this.DecodePrologue();
            
            if (prologue != (uint)PrologueType.EXTENDED_DATA_STREAM_LED_CONFIG)
                throw new InvalidDataException($"Expected type {(uint)PrologueType.EXTENDED_DATA_STREAM_LED_CONFIG} but got {prologue}");

            results[i] = new LedConfig
            {
                Id = this.DecodeUInt32(),
                Unknown0 = this.DecodeUInt32(),
                Unknown1 = this.DecodeUInt32(),
                Unknown2 = this.DecodeF32()
            };
        }

        return results;
    }

    public class RegionGroupConfig
    {
        public Eye Eye { get; internal set; }
        public float Unknown1 { get; internal set; }
        public float Unknown2 { get; internal set; }
        public uint Unknown3 { get; internal set; }
        public uint Unknown4 { get; internal set; }
        public uint Width { get; internal set; }
        public uint Height { get; internal set; }
        public uint Unknown5 { get; internal set; }
        public uint Unknown6 { get; internal set; }
    }

    public enum Eye
    {
        Left = 0,
        Right = 1,
    }

    public class LedConfig
    {
        public uint Id { get; internal set; }
        public uint Unknown0 { get; internal set; }
        public uint Unknown1 { get; internal set; }
        public float Unknown2 { get; internal set; }
    }

    private enum WearableColumnType : uint
    {
        TIMESTAMP_TRACKER_US = 1,
        UNKNOWN0 = 2,
        UNKNOWN1 = 3,
        UNKNOWN2 = 4,
        UNKNOWN3 = 5,
        UNKNOWN4 = 6,
        UNKNOWN5 = 7,
        REGION_GROUP_CONFIG = 8,
        UNKNOWN6 = 9,
        UNKNOWN7 = 10,
        UNKNOWN8 = 11,
        UNKNOWN9 = 12,
        UNKNOWN10 = 13,
        LED_CONFIG = 14,
        IMAGE_DATA = 15,
    }
}