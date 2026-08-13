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
    private readonly Queue<GameObject> pool = new Queue<GameObject>();

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
        var spawnPoint = roadNetwork.GetRandomEntradaSpawnPoint();
        if (spawnPoint == null || spawnPoint.connectedNode == null) return;

        var entryNode = spawnPoint.connectedNode;
        var firstTarget = roadNetwork.GetRandomNeighbor(entryNode, null);
        if (firstTarget == null) return;

        var rotation = FacingRotation(spawnPoint.transform.position, entryNode.transform.position);
        
        GameObject car;
        if (pool.Count > 0)
        {
            car = pool.Dequeue();
            car.transform.position = spawnPoint.transform.position;
            car.transform.rotation = rotation;
            car.SetActive(true);
        }
        else
        {
            car = Instantiate(carPrefab, spawnPoint.transform.position, rotation);
            car.transform.SetParent(this.transform);
        }
        
        activeCars.Add(car);

        var obstacle = car.GetComponent<CarObstacle>();
        if (obstacle != null) obstacle.Init(roadNetwork, entryNode, firstTarget, carSpeed, OnCarDespawn);

        if (carMaterials != null && carMaterials.Length > 0)
        {
            var carRenderer = car.GetComponentInChildren<Renderer>();
            if (carRenderer != null) carRenderer.material = carMaterials[Random.Range(0, carMaterials.Length)];
        }
    }

    private void OnCarDespawn(CarObstacle car)
    {
        car.gameObject.SetActive(false);
        activeCars.Remove(car.gameObject);
        if (!pool.Contains(car.gameObject))
        {
            pool.Enqueue(car.gameObject);
        }
    }

    private static Quaternion FacingRotation(Vector3 from, Vector3 to)
    {
        var dir = to - from;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity;
    }
}
