using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace TheBoys.Infrastructure.Printing;

public static class StlGeometry
{
    // STL has no units. Match the already-resized GLB dimensions, then write coordinates in mm.
    public static void NormalizeToMillimeters(string path, decimal widthMm, decimal depthMm, decimal heightMm)
    {
        var data = ReadBinary(path);
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

    public static JsonElement AnalyzePrintability(string path, int intendedHeightMm)
    {
        var data = ReadBinary(path);
        var count = BitConverter.ToUInt32(data, 80);
        if (count == 0 || count > 4_000_000) throw new InvalidDataException("STL không có mesh hợp lệ.");
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        double signedVolume = 0;
        var edges = new Dictionary<EdgeKey, int>();
        Span<Vector3> vertices = stackalloc Vector3[3];
        for (var triangle = 0; triangle < count; triangle++)
        {
            for (var vertex = 0; vertex < 3; vertex++)
            {
                var offset = 84 + triangle * 50 + 12 + vertex * 12;
                var point = new Vector3(BitConverter.ToSingle(data, offset), BitConverter.ToSingle(data, offset + 4), BitConverter.ToSingle(data, offset + 8));
                if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
                    throw new InvalidDataException("STL có tọa độ không hợp lệ.");
                vertices[vertex] = point;
                min = Vector3.Min(min, point); max = Vector3.Max(max, point);
            }
            signedVolume += Vector3.Dot(vertices[0], Vector3.Cross(vertices[1], vertices[2])) / 6d;
            AddEdge(edges, vertices[0], vertices[1]);
            AddEdge(edges, vertices[1], vertices[2]);
            AddEdge(edges, vertices[2], vertices[0]);
        }
        var extent = max - min;
        var extents = new[] { (double)extent.X, (double)extent.Y, (double)extent.Z };
        if (extents.Any(x => !double.IsFinite(x) || x <= 0) || intendedHeightMm <= 0)
            throw new InvalidDataException("STL có kích thước bằng 0.");
        var largestAxis = Array.IndexOf(extents, extents.Max());
        var scale = intendedHeightMm / extents[largestAxis];
        var horizontal = extents.Where((_, index) => index != largestAxis).Select(x => decimal.Round((decimal)(x * scale), 3)).ToArray();
        var nonManifoldEdges = edges.Count(x => x.Value != 2);
        var watertight = nonManifoldEdges == 0;
        var volumeM3 = watertight ? Math.Abs(signedVolume) * Math.Pow(scale, 3) / 1_000_000_000d : 0d;
        return JsonSerializer.SerializeToElement(new
        {
            status = watertight ? "healthy" : "warning",
            metrics = new { is_watertight = watertight, volume = volumeM3, non_manifold_edges = nonManifoldEdges, holes = nonManifoldEdges },
            intended_print = new { dimensions_mm = new decimal[] { horizontal[0], horizontal[1], intendedHeightMm }, wall_thickness_mm = 0.8m },
            thin_walls = new { status = "unknown" }
        });
    }

    private static byte[] ReadBinary(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < 84 || 84L + 50L * BitConverter.ToUInt32(data, 80) != data.Length)
            data = ReadAscii(data);
        return data;
    }

    private static void AddEdge(Dictionary<EdgeKey, int> edges, Vector3 first, Vector3 second)
    {
        var a = VertexKey.From(first); var b = VertexKey.From(second);
        var edge = a.CompareTo(b) <= 0 ? new EdgeKey(a, b) : new EdgeKey(b, a);
        edges[edge] = edges.GetValueOrDefault(edge) + 1;
    }

    private readonly record struct EdgeKey(VertexKey A, VertexKey B);
    private readonly record struct VertexKey(int X, int Y, int Z) : IComparable<VertexKey>
    {
        public static VertexKey From(Vector3 value) => new(BitConverter.SingleToInt32Bits(value.X),
            BitConverter.SingleToInt32Bits(value.Y), BitConverter.SingleToInt32Bits(value.Z));
        public int CompareTo(VertexKey other)
        {
            var x = X.CompareTo(other.X);
            if (x != 0) return x;
            var y = Y.CompareTo(other.Y);
            return y != 0 ? y : Z.CompareTo(other.Z);
        }
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
