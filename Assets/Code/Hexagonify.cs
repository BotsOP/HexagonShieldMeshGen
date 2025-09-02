using System;
using System.IO;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[BurstCompile]
public class Hexagonify : MonoBehaviour
{
    public int tri1;
    public int tri2;
    public int tri3;
    [Range(0, 0.1f)]
    public float radius;
    public Mesh mesh;
    public Material mat;

    private void OnDrawGizmos()
    {
        if (mesh == null)
            return;
        
        Gizmos.DrawSphere(mesh.vertices[tri1], radius);
        Gizmos.DrawSphere(mesh.vertices[tri2], radius);
        Gizmos.DrawSphere(mesh.vertices[tri3], radius);
    }

    private void Start()
    {
        NativeArray<float3> verts = new NativeArray<float3>(mesh.vertices.Length, Allocator.TempJob);
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] = mesh.vertices[i];
        }
        NativeArray<float3> normals = new NativeArray<float3>(mesh.vertices.Length, Allocator.TempJob);
        for (int i = 0; i < verts.Length; i++)
        {
            normals[i] = mesh.normals[i];
        }
        HexagonifyJob job = new HexagonifyJob(verts, normals);
        job.Schedule().Complete();
        
        Vector2[] tempHexID = new Vector2[job.hexID.Length];
        for (int i = 0; i < job.hexID.Length; i++)
        {
            tempHexID[i] = new Vector2(job.hexID[i], job.hexOutline[i]);
        }
        mesh.uv = tempHexID;
        
        int width = Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Sqrt(job.amountHexagonsArray[0])));
        Debug.Log($"amount hexagons: {job.amountHexagonsArray[0]} texture width: {width}");
        Texture2D posTexture = ToTexture2D(job.hexPos, width, width);
        mat.SetTexture("_HexPos", posTexture);
        Texture2D normalTexture = ToTexture2D(job.hexNormal, width, width);
        mat.SetTexture("_HexNormal", normalTexture);

        job.Dispose();
        
        Debug.Log(posTexture.GetPixel(0, 0));
        Debug.Log(normalTexture.GetPixel(0, 0));

        SaveTexture(posTexture, mesh.name + "_HexPositions.exr");
        SaveTexture(normalTexture, mesh.name + "_HexNormals.exr");
    }
    
    private static Texture2D ToTexture2D(NativeArray<float3> data, int width, int height)
    {
        // Create the texture
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBAFloat, false);

        // Convert NativeArray<float3> into Color[]
        Color[] pixels = new Color[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            float3 v = data[i];
            pixels[i] = new Color(v.x, v.y, v.z, 1f); // alpha = 1
        }

        // Apply to texture
        tex.SetPixels(pixels);
        tex.Apply();

        return tex;
    }
    
    private static void SaveTexture(Texture2D texture, string fileName = "SavedTexture.exr")
    {
        if (texture == null)
        {
            Debug.LogError("SaveTexture failed: texture is null");
            return;
        }

        // Encode texture to PNG
        byte[] exrData = texture.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);
        if (exrData == null)
        {
            Debug.LogError("SaveTexture failed: could not encode texture.");
            return;
        }

        // Build file path in project Assets folder
        string path = Path.Combine(Application.dataPath, fileName);

        // Write to disk
        File.WriteAllBytes(path, exrData);

#if UNITY_EDITOR
        // Refresh the editor so the file shows up in the Project window
        UnityEditor.AssetDatabase.Refresh();
#endif

        Debug.Log($"Saved texture to: {path}");
    }

    [BurstCompile]
    private struct HexagonifyJob : IJob
    {
        public NativeArray<float> hexID;
        public NativeArray<float> hexOutline;
        public NativeArray<float3> hexPos;
        public NativeArray<float3> hexNormal;
        public NativeArray<int> amountHexagonsArray;

        private NativeArray<float3> normals;
        private NativeArray<float3> verts;
        private NativeHashSet<float3> closedSet;
        private NativeQueue<int2> toSearch;
        private int amountHexagons;

        public HexagonifyJob(NativeArray<float3> verts, NativeArray<float3> normals)
        {
            this.verts = verts;
            this.normals = normals;
            hexID = new NativeArray<float>(verts.Length, Allocator.TempJob);
            hexID.FillArray(-1);
            hexOutline = new NativeArray<float>(verts.Length, Allocator.TempJob);
            hexPos = new NativeArray<float3>(verts.Length / 3, Allocator.TempJob);
            hexNormal = new NativeArray<float3>(verts.Length / 3, Allocator.TempJob);
            amountHexagonsArray = new NativeArray<int>(1, Allocator.TempJob);
            
            closedSet = new NativeHashSet<float3>(verts.Length, Allocator.TempJob);
            toSearch = new NativeQueue<int2>(Allocator.TempJob);

            amountHexagons = 0;
        }

        public void Dispose()
        {
            verts.Dispose();
            normals.Dispose();
            hexID.Dispose();
            hexOutline.Dispose();
            hexPos.Dispose();
            hexNormal.Dispose();
            closedSet.Dispose();
            toSearch.Dispose();
            amountHexagonsArray.Dispose();
        }

        public void Execute()
        {
            // int startIndex = verts.Length / 6;
            // toSearch.Enqueue(new int2(startIndex, startIndex + 1));
            toSearch.Enqueue(new int2(0, 1));
            int whileEscapeCounter = 0;

            while (toSearch.Count > 0 && whileEscapeCounter < 100000)
            {
                whileEscapeCounter++;
                
                int originIndex = -1;
                int2 originEdge = toSearch.Dequeue();
                float3 originVert1 = verts[originEdge.x];
                float3 originVert2 = verts[originEdge.y];
                for (int i = 0; i < verts.Length / 3; i++)
                {
                    // if(originEdge.x / 3 == i)
                    //     continue;
                    bool isOwnTriangle = originEdge.x / 3 == i;

                    int vertIndex1 = i * 3 + 0;
                    int vertIndex2 = i * 3 + 1;
                    int vertIndex3 = i * 3 + 2;

                    float3 vert1 = verts[vertIndex1];
                    float3 vert2 = verts[vertIndex2];
                    float3 vert3 = verts[vertIndex3];

                    int counter = 0;
                    bool matchingVert11 = math.all(vert1 == originVert1);
                    bool matchingVert12 = math.all(vert2 == originVert1);
                    bool matchingVert13 = math.all(vert3 == originVert1);
                    bool matchingVert21 = math.all(vert1 == originVert2);
                    bool matchingVert22 = math.all(vert2 == originVert2);
                    bool matchingVert23 = math.all(vert3 == originVert2);
                    
                    if (matchingVert11 || matchingVert12 || matchingVert13)
                        counter++;
                    if (matchingVert21 || matchingVert22 || matchingVert23)
                        counter++;

                    if (counter != 2)
                        continue;
                    
                    if (!matchingVert11 && !matchingVert21)
                    {
                        originIndex = i * 3;
                        if (closedSet.Contains(verts[originIndex]))
                            originIndex = -1;
                        if(!isOwnTriangle) 
                            break;
                    }
                        
                    if (!matchingVert12 && !matchingVert22)
                    {
                        originIndex = i * 3 + 1;
                        if (closedSet.Contains(verts[originIndex]))
                            originIndex = -1;
                        if(!isOwnTriangle) 
                            break;
                    }
                        
                    if (!matchingVert13 && !matchingVert23)
                    {
                        originIndex = i * 3 + 2;
                        if (closedSet.Contains(verts[originIndex]))
                            originIndex = -1;
                        if(!isOwnTriangle) 
                            break;
                    }
                }
                
                if(originIndex == -1)
                    continue;

                closedSet.Add(verts[originIndex]);
                amountHexagonsArray[0]++;
                amountHexagons++;
                hexPos[amountHexagons] = verts[originIndex];
                hexNormal[amountHexagons] = normals[originIndex];
                float3 centerHexagon = verts[originIndex];
                
                for (int j = 0; j < verts.Length / 3; j++)
                {
                    int vertIndex1 = j * 3 + 0;
                    int vertIndex2 = j * 3 + 1;
                    int vertIndex3 = j * 3 + 2;

                    float3 vert1 = verts[vertIndex1];
                    float3 vert2 = verts[vertIndex2];
                    float3 vert3 = verts[vertIndex3];

                    if (!math.all(verts[originIndex] == vert1) && !math.all(verts[originIndex] == vert2) && !math.all(verts[originIndex] == vert3))
                        continue;

                    closedSet.Add(vert1);
                    closedSet.Add(vert2);
                    closedSet.Add(vert3);
                    
                    hexID[vertIndex1] = amountHexagons;
                    hexID[vertIndex2] = amountHexagons;
                    hexID[vertIndex3] = amountHexagons;

                    bool foundFirstVert = false;
                    int2 toSearchIndex = int2.zero;
                    
                    if (!math.all(vert1 == centerHexagon))
                    {
                        toSearchIndex.x = vertIndex1;
                        foundFirstVert = true;
                    }
                    else
                    {
                        hexOutline[vertIndex1] = 1;
                    }
                    if (!math.all(vert2 == centerHexagon))
                    {
                        if (foundFirstVert)
                            toSearchIndex.y = vertIndex2;
                        else
                            toSearchIndex.x = vertIndex2;
                        foundFirstVert = true;
                    }
                    else
                    {
                        hexOutline[vertIndex2] = 1;
                    }
                    if (!math.all(vert3 == centerHexagon))
                    {
                        if (foundFirstVert)
                            toSearchIndex.y = vertIndex3;
                        else
                            toSearchIndex.x = vertIndex3;
                    }
                    else
                    {
                        hexOutline[vertIndex3] = 1;
                    }
                    
                    toSearch.Enqueue(toSearchIndex);
                }
            }
        }
    }
}
