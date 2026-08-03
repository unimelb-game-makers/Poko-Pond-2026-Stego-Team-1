using UnityEngine;

public class test : MonoBehaviour
{
    public int pointsPerSide = 5; 
    public float squareSize = 10f;
    public GameObject pointPrefab;

    void Start()
    {
        GenerateBorder();
    }

    void GenerateBorder()
    {
        float halfSize = squareSize / 2f;

        for (int i = 0; i < pointsPerSide; i++)
        {
            // Normalize current step along the side
            float t = (float)i / (pointsPerSide - 1);
            float currentPos = Mathf.Lerp(-halfSize, halfSize, t);

            // 1. Bottom Edge (Left to Right)
            SpawnPoint(new Vector3(currentPos, -halfSize, 0));

            // 2. Top Edge (Left to Right)
            SpawnPoint(new Vector3(currentPos, halfSize, 0));

            // Avoid duplicating corner points on vertical sides
            if (i > 0 && i < pointsPerSide - 1)
            {
                // 3. Left Edge (Bottom to Top)
                SpawnPoint(new Vector3(-halfSize, currentPos, 0));

                // 4. Right Edge (Bottom to Top)
                SpawnPoint(new Vector3(halfSize, currentPos, 0));
            }
        }
    }

    void SpawnPoint(Vector3 position)
    {
        if (pointPrefab != null)
        {
            Instantiate(pointPrefab, position, Quaternion.identity);
        }
    }
}