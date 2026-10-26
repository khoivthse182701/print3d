using System.Numerics;
using System.Text;
using System.Text.Json;

namespace TheBoys.Infrastructure.Printing;

public static class GlbToStlConverter
{
    private const uint GlbMagic = 0x46546C67; // "glTF"
    private const uint ChunkTypeJson = 0x4E4F534A; // "JSON"
    private const uint ChunkTypeBin = 0x004E4942; // "BIN\0"

    private readonly struct Triangle(Vector3 normal, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        public readonly Vector3 Normal = normal;
        public readonly Vector3 V1 = v1;
        public readonly Vector3 V2 = v2;
        public readonly Vector3 V3 = v3;
    }

    /// <summary>
    /// Chuyển đổi trực tiếp file .glb sang định dạng Binary STL.
    /// </summary>
    public static void ConvertGlbFileToBinaryStl(string glbPath, string stlPath)
    {
        if (!File.Exists(glbPath))
            throw new FileNotFoundException("Không tìm thấy file GLB nguồn.", glbPath);

        var glbBytes = File.ReadAllBytes(glbPath);
        var stlBytes = ConvertGlbToBinaryStl(glbBytes);

        var dir = Path.GetDirectoryName(stlPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = stlPath + ".tmp";
        File.WriteAllBytes(tempPath, stlBytes);
        File.Move(tempPath, stlPath, overwrite: true);
    }

    /// <summary>
    /// Chuyển đổi mảng bytes GLB sang mảng bytes Binary STL.
    /// </summary>
    public static byte[] ConvertGlbToStl(byte[] glbBytes) => ConvertGlbToBinaryStl(glbBytes);

    /// <summary>
    /// Chuyển đổi mảng bytes GLB sang mảng bytes Binary STL.
    /// </summary>
    public static byte[] ConvertGlbToBinaryStl(byte[] glbBytes)
    {
        if (glbBytes == null || glbBytes.Length < 12)
            throw new InvalidDataException("Dữ liệu GLB không hợp lệ hoặc quá ngắn.");

        // 1. Đọc GLB Header
        var magic = BitConverter.ToUInt32(glbBytes, 0);
        var version = BitConverter.ToUInt32(glbBytes, 4);
        var totalLength = BitConverter.ToUInt32(glbBytes, 8);

        if (magic != GlbMagic || version != 2 || totalLength > glbBytes.Length)
            throw new InvalidDataException("File không phải định dạng glTF 2.0 Binary (GLB) hợp lệ.");

        // 2. Tách Chunks (JSON và BIN)
        byte[]? jsonBytes = null;
        byte[]? binBytes = null;

        var offset = 12;
        while (offset + 8 <= glbBytes.Length)
        {
            var chunkLength = BitConverter.ToUInt32(glbBytes, offset);
            var chunkType = BitConverter.ToUInt32(glbBytes, offset + 4);
            offset += 8;

            if (offset + chunkLength > glbBytes.Length)
                throw new InvalidDataException("Độ dài chunk GLB vượt quá kích thước file.");

            if (chunkType == ChunkTypeJson && jsonBytes == null)
            {
                jsonBytes = glbBytes.AsSpan(offset, (int)chunkLength).ToArray();
            }
            else if (chunkType == ChunkTypeBin && binBytes == null)
            {
                binBytes = glbBytes.AsSpan(offset, (int)chunkLength).ToArray();
            }

            offset += (int)chunkLength;
        }

        if (jsonBytes == null)
            throw new InvalidDataException("Không tìm thấy chunk JSON trong file GLB.");

        binBytes ??= [];

        // 3. Phân tích cú pháp JSON của glTF
        using var doc = JsonDocument.Parse(jsonBytes);
        var root = doc.RootElement;

        if (!root.TryGetProperty("meshes", out var meshesProp) || meshesProp.GetArrayLength() == 0)
            throw new InvalidDataException("File GLB không chứa mesh hình học nào.");

        root.TryGetProperty("bufferViews", out var bufferViewsProp);
        root.TryGetProperty("accessors", out var accessorsProp);

        // 4. Tính toán ma trận biến đổi (Transforms) cho các node chứa mesh
        var meshInstanceTransforms = new Dictionary<int, List<Matrix4x4>>();
        if (root.TryGetProperty("nodes", out var nodesProp))
        {
            var nodeCount = nodesProp.GetArrayLength();
            var localTransforms = new Matrix4x4[nodeCount];
            var parentIndices = new int[nodeCount];
            Array.Fill(parentIndices, -1);

            for (var i = 0; i < nodeCount; i++)
            {
                var node = nodesProp[i];
                localTransforms[i] = ParseNodeTransform(node);
                if (node.TryGetProperty("children", out var childrenProp))
                {
                    foreach (var child in childrenProp.EnumerateArray())
                    {
                        var childIdx = child.GetInt32();
                        if (childIdx >= 0 && childIdx < nodeCount)
                        {
                            parentIndices[childIdx] = i;
                        }
                    }
                }
            }

            var worldTransforms = new Matrix4x4[nodeCount];
            for (var i = 0; i < nodeCount; i++)
            {
                worldTransforms[i] = ComputeWorldTransform(i, localTransforms, parentIndices);
            }

            for (var i = 0; i < nodeCount; i++)
            {
                var node = nodesProp[i];
                if (node.TryGetProperty("mesh", out var meshIdxProp))
                {
                    var meshIdx = meshIdxProp.GetInt32();
                    if (!meshInstanceTransforms.TryGetValue(meshIdx, out var list))
                    {
                        list = [];
                        meshInstanceTransforms[meshIdx] = list;
                    }
                    list.Add(worldTransforms[i]);
                }
            }
        }

        // 5. Trích xuất tam giác từ tất cả các mesh
        var allTriangles = new List<Triangle>();
        var meshArray = meshesProp.EnumerateArray().ToArray();

        for (var meshIdx = 0; meshIdx < meshArray.Length; meshIdx++)
        {
            var mesh = meshArray[meshIdx];
            if (!meshInstanceTransforms.TryGetValue(meshIdx, out var transforms) || transforms.Count == 0)
            {
                transforms = [Matrix4x4.Identity];
            }

            if (!mesh.TryGetProperty("primitives", out var primitivesProp))
                continue;

            foreach (var primitive in primitivesProp.EnumerateArray())
            {
                // Mode 4 = TRIANGLES (mặc định trong glTF)
                if (primitive.TryGetProperty("mode", out var modeProp) && modeProp.GetInt32() != 4)
                    continue;

                if (!primitive.TryGetProperty("attributes", out var attrsProp) ||
                    !attrsProp.TryGetProperty("POSITION", out var posAccessorIdxProp))
                    continue;

                var posAccessorIdx = posAccessorIdxProp.GetInt32();
                var posAccessor = accessorsProp[posAccessorIdx];
                var vertexCount = posAccessor.GetProperty("count").GetInt32();
                if (vertexCount == 0) continue;

                var posBufferViewIdx = posAccessor.GetProperty("bufferView").GetInt32();
                var posBufferView = bufferViewsProp[posBufferViewIdx];

                var posViewOffset = posBufferView.TryGetProperty("byteOffset", out var pbvo) ? pbvo.GetInt32() : 0;
                var posAccOffset = posAccessor.TryGetProperty("byteOffset", out var pao) ? pao.GetInt32() : 0;
                var posStride = posBufferView.TryGetProperty("byteStride", out var pbs) ? pbs.GetInt32() : 12;
                if (posStride <= 0) posStride = 12;

                var posBaseOffset = posViewOffset + posAccOffset;
                var rawPositions = new Vector3[vertexCount];

                for (var v = 0; v < vertexCount; v++)
                {
                    var offsetV = posBaseOffset + v * posStride;
                    if (offsetV + 12 > binBytes.Length)
                        throw new InvalidDataException("Vị trí buffer vượt quá kích thước buffer BIN.");

                    var x = BitConverter.ToSingle(binBytes, offsetV);
                    var y = BitConverter.ToSingle(binBytes, offsetV + 4);
                    var z = BitConverter.ToSingle(binBytes, offsetV + 8);
                    rawPositions[v] = new Vector3(x, y, z);
                }

                // Đọc Indices (nếu có)
                int[]? indices = null;
                if (primitive.TryGetProperty("indices", out var indicesAccessorIdxProp))
                {
                    var idxAccessorIdx = indicesAccessorIdxProp.GetInt32();
                    var idxAccessor = accessorsProp[idxAccessorIdx];
                    var idxCount = idxAccessor.GetProperty("count").GetInt32();
                    var idxComponentType = idxAccessor.GetProperty("componentType").GetInt32();
                    var idxBufferViewIdx = idxAccessor.GetProperty("bufferView").GetInt32();
                    var idxBufferView = bufferViewsProp[idxBufferViewIdx];

                    var idxViewOffset = idxBufferView.TryGetProperty("byteOffset", out var ibvo) ? ibvo.GetInt32() : 0;
                    var idxAccOffset = idxAccessor.TryGetProperty("byteOffset", out var iao) ? iao.GetInt32() : 0;
                    var idxBaseOffset = idxViewOffset + idxAccOffset;

                    indices = new int[idxCount];
                    for (var k = 0; k < idxCount; k++)
                    {
                        indices[k] = idxComponentType switch
                        {
                            5121 => binBytes[idxBaseOffset + k], // UNSIGNED_BYTE
                            5123 => BitConverter.ToUInt16(binBytes, idxBaseOffset + k * 2), // UNSIGNED_SHORT
                            5125 => (int)BitConverter.ToUInt32(binBytes, idxBaseOffset + k * 4), // UNSIGNED_INT
                            _ => throw new InvalidDataException($"ComponentType {idxComponentType} không được hỗ trợ cho index buffer.")
                        };
                    }
                }

                // Duyệt qua từng ma trận transform của instance
                foreach (var worldTransform in transforms)
                {
                    if (indices != null)
                    {
                        for (var k = 0; k + 2 < indices.Length; k += 3)
                        {
                            var i1 = indices[k];
                            var i2 = indices[k + 1];
                            var i3 = indices[k + 2];

                            if (i1 >= rawPositions.Length || i2 >= rawPositions.Length || i3 >= rawPositions.Length)
                                continue;

                            var p1 = Vector3.Transform(rawPositions[i1], worldTransform);
                            var p2 = Vector3.Transform(rawPositions[i2], worldTransform);
                            var p3 = Vector3.Transform(rawPositions[i3], worldTransform);

                            var normal = Vector3.Cross(p2 - p1, p3 - p1);
                            if (normal.LengthSquared() > 0) normal = Vector3.Normalize(normal);
                            else normal = Vector3.Zero;

                            allTriangles.Add(new Triangle(normal, p1, p2, p3));
                        }
                    }
                    else
                    {
                        for (var k = 0; k + 2 < rawPositions.Length; k += 3)
                        {
                            var p1 = Vector3.Transform(rawPositions[k], worldTransform);
                            var p2 = Vector3.Transform(rawPositions[k + 1], worldTransform);
                            var p3 = Vector3.Transform(rawPositions[k + 2], worldTransform);

                            var normal = Vector3.Cross(p2 - p1, p3 - p1);
                            if (normal.LengthSquared() > 0) normal = Vector3.Normalize(normal);
                            else normal = Vector3.Zero;

                            allTriangles.Add(new Triangle(normal, p1, p2, p3));
                        }
                    }
                }
            }
        }

        if (allTriangles.Count == 0)
            throw new InvalidDataException("Không tìm thấy tam giác nào để chuyển đổi sang STL.");

        // 6. Ghi định dạng Binary STL chuẩn
        using var ms = new MemoryStream(84 + allTriangles.Count * 50);
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        // Header: 80 bytes
        var header = new byte[80];
        Encoding.ASCII.GetBytes("TheBoys Binary STL from GLB (Hunyuan3D)").CopyTo(header, 0);
        writer.Write(header);

        // Số lượng tam giác: 4 bytes uint32
        writer.Write((uint)allTriangles.Count);

        // Mỗi tam giác: 50 bytes (12 normal + 36 vertices + 2 attribute)
        foreach (var t in allTriangles)
        {
            writer.Write(t.Normal.X);
            writer.Write(t.Normal.Y);
            writer.Write(t.Normal.Z);

            writer.Write(t.V1.X);
            writer.Write(t.V1.Y);
            writer.Write(t.V1.Z);

            writer.Write(t.V2.X);
            writer.Write(t.V2.Y);
            writer.Write(t.V2.Z);

            writer.Write(t.V3.X);
            writer.Write(t.V3.Y);
            writer.Write(t.V3.Z);

            writer.Write((ushort)0);
        }

        writer.Flush();
        return ms.ToArray();
    }

    private static Matrix4x4 ParseNodeTransform(JsonElement node)
    {
        if (node.TryGetProperty("matrix", out var matrixProp) && matrixProp.GetArrayLength() == 16)
        {
            var m = matrixProp.EnumerateArray().Select(e => e.GetSingle()).ToArray();
            // glTF matrix lưu dạng column-major
            return new Matrix4x4(
                m[0], m[1], m[2], m[3],
                m[4], m[5], m[6], m[7],
                m[8], m[9], m[10], m[11],
                m[12], m[13], m[14], m[15]
            );
        }

        var translation = Vector3.Zero;
        if (node.TryGetProperty("translation", out var tProp) && tProp.GetArrayLength() == 3)
        {
            var t = tProp.EnumerateArray().Select(e => e.GetSingle()).ToArray();
            translation = new Vector3(t[0], t[1], t[2]);
        }

        var rotation = Quaternion.Identity;
        if (node.TryGetProperty("rotation", out var rProp) && rProp.GetArrayLength() == 4)
        {
            var r = rProp.EnumerateArray().Select(e => e.GetSingle()).ToArray();
            rotation = new Quaternion(r[0], r[1], r[2], r[3]); // glTF lưu [x, y, z, w]
        }

        var scale = Vector3.One;
        if (node.TryGetProperty("scale", out var sProp) && sProp.GetArrayLength() == 3)
        {
            var s = sProp.EnumerateArray().Select(e => e.GetSingle()).ToArray();
            scale = new Vector3(s[0], s[1], s[2]);
        }

        return Matrix4x4.CreateScale(scale) *
               Matrix4x4.CreateFromQuaternion(rotation) *
               Matrix4x4.CreateTranslation(translation);
    }

    private static Matrix4x4 ComputeWorldTransform(int nodeIdx, Matrix4x4[] localTransforms, int[] parentIndices)
    {
        var result = localTransforms[nodeIdx];
        var parent = parentIndices[nodeIdx];
        var depth = 0;

        while (parent != -1 && depth < 100)
        {
            result = result * localTransforms[parent];
            parent = parentIndices[parent];
            depth++;
        }

        return result;
    }

    /// <summary>
    /// Helper: Tạo file GLB (glTF 2.0 Binary) chứa khối hộp kín nước (watertight cube) 12 tam giác.
    /// </summary>
    public static byte[] CreateCubeGlb(float extentX, float extentY, float extentZ)
    {
        var vertices = new Vector3[]
        {
            new(0, 0, 0),
            new(extentX, 0, 0),
            new(extentX, extentY, 0),
            new(0, extentY, 0),
            new(0, 0, extentZ),
            new(extentX, 0, extentZ),
            new(extentX, extentY, extentZ),
            new(0, extentY, extentZ)
        };

        var indices = new ushort[]
        {
            4, 5, 6,
            4, 6, 7,
            1, 0, 3,
            1, 3, 2,
            3, 2, 6,
            3, 6, 7,
            0, 1, 5,
            0, 5, 4,
            5, 1, 2,
            5, 2, 6,
            0, 4, 7,
            0, 7, 3
        };

        var indicesBytes = new byte[indices.Length * 2];
        Buffer.BlockCopy(indices, 0, indicesBytes, 0, indicesBytes.Length);

        var verticesBytes = new byte[vertices.Length * 12];
        for (var i = 0; i < vertices.Length; i++)
        {
            var offset = i * 12;
            BitConverter.TryWriteBytes(verticesBytes.AsSpan(offset, 4), vertices[i].X);
            BitConverter.TryWriteBytes(verticesBytes.AsSpan(offset + 4, 4), vertices[i].Y);
            BitConverter.TryWriteBytes(verticesBytes.AsSpan(offset + 8, 4), vertices[i].Z);
        }

        var binBytes = new byte[indicesBytes.Length + verticesBytes.Length];
        Buffer.BlockCopy(indicesBytes, 0, binBytes, 0, indicesBytes.Length);
        Buffer.BlockCopy(verticesBytes, 0, binBytes, indicesBytes.Length, verticesBytes.Length);

        var manifest = new
        {
            asset = new { version = "2.0" },
            scene = 0,
            scenes = new[] { new { nodes = new[] { 0 } } },
            nodes = new[] { new { mesh = 0 } },
            meshes = new[]
            {
                new
                {
                    primitives = new[]
                    {
                        new
                        {
                            attributes = new { POSITION = 1 },
                            indices = 0,
                            mode = 4
                        }
                    }
                }
            },
            accessors = new object[]
            {
                new
                {
                    bufferView = 0,
                    byteOffset = 0,
                    componentType = 5123,
                    count = indices.Length,
                    type = "SCALAR"
                },
                new
                {
                    bufferView = 1,
                    byteOffset = 0,
                    componentType = 5126,
                    count = vertices.Length,
                    type = "VEC3",
                    min = new[] { 0f, 0f, 0f },
                    max = new[] { extentX, extentY, extentZ }
                }
            },
            bufferViews = new[]
            {
                new
                {
                    buffer = 0,
                    byteOffset = 0,
                    byteLength = indicesBytes.Length,
                    target = 34963
                },
                new
                {
                    buffer = 0,
                    byteOffset = indicesBytes.Length,
                    byteLength = verticesBytes.Length,
                    target = 34962
                }
            },
            buffers = new[]
            {
                new { byteLength = binBytes.Length }
            }
        };

        var jsonString = JsonSerializer.Serialize(manifest);
        var jsonBytes = Encoding.UTF8.GetBytes(jsonString);

        var jsonPadding = (4 - (jsonBytes.Length % 4)) % 4;
        var paddedJsonBytes = new byte[jsonBytes.Length + jsonPadding];
        Buffer.BlockCopy(jsonBytes, 0, paddedJsonBytes, 0, jsonBytes.Length);
        for (var i = 0; i < jsonPadding; i++) paddedJsonBytes[jsonBytes.Length + i] = 0x20;

        var binPadding = (4 - (binBytes.Length % 4)) % 4;
        var paddedBinBytes = new byte[binBytes.Length + binPadding];
        Buffer.BlockCopy(binBytes, 0, paddedBinBytes, 0, binBytes.Length);

        var totalLength = 12 + (8 + paddedJsonBytes.Length) + (8 + paddedBinBytes.Length);

        using var ms = new MemoryStream(totalLength);
        using var bw = new BinaryWriter(ms);

        bw.Write(0x46546C67u);
        bw.Write(2u);
        bw.Write((uint)totalLength);

        bw.Write((uint)paddedJsonBytes.Length);
        bw.Write(0x4E4F534Au);
        bw.Write(paddedJsonBytes);

        bw.Write((uint)paddedBinBytes.Length);
        bw.Write(0x004E4942u);
        bw.Write(paddedBinBytes);

        return ms.ToArray();
    }
}