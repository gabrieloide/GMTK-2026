using UnityEngine;
using System.Collections.Generic;

public class CarSpawner : MonoBehaviour
{
    [SerializeField] private GameObject carPrefab;
    [SerializeField] private Material[] carMaterials;
    [SerializeField] private RoadNetwork roadNetwork;
    [SerializeField] private float minSpawnInterval = 4f;
    [SerializeField] private float maxSpawnInterval = 9f;
    [SerializeField] private float carSpeed = 8f;
    [SerializeField] private int maxConcurrentCars = 4;

    private float timer;
    private float nextSpawnTime;
    private readonly List<GameObject> activeCars = new List<GameObject>();

    private void Start()
    {
        nextSpawnTime = Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    private void Update()
    {
        if (roadNetwork == null || carPrefab == null) return;

        activeCars.RemoveAll(car => car == null);
        if (activeCars.Count >= maxConcurrentCars) return;

        timer += Time.deltaTime;
        if (timer >= nextSpawnTime)
        {
            timer = 0f;
            nextSpawnTime = Random.Range(minSpawnInterval, maxSpawnInterval);
            SpawnCar();
        }
    }

    private void SpawnCar()
    {
        var spawnNode = roadNetwork.GetRandomSpawnNode();
        if (spawnNode == null) return;

        var firstTarget = roadNetwork.GetRandomNeighbor(spawnNode, null);
        if (firstTarget == null) return;

        var rotation = FacingRotation(spawnNode, firstTarget);
        var car = Instantiate(carPrefab, spawnNode.transform.position, rotation);
        activeCars.Add(car);

        var obstacle = car.GetComponent<CarObstacle>();
        if (obstacle != null) obstacle.Init(roadNetwork, spawnNode, firstTarget, carSpeed);

        if (carMaterials != null && carMaterials.Length > 0)
        {
            var carRenderer = car.GetComponentInChildren<Renderer>();
            if (carRenderer != null) carRenderer.material = carMaterials[Random.Range(0, carMaterials.Length)];
        }
    }

    private static Quaternion FacingRotation(RoadNode from, RoadNode to)
    {
        var dir = to.transform.position - from.transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : from.transform.rotation;
    }
}
