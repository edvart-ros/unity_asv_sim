using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace WaterInteraction {
    public class Submerged {
        public Mesh hullMesh;
        public Mesh mesh = new Mesh();
        public float[] triangleAreas;
        public Vector3[] FaceNormalsL = new Vector3[0];
        public Vector3[] FaceCentersWorld = new Vector3[0];
        public float[] FaceCenterHeightsAboveWater = new float[0];
        public Vector3[] pressureCenters = new Vector3[0];
        public Vector3[] waterLineVerts = new Vector3[0];
        public bool debugWaterLine = false;
        private int L;
        public float volume = 0f;
        public Vector3 centroid = Vector3.zero;
        public Vector3 centroidUp = Vector3.zero;
        public Vector3 centroidDown = Vector3.zero; 
        public Vector3[] centroidsUp = new Vector3[0];
        public Vector3[] centroidsDown = new Vector3[0];
        public Submerged(Mesh simplifiedHullMesh, bool debug=false) {
            hullMesh = simplifiedHullMesh;
            L = hullMesh.vertices.Length;
        }

        public void Update(Patch patch, Transform t) {
            mesh.Clear();
            // cache hull vertices and wave field approximation vertices
            Vector3[] hullVerts = hullMesh.vertices;
            int[] hullTris = hullMesh.triangles;
            Vector3[] hullVertNormals = hullMesh.normals;
            (Vector3[] subVerts, int[] subTris, Vector3[] subNormals, Vector3[] intersectionVerts) = GetSubmergedTriangles(patch, t, hullVerts, hullTris, hullVertNormals);
            mesh.vertices = subVerts;
            mesh.triangles = subTris;
            FaceNormalsL = subNormals;
            waterLineVerts = intersectionVerts;
            triangleAreas = GetTriangleAreas(subVerts);
            FaceCenterHeightsAboveWater = GetTriangleCenterHeights(patch, t, subVerts, subTris);
            (volume, centroid) = GetSubmergedVolume(subVerts, subTris, subNormals, FaceCenterHeightsAboveWater, t);
            FaceCentersWorld = GetFaceCenters(t, subTris, subVerts);
        }



        public (Vector3[], int[], Vector3[], Vector3[]) GetSubmergedTriangles(Patch patch, Transform t, Vector3[] bodyVerts, int[] bodyTris, Vector3[] bodyVertNormals) {
            List<int> trisOut = new List<int>();
            List<Vector3> vertsOut = new List<Vector3>();
            List<Vector3> intersectionVerts = new List<Vector3>();
            List<Vector3> normalsOut = new List<Vector3>();

            Vector3[] vertsL = new Vector3[3];
            Vector3[] normalsL = new Vector3[3];
            Vector3[] vertsW = new Vector3[3];
            float[] vertHeights = new float[3];
            // loop through input triangles
            for (int i = 0; i < bodyTris.Length - 2; i += 3) {

                int submCount = 0;

                // get the local and world positions of the current triangle, compute depth, track number of submerged verts in triangle
                for (int j = 0; j < 3; j++) {
                    vertsL[j] = bodyVerts[bodyTris[i + j]];
                    normalsL[j] = bodyVertNormals[bodyTris[i + j]];
                    vertsW[j] = t.TransformPoint(vertsL[j]);
                    float height = patch.GetPatchRelativeHeight(vertsW[j]);
                    vertHeights[j] = height;
                    if (height < 0) submCount++; // depth > 0 == submerged point
                }
                Vector3 triangleNormal = (normalsL[0] + normalsL[1] + normalsL[2]).normalized;

                // how many vertices are underwater?
                switch (submCount) {
                    case 0: {
                            break;
                        }
                    case 1: {
                            (Vector3[] sortedVertsL, float[] sortedHeights) = Patch.SortVerticesAgainstFloats(vertsL, vertHeights);
                            Vector3 LH = sortedVertsL[2] - sortedVertsL[0];
                            Vector3 LM = sortedVertsL[1] - sortedVertsL[0];

                            float t_M = -sortedHeights[0] / (sortedHeights[1] - sortedHeights[0]);
                            float t_H = -sortedHeights[0] / (sortedHeights[2] - sortedHeights[0]);
                            Vector3 LJ_H = t_H * LH;
                            Vector3 LJ_M = t_M * LM;

                            Vector3 J_H = sortedVertsL[0] + LJ_H;
                            Vector3 J_M = sortedVertsL[0] + LJ_M;
                            Vector3 normal = triangleNormal * Utils.GetFaceNormal(sortedVertsL[0], J_H, J_M).magnitude;
                            AppendTriangle(ref vertsOut, ref trisOut, ref normalsOut, sortedVertsL[0], J_H, J_M, triangleNormal);
                            intersectionVerts.Add(J_H);
                            intersectionVerts.Add(J_M);

                            break;
                        }
                    case 2: {
                            (Vector3[] sortedVertsL, float[] sortedHeights) = Patch.SortVerticesAgainstFloats(vertsL, vertHeights);

                            Vector3 LH = sortedVertsL[2] - sortedVertsL[0];
                            Vector3 MH = sortedVertsL[2] - sortedVertsL[1];

                            float t_M = -sortedHeights[1] / (sortedHeights[2] - sortedHeights[1]);
                            float t_L = -sortedHeights[0] / (sortedHeights[2] - sortedHeights[0]);
                            Vector3 LI_L = t_L * LH;
                            Vector3 LI_M = t_M * MH;

                            Vector3 I_L = sortedVertsL[0] + LI_L;
                            Vector3 I_M = sortedVertsL[1] + LI_M;
                            Vector3 normal = triangleNormal * Utils.GetFaceNormal(sortedVertsL[1], I_M, sortedVertsL[0]).magnitude;
                            AppendTriangle(ref vertsOut, ref trisOut, ref normalsOut, sortedVertsL[1], I_M, sortedVertsL[0], triangleNormal);
                            normal = triangleNormal * Utils.GetFaceNormal(sortedVertsL[0], I_M, I_L).magnitude;
                            AppendTriangle(ref vertsOut, ref trisOut, ref normalsOut, sortedVertsL[0], I_M, I_L, triangleNormal);
                            intersectionVerts.Add(I_M);
                            intersectionVerts.Add(I_L);
                            break;
                        }
                    case 3: {
                            Vector3 normal = triangleNormal * Utils.GetFaceNormal(vertsL[0], vertsL[1], vertsL[2]).magnitude;
                            AppendTriangle(ref vertsOut, ref trisOut, ref normalsOut, vertsL[0], vertsL[1], vertsL[2], triangleNormal);
                            break;
                    }


                }
            }
            return (vertsOut.ToArray(), trisOut.ToArray(), normalsOut.ToArray(), intersectionVerts.ToArray());
        }

        public (float vol, Vector3 volCenter) GetSubmergedVolume(Vector3[] verts, int[] tris, Vector3[]  normals, float[] heights, Transform t){
            float totalVolDown = 0f;
            float totalVolUp = 0f;
            Vector3 volCenterSumDown = Vector3.zero;
            Vector3 volCenterSumUp = Vector3.zero;
            
            List<Vector3> _centroidsDown = new List<Vector3>();
            List<Vector3> _centroidsUp = new List<Vector3>();
            
            for (int i = 0; i < tris.Length; i+=3){
                float depth = -heights[i/3];
                Vector3[] tri = new Vector3[]
                {
                    t.TransformPoint(verts[tris[i]]),
                    t.TransformPoint(verts[tris[i + 1]]),
                    t.TransformPoint(verts[tris[i + 2]])
                };
                //determine if face is pointing up (negative contribution) or down (positive contribution)
                bool triPointingDown = t.TransformDirection(normals[i/3]).y < 0;
                // triangle edges projected on horizontal plane (?)
                Vector3 AB = tri[1]-tri[0]; AB.y = 0f;
                Vector3 AC = tri[2]-tri[0]; AC.y = 0f;
                // get the area of the triangle in the horizontal plane
                float projectedArea = 0.5f*Vector3.Cross(AB, AC).magnitude;
                float vol = projectedArea*depth;
                Vector3 centroid = (tri[0] + tri[1] + tri[2]) / 3.0f + new Vector3(0f, depth*0.5f, 0);
                
                if (triPointingDown) {
                    volCenterSumDown += centroid*vol;
                    totalVolDown += vol;
                    _centroidsDown.Add(centroid);
                }
                else {
                    volCenterSumUp += centroid*vol;
                    totalVolUp += vol;
                    _centroidsUp.Add(centroid);
                }
            }
            float totalVol = totalVolDown-totalVolUp;
            if (totalVol == 0f) return (0f, Vector3.zero);
            
            centroidsUp = _centroidsUp.ToArray();
            centroidsDown = _centroidsDown.ToArray();

            centroidUp =    (totalVolUp == 0f)? Vector3.zero : volCenterSumUp/totalVolUp;
            centroidDown =  (totalVolDown == 0f)? Vector3.zero : volCenterSumDown/totalVolDown;
            Vector3 c = (centroidDown * totalVolDown - centroidUp * totalVolUp) / (totalVol);
            return (totalVol, c);
        }

        public (Vector3[], int[], Vector3[]) SplitTrianglesHorizontally(Vector3[] verts, int[] tris, Vector3[] normals, Transform t) {
            List<Vector3> outVerts = new List<Vector3>(tris.Length*2);
            List<int> outTris = new List<int>(tris.Length*2);
            List<Vector3> outNormals = new List<Vector3>((tris.Length*2)/3);
            for (int i = 0; i < tris.Length - 2; i += 3) {
                Vector3[] vertsL = new Vector3[3];
                Vector3[] vertsW = new Vector3[3];
                Vector3 triNormal = normals[i / 3];
                for (int j = 0; j < 3; j++) {
                    vertsL[j] = verts[tris[i + j]];
                    vertsW[j] = t.TransformPoint(vertsL[j]);
                }
                (Vector3[] topTri, Vector3[] botTri) = SplitSubmergedTriangleHorizontally(vertsW);

                AppendTriangle(ref outVerts, ref outTris, ref outNormals, t.InverseTransformPoint(topTri[0]),t.InverseTransformPoint(topTri[1]), t.InverseTransformPoint(topTri[2]), triNormal);
                AppendTriangle(ref outVerts, ref outTris, ref outNormals, t.InverseTransformPoint(botTri[0]), t.InverseTransformPoint(botTri[1]), t.InverseTransformPoint(botTri[2]), triNormal);
            }
            return (outVerts.ToArray(), outTris.ToArray(), outNormals.ToArray());
        }

        public Vector3[] GetPressureCenters(Patch patch, Transform t, Vector3[] verts, int[] tris) {
            int triIdx = 0;
            Vector3[] pressureCenters = new Vector3[tris.Length / 3];
            for (int i = 0; i < tris.Length - 5; i += 6) {
                Vector3[] vertsTopW = new Vector3[3];
                Vector3[] vertsBotW = new Vector3[3];
                for (int j = 0; j < 3; j++) {
                    vertsTopW[j] = t.TransformPoint(verts[tris[i + j]]);
                    vertsBotW[j] = t.TransformPoint(verts[tris[i + j + 3]]);
                }
                pressureCenters[triIdx] = CalculateBuoyancyCenterTopTriangle(patch, vertsTopW);
                pressureCenters[triIdx+1] = CalculateBuoyancyCenterBottomTriangle(patch, vertsBotW);
                triIdx+=2;
            }
            return pressureCenters;
        }


        public float[] GetTriangleCenterHeights(Patch patch, Transform t, Vector3[] verts, int[] tris) {
            float[] heights = new float[verts.Length / 3];
            for (int i = 0; i < tris.Length - 2; i += 3) {
                Vector3 centerVert = (verts[tris[i]] + verts[tris[i+1]] + verts[tris[i+2]])/3.0f;
                heights[i/3] = patch.GetPatchRelativeHeight(t.TransformPoint(centerVert));
            }
            return heights;
        }

        public Vector3[] GetFaceCenters(Transform t, int[] tris, Vector3[] verts){
            int numFaces = tris.Length/3;
            Vector3[] centers = new Vector3[numFaces];
            for (int i = 0; i < tris.Length - 2; i += 3) {
                Vector3 centerLocal = (verts[tris[i]] + verts[tris[i+1]] + verts[tris[i+2]])/3.0f;
                centers[i/3] = t.TransformPoint(centerLocal);
            }
            return centers;

        }

        private (Vector3[], Vector3[]) SplitSubmergedTriangleHorizontally(Vector3[] tri) {
            float[] vertexHeights = new float[3] { tri[0].y, tri[1].y, tri[2].y };
            (Vector3[] sortedVerticesWorld, float[] sortedHeights) = Patch.SortVerticesAgainstFloats(tri, vertexHeights);
            (Vector3 L, Vector3 M, Vector3 H) = (sortedVerticesWorld[0], sortedVerticesWorld[1], sortedVerticesWorld[2]);

            // Initialize the points for the new triangles
            Vector3 D;
            Vector3[] upperTriangle;
            Vector3[] lowerTriangle;

            // Check for vertical alignment of L and H
            if (Math.Abs(H.x - L.x) < 1e-6 && Math.Abs(H.z - L.z) < 1e-6) {
                // If LH is approximately vertical
                D = new Vector3(L.x, M.y, L.z);

                upperTriangle = new Vector3[] { H, M, D };
                lowerTriangle = new Vector3[] { L, D, M };
            }
            else {
                // General case
                // Calculate the slope for the LH line segment
                float dx = H.x - L.x;
                float dz = H.z - L.z;
                float dy = H.y - L.y;
                if (dy == 0) {
                    dy = 1e-12f;
                }
                float mX = dx / dy;
                float mZ = dz / dy;

                // Calculate the x and z coordinates of D
                float x = L.x + mX * (M.y - L.y);
                float z = L.z + mZ * (M.y - L.y);
                D = new Vector3(x, M.y, z);

                upperTriangle = new Vector3[] { H, M, D };
                lowerTriangle = new Vector3[] { L, D, M };
            }

            return (upperTriangle, lowerTriangle);

        }
        public float[] GetTriangleAreas(Vector3[] verts) {
            float[] areas = new float[verts.Length/3];
            for (int i = 0; i < verts.Length-2; i+=3) {
                Vector3 n = Utils.GetFaceNormal(verts[i], verts[i+1], verts[i+2]);
                areas[i / 3] = n.magnitude;
            }
            return areas;
        }

        public void AppendTriangle(ref List<Vector3> verts, ref List<int> tris, ref List<Vector3> normals, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 triNormal) {
            int count = verts.Count;
            verts.Add(v1);
            verts.Add(v2);
            verts.Add(v3);
            tris.Add(count);
            tris.Add(count + 1);
            tris.Add(count + 2);
            normals.Add(triNormal);
        }


        public float GetResistanceCoefficient(float speed, float hullZmin, float hullZmax) {
            float submergedArea = Utils.CalculateMeshArea(mesh);
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            float Rn = CalculateReynoldsNumber(speed, Math.Abs(hullZmax - hullZmin));

            float onePlusK = 0;
            for (int i = 0; i < triangles.Length - 2; i += 3) {
                Vector3 v0 = vertices[triangles[i]];
                Vector3 v1 = vertices[triangles[i + 1]];
                Vector3 v2 = vertices[triangles[i + 2]];
                float Si = (0.5f) * Vector3.Cross((v1 - v0), (v2 - v0)).magnitude;
                float Ki = GetTriangleK((v0.z + v1.z + v2.z) / 3.0f, hullZmin, hullZmax);
                onePlusK += (1 + Ki) * Si;
            }
            onePlusK = Mathf.Clamp(onePlusK / submergedArea, 1.22f, 1.65f);
            float Cf = 0.075f / ((Mathf.Log10(Rn) - 2.0f) * (Mathf.Log10(Rn) - 2.0f));
            float Cfr = onePlusK * Cf;
            return Cfr;
        }

        private float CalculateReynoldsNumber(float velocity, float L, float viscosity = Constants.waterViscosity) {
            return (velocity * L) / viscosity;
        }


        private float GetTriangleK(float z, float hullZmin, float hullZmax) {
            float f = (-3.0f / (hullZmax - hullZmin)) * z + 3.0f * hullZmax / (hullZmax - hullZmin) - 1.0f;
            return f;
        }

        private Vector3 CalculateBuoyancyCenterTopTriangle(Patch patch, Vector3[] triVerts) {
            // takes in a triangle in world coordinates (with a horizontal base) and calculates its center of pressure/buoyancy
            Vector3 A = triVerts[0];
            Vector3 B = triVerts[1];
            Vector3 C = triVerts[2];

            float y0 = -patch.GetPatchRelativeHeight(A);
            float h = A.y - B.y; 
            float tc = (4.0f * y0 + 3.0f * h) / (6.0f * y0 + 4.0f * h);

            //if ((6 * y0 + 4 * h) == 0) {
            //    tc = 0.75f;
            //}
            //tc = 0.75f;

            Vector3 centerBuoyancy = A + tc * ((B + C) / 2.0f - A);
            return centerBuoyancy;
        }


        private Vector3 CalculateBuoyancyCenterBottomTriangle(Patch patch, Vector3[] triVerts) {
            Vector3 A = triVerts[0];
            Vector3 B = triVerts[1];
            Vector3 C = triVerts[2];

            float y0 = -patch.GetPatchRelativeHeight(B);
            float h = B.y-A.y;
            float tc = (2.0f * y0 + h) / (6.0f * y0 + 2.0f * h);
            //tc = 0.5f;
            Vector3 centerBuoyancy = A + tc * ((B + C) / 2.0f - A);
            return centerBuoyancy;
        }
    }
}