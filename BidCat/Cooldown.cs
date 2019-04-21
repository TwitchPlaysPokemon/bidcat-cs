using System;
using System.Timers;
using Newtonsoft.Json;

namespace BidCat
{
	public class Cooldown
	{
		public SoftCooldown softCooldown = new SoftCooldown();
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
			softCooldown.CooldownLength = other.softCooldown.CooldownLength;
			softCooldown.CooldownActive = other.softCooldown.CooldownActive;
			softCooldown.CooldownIncrementAmount = other.softCooldown.CooldownIncrementAmount;
			softCooldown.CooldownIncrementLength = other.softCooldown.CooldownIncrementLength;
			softCooldown.CooldownLotBased = other.softCooldown.CooldownLotBased;
			softCooldown.CooldownMinimumBid = other.softCooldown.CooldownMinimumBid;
			softCooldown.CooldownTimer = other.softCooldown.CooldownTimer;
			softCooldown.DueTime = other.softCooldown.DueTime;
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
