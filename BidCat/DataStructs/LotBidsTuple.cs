using System.Collections.Generic;

namespace BidCat.DataStructs
{
	public class LotBidsTuple
	{
		public string LotId;

		public List<BidTuple> Bids;

		public void Deconstruct(out string lotId, out List<BidTuple> bids)
		{
			lotId = LotId;
			bids = Bids;
		}
	}

	public class BidTuple
	{
		public int UserId;
		public int Amount;
	}
}
