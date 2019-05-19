using System;
using System.Collections.Generic;
using System.Timers;
using Newtonsoft.Json;

namespace BidCat
{
	public class Cooldown
	{
		public List<SoftCooldown> softCooldowns = new List<SoftCooldown>();
		public HardCooldown hardCooldown = new HardCooldown();

		public class SoftCooldown
		{
			public bool CooldownActive = false;
			public int CooldownLength;
			public int CooldownIncrementLength;
			public int CooldownMinimumBid;
			public int CooldownIncrementAmount;
			[JsonIgnore]
			public Timer CooldownTimer;
			public DateTime DueTime;
			public bool CooldownLotBased = false;

			public SoftCooldown()
			{ }

			public SoftCooldown(SoftCooldown other)
			{
				CooldownActive = other.CooldownActive;
				CooldownTimer = other.CooldownTimer;
				CooldownIncrementAmount = other.CooldownIncrementAmount;
				CooldownIncrementLength = other.CooldownIncrementLength;
				CooldownLength = other.CooldownLength;
				CooldownLotBased = other.CooldownLotBased;
				CooldownMinimumBid = other.CooldownMinimumBid;
				DueTime = other.DueTime;
			}
		}

		public class HardCooldown
		{
			public bool CooldownActive = false;
			public int CooldownLength;
			[JsonIgnore]
			public Timer CooldownTimer;
			public DateTime DueTime;
			public bool CooldownLotBased = false;

			public HardCooldown()
			{ }

			public HardCooldown(HardCooldown other)
			{
				CooldownTimer = other.CooldownTimer;
				CooldownActive = other.CooldownActive;
				CooldownLength = other.CooldownLength;
				CooldownLotBased = other.CooldownLotBased;
				DueTime = other.DueTime;
			}
		}

		public Cooldown(Cooldown other)
		{
			foreach (SoftCooldown otherSoftCooldown in other.softCooldowns)
			{
				SoftCooldown softCooldown = new SoftCooldown
				{
					CooldownLength = otherSoftCooldown.CooldownLength,
					CooldownActive = otherSoftCooldown.CooldownActive,
					CooldownIncrementAmount = otherSoftCooldown.CooldownIncrementAmount,
					CooldownIncrementLength = otherSoftCooldown.CooldownIncrementLength,
					CooldownLotBased = otherSoftCooldown.CooldownLotBased,
					CooldownMinimumBid = otherSoftCooldown.CooldownMinimumBid,
					CooldownTimer = otherSoftCooldown.CooldownTimer,
					DueTime = otherSoftCooldown.DueTime
				};
				softCooldowns.Add(softCooldown);
			}

			hardCooldown.CooldownTimer = other.hardCooldown.CooldownTimer;
			hardCooldown.CooldownActive = other.hardCooldown.CooldownActive;
			hardCooldown.CooldownLength = other.hardCooldown.CooldownLength;
			hardCooldown.CooldownLotBased = other.hardCooldown.CooldownLotBased;
			hardCooldown.DueTime = other.hardCooldown.DueTime;
		}

		public Cooldown()
		{ }
	}
}
