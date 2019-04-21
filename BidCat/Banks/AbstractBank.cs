using ApiListener;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace BidCat.Banks
{
	public abstract class AbstractBank
	{
		/// <summary>
		/// A list of functions that take a user and return reserved money
		/// </summary>
		protected List<Func<int, Task<int>>> ReservedMoneyCheckerFunctions = new List<Func<int, Task<int>>>();

		protected Action<ApiLogMessage> Logger;

		public AbstractBank(Action<ApiLogMessage> logger)
		{
			Logger = logger;
		}

		/// <summary>
		/// Determines the total amount of reserved money.
		/// </summary>
		/// <remarks>
		/// Reserved money is money that is reserved "in memory" and not
		/// represented in storage.
		/// </remarks>
		/// <param name="userId">id of the user to determine the amount of reserved money for.</param>
		/// <returns>Positive integer of reserved money, will be 0 if no money reserved.</returns>
		public async Task<int> GetReservedMoney(int userId)
		{
			int reservedMoney = 0;
			foreach (Func<int, Task<int>> func in ReservedMoneyCheckerFunctions)
			{
				reservedMoney += await func(userId);
			}
			return reservedMoney;
		}

		/// <summary>
		/// Get the amount ot all a user's money, including reserved.
		/// </summary>
		/// <param name="userId">id of the user to get the total money for.</param>
		/// <returns>total amount of money the specified user has.</returns>
		public async Task<int> GetTotalMoney(int userId)
		{
			return await GetStoredMoneyValue(userId);
		}

		/// <summary>
		/// Get the amount of money available to a user.
		/// </summary>
		/// <param name="userId">id of the user to get the available money for.</param>
		/// <returns>the amount of money the user has available, will be 0 in the case of no money.</returns>
		public async Task<int> GetAvailableMoney(int userId)
		{
			return (await GetTotalMoney(userId)) - (await GetReservedMoney(userId));
		}

		protected abstract Task<int> GetStoredMoneyValue(int userId);

		protected abstract Task AdjustStoredMoneyValue(int userId, int change);

		protected abstract Task RecordTransaction(Dictionary<string, object> records);

		private Dictionary<string, Cooldown> cooldownDict = new Dictionary<string, Cooldown>();
		private bool cooldownActive = false;

		public Dictionary<string, Cooldown> GetCooldowns() => cooldownDict;

		/// <summary>
		/// Adjust a user's balance and make a record of it
		/// </summary>
		/// <param name="userId">id of the user whose account is being affected</param>
		/// <param name="change">the amount to adjust the balance by.</param>
		/// <returns>
		/// records of the transaction including:
		/// the user id
		/// the amount changed by
		/// the timestamp of the change
		/// the old balance
		/// the new balance
		/// </returns>
		public async Task<Dictionary<string, object>> MakeTransaction(int userId, int change, Dictionary<string, object> extra)
		{
			if (extra.ContainsKey("cooldown") && (bool) extra["cooldown"])
			{
				if (extra.ContainsKey("cooldown_id"))
				{
					if (cooldownDict.ContainsKey((string) extra["cooldown_id"]))
					{
						Cooldown cooldown = cooldownDict[(string) extra["cooldown_id"]];
						if (cooldown.softCooldown.CooldownActive)
						{
							int absChange = Math.Abs(change);
							if (absChange < cooldown.softCooldown.CooldownMinimumBid)
								throw new BidTooLowError(
									$"The minimum bid for this item is currently {cooldown.softCooldown.CooldownMinimumBid}, you cannot bid {absChange}");
							cooldown.softCooldown.CooldownLength += cooldown.softCooldown.CooldownIncrementLength;
							cooldown.softCooldown.CooldownMinimumBid += cooldown.softCooldown.CooldownIncrementAmount;
							cooldown.softCooldown.DueTime =
								cooldown.softCooldown.DueTime.AddMilliseconds(cooldown.softCooldown
									.CooldownIncrementLength);
							cooldown.softCooldown.CooldownTimer.Stop();
							cooldown.softCooldown.CooldownTimer.Dispose();
							cooldown.softCooldown.CooldownTimer = new Timer((cooldown.softCooldown.DueTime - DateTime.UtcNow).TotalMilliseconds);
							cooldown.softCooldown.CooldownTimer.Elapsed += delegate
							{
								SoftTimerElapsed((string) extra["id"]);
							};
							cooldown.softCooldown.CooldownTimer.Start();
						}
					}
					else if (cooldownActive)
					{
						Cooldown cooldown = new Cooldown
						{
							softCooldown =
							{
								CooldownActive = true,
								CooldownIncrementAmount = (int) extra["cooldown_increment_amount"],
								CooldownIncrementLength = (int) extra["cooldown_increment_length"],
								CooldownLength = (int) extra["cooldown_length"],
								CooldownMinimumBid = (int) extra["cooldown_minimum_bid"]
							}
						};
						cooldown.softCooldown.DueTime = DateTime.UtcNow.AddMilliseconds(cooldown.softCooldown.CooldownLength);
						cooldown.softCooldown.CooldownTimer = new Timer((cooldown.softCooldown.DueTime - DateTime.UtcNow).TotalMilliseconds);
						cooldown.softCooldown.CooldownTimer.Elapsed += delegate
						{
							SoftTimerElapsed((string) extra["cooldown_id"]);
						};
						cooldown.softCooldown.CooldownTimer.Start();
						cooldownDict.Add((string) extra["cooldown_id"], cooldown);
					}
				}
			}
			Logger(new ApiLogMessage($"Adjusting {userId}'s balance by {change}", ApiLogLevel.Debug));
			int oldBalance = await GetStoredMoneyValue(userId);
			await AdjustStoredMoneyValue(userId, change);
			int newBalance = await GetStoredMoneyValue(userId);
			Dictionary<string, object> transaction = new Dictionary<string, object>
			{
				{ "user", userId },
				{ "change", change },
				{ "timestamp", DateTime.UtcNow },
				{ "old_balance", oldBalance },
				{ "new_balance", newBalance }
			};
			foreach (KeyValuePair<string, object> pair in extra.Where(x => !x.Key.StartsWith("cooldown")))
			{
				transaction.Add(pair.Key, pair.Value);
			}
			Logger(new ApiLogMessage($"Recording transaction: {{{string.Join(",", transaction.Select(kv => kv.Key + " - " + kv.Value.ToString()).ToArray())}}}", ApiLogLevel.Debug));
			await RecordTransaction(transaction);
			return transaction;
		}

		private void SoftTimerElapsed(string id)
		{
			Cooldown cooldown = cooldownDict[id];
			cooldown.softCooldown.CooldownTimer.Dispose();
			cooldownDict.Remove(id);
		}

		public async Task<List<Dictionary<string, object>>> MakeTransactions(
			List<Tuple<int, int, Dictionary<string, object>>> records)
		{
			List<Dictionary<string, object>> ret = new List<Dictionary<string, object>>();
			foreach ((int userId, int change, Dictionary<string, object> extra) in records)
				ret.Add(await MakeTransaction(userId, change, extra));
			return ret;
		}

		/// <summary>
		/// Adds the reserved money checker function to the bank
		/// </summary>
		/// <remarks>
		/// If this is used the function <i>must</i> be removed before the auction object is deleted!
		/// </remarks>
		/// <param name="checker">The function to register</param>
		public void RegisterReservedMoneyChecker(Func<int, Task<int>> checker) => ReservedMoneyCheckerFunctions.Add(checker);

		/// <summary>
		/// Removes the reserved money checker function from the bank.
		/// </summary>
		/// <remarks>
		/// This <i>must</i> be called when the auction has been finished and fulfilled.
		/// </remarks>
		/// <param name="checker"></param>
		public void DeregisterReservedMoneyChecker(Func<int, Task<int>> checker) => ReservedMoneyCheckerFunctions.Remove(checker);
	}
}
