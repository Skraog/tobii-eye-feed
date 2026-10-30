using System.Text;

namespace ReVision.EyeDevice.Platform.Server;

public class SExtendedDataStreamEnumerate : BasePacket
{
    public IReadOnlyList<ExtendedDataStreamDescription> DataStreamDescriptions { get; private init; }

    public SExtendedDataStreamEnumerate(Stream stream) : base(stream)
    {
        var dataStreamDescriptions = new List<ExtendedDataStreamDescription>();
        var (entryCount, _) = this.DecodeTreeList();

        for (int i = 0; i < entryCount; i++)
        {
            var prologue = this.DecodePrologue();

            if (prologue != (uint)PrologueType.EXTENDED_DATA_STREAM_ENUMERATION)
                throw new InvalidDataException($"Expected type {(uint)PrologueType.EXTENDED_DATA_STREAM_ENUMERATION} but got {prologue}");

            dataStreamDescriptions.Add(new ExtendedDataStreamDescription
            {
                OpCode = this.DecodeUInt32(),
                Description = this.DecodeString(),
                Unknown1 = this.DecodeString(),
                FeatureLock = this.DecodeUInt32()
            });
        }

        DataStreamDescriptions = dataStreamDescriptions;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"{nameof(SExtendedDataStreamEnumerate)} {{");
        sb.AppendLine($"  {nameof(DataStreamDescriptions)} = [");

        for (int i = 0; i < DataStreamDescriptions.Count; i++)
        {
            var d = DataStreamDescriptions[i];

            sb.AppendLine("    {");
            sb.AppendLine($"      {nameof(ExtendedDataStreamDescription.OpCode)} = {d.OpCode},");
            sb.AppendLine($"      {nameof(ExtendedDataStreamDescription.Description)} = \"{d.Description}\",");
            sb.AppendLine($"      {nameof(ExtendedDataStreamDescription.Unknown1)} = \"{d.Unknown1}\",");
            sb.AppendLine($"      {nameof(ExtendedDataStreamDescription.FeatureLock)} = {d.FeatureLock}");
            sb.AppendLine(i < DataStreamDescriptions.Count - 1 ? "    }," : "    }");
        }

        sb.AppendLine("  ]");
        sb.Append("}");

        return sb.ToString();
    }

    public class ExtendedDataStreamDescription
    {
        public uint OpCode { get; internal init; }
        public string Description { get; internal init; } = "";
        public string Unknown1 { get; internal init; } = "";
        public uint FeatureLock { get; internal init; }
    }
}
