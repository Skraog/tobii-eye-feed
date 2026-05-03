using System.Numerics;
using System.Text;

namespace ReVision.EyeDevice.Platform.Server;

public class SWearable : BasePacket
{
    public long TimestampTrackerUs { get; private set; }
    public uint FrameCount { get; private set; }
    
    public EyeData LeftEyeData  { get; private set; }
    public EyeData RightEyeData  { get; private set; }
    public EyeData CombinedEyeData { get; private set; }
    
    public SWearable(Stream stream) : base(stream)
    {
        var currentRow = 0;
        var rowCount = this.DecodeTreeRow(PrologueType.TREE_XDS_ROW);
        
        EyeData leftEyeData = new EyeData();
        EyeData rightEyeData = new EyeData();
        EyeData combinedEyeData = new EyeData();

        while (rowCount > currentRow)
        {
            switch (this.DecodeTreeColumn(PrologueType.TREE_XDS_COLUMN))
            {
                case (uint)WearableColumnType.TIMESTAMP_TRACKER_US:
                    TimestampTrackerUs = this.DecodeI64();
                    break;
                case (uint)WearableColumnType.GAZE_ORIGIN_MM_LEFT:
                    leftEyeData.GazeOriginMm = this.DecodeVector3();
                    break;
                case (uint)WearableColumnType.GAZE_ORIGIN_MM_LEFT_VALIDITY:
                    leftEyeData.GazeOriginMmValidity = (Validity)this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.GAZE_DIRECTION_NORMALIZED_LEFT:
                    leftEyeData.GazeDirectionNormalized = this.DecodeVector3();
                    break;
                case (uint)WearableColumnType.PUPIL_DIAMETER_MM_LEFT_VALIDITY:
                    leftEyeData.PupilDiameterMmValidity = (Validity)this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.PUPIL_DIAMETER_MM_LEFT:
                    leftEyeData.PupilDiameterMm = this.DecodeF32();
                    break;
                case (uint)WearableColumnType.UNKNOWN0:
                    this.DecodeF32();
                    break;
                case (uint)WearableColumnType.GAZE_ORIGIN_MM_RIGHT:
                    rightEyeData.GazeOriginMm = this.DecodeVector3();
                    break;
                case (uint)WearableColumnType.GAZE_ORIGIN_MM_RIGHT_VALIDITY:
                    rightEyeData.GazeOriginMmValidity = (Validity)this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.GAZE_DIRECTION_NORMALIZED_RIGHT:
                    rightEyeData.GazeDirectionNormalized = this.DecodeVector3();
                    break;
                case (uint)WearableColumnType.PUPIL_DIAMETER_MM_RIGHT_VALIDITY:
                    rightEyeData.PupilDiameterMmValidity = (Validity)this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.PUPIL_DIAMETER_MM_RIGHT:
                    rightEyeData.PupilDiameterMm = this.DecodeF32();
                    break;
                case (uint)WearableColumnType.UNKNOWN1:
                    this.DecodeF32();
                    break;
                case (uint)WearableColumnType.UNKNOWN2:
                    this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.FRAME_COUNT:
                    FrameCount = this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.POSITION_GUIDE_XY_LEFT:
                    leftEyeData.PositionGuideXy = this.DecodeVector2d();
                    break;
                case (uint)WearableColumnType.POSITION_GUIDE_XY_RIGHT:
                    rightEyeData.PositionGuideXy = this.DecodeVector2d();
                    break;
                case (uint)WearableColumnType.GAZE_DIRECTION_COMBINED_NORMALIZED:
                    combinedEyeData.GazeDirectionNormalized = this.DecodeVector3();
                    break;
                case (uint)WearableColumnType.GAZE_DIRECTIOM_COMBINED_NORMALIZED_VALIDITY:
                    combinedEyeData.GazeDirectionNormalizedValidity = (Validity)this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.GAZE_ORIGIN_MM_COMBINED:
                    combinedEyeData.GazeOriginMm = this.DecodeVector3();
                    break;
                case (uint)WearableColumnType.GAZE_ORIGIN_MM_COMBINED_VALIDITY:
                    combinedEyeData.GazeOriginMmValidity = (Validity)this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.PUPIL_POSITION_IN_SENSOR_AREA_XY_LEFT_VALIDITY:
                    leftEyeData.PupilPositionInSensorXyValidity = (Validity)this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.PUPIL_POSITION_IN_SENSOR_AREA_XY_LEFT:
                    leftEyeData.PupilPositionInSensorXy = this.DecodeVector2f();
                    break;
                case (uint)WearableColumnType.PUPIL_POSITION_IN_SENSOR_AREA_XY_RIGHT_VALIDITY:
                    rightEyeData.PupilPositionInSensorXyValidity = (Validity)this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.PUPIL_POSITION_IN_SENSOR_AREA_XY_RIGHT:
                    rightEyeData.PupilPositionInSensorXy = this.DecodeVector2f();
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
                case (uint)WearableColumnType.UNKNOWN6:
                    this.DecodeF32();
                    break;
                case (uint)WearableColumnType.BLINK_LEFT_VALID:
                    leftEyeData.BlinkValidity = (Validity)this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.BLINK_LEFT:
                    leftEyeData.Blink = this.DecodeUInt32() == 1;
                    break;
                case (uint)WearableColumnType.BLINK_RIGHT_VALID:
                    rightEyeData.BlinkValidity = (Validity)this.DecodeUInt32();
                    break;
                case (uint)WearableColumnType.BLINK_RIGHT:
                    rightEyeData.Blink = this.DecodeUInt32() == 1;
                    break;
            }
            
            currentRow++;
        }

        LeftEyeData = leftEyeData;
        RightEyeData = rightEyeData;
        CombinedEyeData = combinedEyeData;
    }
    
    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine("SWearable {");
        sb.AppendLine($"  TimestampTrackerUs: {TimestampTrackerUs}");
        sb.AppendLine($"  FrameCount: {FrameCount}");
        sb.AppendLine();

        AppendEyeData(sb, "LeftEye", LeftEyeData);
        AppendEyeData(sb, "RightEye", RightEyeData);
        AppendEyeData(sb, "CombinedEye", CombinedEyeData);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AppendEyeData(StringBuilder sb, string label, EyeData eye)
    {
        sb.AppendLine($"  {label} {{");

        if (eye == null)
        {
            sb.AppendLine("    <null>");
            sb.AppendLine("  }");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"    GazeOriginMm: {Format(eye.GazeOriginMm)} ({eye.GazeOriginMmValidity})");
        sb.AppendLine($"    GazeDirectionNormalized: {Format(eye.GazeDirectionNormalized)} ({eye.GazeDirectionNormalizedValidity})");
        sb.AppendLine($"    PupilDiameterMm: {eye.PupilDiameterMm} ({eye.PupilDiameterMmValidity})");
        sb.AppendLine($"    PositionGuideXy: {Format(eye.PositionGuideXy)}");
        sb.AppendLine($"    PupilPositionInSensorXy: {Format(eye.PupilPositionInSensorXy)} ({eye.PupilPositionInSensorXyValidity})");
        sb.AppendLine($"    Blink: {eye.Blink} ({eye.BlinkValidity})");

        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static string Format(Vector3 v)
        => $"({v.X:0.###}, {v.Y:0.###}, {v.Z:0.###})";

    private static string Format(Vector2 v)
        => $"({v.X:0.###}, {v.Y:0.###})";

    private static string Format(Vector2d v)
        => $"({v.X:0.###}, {v.Y:0.###})";

    public enum Validity : uint
    {
        Invalid = 0,
        Valid = 1,
    }

    public class EyeData
    {
        public Vector3 GazeOriginMm { get; internal set; }
        public Validity GazeOriginMmValidity { get; internal set; }
        
        public Vector3 GazeDirectionNormalized { get; internal set; }
        public Validity GazeDirectionNormalizedValidity { get; internal set; }
        
        public Validity PupilDiameterMmValidity { get; internal set; }
        public float PupilDiameterMm { get; internal set; }
        
        public Vector2d PositionGuideXy { get; internal set; }
        
        public Vector2 PupilPositionInSensorXy { get; internal set; }
        public Validity PupilPositionInSensorXyValidity { get; internal set; }
        
        public Validity BlinkValidity { get; internal set; }
        public bool Blink { get; internal set; }
    }

    private enum WearableColumnType : uint
    {
        TIMESTAMP_TRACKER_US = 1,
        GAZE_ORIGIN_MM_LEFT = 2,
        GAZE_ORIGIN_MM_LEFT_VALIDITY = 3,
        GAZE_DIRECTION_NORMALIZED_LEFT = 4,
        PUPIL_DIAMETER_MM_LEFT_VALIDITY = 5,
        PUPIL_DIAMETER_MM_LEFT = 6,
        UNKNOWN0 = 7,
        GAZE_ORIGIN_MM_RIGHT = 8,
        GAZE_ORIGIN_MM_RIGHT_VALIDITY = 9,
        GAZE_DIRECTION_NORMALIZED_RIGHT = 10,
        PUPIL_DIAMETER_MM_RIGHT_VALIDITY = 11,
        PUPIL_DIAMETER_MM_RIGHT = 12,
        UNKNOWN1 = 13,
        UNKNOWN2 = 14,
        FRAME_COUNT = 15,
        POSITION_GUIDE_XY_LEFT = 16,
        POSITION_GUIDE_XY_RIGHT = 17,
        GAZE_DIRECTION_COMBINED_NORMALIZED = 18,
        GAZE_DIRECTIOM_COMBINED_NORMALIZED_VALIDITY = 19,
        GAZE_ORIGIN_MM_COMBINED = 20,
        GAZE_ORIGIN_MM_COMBINED_VALIDITY = 21,
        PUPIL_POSITION_IN_SENSOR_AREA_XY_LEFT_VALIDITY = 22,
        PUPIL_POSITION_IN_SENSOR_AREA_XY_LEFT = 23,
        PUPIL_POSITION_IN_SENSOR_AREA_XY_RIGHT_VALIDITY = 24,
        PUPIL_POSITION_IN_SENSOR_AREA_XY_RIGHT = 25,
        UNKNOWN3 = 26,
        UNKNOWN4 = 27,
        UNKNOWN5 = 28,
        UNKNOWN6 = 29,
        BLINK_LEFT_VALID = 30,
        BLINK_LEFT = 31,
        BLINK_RIGHT_VALID = 32,
        BLINK_RIGHT = 33
    }
}