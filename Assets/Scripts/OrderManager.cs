using UnityEngine;

public class OrderManager : MonoBehaviour
{
    [SerializeField] GameObject orderTransform;
    [SerializeField] Vector3 areaToSpawnOrder;

    void Start()
    {
        var randomPosition = new Vector3(Random.Range(-areaToSpawnOrder.x, areaToSpawnOrder.x), 0f, Random.Range(-areaToSpawnOrder.z, areaToSpawnOrder.z));

        Instantiate(orderTransform, randomPosition, transform.rotation);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, areaToSpawnOrder);
    }
}