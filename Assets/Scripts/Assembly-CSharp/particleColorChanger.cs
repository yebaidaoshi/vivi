using System;
using UnityEngine;

[ExecuteInEditMode]
public class particleColorChanger : MonoBehaviour
{
	[Serializable]
	public class colorChange
	{
		public string Name;

		public ParticleSystem[] colored_ParticleSystem;

		public Gradient Gradient_custom;
	}

	public colorChange[] colorChangeList;

	public bool applyChanges;

	public bool Keep_applyChanges;

	private void Update()
	{
		if (!applyChanges && !Keep_applyChanges)
		{
			return;
		}
		for (int i = 0; i < colorChangeList.Length; i++)
		{
			for (int j = 0; j < colorChangeList[i].colored_ParticleSystem.Length; j++)
			{
				ParticleSystem.ColorOverLifetimeModule colorOverLifetime = colorChangeList[i].colored_ParticleSystem[j].colorOverLifetime;
				colorOverLifetime.color = colorChangeList[i].Gradient_custom;
			}
		}
		applyChanges = false;
	}
}
