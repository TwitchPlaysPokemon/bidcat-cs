using ApiListener;
using BidCat.Banks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BidCat.API
{
	public class Auction
	{
		Dictionary<string, Dictionary<int, int>> lotDict = new Dictionary<string, Dictionary<int, int>>();

		AbstractBank bank;
		public Auction(Action<ApiLogMessage> logger)
		{
			switch (Listener.Config.StorageType)
			{
				case StorageType.Memory:
					bank = new MemoryBank(logger);
					break;
				case StorageType.Mongo:
					bank = new MongoBank(logger);
					break;
				case StorageType.Postgres:
					bank = new PostgresBank(logger);
					break;
			}

		}

		public async Task<int> GetReservedMoney(int userId)
		{
			return (await GetBidsForUser(userId)).Sum(x => x.Value);
		}

		public async Task<Dictionary<string, int>> GetBidsForUser(int userId)
		{
			Dictionary<string, int> bids = new Dictionary<string, int>();
			foreach (KeyValuePair<string, Dictionary<int, int>> pair in lotDict)
			{
				if (pair.Value.ContainsKey(userId))
					bids.Add(pair.Key, pair.Value[userId]);
			}
			return bids;
		}
	}
}
