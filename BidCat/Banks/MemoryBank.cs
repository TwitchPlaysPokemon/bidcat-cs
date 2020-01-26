using ApiListener;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BidCat.Banks
{
	public class MemoryBank : AbstractBank
	{
		private readonly Dictionary<int, int> storage = new Dictionary<int, int>();

		private const int startingAmount = 50000;

		public MemoryBank(Action<ApiLogMessage> logger)
			: base(logger)
		{ }

#pragma warning disable 1998
		protected override async Task<int> GetStoredMoneyValue(int userId)
		{
			if (!storage.ContainsKey(userId))
				storage[userId] = startingAmount;
			return storage[userId];
		}

		protected override async Task AdjustStoredMoneyValue(int userId, int change)
		{
			if (!storage.ContainsKey(userId))
				storage[userId] = startingAmount;
			storage[userId] += change;
			Logger(new ApiLogMessage($"Dummy storage: {{{string.Join(",", storage.Select(kv => kv.Key + " - " + kv.Value.ToString()).ToArray())}}}", ApiLogLevel.Debug));
		}

		protected override async Task RecordTransaction(Dictionary<string, object> records)
		{
			//do nothing
		}
#pragma warning restore 1998
	}
}
