using UnityEngine;

public class destroyMe : MonoBehaviour
{
	private float timer;

	public float deathtimer = 10f;

	private void Start()
	{
	}

	private void Update()
	{
		timer += Time.deltaTime;
		if (timer >= deathtimer)
		{
			Object.Destroy(base.gameObject);
		}
	}
}
