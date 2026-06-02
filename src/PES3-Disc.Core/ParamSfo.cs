namespace PES3Disc.Core;

internal static class ParamSfo
{
    public static IReadOnlyDictionary<string, string> ReadFields(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
            return result;

        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 20)
                return result;
            if (bytes[0] != 0 || bytes[1] != 0x50 || bytes[2] != 0x53 || bytes[3] != 0x46)
                return result;

            var keyCount = BitConverter.ToUInt32(bytes, 8);
            var keyTableOff = BitConverter.ToUInt32(bytes, 12);
            var dataTableOff = BitConverter.ToUInt32(bytes, 16);

            for (var i = 0u; i < keyCount; i++)
            {
                var entryOff = keyTableOff + (i * 16);
                if (entryOff + 16 > bytes.Length)
                    break;

                var nameOff = BitConverter.ToUInt16(bytes, (int)entryOff);
                var dataLen = BitConverter.ToUInt32(bytes, (int)entryOff + 4);
                var dataOff = BitConverter.ToUInt32(bytes, (int)entryOff + 12);

                var nameEnd = nameOff;
                while (nameEnd < bytes.Length && bytes[nameEnd] != 0)
                    nameEnd++;
                var key = System.Text.Encoding.ASCII.GetString(bytes, nameOff, nameEnd - nameOff);

                if (key is not ("TITLE_ID" or "TITLE"))
                    continue;

                var absOff = dataTableOff + dataOff;
                if (absOff + dataLen > bytes.Length)
                    continue;

                var raw = bytes.AsSpan((int)absOff, (int)dataLen);
                var text = System.Text.Encoding.UTF8.GetString(raw).TrimEnd('\0');
                if (!string.IsNullOrEmpty(text))
                    result[key] = text;
            }
        }
        catch
        {
            // ignore
        }

        return result;
    }
}
