using ApiListener;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BidCat.Banks
{
	public class PostgresBank : AbstractBank
	{
		//Not implemented until the schema has been worked out
		public PostgresBank(Action<ApiLogMessage> logger)
			: base (logger)
		{
			throw new NotImplementedException("Postgres bank hasn't been implemented yet");
		}

		protected override Task AdjustStoredMoneyValue(int userId, int change)
		{
			throw new NotImplementedException("Postgres bank hasn't been implemented yet");
		}

		protected override Task<int> GetStoredMoneyValue(int userId)
		{
			throw new NotImplementedException("Postgres bank hasn't been implemented yet");
		}

		protected override Task RecordTransaction(Dictionary<string, object> records)
		{
			throw new NotImplementedException("Postgres bank hasn't been implemented yet");
		}
	}
}
