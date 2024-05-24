using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class FloatersBurst : MonoBehaviour
{
    // Public parameters
    public WaterSurface waterSurface;

    // List of internal cubes
    public List<GameObject> objects = new List<GameObject>();
    private int numObjects;

    // Input job parameters
    NativeArray<float3> targetPositionBuffer;

    // Output job parameters
    NativeArray<float> errorBuffer;
    NativeArray<float3> candidatePositionBuffer;
    NativeArray<float3> projectedPositionWSBuffer;
    NativeArray<float3> directionBuffer;
    NativeArray<int> stepCountBuffer;

    // Start is called before the first frame update
    void Start()
    {
        foreach(Transform child in gameObject.transform)
        {
            objects.Add(child.gameObject);
        }

        numObjects = objects.Count;

        // Allocate the buffers
        targetPositionBuffer = new NativeArray<float3>(numObjects, Allocator.Persistent);
        errorBuffer = new NativeArray<float>(numObjects, Allocator.Persistent);
        candidatePositionBuffer = new NativeArray<float3>(numObjects, Allocator.Persistent);
        projectedPositionWSBuffer = new NativeArray<float3>(numObjects, Allocator.Persistent);
        directionBuffer = new NativeArray<float3>(numObjects, Allocator.Persistent);
        stepCountBuffer = new NativeArray<int>(numObjects, Allocator.Persistent);
        
    }

    // Update is called once per frame
    void Update()
    {
        if (waterSurface == null)
        {
            Debug.LogWarning("water surface property not set");
            return;
        }
        // Try to get the simulation data if available
        WaterSimSearchData simData = new WaterSimSearchData();
        if (!waterSurface.FillWaterSearchData(ref simData))
            return;

        // Fill the input positions
        int numElements = numObjects;
        for (int i = 0; i < numElements; ++i)
            targetPositionBuffer[i] = objects[i].transform.position;

        // Prepare the first band
        WaterSimulationSearchJob searchJob = new WaterSimulationSearchJob();

        // Assign the simulation data
        searchJob.simSearchData = simData;

        // Fill the input data
        searchJob.targetPositionWSBuffer = targetPositionBuffer;
        searchJob.startPositionWSBuffer = targetPositionBuffer;
        searchJob.maxIterations = 8;
        searchJob.error = 0.01f;
        searchJob.includeDeformation = true;
        searchJob.excludeSimulation = false;

        searchJob.errorBuffer = errorBuffer;
        searchJob.candidateLocationWSBuffer = candidatePositionBuffer;
        searchJob.projectedPositionWSBuffer = projectedPositionWSBuffer;
        searchJob.directionBuffer = directionBuffer;
        searchJob.stepCountBuffer = stepCountBuffer;

        // Schedule the job with one Execute per index in the results array and only 1 item per processing batch
        JobHandle handle = searchJob.Schedule(numElements, 1);
        handle.Complete();

        // Fill the input positions
        for (int i = 0; i < numElements; ++i)
            objects[i].transform.position = projectedPositionWSBuffer[i];
    }

    private void OnDestroy()
    {
        targetPositionBuffer.Dispose();
        errorBuffer.Dispose();
        candidatePositionBuffer.Dispose();
        projectedPositionWSBuffer.Dispose();
        directionBuffer.Dispose();
        stepCountBuffer.Dispose();
    }
}
