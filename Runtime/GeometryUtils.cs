using System.Collections.Generic;
using UnityEngine;

namespace LightingTools.LightProbesVolumes
{
    /// <summary>
    /// Utility class for mesh-based geometry detection.
    /// Raycasts directly against mesh geometry without requiring colliders.
    /// This allows detection of any static geometry in the scene.
    /// </summary>
    public static class GeometryUtils
    {
        /// <summary>
        /// Performs a raycast against all meshes in the scene (collider or not).
        /// Returns the closest hit point.
        /// </summary>
        /// <param name="ray">The ray to cast</param>
        /// <param name="maxDistance">Maximum distance for the raycast</param>
        /// <param name="hitInfo">Output hit information</param>
        /// <param name="excludeTransform">Optional transform to exclude from raycasting</param>
        /// <returns>True if a mesh was hit</returns>
        public static bool RaycastToMeshes(Ray ray, float maxDistance, out MeshHitInfo hitInfo, Transform excludeTransform = null)
        {
            hitInfo = default;
            float closestDistance = maxDistance;
            bool foundHit = false;

            // Get all MeshFilters in the scene (works with or without colliders)
            MeshFilter[] allMeshFilters = Object.FindObjectsOfType<MeshFilter>();

            foreach (MeshFilter meshFilter in allMeshFilters)
            {
                // Skip excluded transform and its children
                if (excludeTransform != null && meshFilter.transform.IsChildOf(excludeTransform))
                    continue;

                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null)
                    continue;

                // Test ray intersection with this mesh
                if (RayIntersectsMesh(ray, mesh, meshFilter.transform, maxDistance, out MeshHitInfo hit))
                {
                    if (hit.distance < closestDistance)
                    {
                        closestDistance = hit.distance;
                        hitInfo = hit;
                        foundHit = true;
                    }
                }
            }

            return foundHit;
        }

        /// <summary>
        /// Performs a raycast against all meshes and returns ALL hits along the ray, sorted by distance.
        /// </summary>
        /// <param name="ray">The ray to cast</param>
        /// <param name="maxDistance">Maximum distance for the raycast</param>
        /// <param name="excludeTransform">Optional transform to exclude from raycasting</param>
        /// <returns>Array of all hits found, sorted by distance</returns>
        public static MeshHitInfo[] RaycastAllToMeshes(Ray ray, float maxDistance, Transform excludeTransform = null)
        {
            List<MeshHitInfo> allHits = new List<MeshHitInfo>();
            MeshFilter[] allMeshFilters = Object.FindObjectsOfType<MeshFilter>();

            foreach (MeshFilter meshFilter in allMeshFilters)
            {
                // Skip excluded transform and its children
                if (excludeTransform != null && meshFilter.transform.IsChildOf(excludeTransform))
                    continue;

                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null)
                    continue;

                // Get all intersections with this mesh
                MeshHitInfo[] meshHits = RayIntersectsMeshAll(ray, mesh, meshFilter.transform, maxDistance);
                allHits.AddRange(meshHits);
            }

            // Sort by distance
            allHits.Sort((a, b) => a.distance.CompareTo(b.distance));
            return allHits.ToArray();
        }

        /// <summary>
        /// Tests ray-mesh intersection using the Möller-Trumbore algorithm.
        /// Returns true and the closest hit point if intersection occurs.
        /// </summary>
        private static bool RayIntersectsMesh(Ray ray, Mesh mesh, Transform meshTransform, float maxDistance, out MeshHitInfo hitInfo)
        {
            hitInfo = default;
            float closestDistance = maxDistance;
            bool foundHit = false;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            // For each triangle in the mesh
            for (int i = 0; i < triangles.Length; i += 3)
            {
                // Get the three vertices of this triangle in world space
                Vector3 v0 = meshTransform.TransformPoint(vertices[triangles[i]]);
                Vector3 v1 = meshTransform.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 v2 = meshTransform.TransformPoint(vertices[triangles[i + 2]]);

                // Test ray-triangle intersection using Möller-Trumbore algorithm
                if (RayTriangleIntersect(ray.origin, ray.direction, v0, v1, v2, maxDistance, out float distance, out Vector3 hitPoint))
                {
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        hitInfo = new MeshHitInfo
                        {
                            point = hitPoint,
                            distance = distance,
                            transform = meshTransform
                        };
                        foundHit = true;
                    }
                }
            }

            return foundHit;
        }

        /// <summary>
        /// Tests ray-mesh intersection and returns ALL hit points.
        /// </summary>
        private static MeshHitInfo[] RayIntersectsMeshAll(Ray ray, Mesh mesh, Transform meshTransform, float maxDistance)
        {
            List<MeshHitInfo> hits = new List<MeshHitInfo>();

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            // For each triangle in the mesh
            for (int i = 0; i < triangles.Length; i += 3)
            {
                // Get the three vertices of this triangle in world space
                Vector3 v0 = meshTransform.TransformPoint(vertices[triangles[i]]);
                Vector3 v1 = meshTransform.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 v2 = meshTransform.TransformPoint(vertices[triangles[i + 2]]);

                // Test ray-triangle intersection
                if (RayTriangleIntersect(ray.origin, ray.direction, v0, v1, v2, maxDistance, out float distance, out Vector3 hitPoint))
                {
                    hits.Add(new MeshHitInfo
                    {
                        point = hitPoint,
                        distance = distance,
                        transform = meshTransform
                    });
                }
            }

            return hits.ToArray();
        }

        /// <summary>
        /// Möller-Trumbore ray-triangle intersection algorithm.
        /// Returns true if ray intersects the triangle and outputs the hit distance and point.
        /// </summary>
        private static bool RayTriangleIntersect(Vector3 rayOrigin, Vector3 rayDir, Vector3 v0, Vector3 v1, Vector3 v2, float maxDist, out float t, out Vector3 hitPoint)
        {
            const float Epsilon = 0.0000001f;
            hitPoint = Vector3.zero;
            t = maxDist;

            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 h = Vector3.Cross(rayDir, edge2);
            float a = Vector3.Dot(edge1, h);

            // Ray is parallel to triangle
            if (Mathf.Abs(a) < Epsilon)
                return false;

            float f = 1.0f / a;
            Vector3 s = rayOrigin - v0;
            float u = f * Vector3.Dot(s, h);

            if (u < 0.0f || u > 1.0f)
                return false;

            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(rayDir, q);

            if (v < 0.0f || u + v > 1.0f)
                return false;

            // Calculate intersection distance
            t = f * Vector3.Dot(edge2, q);

            if (t > Epsilon && t < maxDist)
            {
                hitPoint = rayOrigin + rayDir * t;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Finds the closest mesh hit below a point (raycasts downward).
        /// Works with any static geometry, collider or not.
        /// </summary>
        public static bool FindFloorBelow(Vector3 position, float maxDistance, out MeshHitInfo hitInfo, Transform excludeTransform = null)
        {
            Ray downRay = new Ray(position, Vector3.down);
            return RaycastToMeshes(downRay, maxDistance, out hitInfo, excludeTransform);
        }

        /// <summary>
        /// Finds all mesh hits below a point, useful for multi-layer geometry detection.
        /// </summary>
        public static MeshHitInfo[] FindAllFloorsBelow(Vector3 position, float maxDistance, Transform excludeTransform = null)
        {
            Ray downRay = new Ray(position, Vector3.down);
            return RaycastAllToMeshes(downRay, maxDistance, excludeTransform);
        }

        /// <summary>
        /// Tests if a position is inside geometry by casting rays in opposite directions.
        /// If hit counts differ, the probe is likely inside geometry.
        /// </summary>
        public static bool IsPositionInsideGeometry(Vector3 probePosition, Vector3 referencePosition, Transform excludeTransform = null)
        {
            Vector3 direction = Vector3.Normalize(probePosition - referencePosition);
            float distance = Vector3.Distance(probePosition, referencePosition);

            // Ray from reference to probe
            Ray forwardRay = new Ray(referencePosition, direction);
            MeshHitInfo[] hitsForward = RaycastAllToMeshes(forwardRay, distance, excludeTransform);

            // Ray from probe back to reference
            Ray backwardRay = new Ray(probePosition, -direction);
            MeshHitInfo[] hitsBackward = RaycastAllToMeshes(backwardRay, distance, excludeTransform);

            // If hit counts differ, probe is inside geometry
            return hitsForward.Length != hitsBackward.Length;
        }
    }

    /// <summary>
    /// Information about a mesh raycast hit.
    /// Similar to Unity's RaycastHit but for mesh-based raycasting.
    /// </summary>
    public struct MeshHitInfo
    {
        /// <summary>The point in world space where the ray hit the mesh.</summary>
        public Vector3 point;

        /// <summary>The distance from the ray origin to the hit point.</summary>
        public float distance;

        /// <summary>The transform of the mesh that was hit.</summary>
        public Transform transform;
    }
}
