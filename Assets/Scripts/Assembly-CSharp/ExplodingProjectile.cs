using UnityEngine;

public class ExplodingProjectile : MonoBehaviour
{
	public GameObject impactPrefab;

	public GameObject explosionPrefab;

	public float thrust;

	public Rigidbody thisRigidbody;

	public GameObject particleKillGroup;

	private Collider thisCollider;

	public bool LookRotation = true;

	public bool Missile;

	public Transform missileTarget;

	public float projectileSpeed;

	public float projectileSpeedMultiplier;

	public bool ignorePrevRotation;

	public bool explodeOnTimer;

	public float explosionTimer;

	private float timer;

	private Vector3 previousPosition;

	private void Start()
	{
		thisRigidbody = GetComponent<Rigidbody>();
		if (Missile)
		{
			missileTarget = GameObject.FindWithTag("Target").transform;
		}
		thisCollider = GetComponent<Collider>();
		previousPosition = base.transform.position;
	}

	private void Update()
	{
		timer += Time.deltaTime;
		if (timer >= explosionTimer && explodeOnTimer)
		{
			Explode();
		}
	}

	private void FixedUpdate()
	{
		if (Missile)
		{
			projectileSpeed += projectileSpeed * projectileSpeedMultiplier;
			base.transform.LookAt(missileTarget);
			thisRigidbody.AddForce(base.transform.forward * projectileSpeed);
		}
		if (LookRotation && timer >= 0.05f)
		{
			base.transform.rotation = Quaternion.LookRotation(thisRigidbody.velocity);
		}
		CheckCollision(previousPosition);
		previousPosition = base.transform.position;
	}

	private void CheckCollision(Vector3 prevPos)
	{
		Vector3 direction = base.transform.position - prevPos;
		Ray ray = new Ray(prevPos, direction);
		float maxDistance = Vector3.Distance(base.transform.position, prevPos);
		if (Physics.Raycast(ray, out var hitInfo, maxDistance))
		{
			base.transform.position = hitInfo.point;
			Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, hitInfo.normal);
			Vector3 point = hitInfo.point;
			Object.Instantiate(impactPrefab, point, rotation);
			if (!explodeOnTimer && !Missile)
			{
				Object.Destroy(base.gameObject);
			}
			else if (Missile)
			{
				thisCollider.enabled = false;
				particleKillGroup.SetActive(value: false);
				thisRigidbody.velocity = Vector3.zero;
				Object.Destroy(base.gameObject, 5f);
			}
		}
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (collision.gameObject.tag != "FX")
		{
			ContactPoint contactPoint = collision.contacts[0];
			Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, contactPoint.normal);
			if (ignorePrevRotation)
			{
				rotation = Quaternion.Euler(0f, 0f, 0f);
			}
			Vector3 point = contactPoint.point;
			Object.Instantiate(impactPrefab, point, rotation);
			if (!explodeOnTimer && !Missile)
			{
				Object.Destroy(base.gameObject);
			}
			else if (Missile)
			{
				thisCollider.enabled = false;
				particleKillGroup.SetActive(value: false);
				thisRigidbody.velocity = Vector3.zero;
				Object.Destroy(base.gameObject, 5f);
			}
		}
	}

	private void Explode()
	{
		Object.Instantiate(explosionPrefab, base.gameObject.transform.position, Quaternion.Euler(0f, 0f, 0f));
		Object.Destroy(base.gameObject);
	}
}
