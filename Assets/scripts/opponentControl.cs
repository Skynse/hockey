using System;
using UnityEngine;
using UnityEngine.UIElements;

public class opponentControl : MonoBehaviour
{

    private float leftBoundary = 4.5f;
    private float rightBoundary = -4.5f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        float startingValue = 0;
        float moveSpeed = 2f;

        var opponentPos = Math.Sin(Time.time * moveSpeed) * leftBoundary;
        if (opponentPos > leftBoundary)
        {
            opponentPos = leftBoundary;
        }

        else
        {
            if (opponentPos < rightBoundary)
            {
                opponentPos = rightBoundary;
            }
        }

        transform.position = new Vector3((float)opponentPos, transform.position.y, transform.position.z);

    }
}
