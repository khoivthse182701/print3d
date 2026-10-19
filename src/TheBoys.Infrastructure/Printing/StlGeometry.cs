using System.Globalization;
using System.Numerics;
using System.Text;

namespace TheBoys.Infrastructure.Printing;

public static class StlGeometry
{
    // STL has no units. Match the already-resized GLB dimensions, then write coordinates in mm.
    public static void NormalizeToMillimeters(string path, decimal widthMm, decimal depthMm, decimal heightMm)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < 84 || 84L + 50L * BitConverter.ToUInt32(data, 80) != data.Length)
            data = ReadAscii(data);
        var count = BitConverter.ToUInt32(data, 80);
        if (count == 0 || count > 4_000_000) throw new InvalidDataException("STL không có mesh hợp lệ.");
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        for (var triangle = 0; triangle < count; triangle++)
        for (var vertex = 0; vertex < 3; vertex++)
        {
            var offset = 84 + triangle * 50 + 12 + vertex * 12;
            var point = new Vector3(BitConverter.ToSingle(data, offset), BitConverter.ToSingle(data, offset + 4), BitConverter.ToSingle(data, offset + 8));
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z)) throw new InvalidDataException("STL có tọa độ không hợp lệ.");
            min = Vector3.Min(min, point); max = Vector3.Max(max, point);
        }
        var extent = max - min;
        double[] actual = [extent.X, extent.Y, extent.Z];
        double[] expected = [(double)widthMm, (double)depthMm, (double)heightMm];
        Array.Sort(actual); Array.Sort(expected);
        if (actual.Any(x => !double.IsFinite(x) || x <= 0) || expected.Any(x => x <= 0)) throw new InvalidDataException("STL có kích thước bằng 0.");
        var scale = expected[2] / actual[2];
        for (var i = 0; i < 3; i++)
            if (Math.Abs(actual[i] * scale - expected[i]) > Math.Max(0.5, expected[i] * 0.02))
                throw new InvalidDataException("Tỷ lệ STL không khớp GLB. Cần kiểm tra file trước khi in.");
        for (var triangle = 0; triangle < count; triangle++)
        for (var vertex = 0; vertex < 3; vertex++)
        for (var axis = 0; axis < 3; axis++)
        {
            var offset = 84 + triangle * 50 + 12 + vertex * 12 + axis * 4;
            var origin = axis == 0 ? min.X : axis == 1 ? min.Y : min.Z;
            var result = (float)((BitConverter.ToSingle(data, offset) - origin) * scale);
            BitConverter.TryWriteBytes(data.AsSpan(offset, 4), result);
        }
        Encoding.ASCII.GetBytes("TheBoys STL; coordinates in millimeters").CopyTo(data, 0);
        var temp = path + ".scaled";
        File.WriteAllBytes(temp, data);
        File.Move(temp, path, overwrite: true);
    }

    private static byte[] ReadAscii(byte[] data)
    {
        using var reader = new StringReader(Encoding.ASCII.GetString(data));
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(new byte[84]);
        var vertices = new List<Vector3>(3);
        uint count = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var parts = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts[0] != "vertex") continue;
            if (parts.Length != 4 || !float.TryParse(parts[1], CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(parts[2], CultureInfo.InvariantCulture, out var y)
                || !float.TryParse(parts[3], CultureInfo.InvariantCulture, out var z)) throw new InvalidDataException("STL ASCII hỏng.");
            vertices.Add(new(x, y, z));
            if (vertices.Count != 3) continue;
            var normal = Vector3.Cross(vertices[1] - vertices[0], vertices[2] - vertices[0]);
            normal = normal.LengthSquared() > 0 ? Vector3.Normalize(normal) : Vector3.Zero;
            writer.Write(normal.X); writer.Write(normal.Y); writer.Write(normal.Z);
            foreach (var v in vertices) { writer.Write(v.X); writer.Write(v.Y); writer.Write(v.Z); }
            writer.Write((ushort)0); vertices.Clear(); count++;
            if (count > 4_000_000) throw new InvalidDataException("STL quá lớn.");
        }
        if (vertices.Count != 0 || count == 0) throw new InvalidDataException("STL ASCII hỏng.");
        stream.Position = 80; writer.Write(count);
        return stream.ToArray();
    }
}
