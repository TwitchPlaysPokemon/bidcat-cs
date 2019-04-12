using ApiListener;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
		public async Task<int> GetReserverdMoney(int userId)
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
			return (await GetTotalMoney(userId)) - (await GetReserverdMoney(userId));
		}

		protected abstract Task<int> GetStoredMoneyValue(int userId);

		protected abstract Task AdjustStoredMoneyValue(int userId, int change);

		protected abstract Task RecordTransaction(Dictionary<string, object> records);

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
			Logger(new ApiLogMessage($"Adjusting {userId}'s balance by {change}", ApiLogLevel.Debug));
			int oldBalance = await GetStoredMoneyValue(userId);
			await AdjustStoredMoneyValue(userId, change);
			int newBalance = await GetStoredMoneyValue(userId);
			Dictionary<string, object> transaction = new Dictionary<string, object>()
			{
				{ "user", userId },
				{ "change", change },
				{ "timestamp", DateTime.UtcNow },
				{ "old_balance", oldBalance },
				{ "new _balance", newBalance }
			};
			foreach (KeyValuePair<string, object> pair in extra)
			{
				transaction.Add(pair.Key, pair.Value);
			}
			Logger(new ApiLogMessage($"Recording transaction: {{{string.Join(",", transaction.Select(kv => kv.Key + " - " + kv.Value.ToString()).ToArray())}}}", ApiLogLevel.Debug));
			await RecordTransaction(transaction);
			return transaction;
		}
	}
}
