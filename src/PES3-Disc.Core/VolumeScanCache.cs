using System.Security.Cryptography;
using System.Text;

namespace PES3Disc.Core;

/// <summary>Detects when optical volume enumeration has not changed since the last scan.</summary>
public static class VolumeScanCache
{
    public static string ComputeSignature()
    {
        var sb = new StringBuilder(256);
        foreach (var drive in DiscDetector.GetOpticalDrives())
        {
            sb.Append(drive.Id).Append('|').Append(drive.Root).Append('|');
            try
            {
                var kind = DiscDetector.GetVolumeStatus(drive.Root).Kind;
                sb.Append((int)kind).Append(';');
            }
            catch
            {
                sb.Append("err;");
            }
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
