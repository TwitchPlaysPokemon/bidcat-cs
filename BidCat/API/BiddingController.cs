using ApiListener;
using System;
using System.Collections.Generic;

namespace BidCat.API
{
	public class BiddingController : ApiProvider
	{
		private Auction auctionController;

		public override IEnumerable<ApiCommand> Commands => new List<ApiCommand>()
		{
		};

		public BiddingController(Action<ApiLogMessage> logger)
			: base(logger)
		{
			auctionController = new Auction(logger);
		}
	}
}
