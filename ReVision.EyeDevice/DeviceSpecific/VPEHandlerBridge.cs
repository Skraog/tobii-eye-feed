using System.Runtime.InteropServices;

namespace ReVision.EyeDevice.DeviceSpecific;

public class VPEHandlerBridge
{
    [DllImport("ReVision.VPEHandler")]
    private static extern unsafe ChallengeData* complete_challenge(byte* challenge, int challenge_size);
    [DllImport("ReVision.VPEHandler")]
    private static extern unsafe void free_challenge(ChallengeData* challenge_data);

    public unsafe (byte[] data, byte[] salt) Compute(byte[] challenge)
    {
        int challenge_size = challenge.Length;
        IntPtr challenge_data = Marshal.AllocHGlobal(challenge_size);
        Marshal.Copy(challenge, 0, challenge_data, challenge_size);
        
        ChallengeData* challengeData = complete_challenge((byte*)challenge_data, challenge_size);

        byte[] data = new byte[challengeData->data_size];
        Marshal.Copy((IntPtr)challengeData->data, data, 0, challengeData->data_size);
        byte[] salt = new byte[challengeData->salt_size];
        Marshal.Copy((IntPtr)challengeData->salt, salt, 0, challengeData->salt_size);
        
        free_challenge(challengeData);
        return (data, salt);
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct ChallengeData
    {
        public unsafe byte* data;
        public int data_size;
        public unsafe byte* salt;
        public int salt_size;
    }
}