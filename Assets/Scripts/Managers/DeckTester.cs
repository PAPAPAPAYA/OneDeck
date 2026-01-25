using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DefaultNamespace.Managers
{
	public class DeckTester : MonoBehaviour
	{
		#region SINGLETON

		public static DeckTester me;

		private void Awake()
		{
			me = this;
		}

		#endregion

		public DeckSO deckARef;
		public DeckSO deckBRef;
		public bool autoSpace;
		public int sessionAmountTarget;
		[HideInInspector]
		public int currentSessionAmount;
		public float deckAWins;
		public float deckBWins;
		public List<int> deckAHPs;
		public List<int> deckBHPs;
		public List<float> deckADmgOutputs_ToOpp;
		public List<float> deckADmgOutputs_ToSelf;
		public List<float> deckBDmgOutputs_ToOpp;
		public List<float> deckBDmgOutputs_ToSelf;
		public List<float> deckADmgOutputs_ToOpp_PerSession = new List<float>();
		public List<float> deckADmgOutputs_ToSelf_PerSession = new List<float>();
		public List<float> deckBDmgOutputs_ToOpp_PerSession = new List<float>();
		public List<float> deckBDmgOutputs_ToSelf_PerSession = new List<float>();
		

		public void CalculateSessionAveDmg()
		{
			if (deckADmgOutputs_ToOpp.Count > 0)
			{
				deckADmgOutputs_ToOpp_PerSession.Add(deckADmgOutputs_ToOpp.Sum());
				deckADmgOutputs_ToOpp.Clear();
			}
			else
			{
				deckADmgOutputs_ToOpp_PerSession.Add(0);
			}

			if (deckBDmgOutputs_ToOpp.Count > 0)
			{
				deckBDmgOutputs_ToOpp_PerSession.Add(deckBDmgOutputs_ToOpp.Sum());
				deckBDmgOutputs_ToOpp.Clear();
			}
			else
			{
				deckBDmgOutputs_ToOpp_PerSession.Add(0);
			}

			if (deckADmgOutputs_ToSelf.Count > 0)
			{
				deckADmgOutputs_ToSelf_PerSession.Add(deckADmgOutputs_ToSelf.Sum());
				deckADmgOutputs_ToSelf.Clear();
			}
			else
			{
				deckADmgOutputs_ToSelf_PerSession.Add(0);
			}

			if (deckBDmgOutputs_ToSelf.Count > 0)
			{
				deckBDmgOutputs_ToSelf_PerSession.Add(deckBDmgOutputs_ToSelf.Sum());
				deckBDmgOutputs_ToSelf.Clear();
			}
			else
			{
				deckBDmgOutputs_ToSelf_PerSession.Add(0);
			}
		}
		
		private void Update()
		{
			if (currentSessionAmount >= sessionAmountTarget && autoSpace)
			{
				autoSpace = false;

				float deckAWinRate = deckAWins / (deckAWins + deckBWins);
				float deckBWinRate = 1 - deckAWinRate;

				float deckAAveDmgToOppPerSession = deckADmgOutputs_ToOpp_PerSession.Average();
				float deckBAveDmgToOppPerSession = deckBDmgOutputs_ToOpp_PerSession.Average();
				
				float deckAAveDmgToSelfPerSession = deckADmgOutputs_ToSelf_PerSession.Average();
				float deckBAveDmgToSelfPerSession = deckBDmgOutputs_ToSelf_PerSession.Average();

				print("win rates: " + deckAWinRate * 100 + "% vs " + deckBWinRate * 100 + "%");
				print("HPs: " + deckAHPs.Average() + " vs " + deckBHPs.Average());
				print("Ave Dmgs to enemy: " + deckAAveDmgToOppPerSession + " vs " + deckBAveDmgToOppPerSession);
				print("Ave Dmgs to self: " + deckAAveDmgToSelfPerSession + " vs " + deckBAveDmgToSelfPerSession);
				
			}
		}
	}
}