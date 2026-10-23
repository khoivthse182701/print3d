using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace TheBoys.Infrastructure.Printing;

public enum ModelUpAxis
{
    Z,
    Y,
    X
}

public static class StlGeometry
{
    public static ModelUpAxis ParseUpAxis(string? upAxis)
    {
        if (string.IsNullOrWhiteSpace(upAxis)) return ModelUpAxis.Z; // Default established convention in 3D printing
        return upAxis.Trim().ToUpperInvariant() switch
        {
            "Z" or "+Z" => ModelUpAxis.Z,
            "Y" or "+Y" => ModelUpAxis.Y,
            "X" or "+X" => ModelUpAxis.X,
            _ => throw new InvalidDataException($"Trục hướng lên '{upAxis}' không được hỗ trợ. Chỉ hỗ trợ Z, Y hoặc X.")
        };
    }

    // STL has no units. Match the already-resized dimensions, then write coordinates in mm.
    // Scales uniformly by targetHeight / dimensionAlongDeclaredUpAxis.
    public static void NormalizeToMillimeters(string path, decimal widthMm, decimal depthMm, decimal heightMm, string upAxis = "Z")
    {
        var axis = ParseUpAxis(upAxis);
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
        if (extent.X <= 0 || extent.Y <= 0 || extent.Z <= 0 || !float.IsFinite(extent.X) || !float.IsFinite(extent.Y) || !float.IsFinite(extent.Z))
            throw new InvalidDataException("STL có kích thước bằng 0.");
        if (widthMm <= 0 || depthMm <= 0 || heightMm <= 0)
            throw new InvalidDataException("Kích thước yêu cầu phải lớn hơn 0.");

        double scale;
        double expectedX, expectedY, expectedZ;

        if (axis == ModelUpAxis.Z)
        {
            scale = (double)heightMm / extent.Z;
            expectedX = (double)widthMm;
            expectedY = (double)depthMm;
            expectedZ = (double)heightMm;

            if (Math.Abs(extent.X * scale - expectedX) > Math.Max(0.5, expectedX * 0.02)
                || Math.Abs(extent.Y * scale - expectedY) > Math.Max(0.5, expectedY * 0.02)
                || Math.Abs(extent.Z * scale - expectedZ) > Math.Max(0.5, expectedZ * 0.02))
            {
                throw new InvalidDataException("Tỷ lệ STL không khớp kích thước yêu cầu. Cần kiểm tra file trước khi in.");
            }
        }
        else if (axis == ModelUpAxis.Y)
        {
            scale = (double)heightMm / extent.Y;
            expectedX = (double)widthMm;
            expectedY = (double)heightMm;
            expectedZ = (double)depthMm;

            if (Math.Abs(extent.X * scale - expectedX) > Math.Max(0.5, expectedX * 0.02)
                || Math.Abs(extent.Y * scale - expectedY) > Math.Max(0.5, expectedY * 0.02)
                || Math.Abs(extent.Z * scale - expectedZ) > Math.Max(0.5, expectedZ * 0.02))
            {
                throw new InvalidDataException("Tỷ lệ STL không khớp kích thước yêu cầu. Cần kiểm tra file trước khi in.");
            }
        }
        else // X
        {
            scale = (double)heightMm / extent.X;
            expectedX = (double)heightMm;
            expectedY = (double)widthMm;
            expectedZ = (double)depthMm;

            if (Math.Abs(extent.X * scale - expectedX) > Math.Max(0.5, expectedX * 0.02)
                || Math.Abs(extent.Y * scale - expectedY) > Math.Max(0.5, expectedY * 0.02)
                || Math.Abs(extent.Z * scale - expectedZ) > Math.Max(0.5, expectedZ * 0.02))
            {
                throw new InvalidDataException("Tỷ lệ STL không khớp kích thước yêu cầu. Cần kiểm tra file trước khi in.");
            }
        }

        for (var triangle = 0; triangle < count; triangle++)
        for (var vertex = 0; vertex < 3; vertex++)
        for (var axisIdx = 0; axisIdx < 3; axisIdx++)
        {
            var offset = 84 + triangle * 50 + 12 + vertex * 12 + axisIdx * 4;
            var origin = axisIdx == 0 ? min.X : axisIdx == 1 ? min.Y : min.Z;
            var result = (float)((BitConverter.ToSingle(data, offset) - origin) * scale);
            BitConverter.TryWriteBytes(data.AsSpan(offset, 4), result);
        }
        Encoding.ASCII.GetBytes("TheBoys STL; coordinates in millimeters").CopyTo(data, 0);
        var temp = path + ".scaled";
        File.WriteAllBytes(temp, data);
        File.Move(temp, path, overwrite: true);
    }

    public static JsonElement AnalyzePrintability(string path, int intendedHeightMm, string upAxis = "Z")
    {
        var axis = ParseUpAxis(upAxis);
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

        double heightExtent = axis switch
        {
            ModelUpAxis.Z => (double)extent.Z,
            ModelUpAxis.Y => (double)extent.Y,
            ModelUpAxis.X => (double)extent.X,
            _ => throw new InvalidDataException("Trục hướng lên không hợp lệ.")
        };

        if (heightExtent <= 0 || !double.IsFinite(heightExtent))
            throw new InvalidDataException("Kích thước theo trục chiều cao không hợp lệ.");

        // Uniform scaling factor: targetHeight / dimensionAlongDeclaredUpAxis
        var scale = intendedHeightMm / heightExtent;

        decimal widthMm, depthMm, physicalHeightMm;
        if (axis == ModelUpAxis.Z)
        {
            widthMm = decimal.Round((decimal)(extent.X * scale), 3);
            depthMm = decimal.Round((decimal)(extent.Y * scale), 3);
            physicalHeightMm = (decimal)intendedHeightMm;
        }
        else if (axis == ModelUpAxis.Y)
        {
            widthMm = decimal.Round((decimal)(extent.X * scale), 3);
            depthMm = decimal.Round((decimal)(extent.Z * scale), 3);
            physicalHeightMm = (decimal)intendedHeightMm;
        }
        else // X
        {
            widthMm = decimal.Round((decimal)(extent.Y * scale), 3);
            depthMm = decimal.Round((decimal)(extent.Z * scale), 3);
            physicalHeightMm = (decimal)intendedHeightMm;
        }

        var nonManifoldEdges = edges.Count(x => x.Value != 2);
        var watertight = nonManifoldEdges == 0;
        var volumeM3 = watertight ? Math.Abs(signedVolume) * Math.Pow(scale, 3) / 1_000_000_000d : 0d;
        return JsonSerializer.SerializeToElement(new
        {
            status = watertight ? "healthy" : "warning",
            metrics = new { is_watertight = watertight, volume = volumeM3, non_manifold_edges = nonManifoldEdges, holes = nonManifoldEdges },
            intended_print = new { dimensions_mm = new decimal[] { widthMm, depthMm, physicalHeightMm }, wall_thickness_mm = 0.8m },
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
