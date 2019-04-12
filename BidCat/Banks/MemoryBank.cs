using ApiListener;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BidCat.Banks
{
	public class MemoryBank : AbstractBank
	{
		private Dictionary<int, int> storage = new Dictionary<int, int>();

		private const int startingAmount = 50000;

		public MemoryBank(Action<ApiLogMessage> logger)
			: base(logger)
		{ }

#pragma warning disable 1998
		protected async override Task<int> GetStoredMoneyValue(int userId)
		{
			if (!storage.ContainsKey(userId))
				storage[userId] = startingAmount;
			return storage[userId];
		}

		protected async override Task AdjustStoredMoneyValue(int userId, int change)
		{
			if (!storage.ContainsKey(userId))
				storage[userId] = startingAmount;
			storage[userId] += change;
			Logger(new ApiLogMessage($"Dummy storage: {{{string.Join(",", storage.Select(kv => kv.Key + " - " + kv.Value.ToString()).ToArray())}}}", ApiLogLevel.Debug));
		}

		protected async override Task RecordTransaction(Dictionary<string, object> records)
		{
			//do nothing
		}
#pragma warning restore 1998
	}
}
