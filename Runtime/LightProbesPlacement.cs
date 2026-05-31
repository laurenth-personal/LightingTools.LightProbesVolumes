using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LightingTools.LightProbesVolumes
{
    public static class LightProbesPlacement
    {
#if UNITY_EDITOR
        public static void Populate (GameObject gameObject, float horizontalSpacing, float verticalSpacing, float offsetFromFloor, int numberOfLayers, bool drawDebug, bool fillVolume, bool discardInsideGeometry, bool followFloor)
        {
            BoxCollider boxCollider = gameObject.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                Debug.LogWarning("Box collider not found on " + gameObject.name);
                return;
            }
            //Make sure collider is a trigger
            boxCollider.isTrigger = true;

            //avoid division by 0
            horizontalSpacing = Mathf.Max(horizontalSpacing, 0.01f);
            verticalSpacing = Mathf.Max(verticalSpacing, 0.01f);

            //Check if there is already a lightprobegroup component
            // if there is destroy it
            LightProbeGroup oldLightprobes = gameObject.GetComponent<LightProbeGroup>();

            //Calculate Start Points at the top of the collider
            Vector3[] startPositions = StartPoints(boxCollider.size, boxCollider.center, boxCollider.transform, horizontalSpacing);

            float minY = boxCollider.bounds.min.y;
            float maxY = boxCollider.bounds.max.y;

            float sizeY = boxCollider.size.y;
            int ycount = Mathf.FloorToInt((sizeY-offsetFromFloor) / verticalSpacing) + 1;

            List<Vector3> VertPositions = new List<Vector3>();

            int currentTrace = 0;

            //if followFloor we raycast from top to down to find the floor height using Physics colliders
            //and then place probes above that floor
            if(followFloor)
            {
                foreach (Vector3 startPos in startPositions)
                {
                    // Use Physics raycast to detect floor colliders (fast and optimized)
                    Ray ray = new Ray(startPos, Vector3.down);
                    RaycastHit[] hits = Physics.RaycastAll(ray, sizeY + 1, -1, QueryTriggerInteraction.Ignore);

                    //Validate hits and find the floor
                    foreach (var hit in hits)
                    {
                        // Only use static geometry as floor
                        if (!hit.collider.gameObject.isStatic)
                            continue;

                        float floorHeight = hit.point.y;
                        
                        if (floorHeight + offsetFromFloor < maxY && floorHeight + offsetFromFloor > minY)
                            VertPositions.Add(hit.point + new Vector3(0, offsetFromFloor, 0));

                        int maxLayer = fillVolume ? ycount : numberOfLayers;

                        for (int i = 1; i < maxLayer; i++)
                        {
                            float probeHeight = floorHeight + offsetFromFloor + i * verticalSpacing;
                            if (probeHeight < maxY && probeHeight > minY)
                                VertPositions.Add(new Vector3(startPos.x, probeHeight, startPos.z));
                        }
                        
                        // Only use the first (lowest) hit as the floor
                        break;
                    }

                    if (hits.Length == 0)
                    {
                        Debug.LogWarning("No floor collider found below probe position at " + startPos + ". Make sure your floor geometry has a collider and is marked as static.");
                    }

                    EditorUtility.DisplayProgressBar("Tracing floor colliders", currentTrace.ToString() + "/" + startPositions.Length.ToString(), (float)currentTrace / (float)startPositions.Length);
                    currentTrace++;
                }
                EditorUtility.ClearProgressBar();
            }

            else
            {
                int maxLayer = fillVolume ? ycount : numberOfLayers;

                for (int i = 0; i< maxLayer; i++)
                {
                    foreach(Vector3 position in startPositions)
                    {
                        VertPositions.Add(position + Vector3.up * verticalSpacing * i - Vector3.up*sizeY + Vector3.up * offsetFromFloor);
                    }
                }
            }

            if(drawDebug)
            {
                foreach(Vector3 position in VertPositions)
                {
                    Debug.DrawLine(position, position + Vector3.up * 0.5f, Color.red, 3);
                }
            }

            List<Vector3> validVertPositions = new List<Vector3>();

            //Inside Geometry test : use mesh-based detection to check if probes are inside geometry
            //This works with any geometry in the scene, regardless of colliders
            if (discardInsideGeometry)
            {
                int j = 0;
                Vector3 insideTestPosition = gameObject.transform.position + gameObject.GetComponent<BoxCollider>().center + new Vector3(0, maxY / 2, 0);
                if (drawDebug)
                {
                    Debug.DrawLine(insideTestPosition + Vector3.up, insideTestPosition - Vector3.up, Color.green, 5);
                    Debug.DrawLine(insideTestPosition + Vector3.right, insideTestPosition - Vector3.right, Color.green, 5);
                    Debug.DrawLine(insideTestPosition + Vector3.forward, insideTestPosition - Vector3.forward, Color.green, 5);
                }
                foreach (Vector3 positionCandidate in VertPositions)
                {
                    EditorUtility.DisplayProgressBar("Checking probes inside geometry", j.ToString() + "/" + VertPositions.Count, (float)j / (float)VertPositions.Count);

                    // Use mesh-based geometry detection to check if probe is inside any mesh
                    if (GeometryUtils.IsPositionInsideGeometry(positionCandidate, insideTestPosition, gameObject.transform))
                    {
                        // Probe is inside geometry, skip it
                        if (drawDebug)
                        {
                            Vector3 direction = Vector3.Normalize(positionCandidate - insideTestPosition);
                            float distance = Vector3.Distance(positionCandidate, insideTestPosition);
                            Debug.DrawRay(positionCandidate, -direction * distance, Color.cyan, 5);
                        }
                    }
                    else
                    {
                        validVertPositions.Add(positionCandidate);
                    }
                    j++;
                }
                EditorUtility.ClearProgressBar();
            }
            else
                validVertPositions = VertPositions;


            // Check if we have any hits
            if (validVertPositions.Count < 1)
            {
                Debug.Log("no valid hit for " + gameObject.name);
                return;
            }

            LightProbeGroup LPGroup = oldLightprobes != null ? oldLightprobes : gameObject.AddComponent<LightProbeGroup>();

            // Feed lightprobe positions
            Vector3[] ProbePos = new Vector3[validVertPositions.Count];
            for (int i = 0; i < validVertPositions.Count; i++)
            {
                ProbePos[i] = gameObject.transform.InverseTransformPoint(validVertPositions[i]);
            }
            LPGroup.probePositions = ProbePos;

            //Finish
            Debug.Log("Finished placing " + ProbePos.Length + " probes for " + gameObject.name);
        }

        static Vector3[] StartPoints(Vector3 size, Vector3 offset, Transform transform, float horizontalSpacing)
        {
            // Calculate count and start offset
            int xCount = Mathf.FloorToInt(size.x / horizontalSpacing) + 1;
            int zCount = Mathf.FloorToInt(size.z / horizontalSpacing) + 1;
            float startxoffset = (size.x - (xCount-1) * horizontalSpacing)/2;
            float startzoffset = (size.z - (zCount-1) * horizontalSpacing)/2;

            //if lightprobe count fits exactly in bounds, I know the probes at the maximum bounds will be rejected, so add offset
            if (startxoffset == 0)
                startxoffset = horizontalSpacing / 2;
            if (startzoffset == 0)
                startzoffset = horizontalSpacing / 2;

            Vector3[] vertPositions = new Vector3[ xCount * zCount ];

            int vertexnumber = 0;

            for (int i = 0; i < xCount; i++)
            {
                for (int j = 0; j < zCount; j++ )
                {
                    Vector3 position = new Vector3
                    {
                        y = size.y / 2,
                        x = startxoffset + (i * horizontalSpacing) - (size.x / 2),
                        z = startzoffset + (j * horizontalSpacing) - (size.z / 2)
                    };

                    vertPositions[vertexnumber] = transform.TransformPoint(position + offset);

                    vertexnumber++;
                }
            }

            return vertPositions;
        }
#endif
    }
}
