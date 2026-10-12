using System;
using System.IO;
using Unity.Collections;
using Unity.DemoTeam.Hair;
using UnityEngine;

[CreateAssetMenu(menuName = "Hair/Blender Fitted Curves")]
public sealed class BlenderFittedHairData : HairAssetCustomData
{
    public TextAsset curveFile;

    public override bool AcquireCurves(out HairAssetProvisional.CurveSet curves, Allocator allocator)
    {
        curves = default;
        if (curveFile == null) return false;
        using (var reader = new BinaryReader(new MemoryStream(curveFile.bytes)))
        {
            if (reader.ReadInt32() != 0x48414952) throw new InvalidDataException("Invalid fitted groom header.");
            var count = reader.ReadInt32();
            var resolution = reader.ReadInt32();
            if (count <= 0 || resolution < 2 || reader.BaseStream.Length != 12L + 12L * count * resolution)
                throw new InvalidDataException("Invalid fitted groom dimensions.");
            curves = new HairAssetProvisional.CurveSet(count, count * resolution, allocator);
            try
            {
                curves.curveCount = count;
                for (int i = 0; i < count; i++) curves.curveVertexCount.Add(resolution);
                for (int i = 0; i < count * resolution; i++)
                    curves.vertexDataPosition.Add(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                return true;
            }
            catch
            {
                curves.Dispose();
                curves = default;
                throw;
            }
        }
    }
}
